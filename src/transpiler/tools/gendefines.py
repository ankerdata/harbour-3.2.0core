#!/usr/bin/env python3
"""
Generate per-source-file C# const classes from Harbour `#define` directives.

The transpiler emits source-level `#define`s directly into the generated
C# (as consts at file scope). But header `#define`s living in .ch files
aren't expanded by the transpiler's preprocessor (which runs with
setNoInclude so header text doesn't flow into the parser). This tool
fills that gap for literal-valued defines — enough to cover the large
majority of real-world headers that consist of flag/enum-style constants.

## Output

  - One `<Name>Const.cs` per source file that contributes at least one
    used literal define. Class naming disambiguates .ch/.prg collisions:
        rmcomm.ch   → public static class RmcommConst
        deadbill.prg → public static class DeadbillPrgConst

  - A `defines_map.txt` sibling. One `NAME<TAB>ClassName<TAB>Owner` line
    per used define. Owner is empty for header-origin defines (globally
    visible). Owner is the .prg basename for defines harvested from a
    .prg — those shadow the global map *only* when compiling that file.

## Scoping rules

Header-origin defines are global: any .prg that references the name gets
qualified against the header's class. Prg-origin defines are strictly
file-local — Harbour preprocessor semantics. The transpiler reads the
map and for the file it is compiling:

  - If a local row matches (Owner == current file, Name == ref) →
    qualify as `OwnerClass.NAME` and skip inline PPDEFINE emission.
  - Else if a global row matches → qualify as `HeaderClass.NAME`.
  - Else → emit bare (locals outside their owning file stay bare).

## What's recognized for `#define NAME VALUE`

  - Numeric literals: `1`, `-3`, `0xFF`, `1.5`
  - String literals: `"foo"`
  - Harbour boolean literals: `.T.` / `.F.`
  - Pure numeric expressions of literals: `(256 * 8) - 1`
  - `chr(N) [+ chr(N)]...` sequences — common for binary magic values
  - Trailing `//` / `&&` Harbour-style comments are stripped

## Skipped

  - Macro-like: `#define FOO(x) ...`
  - Values that reference other identifiers or call functions other
    than `chr()`

## CLI

    python3 gendefines.py \\
        --include-dir /path/to/include [--include-dir ...] \\
        --src-dir     /path/to/src [--src-dir ...] \\
        --output-dir  /path/to/Defines \\
        [--class-suffix Const]

`--include-dir` is walked recursively for `*.ch`. `--src-dir` is walked
recursively for `*.ch` and `*.prg` — any .prg is additionally used for
usage filtering (only defines whose name appears as a token in some .prg
are emitted).
"""
from __future__ import annotations

import argparse
import ast
import os
import re
import sys
from typing import Dict, Iterable, List, Optional, Set, Tuple

# ---------------------------------------------------------------------------
# Parsers
# ---------------------------------------------------------------------------

DEF_RE   = re.compile(r'^\s*#\s*define\s+([A-Za-z_][A-Za-z0-9_]*)\s+(.+?)\s*$')
# Function-like macro: `NAME(` with NO whitespace — that distinguishes
# it from `#define NAME (expr)` which is an expression-valued constant
# (parens around an expression are common to avoid operator-precedence
# surprises at substitution sites).
MACRO_RE = re.compile(r'^\s*#\s*define\s+([A-Za-z_][A-Za-z0-9_]*)\(')

NUM_RE      = re.compile(r'^-?(?:0x[0-9a-fA-F]+|\d+(?:\.\d+)?)$')
STR_RE      = re.compile(r'^"(.*)"$')
EXPR_RE     = re.compile(r'^[\s\d.()+\-*/%xA-Fa-f]+$')
CHR_CALL_RE = re.compile(r'^\s*chr\s*\(\s*(\d+)\s*\)\s*$', re.IGNORECASE)
SPACE_RE    = re.compile(r'^\s*space\s*\(\s*(\d+)\s*\)\s*$', re.IGNORECASE)
REPL_RE     = re.compile(
    r'^\s*replicate\s*\(\s*(?:"(.)"|chr\s*\(\s*(\d+)\s*\))\s*,\s*(\d+)\s*\)\s*$',
    re.IGNORECASE)
