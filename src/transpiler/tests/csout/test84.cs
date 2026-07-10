using System;
using static HbRuntime;
using static Program;

// Test 84: Harbour float-division semantics over C#-integral operands.
//
// Harbour `/` is ALWAYS float: 7/2 == 3.5. Every int source the C#
// emitter produces — INTEGER locals (Pass 2.5 index-shaped candidacy),
// int #defines, ORM def-class int fields (tests/orm/fieldtypes.tsv) —
// would truncate under C# int/int division. gencsharp forces decimal
// division by casting the left operand ((decimal)(a) / b) when BOTH
// operands are statically C#-integral; a decimal on either side stays
// cast-free.
//
// Guarded by runtime parity: the Harbour run computes with plain
// numerics, so any C#-side truncation diverges the output. `%` needs
// no cast (int%int is exact) and is pinned to prove we don't over-cast.

// #include "hbclass.ch"
public class TestOrmRec84
{
    public decimal nNo;

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

    public static TestOrmRec84 ConstructORMTable(dynamic[] aFileDefinition = default, bool lReadOnly = default, bool lShared = default)
    {
        return new TestOrmRec84();
    }

    public static void Main(string[] args)
    {
        dynamic[] aList = new dynamic[] { 10, 20, 30 };
        long i = 3;
        TestDeptDef oDept = new TestDeptDef(TestDeptDef());

        oDept.nNo = 5;

        // Explicit width/decimals — Harbour's SET DECIMALS default renders
        // division results as 1.50 where C# Str gives 1.5; the semantics
        // under test are the VALUES, so pin the format on both sides.
        // 30 — index use makes i an int
        HbRuntime.QOut(HbRuntime.LTrim(HbRuntime.Str(aList[i - 1], 4, 0)));
        // 1.50  int local / int literal
        HbRuntime.QOut(HbRuntime.LTrim(HbRuntime.Str((decimal)(i) / 2, 8, 2)));
        // 3.50  int define / int literal
        HbRuntime.QOut(HbRuntime.LTrim(HbRuntime.Str((decimal)(Test84PrgConst.SEVEN) / 2, 8, 2)));
        // 2.00  int arithmetic operand
        HbRuntime.QOut(HbRuntime.LTrim(HbRuntime.Str((decimal)(i + 1) / 2, 8, 2)));
        // 2.50  ORM int field / literal
        HbRuntime.QOut(HbRuntime.LTrim(HbRuntime.Str((decimal)(oDept.nNo) / 2, 8, 2)));
        // 1.40  define / field
        HbRuntime.QOut(HbRuntime.LTrim(HbRuntime.Str((decimal)(Test84PrgConst.SEVEN) / oDept.nNo, 8, 2)));
        // 1     mod is exact on ints
        HbRuntime.QOut(HbRuntime.LTrim(HbRuntime.Str(i % 2, 4, 0)));
        // 1.50  decimal side, no cast needed
        HbRuntime.QOut(HbRuntime.LTrim(HbRuntime.Str(i / 2.0m, 8, 2)));
        return;
    }
}
