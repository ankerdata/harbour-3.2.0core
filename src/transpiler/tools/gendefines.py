#!/usr/bin/env python3
"""
Generate a C# file of `public const` declarations from Harbour `#define`
directives found in include headers, filtered to those actually
referenced in a body of .prg source.

The transpiler emits source-level `#define`s directly into the generated
C# (as consts at file scope). But header `#define`s living in .ch files
aren't expanded by the transpiler's preprocessor (which runs with
setNoInclude so header text doesn't flow into the parser). This tool
fills that gap for literal-valued defines — enough to cover the large
majority of real-world headers that consist of flag/enum-style constants.

## What's recognized

For `#define NAME VALUE`:

  - Numeric literals: `1`, `-3`, `0xFF`, `1.5`
  - String literals: `"foo"`
  - Pure numeric expressions of literals: `(256 * 8) - 1`
  - `chr(N) [+ chr(N)]...` sequences — common for binary magic values
  - Trailing `//` / `&&` Harbour-style comments are stripped

## What's skipped

  - Macro-like: `#define FOO(x) ...`
  - Values that reference other identifiers or call functions other
    than `chr()`
  - Names that are also `#define`d inside one of the `.prg` sources
    (the transpiler already emits those as file-scope consts; duplicating
    here causes CS0102 collisions)
  - Conflicting re-definitions across headers (kept first, logged to stderr)

## CLI

    python3 gendefines.py \\
        --include-dir /path/to/include [--include-dir ...] \\
        --src-dir     /path/to/src [--src-dir ...] \\
        --output      /path/to/EasiDefines.cs \\
        [--class-name Program]

`--include-dir` is walked recursively for `*.ch`. `--src-dir` is walked
recursively for `*.prg` and used both for the usage check (emit only
names that appear in at least one PRG token) and for the PRG-defines
skip list. `--class-name` defaults to `Program`, matching the class
the C# transpiler emits standalone functions into.
"""
from __future__ import annotations

import argparse
import ast
import os
import re
import sys
from typing import Dict, Iterable, Optional, Set, Tuple

# ---------------------------------------------------------------------------
# Parsers
# ---------------------------------------------------------------------------

DEF_RE   = re.compile(r'^\s*#\s*define\s+([A-Za-z_][A-Za-z0-9_]*)\s+(.+?)\s*$')
MACRO_RE = re.compile(r'^\s*#\s*define\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(')

NUM_RE     = re.compile(r'^-?(?:0x[0-9a-fA-F]+|\d+(?:\.\d+)?)$')
STR_RE     = re.compile(r'^"(.*)"$')
EXPR_RE    = re.compile(r'^[\s\d.()+\-*/%xA-Fa-f]+$')
CHR_CALL_RE = re.compile(r'^\s*chr\s*\(\s*(\d+)\s*\)\s*$', re.IGNORECASE)


def strip_trailing_comment(v: str) -> str:
    """Strip `//` or `&&` comments after a value, outside string literals."""
    out = []
    in_str = False
    i = 0
    while i < len(v):
        c = v[i]
        if c == '"':
            in_str = not in_str
            out.append(c)
        elif not in_str and c == '/' and i + 1 < len(v) and v[i + 1] == '/':
            break
        elif not in_str and c == '&' and i + 1 < len(v) and v[i + 1] == '&':
            break
        else:
            out.append(c)
        i += 1
    return ''.join(out).rstrip()


def try_chr_expr(v: str) -> Optional[str]:
    """`chr(N) [+ chr(N)]...` → concatenated string."""
    parts = [p.strip() for p in v.split('+')]
    chars = []
    for p in parts:
        m = CHR_CALL_RE.match(p)
        if not m:
            return None
        chars.append(chr(int(m.group(1))))
    return ''.join(chars) if chars else None


def parse_value(raw: str) -> Optional[Tuple[str, object]]:
    """Returns ('decimal', int|float) | ('string', str) | None."""
    v = strip_trailing_comment(raw).strip()
    if not v:
        return None

    m = STR_RE.match(v)
    if m:
        return ('string', m.group(1))

    chr_val = try_chr_expr(v)
    if chr_val is not None:
        return ('string', chr_val)

    if NUM_RE.match(v):
        if v.lower().lstrip('-').startswith('0x'):
            return ('decimal', int(v, 16))
        if '.' in v:
            return ('decimal', float(v))
        return ('decimal', int(v))

    # Numeric arithmetic over literals. EXPR_RE filters the token set;
    # ast.parse then bounds what operations are permitted.
    if EXPR_RE.match(v):
        try:
            node = ast.parse(v, mode='eval')
            allowed = (
                ast.Expression, ast.BinOp, ast.UnaryOp, ast.Constant,
                ast.Add, ast.Sub, ast.Mult, ast.Div, ast.Mod,
                ast.USub, ast.UAdd, ast.FloorDiv, ast.Pow,
            )
            for n in ast.walk(node):
                if not isinstance(n, allowed):
                    return None
            val = eval(compile(node, '<def>', 'eval'))
            if isinstance(val, (int, float)):
                return ('decimal', val)
        except Exception:
            return None
    return None


# ---------------------------------------------------------------------------
# Walking the file tree
# ---------------------------------------------------------------------------