BOOL_RE     = re.compile(r'^\.([TtFf])\.$')


def strip_trailing_comment(v: str) -> str:
    """Strip `//`, `&&`, and `/* ... */` comments after a value, outside
    string literals. Many easipos headers annotate numeric defines with
    a block comment (`#define GENERIC_WRITE 1073741824 /* 0x40000000L */`);
    without block-comment stripping the numeric literal never matches
    NUM_RE and the define is silently dropped."""
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
        elif not in_str and c == '/' and i + 1 < len(v) and v[i + 1] == '*':
            end = v.find('*/', i + 2)
            if end == -1:
                break                     # unterminated — drop the rest
            i = end + 2
            continue
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
    """Returns ('decimal', int|float) | ('string', str) | ('bool', bool) | None."""
    v = strip_trailing_comment(raw).strip()
    if not v:
        return None

    m = BOOL_RE.match(v)
    if m:
        return ('bool', m.group(1).upper() == 'T')

    m = STR_RE.match(v)
    if m:
        return ('string', m.group(1))

    chr_val = try_chr_expr(v)
    if chr_val is not None:
        return ('string', chr_val)

    # `space(N)` → string of N spaces.
    m = SPACE_RE.match(v)
    if m:
        return ('string', ' ' * int(m.group(1)))

    # `replicate("x", N)` or `replicate(chr(N), N)` → N copies of char.
    m = REPL_RE.match(v)
    if m:
        ch = m.group(1) if m.group(1) is not None else chr(int(m.group(2)))
        return ('string', ch * int(m.group(3)))

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
# Class naming
# ---------------------------------------------------------------------------

_FILENAME_CASING: Dict[str, str] = {}


def load_filename_casing(path: str) -> None:
    """Populate the global stem → CamelCase map from a casing file.

    Same format consumed by the C transpiler via --filename-casing.
    Silent no-op if the path is empty / missing — the caller falls
    back to the on-disk casing.
    """
    _FILENAME_CASING.clear()
    if not path or not os.path.isfile(path):
        return
    with open(path) as f:
        for line in f:
            s = line.strip()
            if not s or s.startswith('#'):
                continue
            parts = s.split(None, 1)
            if len(parts) != 2:
                continue
            stem, canon = parts
            _FILENAME_CASING[stem.lower()] = canon


def class_name_for(source_basename: str, suffix: str) -> str:
    """Derive a C# class name from a source filename.

    `rmcomm.ch`   → `RmcommConst`           (no map entry)
    `staticmem.ch` → `StaticMemConst`       (with `staticmem StaticMem`)
    `deadbill.prg` → `DeadbillPrgConst`     (no map entry)
    `fplu.prg`    → `FPluPrgConst`          (with `fplu FPlu`)

    The `Prg` infix disambiguates the ~20 stem collisions (same-named
    .ch and .prg) in the easipos corpus.
    """
    stem, ext = os.path.splitext(os.path.basename(source_basename))
    if not stem:
        return suffix
    titled = _FILENAME_CASING.get(stem.lower(), stem[0].upper() + stem[1:])
    if ext.lower() == '.prg':
        return titled + 'Prg' + suffix
    return titled + suffix


# ---------------------------------------------------------------------------
# Walking the file tree
# ---------------------------------------------------------------------------

def walk_files(roots: Iterable[str], exts: Tuple[str, ...]) -> Iterable[str]:
    for root in roots:
        for dirpath, _, files in os.walk(root):
            for f in files:
                if f.endswith(exts):
                    yield os.path.join(dirpath, f)


def extract_defines(content: str) -> Dict[str, Tuple[str, object]]:
    """Parse literal #defines from a single file's text. First-wins on collisions."""
    out: Dict[str, Tuple[str, object]] = {}
    for line in content.splitlines():
        if MACRO_RE.match(line):
            continue
        m = DEF_RE.match(line)
        if not m:
            continue
        name, rawval = m.group(1), m.group(2)
        parsed = parse_value(rawval)
        if parsed is None:
            continue
        if name in out:
            continue
        out[name] = parsed
    return out


