#!/usr/bin/env python3
"""Turn Harbour's own RTL regression tests (utils/hbtest/rt_*.prg) into
test programs for HbRuntime, the C# runtime of transpiled code.

Each rt_<name>.prg is run through Harbour's preprocessor (harbour -p),
which resolves its #ifdefs for this platform and turns every

    HBTEST <x> IS <result>[, <alt>]

into TEST_CALL( "<x>", {|| <x> }, <result>[, <alt>] ). The preprocessed
module is written back as generated/hbt_<name>.prg with every TEST_CALL
given its id (`rt_str:68`, the assertion's line in the original file)
and a flag for its alternative result, or commented out with the reason
it is not run. The rest of the module stays as it is: rt_file's
assertions share a file handle and run in sequence, so the statements
between them are part of the test. A generated Main initialises the
statics as rt_init.ch's INIT PROCEDURE does and calls the module's entry
procedures in file order.

Every assertion gets a disposition in manifest.tsv:

  run-fn        runs; every core function it calls is one the
                application needs (--needed)
  run-op        runs; it calls no core function (operators, the VM) —
                the second tier, reported but not gating
  rte           not run: it expects a runtime error. Most pass the
                wrong type, which a strict C# signature makes a compile
                error instead
  unsupported:memvar|macro|alias|class|c-helper|get|equal
                not run: needs something the C# side does not have
  out-of-scope  not run: calls a core function the application does not
                use (whether or not HbRuntime has it)

The statements between the assertions get the same treatment: one that
needs a macro, an alias, a memvar, the GET system, a single `=` or an
out-of-scope function is commented out on both sides (`statement:<why>` in the
manifest) — it is the setup of assertions that are not run. Declarations,
control flow and a command's multi-statement expansion are left alone.
So do the module's helper routines: one that calls an out-of-scope
function (rt_misc's TESTFNAME, hb_FNameMerge's test in all but name) is
out of scope itself, commented out with every assertion that calls it.

Our own tests, own/own_<family>.prg, are written the same way (HBTEST
lines, #include "rt_main.ch") for the functions hbtest does not cover,
and go through the same steps; their ids are `own_<family>:<line>`.

rulings.tsv (hand-written: id or file:line, ruling, reason) overrides
the disposition. `quarantine` comments the assertion or statement out on
both sides; `divergent` runs it but records that C# differs by design,
so it does not gate; `exclude` on a module name (rt_class) drops the
whole module, and on `<module>/<routine>` (rt_misc/HB_TString) that
routine, its assertions marked `excluded` in the manifest.
runrtl.py builds, runs and compares.

Usage: extract.py [--needed <notes/hbruntime.txt>] [rt_name ...]
"""
import argparse
import glob
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
HBTEST = os.path.join(ROOT, "utils", "hbtest")
OWN = os.path.join(HERE, "own")
GENERATED = os.path.join(HERE, "generated")
WORK = os.path.join(HERE, "work")
MANIFEST = os.path.join(HERE, "manifest.tsv")
RULINGS = os.path.join(HERE, "rulings.tsv")
DEFAULT_NEEDED = os.path.join(ROOT, "..", "easipos-transpiled", "notes", "hbruntime.txt")

# The Harbour whose behaviour is the reference: the one the application
# is built with (easipos-9.0's mk2env.bat: %USERPROFILE%\dev\harbour-3.2.0dev).
HB_INSTALL = os.environ.get("HB_INSTALL") or \
    os.path.join(os.path.expanduser("~"), "dev", "harbour-3.2.0dev")

# Entry procedures in the order hbtest.prg's Main calls them.
ENTRY_ORDER = ["Main_HVM", "Main_HVMA", "Main_MATH", "Main_DATE", "Main_STR",
               "Main_STRA", "Main_TRANS", "Comp_Str", "Exact_Str", "New_STRINGS",
               "Long_STRINGS", "Main_ARRAY", "Main_FILE", "Main_MISC",
               "Main_OPOVERL", "Main_CLASS", "Main_MT"]

