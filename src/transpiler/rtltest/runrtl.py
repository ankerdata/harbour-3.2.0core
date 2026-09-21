#!/usr/bin/env python3
"""Run the generated RTL tests under Harbour and as C#, and compare them
assertion by assertion.

For every generated/<name>.prg (extract.py writes them):

  prg   hbmk2 builds it with harness.prg and runs it; the output is the
        reference, hbref/<name>.txt (tracked, like the suite's hbout/)
  cs    the transpiler turns both files into C# (-GF, then -GS, into a
        reftab of the test's own); dotnet builds them against HbRuntime
        ALONE — no generated stubs — so a function HbRuntime does not
        implement fails to compile (CS0117). Compile triage: every error
        on a TEST_CALL line quarantines that assertion (its statement is
        commented out of the C#) and the build is retried until it is
        clean. An error on any other line is reported and the test is not
        run: that needs a ruling (rulings.tsv) or a transpiler fix.
  run   the C# program runs; each assertion's line is compared with the
        reference's

and status.tsv sums it per function — charged to the assertion's subject,
its outermost call — with the application's call counts (--needed): the
standard library's leaderboard.

An assertion's outcome:
  pass        C# does what Harbour does: both PASS, or where Harbour
              itself misses hbtest's expectation, the same line (both
              RTE counts as the same) — the reference is the Harbour 9.0
              ships with, not hbtest's text
  fail        Harbour PASS, C# FAIL or RTE
  quarantine  C# could not compile it (the CS code is recorded)
  missing     no C# line: the C# program stopped before it
  harbour     Harbour misses hbtest's expectation and C# does something
              else again

Exit status 1 when any run-fn assertion is not a pass and carries no
ruling. run-op (operators and the VM) is reported only.

Usage: runrtl.py [all|prg|cs|run|report] [name ...]
Run it from runrtl.bat, which sets up MSVC and Harbour as the
transpiler suite's runtests.bat does.
"""
import os
import re
import shutil
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "src", "transpiler", "tests"))
sys.dont_write_bytecode = True     # no __pycache__ in the tracked tests/
import runsuite                                                   # noqa: E402

GENERATED = os.path.join(HERE, "generated")
HBREF = os.path.join(HERE, "hbref")
WORK = os.path.join(HERE, "work")
PRGEXE = os.path.join(WORK, "prg")
CSEXE = os.path.join(WORK, "cs")
HARNESS = os.path.join(HERE, "harness.prg")
PRELOAD = os.path.join(HERE, "preload.txt")
MANIFEST = os.path.join(HERE, "manifest.tsv")
STATUS = os.path.join(HERE, "status.tsv")
RULINGS = os.path.join(HERE, "rulings.tsv")
DEFAULT_NEEDED = os.path.join(ROOT, "..", "easipos-transpiled", "notes", "hbruntime.txt")

TRANSPILER = runsuite.TRANSPILER
# No \b after the verdict: an unhandled exception's text follows the last
# line directly (QOut writes its newline first), "rt_math:353 PASSUnhandled"
RESULT_RE = re.compile(r"^(\S+:\d+) (PASS|FAIL|RTE)(.*)$")
CS_ERROR_RE = re.compile(r"^(.*?\.cs)\((\d+),\d+\): error (CS\d+): (.*?)(?: \[.*\])?$")
ID_ON_LINE_RE = re.compile(r'TEST_CALL\("([^"]+:\d+)"')


def names_from(args):
    have = sorted(os.path.splitext(f)[0] for f in os.listdir(GENERATED) if f.endswith(".prg"))
    if not args:
        return have
    return [n if n in have else "hbt_" + n for n in args]


RUN_TIMEOUT = 120               # seconds; a whole module runs in a few
TIMEOUT_MARK = "rtltest: TIMEOUT"