def extract_raw_defines(content: str) -> Dict[str, str]:
    """Parse every #define NAME VALUE line, return {name: raw_value}.
    Used by the cross-reference resolver — we keep unparseable values
    so a second pass can substitute identifiers and retry."""
    out: Dict[str, str] = {}
    for line in content.splitlines():
        if MACRO_RE.match(line):
            continue
        m = DEF_RE.match(line)
        if not m:
            continue
        name, rawval = m.group(1), m.group(2)
        if name in out:
            continue
        out[name] = strip_trailing_comment(rawval).strip()
    return out


# Matches an identifier `(NAME)` or bare `NAME` surrounded by non-word
# chars in an expression — lets the resolver swap in known constant
# values before re-parsing. Bare-word boundary handled via lookarounds.
IDENT_IN_EXPR_RE = re.compile(r'\b([A-Za-z_][A-Za-z0-9_]*)\b')


def resolve_cross_refs(
    raw_by_file: Dict[str, Dict[str, str]],
    already_parsed: Dict[str, Dict[str, Tuple[str, object]]]
) -> None:
    """Iteratively resolve #define expressions that reference other
    #defines (e.g. `ITEMCONSOL (NOITEMFIELDS + 1)` where NOITEMFIELDS
    is also a #define). Mutates `already_parsed` in place — newly
    resolved values land in the same per-file bucket as their raw entry.

    Algorithm: keep a global map of already-known numeric literals
    (first-wins across files, matching existing `resolve_header_globals`
    behaviour). Each iteration walks every still-unresolved raw entry
    and tries substituting every identifier reference with its known
    decimal value, then re-running `parse_value`. Stops when no new
    entry is resolved in a full pass (fixpoint) or after a generous
    iteration cap."""
    known: Dict[str, object] = {}
    for file_defs in already_parsed.values():
        for name, (kind, val) in file_defs.items():
            if kind == 'decimal' and name not in known:
                known[name] = val

    unresolved = [
        (basename, name, raw)
        for basename, defs in raw_by_file.items()
        for name, raw in defs.items()
        if name not in already_parsed.get(basename, {})
    ]

    for _ in range(8):                         # fixpoint with cap
        progressed = False
        still_unresolved = []
        for basename, name, raw in unresolved:
            # Substitute identifier references with their known decimal
            # value. Unknown identifiers are left as-is — parse_value
            # will reject anything it can't reduce to a literal.
            def sub(m):
                ident = m.group(1)
                if ident in known:
                    return str(known[ident])
                return ident
            substituted = IDENT_IN_EXPR_RE.sub(sub, raw)
            parsed = parse_value(substituted)
            if parsed is None:
                still_unresolved.append((basename, name, raw))
                continue
            already_parsed.setdefault(basename, {})[name] = parsed
            if parsed[0] == 'decimal' and name not in known:
                known[name] = parsed[1]
            progressed = True
        unresolved = still_unresolved
        if not progressed:
            break