CALL_RE = re.compile(r"(?<![\w:])([A-Za-z_][A-Za-z0-9_]*)\s*\(")
LINE_RE = re.compile(r'^#line\s+(\d+)\s+"([^"]+)"')
ROUTINE_RE = re.compile(r"(?i)^(INIT\s+|EXIT\s+|STATIC\s+)?(PROCEDURE|FUNCTION)\s+(\w+)\s*\(")
# hbtest's memvars (rt_vars.ch); mxNotHere is deliberately undeclared.
MEMVAR_RE = re.compile(r"(?<![\w:])(m[cndlobau][A-Z]\w*|mxNotHere)\b")
# hbtest's C helpers (rt_miscc.c) and the class-internals functions.
C_HELPERS = {"R_PASSENL", "R_PASSENLC"}
# A single `=` — not `:=`, `==`, `<=`, `>=`, `!=`, `+=` and kin, not a
# hash's `=>`: the transpiler refuses it as an assignment and as a
# comparison (E0100; Alex, 2026-09-19), so an assertion or statement
# using it cannot be carried to C#. Run on code with strings blanked.
EQUAL_RE = re.compile(r"(?<![:=<>!+\-*/%^$#])=(?![=>])")
# BEGIN SEQUENCE WITH { |e| break(e) } (or { || break() }), the one error
# block the transpiler accepts (E0101), becomes a catch in C#: the Break()
# in it is no call to the library, so a helper is not out of scope for it.
SEQ_WITH_RE = re.compile(r"(?i)^BEGIN\s+SEQUENCE\s+WITH\s*\{.*\}$")


def hbx_core():
    """Upper-cased core function names: Harbour's include/*.hbx."""
    names = set()
    for p in glob.glob(os.path.join(ROOT, "include", "*.hbx")):
        for line in open(p, encoding="latin-1"):
            m = re.match(r"\s*DYNAMIC\s+([A-Za-z_]\w*)", line)
            if m:
                names.add(m.group(1).upper())
    return names


def needed_names(path):
    """Upper-cased names the application calls on HbRuntime
    (easipos-transpiled scripts/gen_stubs.py writes the file)."""
    names = set()
    if not os.path.isfile(path):
        sys.exit("needed list not found: %s (run the easipos-transpiled build, "
                 "or pass --needed)" % path)
    for line in open(path, encoding="utf-8"):
        if line.startswith("#") or not line.strip():
            continue
        names.add(line.split("\t", 1)[0].upper())
    return names


def load_rulings():
    """{id or 'file:line': (ruling, reason)} from rulings.tsv."""
    rulings = {}
    if os.path.isfile(RULINGS):
        for line in open(RULINGS, encoding="utf-8"):
            if line.startswith("#") or not line.strip():
                continue
            f = line.rstrip("\n").split("\t")
            if len(f) >= 2:
                rulings[f[0]] = (f[1], f[2] if len(f) > 2 else "")
    return rulings


def source_dir(name):
    """utils/hbtest for Harbour's rt_*.prg, own/ for ours (own_*.prg)."""
    return OWN if name.startswith("own_") else HBTEST


def preprocess(name):
    """harbour -p the module into work/ppo; returns the .ppo text. Our
    own tests include hbtest's rt_main.ch for the HBTEST command."""
    out = os.path.join(WORK, "ppo")
    os.makedirs(out, exist_ok=True)
    harbour = os.path.join(HB_INSTALL, "bin", "harbour.exe" if os.name == "nt" else "harbour")
    r = subprocess.run([harbour, name + ".prg", "-p" + out + os.sep, "-s", "-n", "-q0",
                        "-kmo", "-I" + os.path.join(HB_INSTALL, "include"), "-I" + HBTEST],
                       cwd=source_dir(name), capture_output=True, text=True)
    ppo = os.path.join(out, name + ".ppo")
    if r.returncode != 0 or not os.path.isfile(ppo):
        sys.exit("harbour -p %s failed:\n%s%s" % (name, r.stdout, r.stderr))
    return open(ppo, encoding="latin-1").read()


def split_args(text, start):
    """Top-level arguments of the call whose '(' is at text[start - 1].
    Returns ([args], index after the closing ')'). Strings: "..." and
    '...', and e"..." with backslash escapes; a [...] is a string only
    where an argument begins (hbtest's stringify uses [] or e"" for text
    holding both quote kinds), elsewhere it is a subscript and nests."""
    args, cur, depth, i = [], [], 1, start
    at_arg_start = True
    while i < len(text):
        c = text[i]
        if c in "eE" and at_arg_start and text[i + 1:i + 2] == '"':
            j = i + 2               # e"..." — C-style escapes, \" inside
            while text[j] != '"':
                j += 2 if text[j] == "\\" else 1
            cur.append(text[i:j + 1])
            i = j + 1
            at_arg_start = False
            continue
        if c in "\"'" or (c == "[" and at_arg_start):
            close = "]" if c == "[" else c
            j = text.index(close, i + 1)
            cur.append(text[i:j + 1])
            i = j + 1
            at_arg_start = False
            continue
        if c in "([{":
            depth += 1
        elif c in ")]}":
            depth -= 1
            if depth == 0:
                args.append("".join(cur).strip())
                return args, i + 1
        elif c == "," and depth == 1:
            args.append("".join(cur).strip())
            cur, i, at_arg_start = [], i + 1, True
            continue
        cur.append(c)
        if not c.isspace():
            at_arg_start = False
        i += 1
    raise ValueError("unbalanced TEST_CALL")


