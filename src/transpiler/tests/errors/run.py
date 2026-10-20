#!/usr/bin/env python3
"""Negative regression tests: each .prg in this directory must surface a
specific warning on stderr (or, for E0100 and E0101, the error that fails
the file), as the transpiler reports it. Most are W0016 unsupported
constructs, where the transpiler emits a `default` placeholder so the .cs
still compiles; others flag a source smell codegen still handles. A test
checks that the warning is surfaced, and where the file has quiet lines,
that nothing else is.

Usage: py tests/errors/run.py    (HBTRANSPILER names the binary; by default
harbour-core's bin/hbtranspiler, .exe on Windows)
"""
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
TRANS = os.environ.get("HBTRANSPILER") or os.path.join(
    REPO, "bin", "hbtranspiler" + (".exe" if os.name == "nt" else ""))
INC = os.path.join(REPO, "include")

passed = 0
failed = 0


def trans(*args):
    """The transpiler's exit code and its stdout and stderr, as one text."""
    r = subprocess.run([TRANS, "-I" + INC] + list(args), capture_output=True)
    out = (r.stdout + r.stderr).decode("utf-8", errors="replace")
    return r.returncode, out


def here(name):
    return os.path.join(HERE, name)


def remove(*names):
    for name in names:
        try:
            os.remove(here(name))
        except FileNotFoundError:
            pass


def lines_matching(text, pattern):
    return [l for l in text.splitlines() if re.search(pattern, l)]


def result(ok, passing, failing, detail=""):
    global passed, failed
    if ok:
        print("PASS: " + passing)
        passed += 1
    else:
        print("FAIL: " + failing)
        if detail:
            print("\n".join("  " + l for l in detail.splitlines()))
        failed += 1


def run_one(prg, code, pattern):
    """The file's -GS surfaces `code` with a message matching `pattern`."""
    name = os.path.splitext(prg)[0]
    rc, out = trans("-o" + HERE + os.sep, here(prg), "-GS")
    result(lines_matching(out, "(warning|Error) " + code + ".*" + pattern),
           "%s (%s surfaced)" % (name, code),
           "%s (expected %s /%s/, got rc=%d)" % (name, code, pattern, rc), out)
    remove(name + ".cs")


def count_of(prg, pattern, want, what="warnings"):
    """The file's -GS reports exactly `want` lines matching `pattern`."""
    name = os.path.splitext(prg)[0]
    _, out = trans("-o" + HERE + os.sep, here(prg), "-GS")
    remove(name + ".cs")
    n = len(lines_matching(out, pattern))
    result(n == want, "%s (%d %s, no more)" % (name, want, what),
           "%s (expected %d %s, got %d)" % (name, want, what, n))


def scan_twice(tab, prg):
    """Two -GF passes on a private reftab, as the scan's last pass decides: the second's output."""
    remove(tab)
    trans("--reftab=" + here(tab), here(prg), "-GF")
    _, out = trans("--reftab=" + here(tab), here(prg), "-GF")
    remove(tab)
    return out


CASES_A = [
    ('alias_stmt.prg', 'W0016', 'ALIAS (expression|reference)'),
    ('alias_expr.prg', 'W0016', 'ALIAS (expression|reference)'),
    ('macro_expr.prg', 'W0016', 'macro &'),
    ('comma_op.prg', 'W0016', 'comma-operator'),
    ('array_ref_noreassign.prg', 'W0023', 'redundant'),
    ('hungarian_mismatch.prg', 'W0024', 'contradicts its Hungarian-prefix'),
    ('equal_assign.prg', 'E0100', 'as an assignment'),
    ('equal_compare.prg', 'E0100', 'as a comparison'),
    ('equal_for.prg', 'E0100', 'in FOR'),
    ('seq_with_block.prg', 'E0101', 'takes only'),
    ('switch_break.prg', 'W0033', 'ends a SWITCH CASE'),
    ('thread_static_init.prg', 'W0034', 'is initialised'),
    ('integer_fraction.prg', 'W0024', "assigning a value that may hold a fraction to 'iHalf'"),
    ('integer_fraction.prg', 'W0024', "'/=' may leave a fraction in 'iCount'"),
    ('integer_fraction.prg', 'W0024', "'[*]=' may leave a fraction in 'iCount'"),
    ('integer_fraction.prg', 'W0024', "FOR STEP that may hold a fraction for 'iPos'"),
    ('integer_fraction.prg', 'W0024', "passing a value that may hold a fraction as 'iValue'"),
]