def scan_all(include_dirs: Iterable[str], src_dirs: Iterable[str]
             ) -> Tuple[Dict[str, Dict[str, Tuple[str, object]]],
                        Dict[str, Dict[str, Tuple[str, object]]],
                        Set[str]]:
    """Walk inputs. Harvest per-file defines. Collect token usage from .prg.

    Returns (header_defines, prg_defines, tokens):
      header_defines — {basename: {name: (type, value)}} for .ch inputs
      prg_defines    — {basename: {name: (type, value)}} for .prg inputs
      tokens         — identifier tokens seen anywhere in .prg sources
    """
    header_defines: Dict[str, Dict[str, Tuple[str, object]]] = {}
    prg_defines: Dict[str, Dict[str, Tuple[str, object]]] = {}
    tokens: Set[str] = set()
    # Raw (unparsed) defines per file, for cross-reference resolution
    # below. An entry like `ITEMCONSOL (NOITEMFIELDS + 1)` fails the
    # first-pass parse; the resolver then substitutes NOITEMFIELDS with
    # its known decimal value and retries.
    header_raw: Dict[str, Dict[str, str]] = {}
    prg_raw: Dict[str, Dict[str, str]] = {}

    def scan(path: str) -> None:
        try:
            with open(path, encoding='utf-8', errors='replace') as fh:
                content = fh.read()
        except Exception as e:
            print(f"warn: {path}: {e}", file=sys.stderr)
            return
        basename = os.path.basename(path)
        is_prg = path.endswith('.prg')
        if is_prg:
            tokens.update(re.findall(r'\b[A-Za-z_][A-Za-z0-9_]{1,}\b',
                                     content))
        defs = extract_defines(content)
        raw = extract_raw_defines(content)
        raw_bucket = (prg_raw if is_prg else header_raw).setdefault(
            basename, {})
        for n, v in raw.items():
            if n not in raw_bucket:
                raw_bucket[n] = v
        if not defs:
            return
        bucket = (prg_defines if is_prg else header_defines).setdefault(
            basename, {})
        for n, v in defs.items():
            if n not in bucket:
                bucket[n] = v

    for path in walk_files(include_dirs, ('.ch',)):
        scan(path)
    for path in walk_files(src_dirs, ('.ch', '.prg')):
        scan(path)

    # Resolve cross-references: defines whose value references other
    # #defines (e.g. `ITEMCONSOL (NOITEMFIELDS + 1)`). Run once with
    # the union of header + prg raws so a prg can reference a header
    # global. Mutates header_defines/prg_defines in place.
    combined_raw = {**header_raw, **prg_raw}
    combined_parsed = {**header_defines, **prg_defines}
    resolve_cross_refs(combined_raw, combined_parsed)
    # Split back — header entries may have gained new names.
    for basename, defs in combined_parsed.items():
        if basename in header_raw:
            header_defines[basename] = defs
        else:
            prg_defines[basename] = defs
    return header_defines, prg_defines, tokens


def resolve_header_globals(header_defines: Dict[str, Dict[str, Tuple[str, object]]]
                           ) -> Tuple[Dict[str, Tuple[str, Tuple[str, object]]],
                                      List[Tuple[str, str, str,
                                                 Tuple[str, object],
                                                 Tuple[str, object]]]]:
    """{name: (header_basename, val)} — first header to declare each name wins.

    Returns (first_wins, conflicts)."""
    first: Dict[str, Tuple[str, Tuple[str, object]]] = {}
    conflicts = []
    for basename in sorted(header_defines):
        for name, val in header_defines[basename].items():
            if name not in first:
                first[name] = (basename, val)
            elif first[name][1] != val:
                conflicts.append((name, first[name][0], basename,
                                  first[name][1], val))
    return first, conflicts


# ---------------------------------------------------------------------------
# Emission
# ---------------------------------------------------------------------------

def cs_escape_string(s: str) -> str:
    # Verbatim strings preserve Harbour no-escape semantics; double any `"`.
    return '@"' + s.replace('"', '""') + '"'


def emit_cs(used: Dict[str, Tuple[str, object]], class_name: str,
            source_basename: str) -> str:
    lines = [
        '// Auto-generated by src/transpiler/tools/gendefines.py',
        f'// Harvested from {source_basename}.',
        '',
        f'public static class {class_name}',
        '{',
    ]
    for name in sorted(used):
        t, val = used[name]
        if t == 'string':
            assert isinstance(val, str)
            lines.append(
                f'    public const string {name} = {cs_escape_string(val)};'
            )
        elif t == 'bool':
            lines.append(
                f'    public const bool {name} = '
                f'{"true" if val else "false"};'
            )
        else:
            if isinstance(val, float):
                lines.append(f'    public const decimal {name} = {val}m;')
            else:
                lines.append(f'    public const decimal {name} = {val};')
    lines.append('}')
    return '\n'.join(lines) + '\n'


def write_class_files(per_file: Dict[str, Dict[str, Tuple[str, object]]],
                      tokens: Set[str], class_suffix: str,
                      output_dir: str) -> Tuple[int, int]:
    """Emit one <Class>.cs per basename in per_file, filtering by tokens."""
    os.makedirs(output_dir, exist_ok=True)
    files_written = 0
    defines_emitted = 0
    for basename, defs in sorted(per_file.items()):
        used_defs = {n: v for n, v in defs.items() if n in tokens}
        if not used_defs:
            continue
        class_name = class_name_for(basename, class_suffix)
        out_path = os.path.join(output_dir, class_name + '.cs')
        with open(out_path, 'w') as f:
            f.write(emit_cs(used_defs, class_name, basename))
        files_written += 1
        defines_emitted += len(used_defs)
    return files_written, defines_emitted