def code_only(text):
    """text with the contents of its string literals blanked, so an `&`,
    a `->` or a name inside a string is not taken for code."""
    out, i = [], 0
    while i < len(text):
        c = text[i]
        if c in "eE" and text[i + 1:i + 2] == '"' and (i == 0 or not text[i - 1].isalnum()):
            j = i + 2
            while j < len(text) and text[j] != '"':
                j += 2 if text[j] == "\\" else 1
            out.append('""')
            i = j + 1
        elif c in "\"'":
            j = text.find(c, i + 1)
            j = len(text) if j < 0 else j
            out.append(c + c)
            i = j + 1
        else:
            out.append(c)
            i += 1
    return "".join(out)


def unsupported(code, raw, calls):
    """Why code needs what the C# side does not have, or None. `code` has
    its strings blanked, so a `&` or `->` inside one is not code; memvars
    are looked for in `raw`, since Type( "mcString" ) names one in a
    string."""
    if "&" in code:
        return "unsupported:macro"
    if "->" in code:
        return "unsupported:alias"
    if MEMVAR_RE.search(raw):
        return "unsupported:memvar"
    if calls & C_HELPERS:
        return "unsupported:c-helper"
    if any(c.startswith(("__CLS", "__OBJ", "HB_CLS")) for c in calls):
        return "unsupported:class"
    if calls & {"_GET_", "__GET"}:
        return "unsupported:get"            # the GET system; nothing calls it
    if EQUAL_RE.search(code):
        return "unsupported:equal"          # the transpiler refuses `=` (E0100)
    return None


# Lines the statement rule leaves alone: declarations and control flow,
# whose removal would change the code's shape rather than drop one step.
KEYWORDS = {"LOCAL", "STATIC", "MEMVAR", "FIELD", "PRIVATE", "PUBLIC", "PARAMETERS",
            "IF", "ELSEIF", "ELSE", "ENDIF", "END", "DO", "CASE", "OTHERWISE",
            "ENDCASE", "FOR", "NEXT", "WHILE", "ENDDO", "LOOP", "EXIT", "RETURN",
            "PROCEDURE", "FUNCTION", "INIT", "BEGIN", "RECOVER", "SWITCH",
            "ENDSWITCH", "ANNOUNCE", "REQUEST", "EXTERNAL", "WITH"}


def statement_exclusion(text, core, needed, helpers=frozenset()):
    """Why a plain statement between the assertions cannot be carried to
    C# — the assertion rule applied to a statement, so the setup of an
    excluded assertion goes with it — or None. Declarations and control
    flow are never excluded."""
    stripped = text.strip()
    word = re.match(r"[A-Za-z_]+", stripped)
    if not stripped or stripped.startswith(("#", "//")) or \
            (word and word.group(0).upper() in KEYWORDS):
        return None
    code = code_only(stripped)
    if ";" in code:         # a command's expansion (hbclass.ch's CLASS): not one step
        return None
    calls = {c.upper() for c in CALL_RE.findall(code)}
    why = unsupported(code, stripped, calls)
    if why:
        return why
    if (calls & core) - needed or calls & helpers:
        return "out-of-scope"
    return None


