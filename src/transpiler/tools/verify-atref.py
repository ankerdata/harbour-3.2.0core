#!/usr/bin/env python3
"""Verify /*@*/ by-ref parameter annotations against the reftab.

The /*@*/ (also /* @ */) marker on a parameter is a hand-written
developer convention meaning "out-parameter, passed by reference".
Nothing enforces it. The transpiler's reftab carries computed
per-parameter flags: W = the body assigns this parameter (directly or
via compound-assign / inc-dec), R = at least one call site passes it by
reference (@).

"Is this actually an out-parameter?" = W (direct write) OR the body
@-passes it to a nested call (transitive out-param — W is direct-only).
Cross-checked against the annotation and R:

  ANNOTATED-NOT-ASSIGNED  /*@*/ present but the param is never written
                          — a wrong/stale annotation; it is really an
                          input. Remove the /*@*/.
  OUTPARAM-UNANNOTATED    the param IS written AND some caller passes @
                          (R set) — a genuine out-param missing its
                          /*@*/. Add it; its non-@ callers are W0020
                          lost-result suspects. Params written but never
                          @-passed are scratch reuse and are dropped.

Methods are matched to their FULL reftab key <Class>::<Class>__<Method>
(from the `CLASS Y` clause), not name+arity — two 7-arg Refund methods
in different classes must not share flags.

Usage:
  verify-atref.py --src=<dir> --reftab=<path> [--out=<report>] [--tsv=<audit.tsv>]
    --src     the .prg tree the reftab was scanned from (every *.prg below)
    --out     the readable report; printed to stdout when not given
    --tsv     ALSO appends audit-format rows (category TAB file TAB line TAB
              symbol TAB detail TAB fix) so a type audit can fold the two
              categories into its leaderboard (easipos-transpiled's audit.py)
"""
import re, sys, collections
from pathlib import Path

SRC = REFTAB = OUT = TSV = None
for a in sys.argv[1:]:
    if a.startswith('--src='):
        SRC = Path(a.split('=', 1)[1])
    elif a.startswith('--reftab='):
        REFTAB = Path(a.split('=', 1)[1])
    elif a.startswith('--out='):
        OUT = Path(a.split('=', 1)[1])
    elif a.startswith('--tsv='):
        TSV = Path(a.split('=', 1)[1])
if not SRC or not REFTAB:
    sys.stderr.write(__doc__)
    sys.exit(2)

# reftab parsed into per-parameter (W, R) flag lists, indexed two ways:
#   methods[(class_lower, method_lower)]         -> [(W, R), ...]
#   frees[func_lower]  -> list of (filebase|None, [(W, R), ...])
# so a source METHOD resolves by its class and a free/static function
# by its (optionally file-scoped) name.
methods = {}
frees = collections.defaultdict(list)
for line in open(REFTAB, encoding='utf-8', errors='replace'):
    if line.startswith('#') or not line.strip():
        continue
    f = line.rstrip('\n').split('\t')
    if len(f) < 4:
        continue
    name = f[0]
    try:
        npar = int(f[3])
    except ValueError:
        continue
    flags = []
    for slot in f[4:4 + npar]:
        parts = slot.split(':')
        pf = parts[2] if len(parts) >= 3 else ''
        flags.append(('W' in pf, 'R' in pf))
    mm = re.match(r'^(\w+)::(\w+)__(\w+)$', name)      # Class::Class__Method
    if mm:
        methods[(mm.group(1).lower(), mm.group(3).lower())] = flags
        continue
    sm = re.match(r'^(\w+)::(\w+)$', name)             # filebase::Static
    if sm:
        frees[sm.group(2).lower()].append((sm.group(1).lower(), flags))
    elif re.match(r'^\w+$', name):                     # bare free function
        frees[name.lower()].append((None, flags))

# METHOD/FUNCTION/PROCEDURE <name> ( ... )  [CLASS <class>]
# Leading indent is [ \t]* NOT \s* — \s eats preceding blank-line
# newlines, which would push the reported line back onto the blanks.
DEF = re.compile(
    r'^[ \t]*(?:STATIC[ \t]+)?(FUNCTION|PROCEDURE|METHOD)[ \t]+(\w+)[ \t]*\(',
    re.IGNORECASE | re.MULTILINE)


def param_list(text, open_idx):
    depth, i, out = 0, open_idx, []
    while i < len(text):
        c = text[i]
        if c == '(':
            depth += 1
            if depth == 1:
                i += 1
                continue
        elif c == ')':
            depth -= 1
            if depth == 0:
                return ''.join(out), i
        out.append(c)
        i += 1
    return None, open_idx