def write_map(header_first: Dict[str, Tuple[str, Tuple[str, object]]],
              prg_defines: Dict[str, Dict[str, Tuple[str, object]]],
              tokens: Set[str], class_suffix: str,
              output_dir: str) -> Tuple[int, int, str]:
    """Emit defines_map.txt (NAME<TAB>ClassName<TAB>Owner).

    Owner is empty for header-origin globals and `basename.prg` for
    file-local prg defines. Returns (global_count, local_count, path)."""
    map_path = os.path.join(output_dir, 'defines_map.txt')
    global_lines = []
    for name in sorted(header_first):
        if name not in tokens:
            continue
        basename, _ = header_first[name]
        global_lines.append(
            f'{name}\t{class_name_for(basename, class_suffix)}\t')

    local_lines = []
    for prg_basename in sorted(prg_defines):
        cls = class_name_for(prg_basename, class_suffix)
        for name in sorted(prg_defines[prg_basename]):
            if name not in tokens:
                continue
            local_lines.append(f'{name}\t{cls}\t{prg_basename}')

    with open(map_path, 'w') as f:
        f.write('\n'.join(global_lines + local_lines))
        if global_lines or local_lines:
            f.write('\n')
    return len(global_lines), len(local_lines), map_path


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main() -> int:
    ap = argparse.ArgumentParser(
        description="Generate per-source-file C# const classes from "
                    "Harbour #define directives.")
    ap.add_argument('--include-dir', action='append', required=True,
                    help='Directory tree to walk for .ch files (repeatable)')
    ap.add_argument('--src-dir', action='append', required=True,
                    help='Directory tree to walk for .prg and .ch files — '
                         'used for token-usage filtering and to harvest '
                         'defines found alongside sources (repeatable)')
    ap.add_argument('--output-dir', required=True,
                    help='Directory to write per-source .cs files and '
                         'defines_map.txt')
    ap.add_argument('--class-suffix', default='Const',
                    help='Suffix on class names (default: Const). .prg '
                         'classes get a `Prg` infix before this suffix.')
    ap.add_argument('--filename-casing', default=None,
                    help='Path to a stem → CamelCase map (one entry per '
                         'line, "<stem><TAB><CamelCase>"). Overrides the '
                         'default upper-first stem casing for Const '
                         'class names.  Same file consumed by the C '
                         'transpiler via --filename-casing.')
    args = ap.parse_args()

    if args.filename_casing:
        load_filename_casing(args.filename_casing)

    header_defines, prg_defines, tokens = scan_all(
        args.include_dir, args.src_dir)
    header_first, header_conflicts = resolve_header_globals(header_defines)

    total = (sum(len(v) for v in header_defines.values())
             + sum(len(v) for v in prg_defines.values()))
    print(f"parsed {total} literal defines from "
          f"{len(header_defines)} header(s) and "
          f"{len(prg_defines)} prg file(s)", file=sys.stderr)
    if header_conflicts:
        print(f"{len(header_conflicts)} header/header conflicts "
              f"(kept first):", file=sys.stderr)
        for name, f1, f2, v1, v2 in header_conflicts[:20]:
            print(f"  {name}: {f1}={v1!r}  vs  {f2}={v2!r}", file=sys.stderr)
        if len(header_conflicts) > 20:
            print(f"  ... and {len(header_conflicts) - 20} more",
                  file=sys.stderr)

    header_files, header_emitted = write_class_files(
        header_defines, tokens, args.class_suffix, args.output_dir)
    prg_files, prg_emitted = write_class_files(
        prg_defines, tokens, args.class_suffix, args.output_dir)
    globals_n, locals_n, map_path = write_map(
        header_first, prg_defines, tokens, args.class_suffix, args.output_dir)

    print(f"wrote {header_files} header class(es) with {header_emitted} "
          f"defines", file=sys.stderr)
    print(f"wrote {prg_files} prg class(es) with {prg_emitted} defines",
          file=sys.stderr)
    print(f"wrote {globals_n} global + {locals_n} local entries to "
          f"{map_path}", file=sys.stderr)
    return 0


if __name__ == '__main__':
    sys.exit(main())
