#!/usr/bin/env python3
"""
Generate src/transpiler/hbfuncs.tab from the Harbour source tree.

Three sources of truth:

  1. HbRuntime.cs — the C# runtime that the transpiler emits calls into.
     Any public static method here is a function the C# emitter must
     remap (e.g., QOut -> HbRuntime.QOut). This is the *prefix* source.

  2. doc/ + contrib/.../doc/ — Harbour's structured doc blocks.
     The $SYNTAX$ line carries a `--> token` whose Hungarian prefix
     reveals the return type. This is the *return type* source.

  3. src/rtl, src/rdd, contrib/, ... — every HB_FUNC() in C and every
     FUNCTION/PROCEDURE in .prg. Used only as a sanity check that the
     names we type from doc blocks really do correspond to runtime
     functions. Not directly written to the table.

A row is written whenever a function has *either* a known prefix
(it's implemented in HbRuntime.cs) *or* a known return type (we got
it from a doc block). Functions with neither contribute nothing and
are omitted.

Usage:  python3 src/transpiler/tools/genfunctab.py
        (run from the repo root)
"""

from __future__ import annotations

import os
import re
import sys
from pathlib import Path

# ---- repo layout ----------------------------------------------------------

REPO_ROOT     = Path(__file__).resolve().parents[3]
OUTPUT_PATH   = REPO_ROOT / "src" / "transpiler" / "hbfuncs.tab"
HBRUNTIME_CS  = REPO_ROOT / "src" / "transpiler" / "HbRuntime.cs"

# Where to look for HB_FUNC declarations and PRG functions.
SOURCE_ROOTS = [
    REPO_ROOT / "src" / "rtl",
    REPO_ROOT / "src" / "rdd",
    REPO_ROOT / "src" / "vm",
    REPO_ROOT / "src" / "common",
    REPO_ROOT / "src" / "lang",
    REPO_ROOT / "src" / "macro",
    REPO_ROOT / "src" / "main",
    REPO_ROOT / "src" / "pp",
    REPO_ROOT / "src" / "compiler",
    REPO_ROOT / "src" / "debug",
    REPO_ROOT / "src" / "codepage",
    REPO_ROOT / "contrib",
]

# Where to look for documentation blocks.
DOC_ROOTS = [
    REPO_ROOT / "doc" / "en",
    REPO_ROOT / "contrib",
]

# ---- patterns -------------------------------------------------------------

# HB_FUNC( NAME )            — single-argument: a defined function
# HB_FUNC_STATIC( NAME )     — module-private; not callable from PRG
# HB_FUNC_TRANSLATE( ALIAS, REAL )  — registers ALIAS as another name
#                                     for REAL. We capture ALIAS only;
#                                     REAL is a separate HB_FUNC elsewhere.
# HB_FUNC_EXTERN( NAME )     — forward declaration; counted (some
#                              functions are only declared this way
#                              in the unit they're used).
RE_HB_FUNC = re.compile(
    r"^[ \t]*HB_FUNC(?:_STATIC|_TRANSLATE|_EXTERN)?\(\s*"
    r"([A-Za-z_][A-Za-z0-9_]*)",
    re.MULTILINE,
)

# FUNCTION / PROCEDURE NAME( ... ) — Harbour is case-insensitive.
RE_PRG_FUNC = re.compile(
    r"^[ \t]*(?:STATIC[ \t]+)?(?:FUNCTION|PROCEDURE)[ \t]+"
    r"([A-Za-z_][A-Za-z0-9_]*)\s*\(",
    re.MULTILINE | re.IGNORECASE,
)

# Doc block: $NAME$ <name>  …  $SYNTAX$ <one or more lines>.
# The $SYNTAX$ body can wrap across lines (StrTran is a real example).
# We capture everything from $SYNTAX$ up to (but not including) the next
# $KEYWORD$ marker, so multi-line syntaxes get parsed in one piece.
RE_DOC_NAME    = re.compile(r"\$NAME\$\s*\n\s*([^\n]+)")
RE_DOC_SYNTAX  = re.compile(
    r"\$SYNTAX\$\s*\n(.*?)(?=\n\s*\$[A-Z]+\$|\Z)", re.DOTALL
)