def out_of_scope_helpers(lines, core, needed):
    """The module's helper routines (every routine but its entry
    procedures and INIT procedures) that call a core function the
    application does not use, directly or through another such helper,
    upper-cased. rt_misc's TESTFNAME() is hb_FNameMerge's test in all
    but name: an assertion calling it is out of scope like one calling
    hb_FNameMerge, and the helper goes with it."""
    bodies, cur = {}, None
    for text in lines:
        stripped = text.strip()
        rm = ROUTINE_RE.match(stripped)
        if rm:
            kind = (rm.group(1) or "").strip().upper()
            entry = rm.group(2).upper() == "PROCEDURE" and not kind
            cur = None if entry or kind == "INIT" else rm.group(3).upper()
            if cur:
                bodies[cur] = set()
        elif cur and not stripped.startswith("#") and not SEQ_WITH_RE.match(stripped):
            bodies[cur] |= {c.upper() for c in CALL_RE.findall(code_only(stripped))}
    out = {h for h, calls in bodies.items() if (calls & core) - needed}
    grew = True
    while grew:
        grew = False
        for h, calls in bodies.items():
            if h not in out and calls & out:
                out.add(h)
                grew = True
    return out


def classify(expr, expected, core, needed, helpers=frozenset()):
    """(disposition, subject, core functions called). The subject is the
    outermost core call, the leftmost in the text: hbtest writes the
    function under test outermost (Str( Val( "1." ) ) tests Str).
    `helpers`: the module's out-of-scope helpers."""
    code = code_only(expr)
    order = [c.upper() for c in CALL_RE.findall(code)]
    calls = set(order)
    subject = next((c for c in order if c in core), "")
    if re.match(r'''^["'\[][EW] \d''', expected):
        return "rte", subject, calls
    why = unsupported(code, expr, calls)
    if why:
        return why, subject, calls
    core_calls = calls & core
    if core_calls - needed or calls & helpers:
        return "out-of-scope", subject, calls
    return ("run-fn" if core_calls else "run-op"), subject, calls