CASES_B = [
    ('hash_key_name.prg', 'W0035', "'hWrong' is given a hash keyed by numbers"),
    ('hash_key_name.prg', 'W0035', "'hByName' takes a number as a key"),
    ('hash_key_name.prg', 'W0035', "'hnById' takes a string as a key"),
    ('x_subscript.prg', 'W0036', "'xValue' is subscripted"),
    ('foreach_after.prg', 'W0038', "'cName' is read after its FOR EACH"),
    ('foreach_after.prg', 'W0038', "'cCell' is read after its FOR EACH"),
    ('foreach_integer.prg', 'W0039', "FOR EACH 'nId' is an integer only by inference"),
    ('cond_unread.prg', 'W0042', "whether 'COND_UNREAD_FLAG' is defined.*could not read 'cond_unread_missing.ch'"),
    ('fixed_record.prg', 'W0041', 'an array of 3 elements .*, declared FieldRow41, a record of 4 fields'),
    ('fixed_record.prg', 'W0041', "putting STRING into 'iLen' of 'FieldRow41'"),
    ('fixed_record.prg', 'W0041', "putting STRING into 'iLen' of 'aRow'"),
    ('fixed_record.prg', 'W0041', "assigning ARRAY to 'aRow', declared FieldRow41"),
    ('fixed_record.prg', 'W0041', "assigning ARRAY to 'aRows', declared FIXED<FieldRow41>"),
    ('fixed_record.prg', 'W0041', "AAdd\\(\\) changes the length of 'aRows', declared FIXED"),
    ('fixed_record.prg', 'W0041', "ASize\\(\\) changes the length of 'aRows', declared FIXED"),
    ('fixed_record.prg', 'W0041', "passing an array of 3 elements as 'aRow' to 'Take41', declared FieldRow41"),
    ('fixed_record.prg', 'W0046', "'aRow' is a FieldRow41, read by a subscript that is not a constant"),
    ('fixed_record.prg', 'W0046', "'aRow' is a FieldRow41 of 4 fields, read at position 5"),
    ('fixed_record.prg', 'W0046', "an element of 'aRows' is a FieldRow41, read by a subscript that is not a constant"),
    ('fixed_record.prg', 'W0046', "'aRow' is a FieldRow41, given to Len\\(\\) as an array"),
    ('fixed_record.prg', 'W0046', "FOR EACH over 'aRow', a FieldRow41"),
    ('typed_hash.prg', 'W0041', "putting NUMERIC into 'hRows', declared Row41h"),
    ('typed_hash.prg', 'W0035', "'hnById' is given a hash keyed by strings, but its name declares one keyed by numbers"),
    ('typed_hash.prg', 'W0035', "'hnWrong' is declared HASHC, a hash keyed by strings, but its name declares one keyed by numbers"),
    ('typed_hash.prg', 'W0041', "assigning HASH to 'hRows', declared HASHC<Row41h>"),
    ('typed_hash.prg', 'W0041', "putting STRING into 'hRows', declared Row41h"),
    ('typed_hash.prg', 'W0035', "'hRows' takes a number as a key, but its name declares a hash keyed by strings"),
    ('typed_hash.prg', 'W0035', "'hnById' takes a string as a key, but its name declares a hash keyed by numbers"),
]


