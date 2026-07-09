using System;
using static HbRuntime;
using static Program;

// Test 83: ORM def-class field typing (--fieldtypes / fieldtypes.tsv).
//
// `ConstructORMTable(TestDeptDef())` names its def class lexically; with
// tests/orm/fieldtypes.tsv loaded the receiver types as the generated
// model class (tests/orm/OrmFixture.cs) and emits `new TestDeptDef(...)`,
// so every field access below is a typed C# member: nNo int, nClerkNo
// long (len >= 10 overflows Int32), nRate decimal, cName string,
// lDisable bool, dSince DateOnly.
//
// Guarded two ways. Mistyping is a COMPILE error in buildcs.sh: a wrong
// field type in the model (or a wrongly-typed local inferred from a
// field read) will not convert. And the RUNTIME output must match the
// Harbour (.prg) run, where ConstructORMTable is the plain dynamic
// object below — catching wrong-but-compilable typing.
//
// The Harbour-side stand-in class is deliberately NOT named
// TestDeptDef: in the C# build that name belongs to the generated
// model; in the Harbour run the def name only appears as the factory
// function, exactly like the real orm.prg flow.

// #include "hbclass.ch"
public class TestOrmRec
{
    public decimal nNo;
    public decimal nClerkNo;
    public string cName;
    public bool lDisable;
    public decimal nRate;
    public DateOnly dSince;

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

    public static TestOrmRec ConstructORMTable(dynamic[] aFileDefinition = default, bool lReadOnly = default, bool lShared = default)
    {
        return new TestOrmRec();
    }

    public static void Main(string[] args)
    {
        TestDeptDef oDept = new TestDeptDef(TestDeptDef());
        decimal nNext = default;

        oDept.nNo = 5;
        // 10 digits — needs long in C#
        oDept.nClerkNo = 9999999999;
        oDept.cName = "Fred";
        oDept.lDisable = false;
        oDept.nRate = 12.34m;
        oDept.dSince = new DateOnly(2026, 3, 15);

        nNext = oDept.nNo + 1;

        HbRuntime.QOut("no=" + HbRuntime.LTrim(HbRuntime.Str(oDept.nNo)));
        HbRuntime.QOut("next=" + HbRuntime.LTrim(HbRuntime.Str(nNext)));
        HbRuntime.QOut("clerk=" + HbRuntime.LTrim(HbRuntime.Str(oDept.nClerkNo)));
        HbRuntime.QOut("name=" + oDept.cName);
        HbRuntime.QOut("dis=" + (oDept.lDisable ? "T" : "F"));
        HbRuntime.QOut("rate=" + HbRuntime.LTrim(HbRuntime.Str(oDept.nRate, 6, 2)));
        HbRuntime.QOut("since=" + HbRuntime.DToS(oDept.dSince));
        return;
    }
}