# Function name appearing in a doc $NAME$ field, with the trailing
# parens-and-anything-after stripped, e.g. "hb_AtI()" -> "hb_AtI".
RE_NAME_CLEANUP = re.compile(r"^([A-Za-z_][A-Za-z0-9_]*)")

# Return-token after the arrow on a $SYNTAX$ line: capture the token.
# Accepts both `->` and `-->`. Allows trailing punctuation, comments, etc.
RE_ARROW = re.compile(r"-+>\s*([A-Za-z_][A-Za-z0-9_]*)")

# `public static [<modifiers>] <ReturnType> Name(` in HbRuntime.cs.
# We don't care about the return type or args, only the method name.
RE_HBRUNTIME_METHOD = re.compile(
    r"^\s*public\s+static\s+(?:readonly\s+)?[A-Za-z_][\w<>,\[\] ]*\s+"
    r"([A-Z][A-Za-z0-9_]*)\s*\(",
    re.MULTILINE,
)

# ---- type inference -------------------------------------------------------

# Harbour Hungarian-prefix convention applied to the return token.
PREFIX_TO_TYPE = {
    "n": "NUMERIC",
    "c": "STRING",
    "l": "LOGICAL",
    "d": "DATE",
    "t": "DATE",       # tStamp / tDateTime
    "a": "ARRAY",
    "h": "HASH",
    "o": "OBJECT",
    "b": "BLOCK",
    "p": "OBJECT",     # pointers — closest C# equivalent
    "x": None,         # explicitly variant — leave unknown
    "u": None,         # USUAL
    "m": "STRING",     # mMemo (rare)
}

# Special-case literal return tokens that aren't Hungarian.
SPECIAL_RETURN = {
    "NIL":  None,        # void / no return
    "Self": "OBJECT",
    "Identity": None,    # method-chaining / pass-through
}

def infer_return_type(token: str) -> str | None:
    if token in SPECIAL_RETURN:
        return SPECIAL_RETURN[token]
    if not token:
        return None
    first = token[0]
    # Hungarian prefix is a lowercase letter followed by an uppercase letter
    # (e.g. nResult). A bare lowercase word like "result" gives no info.
    if len(token) >= 2 and first.islower() and token[1].isupper():
        return PREFIX_TO_TYPE.get(first)
    return None

# ---- collection -----------------------------------------------------------

def iter_files(roots, suffixes):
    for root in roots:
        if not root.exists():
            continue
        for path in root.rglob("*"):
            if path.is_file() and path.suffix.lower() in suffixes:
                yield path

def harvest_names() -> dict[str, str]:
    """Return {name (uppercase) -> source kind ("c"/"prg")} for every
    Harbour-callable function found in the source tree."""
    names: dict[str, str] = {}

    for path in iter_files(SOURCE_ROOTS, {".c"}):
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        for m in RE_HB_FUNC.finditer(text):
            name = m.group(1).upper()
            names.setdefault(name, "c")

    for path in iter_files(SOURCE_ROOTS, {".prg"}):
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        for m in RE_PRG_FUNC.finditer(text):
            name = m.group(1).upper()
            names.setdefault(name, "prg")

    return names

def harvest_hbruntime_methods() -> set[str]:
    """Return the set of public static method names defined in
    HbRuntime.cs, uppercased. These are the functions the C# emitter
    must remap with the `HbRuntime.` prefix."""
    methods: set[str] = set()
    if not HBRUNTIME_CS.exists():
        print(f"warning: {HBRUNTIME_CS} not found", file=sys.stderr)
        return methods
    text = HBRUNTIME_CS.read_text(encoding="utf-8", errors="replace")
    for m in RE_HBRUNTIME_METHOD.finditer(text):
        methods.add(m.group(1).upper())
    return methods