def walk_files(roots: Iterable[str], ext: str) -> Iterable[str]:
    for root in roots:
        for dirpath, _, files in os.walk(root):
            for f in files:
                if f.endswith(ext):
                    yield os.path.join(dirpath, f)


def scan_headers(include_dirs: Iterable[str]
                 ) -> Tuple[Dict[str, Tuple[str, object, str]], list]:
    """Walk include dirs for .ch files, collect literal #defines.
    Returns (name → (type, value, src_basename), conflicts)."""
    defines: Dict[str, Tuple[str, object, str]] = {}
    conflicts: list = []
    for path in walk_files(include_dirs, '.ch'):
        try:
            with open(path, encoding='utf-8', errors='replace') as fh:
                basename = os.path.basename(path)
                for line in fh:
                    if MACRO_RE.match(line):
                        continue
                    m = DEF_RE.match(line)
                    if not m:
                        continue
                    name, rawval = m.group(1), m.group(2)
                    parsed = parse_value(rawval)
                    if parsed is None:
                        continue
                    if name in defines:
                        prev = defines[name]
                        if (prev[0], prev[1]) != parsed:
                            conflicts.append((name, prev, parsed, basename))
                        continue
                    defines[name] = (parsed[0], parsed[1], basename)
        except Exception as e:
            print(f"warn: {path}: {e}", file=sys.stderr)
    return defines, conflicts


def collect_src_info(src_dirs: Iterable[str]) -> Tuple[Set[str], Set[str]]:
    """Walk PRG sources. Returns (tokens used anywhere, names #define'd in any PRG)."""
    tokens: Set[str] = set()
    prg_defines: Set[str] = set()
    for path in walk_files(src_dirs, '.prg'):
        try:
            with open(path, encoding='utf-8', errors='replace') as fh:
                content = fh.read()
        except Exception:
            continue
        tokens.update(re.findall(r'\b[A-Za-z_][A-Za-z0-9_]{1,}\b', content))
        for line in content.splitlines():
            if MACRO_RE.match(line):
                continue
            m = DEF_RE.match(line)
            if m:
                prg_defines.add(m.group(1))
    return tokens, prg_defines


# ---------------------------------------------------------------------------
# Emission
# ---------------------------------------------------------------------------

def cs_escape_string(s: str) -> str:
    # Verbatim strings preserve Harbour no-escape semantics; double any `"`.
    return '@"' + s.replace('"', '""') + '"'


def emit_cs(used: Dict[str, Tuple[str, object, str]], class_name: str) -> str:
    lines = [
        '// Auto-generated by src/transpiler/tools/gendefines.py',
        '// Consts for Harbour `#define`s from included headers that are',
        '// referenced in the project\'s PRG sources.',
        '',
        f'public static partial class {class_name}',
        '{',
    ]
    for name in sorted(used):
        t, val, src = used[name]
        if t == 'string':
            assert isinstance(val, str)
            lines.append(
                f'    public const string {name} = {cs_escape_string(val)};'
                f' // from {src}'
            )
        else:
            if isinstance(val, float):
                lines.append(
                    f'    public const decimal {name} = {val}m; // from {src}'
                )
            else:
                lines.append(
                    f'    public const decimal {name} = {val}; // from {src}'
                )
    lines.append('}')
    return '\n'.join(lines) + '\n'


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main() -> int:
    ap = argparse.ArgumentParser(
        description="Generate C# consts from Harbour #define directives.")
    ap.add_argument('--include-dir', action='append', required=True,
                    help='Directory tree to walk for .ch files (repeatable)')
    ap.add_argument('--src-dir', action='append', required=True,
                    help='Directory tree to walk for .prg files — used for '
                         'usage filtering and PRG-scope #define exclusion '
                         '(repeatable)')
    ap.add_argument('--output', required=True,
                    help='Path to write the generated .cs file')
    ap.add_argument('--class-name', default='Program',
                    help='C# partial class to emit into (default: Program)')
    args = ap.parse_args()

    defines, conflicts = scan_headers(args.include_dir)
    print(f"parsed {len(defines)} literal defines from "
          f"{len(args.include_dir)} include dir(s)", file=sys.stderr)
    if conflicts:
        print(f"{len(conflicts)} conflicting definitions (kept first):",
              file=sys.stderr)
        for name, (t1, v1, f1), (t2, v2), f2 in conflicts[:20]:
            print(f"  {name}: {f1}={v1!r}  vs  {f2}={v2!r}", file=sys.stderr)
        if len(conflicts) > 20:
            print(f"  ... and {len(conflicts) - 20} more", file=sys.stderr)

    tokens, prg_defines = collect_src_info(args.src_dir)
    used = {n: v for n, v in defines.items()
            if n in tokens and n not in prg_defines}
    print(f"{len(used)} referenced in .prg sources "
          f"(skipped {len(defines) - len(used)} "
          f"unreferenced or PRG-shadowed)", file=sys.stderr)

    cs = emit_cs(used, args.class_name)
    with open(args.output, 'w') as f:
        f.write(cs)
    print(f"wrote {args.output}", file=sys.stderr)
    return 0


if __name__ == '__main__':
    sys.exit(main())
