#!/usr/bin/env python3
"""Tests for gendefines.parse_value — the literal-extraction core that
feeds defines/<Name>Const.cs and the defines_map.txt. Run with:

    python3 src/transpiler/tools/test_gendefines.py

Failures abort with a non-zero exit; success prints "all X tests passed".
Kept deliberately simple (stdlib `assert`, no pytest) so it works in any
environment that can run gendefines.py itself.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from gendefines import HIDDEN_REGION, emit_cs, parse_value, resolve_cross_refs  # type: ignore


CASES = [
    # (raw #define body, expected_kind, expected_value)
    # Strings / bools / numbers — existing coverage; keep them as regression
    # anchors so an over-eager change to the parser can't silently drop them.
    ('.T.',                         'bool',    True),
    ('.f.',                         'bool',    False),
    ('"hello"',                     'string',  'hello'),
    ('"line " // trailing comment', 'string',  'line '),
    ('chr(65)',                     'string',  'A'),
    ('chr(13) + chr(10)',           'string',  '\r\n'),
    ('42',                          'decimal', 42),
    ('0xFF',                        'decimal', 255),
    ('3.14',                        'decimal', 3.14),
    ('5 + 3 * 2',                   'decimal', 11),

    # Block-comment annotations — added 2026-04-22 for fileapi.ch's
    # `#define GENERIC_WRITE 1073741824 /* 0x40000000L */` pattern.
    # Six of eight defines in that header were dropped until the
    # strip-trailing-comment pass learned to eat `/* ... */`.
    ('1073741824 /* 0x40000000L */', 'decimal', 1073741824),
    ('"x" /* inline */',             'string',  'x'),
    ('/* lead */ 42',                'decimal', 42),

    # space(N) — added 2026-04-22 for the MODE/SEQID/FILLER8/etc cluster
    # in kscrprt.prg (Harbour `#define MODE space(12)` etc.). Without this
    # the emit was bare `MODE` which doesn't resolve in C#.
    ('space(12)',                   'string',  '            '),
    ('space(0)',                    'string',  ''),
    ('SPACE(3)',                    'string',  '   '),
    (' space( 7 )',                 'string',  '       '),

    # replicate(literal, N) / replicate(chr(N), M) — same session.
    ('replicate("X", 16)',          'string',  'X' * 16),
    ('replicate( "0", 4 )',         'string',  '0000'),
    ('replicate(chr(32), 5)',       'string',  '     '),

    # Things we should NOT match — must return None, not misparse.
    ('someFunc()',                  None, None),
    ('space(x)',                    None, None),           # non-literal arg
    ('replicate(x, 5)',             None, None),           # non-literal char
    ('replicate("ab", 3)',          None, None),           # only single-char literal supported
]


def test_cross_refs():
    """Added 2026-04-22. `ITEMCONSOL (NOITEMFIELDS + 1)` pattern from
    buffer.ch — expression references another #define which IS a
    literal. Resolver should evaluate it."""
    # already_parsed — what first-pass parse_value could handle.
    parsed = {
        'buffer.ch': {
            'NOITEMFIELDS': ('decimal', 24),
        },
    }
    raw = {
        'buffer.ch': {
            'NOITEMFIELDS': '24',
            'ITEMCONSOL':   '(NOITEMFIELDS + 1)',
            'ITEMKPLIST':   '(NOITEMFIELDS + 1)',
            'UNRELATED':    'someFunc()',         # must stay unresolved
        },
    }
    resolve_cross_refs(raw, parsed)
    errs = []
    if parsed['buffer.ch'].get('ITEMCONSOL') != ('decimal', 25):
        errs.append(f"ITEMCONSOL: expected ('decimal', 25), got "
                    f"{parsed['buffer.ch'].get('ITEMCONSOL')!r}")
    if parsed['buffer.ch'].get('ITEMKPLIST') != ('decimal', 25):
        errs.append(f"ITEMKPLIST: expected ('decimal', 25), got "
                    f"{parsed['buffer.ch'].get('ITEMKPLIST')!r}")
    if 'UNRELATED' in parsed['buffer.ch']:
        errs.append(f"UNRELATED should not have resolved, got "
                    f"{parsed['buffer.ch'].get('UNRELATED')!r}")
    return errs


def test_hidden():
    """A string #define a .prg uses under `#pragma TEXTHIDDEN=1` is
    emitted encoded, as gencsharp.c writes a literal there; one used
    only outside it stays a const."""
    prg = '\n'.join(['return PLAIN', '#pragma TEXTHIDDEN=1', '    return SECRET',
                     '#pragma TEXTHIDDEN=0', 'return OTHER', ''])
    hidden = set()
    for m in HIDDEN_REGION.finditer(prg):
        hidden.update(m.group(1).split())
    cs = emit_cs({'SECRET': ('string', 'Ab'), 'PLAIN': ('string', 'Ab')},
                 'KeyConst', 'key.ch', hidden)
    errs = []
    if 'SECRET' not in hidden or 'PLAIN' in hidden or 'OTHER' in hidden:
        errs.append(f"the region's tokens: {sorted(hidden)!r}")
    if ('public static readonly string SECRET = '
            'HbRuntime.HbHiddenText(new byte[] { 0xE6, 0xA4 });') not in cs:
        errs.append("SECRET is not emitted encoded: " + cs)
    if 'public const string PLAIN = @"Ab";' not in cs:
        errs.append("PLAIN is not a const: " + cs)
    return errs


def run():
    failures = []
    for raw, exp_kind, exp_val in CASES:
        got = parse_value(raw)
        if exp_kind is None:
            if got is not None:
                failures.append(f"{raw!r}: expected None, got {got!r}")
            continue
        if got is None:
            failures.append(f"{raw!r}: expected ({exp_kind!r}, {exp_val!r}), got None")
            continue
        kind, val = got
        if kind != exp_kind or val != exp_val:
            failures.append(
                f"{raw!r}: expected ({exp_kind!r}, {exp_val!r}), got ({kind!r}, {val!r})"
            )
    # Cross-reference resolver — separate from parse_value coverage.
    for msg in test_cross_refs():
        failures.append(f"cross_refs: {msg}")

    for msg in test_hidden():
        failures.append(f"hidden: {msg}")

    total = len(CASES) + 3 + 3                 # 3 asserts in each of the two tests
    if failures:
        print(f"FAILED {len(failures)} of {total} cases:", file=sys.stderr)
        for msg in failures:
            print(f"  {msg}", file=sys.stderr)
        return 1
    print(f"all {total} tests passed")
    return 0


if __name__ == '__main__':
    sys.exit(run())