def harvest_doc_types() -> dict[str, str]:
    """Return {name (uppercase) -> RETTYPE} for every documented function
    where the $SYNTAX$ line carries a recognisable return token."""
    types: dict[str, str] = {}

    for path in iter_files(DOC_ROOTS, {".txt", ".md"}):
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue

        # Walk DOC blocks. Each doc may contain multiple $NAME$/$SYNTAX$
        # pairs; pair them by position so a 5-function file gets parsed
        # correctly.
        names_iter   = list(RE_DOC_NAME.finditer(text))
        syntax_iter  = list(RE_DOC_SYNTAX.finditer(text))

        # Walk both lists in lock-step by source position. Each $NAME$
        # gets paired with the first $SYNTAX$ that follows it (and
        # precedes the next $NAME$).
        i = 0
        for ni, nm in enumerate(names_iter):
            name_start = nm.start()
            name_end   = (names_iter[ni + 1].start()
                          if ni + 1 < len(names_iter)
                          else len(text))
            # find the syntax in [name_start, name_end)
            syntax_match = None
            while i < len(syntax_iter) and syntax_iter[i].start() < name_start:
                i += 1
            if i < len(syntax_iter) and syntax_iter[i].start() < name_end:
                syntax_match = syntax_iter[i]

            if not syntax_match:
                continue

            raw_name = nm.group(1).strip()
            cleanup = RE_NAME_CLEANUP.match(raw_name)
            if not cleanup:
                continue
            name = cleanup.group(1).upper()

            arrow = RE_ARROW.search(syntax_match.group(1))
            if not arrow:
                continue
            ret_type = infer_return_type(arrow.group(1))
            if ret_type and name not in types:
                types[name] = ret_type

    return types

# ---- output ---------------------------------------------------------------

HEADER = """\
# Harbour function table — used by the C# transpiler
#
# Combines two pieces of information about each known Harbour function:
#   1. RETTYPE: declared/inferred return type, used by type propagation.
#               One of STRING NUMERIC LOGICAL DATE ARRAY HASH OBJECT BLOCK
#               or "-" if unknown / void / procedure.
#   2. PREFIX:  namespace to remap calls to. e.g., a function with prefix
#               "HbRuntime" produces `HbRuntime.NAME(...)` in C#.
#               Use "-" if the function is called as-is (no remap).
#
# Format: NAME<TAB>RETTYPE<TAB>PREFIX
# Lines starting with `#` and blank lines are ignored. Names are
# case-insensitive but conventionally written UPPERCASE.
#
# THIS FILE IS GENERATED — see src/transpiler/tools/genfunctab.py
# To override an entry, add a row to a hand-curated overrides file
# (loaded after this one) rather than editing here directly.
"""

def write_table(path: Path,
                hbruntime: set[str],
                types: dict[str, str]):
    """Emit a row whenever a function has at least one piece of
    information attached: either a prefix (it's implemented in
    HbRuntime.cs) or a return type (parsed from a doc block).

    Functions with neither contribute nothing and are omitted; the
    C# emitter will pass those through unchanged with no type info."""
    all_names = set(hbruntime) | set(types.keys())
    rows = []
    for name in sorted(all_names):
        rettype = types.get(name) or "-"
        prefix  = "HbRuntime" if name in hbruntime else "-"
        rows.append(f"{name}\t{rettype}\t{prefix}")
    with path.open("w", encoding="utf-8") as f:
        f.write(HEADER)
        f.write("\n")
        f.write("\n".join(rows))
        f.write("\n")

# ---- entry point ----------------------------------------------------------

def main() -> int:
    print(f"repo root:    {REPO_ROOT}")
    print(f"writing:      {OUTPUT_PATH.relative_to(REPO_ROOT)}")

    print("parsing HbRuntime.cs for prefix entries...")
    hbruntime = harvest_hbruntime_methods()
    print(f"  {len(hbruntime)} public static methods found")

    print("harvesting return types from doc blocks...")
    types = harvest_doc_types()
    print(f"  {len(types)} return types parsed from doc blocks")

    print("sanity-checking names against HB_FUNC declarations...")
    names = harvest_names()
    runtime_only = [n for n in hbruntime if n not in names]
    if runtime_only:
        print(f"  warning: {len(runtime_only)} HbRuntime methods have no "
              f"matching HB_FUNC ({', '.join(sorted(runtime_only)[:8])}...)")
    typed_no_runtime = [n for n in types if n not in names]
    if typed_no_runtime:
        print(f"  note: {len(typed_no_runtime)} typed names have no "
              f"matching HB_FUNC (probably PRG-only or alias)")

    rows = len(set(hbruntime) | set(types))
    print(f"writing {rows} rows...")
    write_table(OUTPUT_PATH, hbruntime, types)
    print("done.")
    return 0

if __name__ == "__main__":
    sys.exit(main())