def split_params(s):
    """Split a parameter list on depth-0 commas, but NOT on commas
    inside a /* ... */ block comment — the source disables params by
    commenting them out (`/*@lTables,*/`), and that inner comma must
    not create a bogus split."""
    parts, depth, cur, i = [], 0, [], 0
    while i < len(s):
        if s[i] == '/' and i + 1 < len(s) and s[i + 1] == '*':
            j = s.find('*/', i + 2)
            j = len(s) if j < 0 else j + 2
            cur.append(s[i:j])
            i = j
            continue
        c = s[i]
        if c in '([{':
            depth += 1
        elif c in ')]}':
            depth -= 1
        if c == ',' and depth == 0:
            parts.append(''.join(cur))
            cur = []
        else:
            cur.append(c)
        i += 1
    if ''.join(cur).strip():
        parts.append(''.join(cur))
    return parts


ANNOT = re.compile(r'/\*\s*@\s*\*/')
mismatches = collections.defaultdict(list)


def flags_for(kind, fname, filebase, nparams):
    """Resolve the source definition to its reftab flag list, or None."""
    if kind.upper() == 'METHOD':
        return None   # method flags resolved via the CLASS clause below
    cands = frees.get(fname.lower(), [])
    # Prefer the file-static entry for this file, then a bare entry,
    # then any entry whose arity matches (disambiguates overloads).
    for fb, fl in cands:
        if fb == filebase:
            return fl
    for fb, fl in cands:
        if fb is None:
            return fl
    for fb, fl in cands:
        if len(fl) == nparams:
            return fl
    return cands[0][1] if cands else None


for p in SRC.rglob('*.prg'):
    # Work on the ORIGINAL text so line numbers are physical. param_list
    # scans across newlines already; `;` continuations inside the list
    # are stripped before splitting (a `;` never appears in a real
    # parameter). CLASS after the `)` may sit past a `;`.
    text = open(p, encoding='latin-1').read()
    filebase = p.stem.lower()
    defs = list(DEF.finditer(text))
    for di, m in enumerate(defs):
        kind, fname = m.group(1), m.group(2)
        inside, close = param_list(text, m.end() - 1)
        if inside is None or not inside.strip():
            continue
        params = split_params(inside.replace(';', ' '))
        if kind.upper() == 'METHOD':
            cm = re.match(r'^[\s;]*CLASS\s+(\w+)', text[close + 1:],
                          re.IGNORECASE)
            if not cm:
                continue
            wl = methods.get((cm.group(1).lower(), fname.lower()))
        else:
            wl = flags_for(kind, fname, filebase, len(params))
        if wl is None:
            continue
        line = text[:m.start()].count('\n') + 1
        body = text[m.end():
                    defs[di + 1].start() if di + 1 < len(defs) else len(text)]
        ri = 0   # reftab param index — advances only for REAL params
        for raw in params:
            annotated = bool(ANNOT.search(raw))
            # A param whose content is only a block comment is a
            # disabled/commented-out param — it is NOT in the reftab, so
            # skip it without consuming a reftab slot.
            pname = re.sub(r'/\*.*?\*/', '', raw).strip()
            if not pname:
                continue
            pname = pname.split()[0]
            if ri >= len(wl):
                break
            w_assigned, r_used = wl[ri]
            ri += 1
            passed_byref = bool(re.search(
                r'@\s*' + re.escape(pname) + r'\b', body, re.IGNORECASE))
            assigned = w_assigned or passed_byref
            rel = str(p.relative_to(SRC))
            if annotated and not assigned:
                mismatches['ANNOTATED-NOT-ASSIGNED'].append(
                    (rel, line, f'{fname}:{pname}',
                     '/*@*/ but never written — stale annotation, remove it'))
            elif assigned and not annotated and r_used:
                mismatches['OUTPARAM-UNANNOTATED'].append(
                    (rel, line, f'{fname}:{pname}',
                     'written out-param (a caller passes @) missing /*@*/'))

# Readable report.
out = open(OUT, 'w', encoding='utf-8') if OUT else sys.stdout
for cat in ('ANNOTATED-NOT-ASSIGNED', 'OUTPARAM-UNANNOTATED'):
    rows = sorted(set(mismatches[cat]))
    out.write(f'== {cat} ({len(rows)}) ==\n')
    for rel, line, sym, detail in rows:
        out.write(f'  {rel}({line})  {sym}  — {detail}\n')
    out.write('\n')
    if OUT:
        print(f'{cat}: {len(rows)}')
if OUT:
    out.close()

# Optional: append audit-format rows for a type audit to fold in.
if TSV:
    fix = {'ANNOTATED-NOT-ASSIGNED': 'remove the /*@*/ (it is an input)',
           'OUTPARAM-UNANNOTATED': 'add /*@*/; check non-@ callers (W0020)'}
    with open(TSV, 'a') as t:
        for cat, rows in mismatches.items():
            for rel, line, sym, detail in sorted(set(rows)):
                t.write(f'{cat}\t{rel}\t{line}\t{sym}\t{detail}\t{fix[cat]}\n')

if OUT:
    print(f'Report: {OUT}')