def generate(name, core, needed, rulings):
    # Our own tests are written for what EasiPOS needs, so none is out of
    # scope: a function EasiPOS stops calling keeps its tests (own_threads_mt
    # tests the thread numbers, once DefError no longer called hb_threadID()).
    if name.startswith("own_"):
        needed = core
    ppo = preprocess(name)
    src_name = name + ".prg"
    lines = ppo.split("\n")
    out, rows = [], []
    cur_file, cur_line = None, 0
    entries, inits = [], []
    helpers = out_of_scope_helpers(lines, core, needed)
    excluded_routine = None     # the label a routine being skipped carries
    for text in lines:
        m = LINE_RE.match(text)
        if m:
            cur_line, cur_file = int(m.group(1)) - 1, m.group(2)
            out.append("")          # keep the line count; the directive is Harbour's own
            continue
        cur_line += 1
        stripped = text.strip()
        sid = "%s:%d" % (os.path.splitext(cur_file)[0], cur_line)
        rm = ROUTINE_RE.match(stripped)
        if rm:
            ruling = rulings.get("%s/%s" % (name, rm.group(3)))
            if ruling and ruling[0] == "exclude":
                excluded_routine = "excluded"
            elif rm.group(3).upper() in helpers:
                excluded_routine = "out-of-scope"
            else:
                excluded_routine = None
        if excluded_routine is not None:
            # a routine ruled out whole, or an out-of-scope helper:
            # commented out on both sides, its assertions marked
            idx = text.find("TEST_CALL(")
            if idx >= 0:
                args, _ = split_args(text, idx + len("TEST_CALL("))
                bm = re.match(r"^\{\|\s*\|\s*(.*)\}$", args[1], re.S)
                expr = bm.group(1).strip() if bm else args[1]
                _, subject, calls = classify(expr, args[2], core, needed)
                rows.append((sid, excluded_routine, subject,
                             ",".join(sorted(c for c in calls if c in core)), expr))
            out.append("// [%s] %s" % (excluded_routine, stripped) if stripped else "")
            continue
        pm = re.match(r"(?i)^(INIT\s+|STATIC\s+)?PROCEDURE\s+(\w+)\s*\(", stripped)
        if pm and not pm.group(1):
            entries.append(pm.group(2))
        if pm and pm.group(1) and pm.group(1).strip().upper() == "INIT":
            text = text.replace(pm.group(1), "STATIC ", 1)
            inits.append(pm.group(2))
        idx = text.find("TEST_CALL(")
        if idx < 0:
            ruling = rulings.get(sid)
            if ruling and ruling[0] == "quarantine":
                text = "// [quarantine: %s] %s" % (ruling[1], text.strip())
            else:
                why = statement_exclusion(text, core, needed, helpers)
                if why:
                    rows.append((sid, "statement:" + why, "", "", text.strip()))
                    indent = text[:len(text) - len(text.lstrip())]
                    text = "%s// [statement %s] %s" % (indent, why, text.strip())
            out.append(text)
            continue
        args, end = split_args(text, idx + len("TEST_CALL("))
        cexpr, block, expected = args[0], args[1], args[2]
        alt = args[3] if len(args) > 3 else None
        bm = re.match(r"^\{\|\s*\|\s*(.*)\}$", block, re.S)
        expr = bm.group(1).strip() if bm else block
        tid = "%s:%d" % (os.path.splitext(cur_file)[0], cur_line)
        disp, subject, calls = classify(expr, expected, core, needed, helpers)
        ruling = rulings.get(tid)
        if ruling:
            disp = "%s:%s" % (ruling[0], ruling[1]) if ruling[1] else ruling[0]
        rows.append((tid, disp, subject, ",".join(sorted(c for c in calls if c in core)), expr))
        if disp.split(":")[0] in ("run-fn", "run-op", "divergent"):
            new = 'TEST_CALL( "%s", %s, %s, %s, %s, %s )' % (
                tid, cexpr, block, expected, ".T." if alt is not None else ".F.",
                alt if alt is not None else "NIL")
            out.append(text[:idx] + new + text[end:])
        else:
            out.append("%s// [%s] %s" % (text[:idx], disp, text[idx:].strip()))
    order = [e for e in ENTRY_ORDER if e in entries] + \
            [e for e in entries if e not in ENTRY_ORDER and e.upper() not in ("MAIN",)]
    head = ["// Generated by rtltest/extract.py from %s/%s, preprocessed"
            % ("rtltest/own" if name.startswith("own_") else "utils/hbtest", src_name),
            "// by Harbour %s. Do not edit: change extract.py or rulings.tsv."
            % os.path.basename(HB_INSTALL), ""]
    # INIT PROCEDUREs (hbtest's RT_InitStatics) run first, called by name
    tail = ["", "PROCEDURE Main()", "", "   TEST_BEGIN()"] + ["   %s()" % p for p in inits]
    tail += ["   %s()" % e for e in order]
    tail += ["   TEST_END()", "", "   RETURN", ""]
    os.makedirs(GENERATED, exist_ok=True)
    stem = name if name.startswith("own_") else "hbt_" + name[3:]
    path = os.path.join(GENERATED, stem + ".prg")
    ruling = rulings.get(name)
    runnable = any(r[1].split(":")[0] in ("run-fn", "run-op", "divergent") for r in rows)
    if (ruling and ruling[0] == "exclude") or not runnable:
        # A module ruled out, or one with nothing left to run (rt_mt's one
        # assertion is out of scope): its assertions stay in the manifest,
        # and it is neither generated nor run.
        for p in (path, os.path.join(HERE, "hbref", stem + ".txt")):
            if os.path.isfile(p):
                os.remove(p)
        if ruling and ruling[0] == "exclude":
            return [(r[0], "excluded", r[2], r[3], r[4]) for r in rows]
        return rows
    with open(path, "w", encoding="latin-1", newline="\n") as fh:
        fh.write("\n".join(head + out + tail))
    return rows


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--needed", default=DEFAULT_NEEDED)
    ap.add_argument("names", nargs="*")
    a = ap.parse_args()
    core, needed = hbx_core(), needed_names(a.needed)
    rulings = load_rulings()
    names = a.names or sorted(os.path.splitext(os.path.basename(p))[0]
                              for p in glob.glob(os.path.join(HBTEST, "rt_*.prg"))
                              + glob.glob(os.path.join(OWN, "own_*.prg")))
    all_rows = []
    for n in names:
        rows = generate(n, core, needed, rulings)
        all_rows += rows
        tally = {}
        for r in rows:
            k = r[1].split(":")[0]
            tally[k] = tally.get(k, 0) + 1
        print("%-10s %4d assertions  %s" % (n, len(rows),
              "  ".join("%s %d" % kv for kv in sorted(tally.items()))))
    if not a.names:
        with open(MANIFEST, "w", encoding="utf-8", newline="\n") as fh:
            fh.write("# id\tdisposition\tsubject\tcore functions\texpression\n")
            for r in all_rows:
                fh.write("\t".join(r) + "\n")
    total = {}
    for r in all_rows:
        k = r[1].split(":")[0]
        total[k] = total.get(k, 0) + 1
    print("total      %4d assertions  %s" % (len(all_rows),
          "  ".join("%s %d" % kv for kv in sorted(total.items()))))
    return 0


if __name__ == "__main__":
    sys.exit(main())
