using System;
using static HbRuntime;
using static Program;

// Test 85: def-class FAMILY BASES — polymorphic params widen to a
// typed base instead of dynamic USUAL.
//
// TestDeptDef and TestBranchDef share their core fields (the
// shared-static pattern: filedefinitions factories drawing rows from
// one `static saXxxFields` array), so tests/orm/fieldtypes.tsv carries
// a TestFamBase with the shared members and `=inherit` edges. The
// reftab class walk (hb_refTabClassParent) consults those edges, so
// `Stamp(oTable, ...)` — called with BOTH classes — converges its
// param slot to TestFamBase rather than conflict-freezing to USUAL:
// inside the helper, oTable:nNo / oTable:cName are typed members of
// the generated family base (C# long / string), not dynamic.
//
// Mirrors easipos SetTable/SetTableHeader(oORMTable), which receive
// TableIndexDef, CheckIndexDef, CSaleIndexDef and ORHeaderIndexDef —
// four classes with one shared field set.

// #include "hbclass.ch"
public class TestOrmRec85
{
    public decimal nNo;
    public string cName;
    public decimal nClerkNo;
    public decimal nRegion;

    public dynamic New()
    {
        return this;
    }
}

public static partial class Program
{
    public static dynamic[] TestDeptDef(string cPath = default)
    {
        return new dynamic[] { new dynamic[] { "Dept", "DEPT" }, (cPath != null ? cPath : ""), new dynamic[] {  }, new dynamic[] {  }, new Dictionary<string, dynamic> {  }, 1 };
    }

    public static dynamic[] TestBranchDef(string cPath = default)
    {
        return new dynamic[] { new dynamic[] { "Branch", "BRNCH" }, (cPath != null ? cPath : ""), new dynamic[] {  }, new dynamic[] {  }, new Dictionary<string, dynamic> {  }, 1 };
    }

    public static TestOrmRec85 ConstructORMTable(dynamic[] aFileDefinition = default, bool lReadOnly = default, bool lShared = default)
    {
        return new TestOrmRec85();

        // Receives both family members in BOTH slots — each param widens to
        // TestFamBase and the shared-field copy stays compile-checked, typed
        // member to typed member (long := long, string := string).
    }
    public static void Stamp(dynamic oTable = default, dynamic oFrom = default)
    {
        oTable.nNo = oFrom.nNo;
        oTable.cName = oFrom.cName;
        return;
    }

    public static void Main(string[] args)
    {
        TestDeptDef oDept = new TestDeptDef(TestDeptDef());
        TestBranchDef oBranch = new TestBranchDef(TestBranchDef());

        oDept.nNo = 5;
        oDept.cName = "Alpha";
        // through-base copy: branch takes 5/Alpha
        Stamp(oBranch, oDept);
        // then diverges: 9
        oBranch.nNo = oBranch.nNo + 4;
        oBranch.cName = "Beta";
        oDept.nClerkNo = 9999999999;
        oBranch.nRegion = 42;

        HbRuntime.QOut(HbRuntime.LTrim(HbRuntime.Str(oDept.nNo, 4, 0)) + oDept.cName);
        HbRuntime.QOut(HbRuntime.LTrim(HbRuntime.Str(oBranch.nNo, 4, 0)) + oBranch.cName);
        HbRuntime.QOut(HbRuntime.LTrim(HbRuntime.Str(oDept.nClerkNo, 12, 0)));
        HbRuntime.QOut(HbRuntime.LTrim(HbRuntime.Str(oBranch.nRegion, 4, 0)));
        // family-base long field divides floatly
        HbRuntime.QOut(HbRuntime.LTrim(HbRuntime.Str((decimal)(oDept.nNo) / 2, 8, 2)));
        return;
    }
}