def run_limited(args, cwd):
    """Run a test program and return its output. One that runs past
    RUN_TIMEOUT is killed with its whole process tree (`dotnet run`
    starts the program as a child of its own) and its output so far is
    kept, closed by TIMEOUT_MARK: the assertions before the hang still
    count, the rest are missing with the reason named. An emitter bug
    can turn a hbtest loop infinite, and without this one hang stalls
    the whole run.

    Each program gets a console of its own, hidden, and no stdin, so
    nothing typed where the harness runs reaches a test, and a program
    that reads input gets end-of-file instead of waiting for it."""
    p = subprocess.Popen(args, cwd=cwd, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                         stderr=subprocess.STDOUT, text=True, errors="replace",
                         creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    try:
        out, _ = p.communicate(timeout=RUN_TIMEOUT)
    except subprocess.TimeoutExpired:
        subprocess.run(["taskkill", "/T", "/F", "/PID", str(p.pid)],
                       capture_output=True)
        out, _ = p.communicate()
        out = (out or "") + "\n%s after %d s\n" % (TIMEOUT_MARK, RUN_TIMEOUT)
    return out.replace("\r\n", "\n")


# --------------------------------------------------------------- prg ----
def build_prg(name):
    exe = os.path.join(PRGEXE, name + ".exe")
    if os.path.isfile(exe):
        os.remove(exe)
    opts = ["-w3", "-es0", "-gtcgi", "-q"] + (["-mt"] if name.endswith("_mt") else [])
    r = subprocess.run(["hbmk2", os.path.join(GENERATED, name + ".prg"), HARNESS,
                        "-o" + os.path.join(PRGEXE, name)] + opts,
                       cwd=PRGEXE, capture_output=True, text=True)
    if r.returncode or not os.path.isfile(exe):
        tail = (r.stdout + r.stderr).strip().splitlines()
        return name, tail[-1][:160] if tail else "rc=%d" % r.returncode
    run_dir = os.path.join(PRGEXE, name + ".run")
    os.makedirs(run_dir, exist_ok=True)
    out = run_limited([exe], run_dir)
    os.makedirs(HBREF, exist_ok=True)
    with open(os.path.join(HBREF, name + ".txt"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(out)
    return name, None


def stage_prg(names):
    if not shutil.which("hbmk2"):
        sys.exit("hbmk2 not on PATH — run runrtl.bat, which sets up Harbour and MSVC")
    os.makedirs(PRGEXE, exist_ok=True)
    with ThreadPoolExecutor(max_workers=4) as ex:
        res = list(ex.map(build_prg, names))
    bad = [(n, e) for n, e in res if e]
    print("hbmk2:  %d built and run, %d failed" % (len(names) - len(bad), len(bad)))
    for n, e in bad:
        print("   FAIL %-12s %s" % (n, e))
    return not bad


# ---------------------------------------------------------------- cs ----
def transpile(name, d):
    """-GF both sources into the test's own reftab, then -GS them into d."""
    reftab = os.path.join(d, "reftab.tab")
    if os.path.isfile(reftab):
        os.remove(reftab)
    srcs = [os.path.join(GENERATED, name + ".prg"), HARNESS]
    base = [TRANSPILER, "--reftab=" + reftab, "--preload-list=" + PRELOAD,
            "-I" + os.path.join(ROOT, "include"), "-I" + HERE]
    log = []
    for mode in ("-GF", "-GS"):
        for s in srcs:
            args = base + ([s, "-GF", "-q"] if mode == "-GF" else ["-o" + d + os.sep, s, "-GS", "-q"])
            r = subprocess.run(args, capture_output=True, text=True)
            log.append(r.stdout + r.stderr)
            if r.returncode:
                return "transpiler %s %s: rc=%d" % (mode, os.path.basename(s), r.returncode), log
    return None, log


def statement_extent(lines, i):
    """(first, last) line index of the C# statement starting at line i:
    the parentheses of TEST_CALL( balanced, string literals skipped."""
    depth, j, started = 0, i, False
    while j < len(lines):
        text, k = lines[j], 0
        while k < len(text):
            c = text[k]
            if c == '"':
                k += 1
                while k < len(text) and text[k] != '"':
                    k += 2 if text[k] == "\\" else 1
            elif c == "(":
                depth, started = depth + 1, True
            elif c == ")":
                depth -= 1
                if started and depth == 0:
                    return i, j
            k += 1
        j += 1
    return i, i


def triage(d, stdout, quarantined):
    """Comment out the TEST_CALL each compile error falls in. Returns the
    errors on lines holding no TEST_CALL (unattributed)."""
    by_file = {}
    unattributed = []
    for line in stdout.splitlines():
        m = CS_ERROR_RE.match(line.strip())
        if m:
            by_file.setdefault(m.group(1), []).append((int(m.group(2)), m.group(3), m.group(4)))
    for path, errs in by_file.items():
        if not os.path.isabs(path):
            path = os.path.join(d, path)
        lines = open(path, encoding="utf-8").read().split("\n")
        changed = False
        for ln, code, msg in errs:
            i = ln - 1
            while i >= 0 and "TEST_CALL(" not in lines[i] and not lines[i].rstrip().endswith((";", "{", "}")):
                i -= 1
            m = ID_ON_LINE_RE.search(lines[i]) if i >= 0 else None
            if not m:
                unattributed.append("%s:%d %s %s" % (os.path.basename(path), ln, code, msg))
                continue
            tid = m.group(1)
            if tid not in quarantined:
                quarantined[tid] = "%s %s" % (code, msg)
            first, last = statement_extent(lines, i)
            for k in range(first, last + 1):
                if not lines[k].lstrip().startswith("//"):
                    lines[k] = "// [quarantine] " + lines[k]
                    changed = True
        if changed:
            open(path, "w", encoding="utf-8", newline="\n").write("\n".join(lines))
    return unattributed


def build_cs(name):
    d = os.path.join(CSEXE, name)
    if os.path.isdir(d):
        shutil.rmtree(d)
    os.makedirs(d)
    err, log = transpile(name, d)
    if err:
        return name, err, {}
    with open(os.path.join(d, name + ".csproj"), "w") as fh:
        fh.write(runsuite.CSPROJ % (name, name))
    quarantined = {}
    for _ in range(50):
        r = subprocess.run(["dotnet", "build", "--no-dependencies", "-v", "q", "--nologo"],
                           cwd=d, capture_output=True, text=True)
        if r.returncode == 0 and "error CS" not in r.stdout:
            with open(os.path.join(d, "quarantine.tsv"), "w", encoding="utf-8", newline="\n") as fh:
                for tid in sorted(quarantined):
                    fh.write("%s\t%s\n" % (tid, quarantined[tid]))
            return name, None, quarantined
        before = len(quarantined)
        unattributed = triage(d, r.stdout, quarantined)
        if unattributed:
            return name, "unattributed: " + "; ".join(unattributed[:3]), quarantined
        if len(quarantined) == before:
            errs = [l.strip() for l in r.stdout.splitlines() if "error" in l]
            return name, (errs[0][:200] if errs else "rc=%d" % r.returncode), quarantined
    return name, "compile triage did not converge", quarantined


def stage_cs(names):
    lib = os.path.join(CSEXE, "HbRuntime")
    os.makedirs(lib, exist_ok=True)
    runsuite.stage_files(runsuite.hbruntime_sources(), lib, ".cs")
    with open(os.path.join(lib, "HbRuntime.csproj"), "w") as fh:
        fh.write(runsuite.LIBPROJ)
    r = subprocess.run(["dotnet", "build", "-v", "q", "--nologo"], cwd=lib, capture_output=True, text=True)
    if r.returncode:
        sys.exit("HbRuntime does not build:\n" + r.stdout[-2000:])
    with ThreadPoolExecutor(max_workers=4) as ex:
        res = list(ex.map(build_cs, names))
    ok = True
    for name, err, quarantined in res:
        print("   %-12s %s%s" % (name, "built" if not err else "NOT BUILT: " + err,
                                  ", %d quarantined" % len(quarantined) if quarantined else ""))
        ok &= not err
    return ok


# --------------------------------------------------------------- run ----
def run_cs(name):
    """dotnet run finds the build wherever the environment's Platform put
    it (vcvarsall x86 builds into bin/x86/); the program runs in a folder
    of its own, since some tests create files."""
    d = os.path.join(CSEXE, name)
    run_dir = os.path.join(CSEXE, name + ".run")
    os.makedirs(run_dir, exist_ok=True)
    if not os.path.isdir(os.path.join(d, "bin")):
        return
    out = run_limited(["dotnet", "run", "--no-build", "--project",
                       os.path.join(d, name + ".csproj")], run_dir)
    with open(os.path.join(CSEXE, name + ".txt"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(out)


def stage_run(names):
    with ThreadPoolExecutor(max_workers=4) as ex:
        list(ex.map(run_cs, names))


# ------------------------------------------------------------ report ----
def results(path):
    out = {}
    if os.path.isfile(path):
        for line in open(path, encoding="utf-8", errors="replace"):
            m = RESULT_RE.match(line.strip())
            if m:
                out[m.group(1)] = (m.group(2), m.group(3).strip())
    return out


def stop_reason(path):
    """Why a C# test program stopped early: the unhandled exception it
    printed, or None when it ran to its summary line. A statement between
    the assertions has no guard, so an exception there ends the run and
    every later assertion is missing."""
    if not os.path.isfile(path):
        return "never ran (no build)"
    text = open(path, encoding="utf-8", errors="replace").read()
    if TIMEOUT_MARK in text:
        return "killed after %d s — an infinite loop" % RUN_TIMEOUT
    if re.search(r"^\s*passed\s+\d+", text, re.M):
        return None
    m = re.search(r"Unhandled exception\.?\s*(.*)", text)
    return m.group(1).strip()[:200] if m else "no summary line"


def load_manifest():
    rows = {}
    for line in open(MANIFEST, encoding="utf-8"):
        if line.startswith("#"):
            continue
        f = line.rstrip("\n").split("\t")
        rows[f[0]] = {"disp": f[1], "subject": f[2],
                      "fns": [x for x in f[3].split(",") if x], "expr": f[4]}
    return rows


def load_needed(path):
    calls = {}
    if os.path.isfile(path):
        for line in open(path, encoding="utf-8"):
            if line.startswith("#") or not line.strip():
                continue
            f = line.rstrip("\n").split("\t")
            if f[1] == "core":
                calls[f[0].upper()] = (f[0], f[2], sum(int(x) for x in f[3:] if x.isdigit()))
    return calls


def stage_report(names, needed_path):
    manifest = load_manifest()
    needed = load_needed(needed_path)
    outcome = {}
    detail = {}
    for name in names:
        ref = results(os.path.join(HBREF, name + ".txt"))
        cs = results(os.path.join(CSEXE, name + ".txt"))
        stopped = stop_reason(os.path.join(CSEXE, name + ".txt"))
        if stopped:
            last = max(cs, key=lambda t: int(t.split(":")[1]), default="its start")
            print("   %-12s C# stopped after %s: %s" % (name, last, stopped))
        qpath = os.path.join(CSEXE, name, "quarantine.tsv")
        quar = {}
        if os.path.isfile(qpath):
            for line in open(qpath, encoding="utf-8"):
                tid, why = line.rstrip("\n").split("\t", 1)
                quar[tid] = why
        for tid, (verdict, rest) in ref.items():
            if tid in quar:
                outcome[tid] = "quarantine"
                detail[tid] = quar[tid]
            elif tid not in cs:
                outcome[tid] = "missing"
            elif verdict != "PASS":
                # Harbour itself misses hbtest's expectation: what Harbour
                # DOES is the reference. Both erroring counts as the same
                # (the messages differ by nature); otherwise the lines must.
                if cs[tid][0] == verdict and (verdict == "RTE" or cs[tid][1] == rest):
                    outcome[tid] = "pass"
                else:
                    outcome[tid] = "harbour"
                    detail[tid] = "harbour: %s %s | C#: %s %s" % ((verdict, rest) + cs[tid])
            elif cs[tid][0] == "PASS":
                outcome[tid] = "pass"
            else:
                outcome[tid] = "fail"
                detail[tid] = "%s %s" % cs[tid]
    per_fn = {}         # subject -> {outcome: n}
    also_in = {}        # function -> failing assertions it takes part in, not as subject
    tiers = {"run-fn": {}, "run-op": {}, "divergent": {}}   # only run-fn gates
    for tid, o in outcome.items():
        row = manifest.get(tid)
        if not row:
            continue
        tier = row["disp"].split(":")[0]
        if tier in tiers:
            tiers[tier][o] = tiers[tier].get(o, 0) + 1
        if tier == "run-fn":
            subj = per_fn.setdefault(row["subject"], {})
            subj[o] = subj.get(o, 0) + 1
            if o != "pass":
                for fn in row["fns"]:
                    if fn != row["subject"]:
                        also_in[fn] = also_in.get(fn, 0) + 1
    kinds = ["pass", "fail", "quarantine", "missing", "harbour"]
    with open(STATUS, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("# Per function the application calls: the outcomes of the assertions\n"
                 "# it is the subject of (their outermost call), and how many other\n"
                 "# failing assertions it takes part in.\n")
        fh.write("# function\tstate\tapp calls\t" + "\t".join(kinds) + "\talso in\n")
        for fn in sorted(needed, key=lambda f: (-needed[f][2], f)):
            canon, state, ncalls = needed[fn]
            c = per_fn.get(fn, {})
            fh.write("%s\t%s\t%d\t%s\t%d\n" % (canon, state, ncalls,
                     "\t".join(str(c.get(k, 0)) for k in kinds), also_in.get(fn, 0)))
    with open(os.path.join(WORK, "failures.txt"), "w", encoding="utf-8", newline="\n") as fh:
        for tid in sorted(detail, key=lambda t: (t.split(":")[0], int(t.split(":")[1]))):
            row = manifest.get(tid, {"expr": "?", "disp": "?"})
            fh.write("%s\t%s\t%s\t%s\t%s\n" % (tid, row["disp"], outcome[tid], row["expr"], detail[tid]))
    for tier, c in tiers.items():
        print("%-7s %s" % (tier, "  ".join("%s %d" % (k, c.get(k, 0)) for k in kinds)))
    gating = sum(tiers["run-fn"].get(k, 0) for k in ("fail", "quarantine", "missing", "harbour"))
    print("status.tsv: %d functions; failures in work/failures.txt" % len(per_fn))
    return gating == 0


def main():
    args = sys.argv[1:]
    stage = args.pop(0) if args and args[0] in ("all", "prg", "cs", "run", "report") else "all"
    needed = DEFAULT_NEEDED
    if "--needed" in args:
        i = args.index("--needed")
        needed = args[i + 1]
        del args[i:i + 2]
    names = names_from(args)
    ok = True
    if stage in ("all", "prg"):
        ok &= stage_prg(names)
    if stage in ("all", "cs"):
        ok &= stage_cs(names)
    if stage in ("all", "run"):
        stage_run(names)
    if stage in ("all", "run", "report"):
        ok &= stage_report(names, needed)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