def main():
    for case in CASES_A:
        run_one(*case)
    # ...and nothing else: the file's quiet lines (Int(), a whole literal) stay quiet
    count_of("integer_fraction.prg", "warning W", 5)

    for case in CASES_B:
        run_one(*case)

    # ...and nothing else: the quiet lines stay quiet
    for name, want in (("hash_key_name", 3), ("x_subscript", 1), ("foreach_after", 2), ("foreach_integer", 1)):
        count_of(name + ".prg", "warning W003[5-9]", want)
    # typed hashes: values by type, keys by kind
    count_of("typed_hash.prg", "warning W00[34][15]", 7)
    # records and fixed arrays, and no more
    count_of("fixed_record.prg", "warning W004[16]", 13)
    # W0042 once, for the one question; the unread header is no warning by itself
    count_of("cond_unread.prg", "warning W", 1, "warning")

    # A batch, as the pipeline runs it: one process, several files. The same
    # warning at the same line of two files is reported for each.
    _, out = trans("-o" + HERE + os.sep, here("batch_dedup_a.prg"), here("batch_dedup_b.prg"), "-GS")
    remove("batch_dedup_a.cs", "batch_dedup_b.cs")
    result(len(lines_matching(out, re.escape("batch_dedup_a.prg(12): warning W0035"))) == 1 and
           len(lines_matching(out, re.escape("batch_dedup_b.prg(12): warning W0035"))) == 1,
           "batch_dedup (each file of the batch has its warning)",
           "batch_dedup (expected one W0035 at line 12 of each file)",
           "\n".join(lines_matching(out, "warning")))

    # W0037 reads the class rows a scan writes: -GF with a private reftab, so
    # the suite's hbreftab.tab is left alone. One warning, and the quiet lines
    # (HBObject's Init, a declared and an inherited member, a dynamic class)
    # stay quiet.
    remove("self_undeclared.tab")
    _, out = trans("--reftab=" + here("self_undeclared.tab"), here("self_undeclared.prg"), "-GF")
    remove("self_undeclared.tab")
    result("warning W0037  'Device37:oDevice37'" in out and len(lines_matching(out, "warning W0037")) == 1,
           "self_undeclared (W0037 surfaced, 1 warning, no more)",
           "self_undeclared (expected one W0037, for Device37:oDevice37)", out)

    # A row, not a warning: a parameter renamed to a name that names a class
    # takes the class on a warm scan, where the reftab still holds the type the
    # callers gave the old name (rename_param_before.prg, then _after.prg, on
    # one private reftab, two passes each).
    remove("rename_param.tab")
    for step in ("before", "before", "after", "after"):
        trans("--reftab=" + here("rename_param.tab"), here("rename_param_%s.prg" % step), "-GF")
    row = ""
    try:
        with open(here("rename_param.tab"), encoding="latin-1") as f:
            for line in f:
                if line.startswith("PriceRn\t"):
                    fields = line.rstrip("\r\n").split("\t")
                    row = fields[4] if len(fields) > 4 else ""
                    break
    except FileNotFoundError:
        pass
    remove("rename_param.tab")
    result(row == "oItemLineRn:ItemLineRn:-", "rename_param (the renamed parameter is its name's class)",
           "rename_param (expected oItemLineRn:ItemLineRn:-, got '%s')" % row)

    # W0040 / W0041 read the class rows too (a class's parents, its members):
    # two -GF passes on a private reftab, the second converged, as the scan's
    # last pass decides them (scan.py's LAST_PASS_CODES).
    out = scan_twice("declared_types.tab", "declared_types.prg")
    for expect in ("W0040  'oPart' is declared Missing40",
                   "W0040  'oGhost' is declared Ghost40",
                   "W0041  assigning Other40 to 'oShape', declared Shape40",
                   "W0041  assigning STRING to 'oShape', declared Shape40",
                   "W0041  putting NUMERIC into 'aShapes', declared Shape40",
                   "W0041  putting Other40 into 'aShapes', declared Shape40",
                   "W0041  passing STRING as 'oShape' to 'Take40', declared Shape40",
                   "W0041  returning STRING from 'Make40', declared Shape40"):
        result(expect in out, "declared_types (%s)" % expect, "declared_types (expected: %s)" % expect, out)
    n = len(lines_matching(out, "warning W004[01]"))
    result(n == 8, "declared_types (8 warnings, no more)",
           "declared_types (expected 8 warnings, got %d)" % n, "\n".join(lines_matching(out, "warning")))

    # W0043 / W0044: two scan passes, as the pipeline decides them on its last
    # (a slot's O gathers over every caller), and exactly the two
    out = scan_twice("omitted_default.tab", "omitted_default.prg")
    for expect in ("W0043  'nNo45' goes to 'omitted_default::Number45' (parameter 1)",
                   "W0044  'xPanel45' may hold NIL and goes to 'omitted_default::Panel45' (parameter 1)"):
        result(expect in out, "omitted_default (%s)" % expect, "omitted_default (expected: %s)" % expect, out)
    n = len(lines_matching(out, "warning W004[34]"))
    result(n == 2, "omitted_default (2 warnings, no more)",
           "omitted_default (expected 2 warnings, got %d)" % n, "\n".join(lines_matching(out, "warning")))

    # --reconcile: reconcile_shared.prg built twice, each build with its own
    # caller and its own table, emits different signatures; once each table has
    # the other's caller facts, identical ones. Two classes with no common
    # ancestor are W0045, from both sides.
    tabs = ("reconcile_a.tab", "reconcile_b.tab", "reconcile_ra.tab", "reconcile_rb.tab")
    remove(*tabs)
    for _ in range(2):
        trans("--reftab=" + here("reconcile_a.tab"), here("reconcile_shared.prg"), here("reconcile_a.prg"), "-GF")
        trans("--reftab=" + here("reconcile_b.tab"), here("reconcile_shared.prg"), here("reconcile_b.prg"), "-GF")
    rc_a, out_a = trans("--reftab=" + here("reconcile_a.tab"), "--reconcile=" + here("reconcile_b.tab"),
                        "--reconcile-out=" + here("reconcile_ra.tab"))
    rc_b, out_b = trans("--reftab=" + here("reconcile_b.tab"), "--reconcile=" + here("reconcile_a.tab"),
                        "--reconcile-out=" + here("reconcile_rb.tab"))

    def sigs(tab):
        trans("--reftab=" + here(tab), "-o" + HERE + os.sep, here("reconcile_shared.prg"), "-GS")
        try:
            with open(here("reconcile_shared.cs"), encoding="utf-8", errors="replace") as f:
                text = "\n".join(l for l in f.read().splitlines() if "public static void" in l)
        except FileNotFoundError:
            text = ""
        remove("reconcile_shared.cs")
        return text

    before_a, before_b = sigs("reconcile_a.tab"), sigs("reconcile_b.tab")
    after_a, after_b = sigs("reconcile_ra.tab"), sigs("reconcile_rb.tab")
    remove(*tabs)
    detail = "\n".join(("before, A: " + before_a.replace("\n", " "), "before, B: " + before_b.replace("\n", " "),
                         "after,  A: " + after_a.replace("\n", " "), "after,  B: " + after_b.replace("\n", " "), out_a))
    for ok, what in (
            (bool(before_a) and before_a != before_b, "each build's own table, different signatures"),
            (bool(after_a) and after_a == after_b, "reconciled, identical signatures"),
            ("Count46(string cKey, ref decimal nTotal)" in after_a and "Count46(string cKey = default)" in after_a,
             "by-ref and the short overload, from either build"),
            ("Weigh46(Fruit46 oThing46" in after_a, "two classes, their common ancestor"),
            (rc_a != 0 and rc_b != 0 and
             len(lines_matching(out_a, re.escape("W0045  'Show46' parameter 1 (oShown46)"))) == 1 and
             len(lines_matching(out_b, re.escape("W0045  'Show46' parameter 1 (oShown46)"))) == 1 and
             len(lines_matching(out_a + "\n" + out_b, "warning W")) == 2,
             "W0045 for classes that share no ancestor, once each way")):
        result(ok, "reconcile (%s)" % what, "reconcile (%s)" % what, detail)

    # --extern=: a .NET class from extern_shim.tab types the static that names
    # it, `@` into an out parameter is `out` (a shim temporary when the local is
    # decimal and the slot long), an omitted out `out _`, and the scan saves
    # none of the table's rows.
    remove("extern_shim_reftab.tab")
    ext = "--extern=" + here("extern_shim.tab")
    trans("--reftab=" + here("extern_shim_reftab.tab"), ext, here("extern_shim.prg"), "-GF")
    trans("--reftab=" + here("extern_shim_reftab.tab"), ext, "-o" + HERE + os.sep, here("extern_shim.prg"), "-GS")
    try:
        with open(here("extern_shim.cs"), encoding="utf-8", errors="replace") as f:
            cs = f.read()
    except FileNotFoundError:
        cs = ""
    try:
        with open(here("extern_shim_reftab.tab"), encoding="latin-1") as f:
            saved = sum(1 for l in f if "NetShim47" in l)
    except FileNotFoundError:
        saved = 0
    remove("extern_shim_reftab.tab", "extern_shim.cs")
    for ok, what in (
            (re.search(r"public static Test47\.NetShim47 [A-Za-z_]*soNetShim47;", cs), "the static is the class's C# type"),
            ("long _hbref_nSize = (long)(nSize);" in cs and re.search(r"\.Fill\(out cName, out _hbref_nSize\);", cs)
             and "nSize = _hbref_nSize;" in cs, "@ into out, a shim for another type"),
            (re.search(r"\.Fill\(out cName, out _\);", cs), "an omitted out is out _"),
            ("bool lReady" in cs and re.search(r"lReady = [A-Za-z_]*soNetShim47\.Ready\(\"probe\"\);", cs), "a typed return"),
            (saved == 0, "the scan saves none of the table's rows")):
        result(bool(ok), "extern (%s)" % what, "extern (%s)" % what, cs)

    print("")
    print("Results: %d passed, %d failed" % (passed, failed))
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
