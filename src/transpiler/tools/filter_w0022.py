#!/usr/bin/env python3
"""Drop transient W0022 false-positives from a warnings log.

W0022 ("passes X for parameter Y ... downgrading to USUAL") is emitted
inline the first time a call-site type disagrees with a parameter slot's
in-progress type. But the reftab converges over several passes, and a
disagreement seen early often resolves cleanly later — yet the inline
warning has already been written and the pipeline accumulates it.

The converged reftab tells the truth: a genuinely-downgraded slot carries
the C (conflict-frozen) flag (hbreftab.c sets fConflict on a real
conflict). So a W0022 whose slot has NO C flag at convergence never
actually downgraded — it is a transient false-positive and is dropped.
Real conflicts (C flag, e.g. an integer flag passed into a STRING param)
are kept.

Usage: filter_w0022.py <warnings.txt> <reftab.tab>   (edits the log in place)
Also importable: filter_file(warn_log, reftab) -> number dropped (a scan
driver does, after its last pass: easipos-transpiled's scan.py and gen_cs.py).
"""
import re
import sys

# "Call to 'FUNC' passes TYPE for parameter 'PARAM'"
MSG = re.compile(r"Call to '([^']+)' passes .* for parameter '([^']+)'")


def load_slots(reftab):
    """reftab: funckey -> { paramname_lower: flags }"""
    slots = {}
    for line in open(reftab, encoding="utf-8", errors="replace"):
        if line.startswith("#") or not line.strip():
            continue
        f = line.rstrip("\n").split("\t")
        if len(f) < 4:
            continue
        params = {}
        for slot in f[4:]:
            p = slot.split(":")
            if len(p) >= 3:
                params[p[0].lower()] = p[2]
        slots[f[0]] = params
        slots[f[0].lower()] = params
    return slots


def filter_file(warn_log, reftab):
    slots = load_slots(reftab)
    kept, dropped = [], 0
    for line in open(warn_log, encoding="utf-8", errors="replace"):
        if "W0022" in line:
            m = MSG.search(line)
            if m:
                func, param = m.group(1), m.group(2).lower()
                slot = slots.get(func) or slots.get(func.lower())
                # Slot found and NOT conflict-frozen -> transient, drop it.
                # Unknown slot -> keep (can't prove it's spurious).
                if slot is not None and param in slot and "C" not in slot[param]:
                    dropped += 1
                    continue
        kept.append(line)
    with open(warn_log, "w", encoding="utf-8") as f:
        f.writelines(kept)
    return dropped


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.stderr.write(__doc__)
        sys.exit(2)
    n = filter_file(sys.argv[1], sys.argv[2])
    sys.stderr.write("filter_w0022: dropped %d transient W0022\n" % n)
