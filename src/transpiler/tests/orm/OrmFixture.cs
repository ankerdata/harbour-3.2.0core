// Test fixture for test83/test84/test85 — mirrors what gen-fieldtypes.py
// --emit-models generates: an OrmTable base stub, a family base
// (TestFamBase — the shared-static pattern), and the typed models.
using System;

public class OrmTable
{
    public static decimal nLanguage;

    public string cInternalName = "";
    public bool lReadOnly;
    public bool lShared;
    public long nId;
    public bool lDeleted;

    protected dynamic? aFileDefinition;

    public OrmTable(dynamic? aFileDefinition = default,
                    dynamic? lReadOnly = default, dynamic? lShared = default,
                    dynamic? lRestructure = default)
    {
        this.aFileDefinition = aFileDefinition;
        this.lReadOnly = lReadOnly is bool b ? b : true;
        this.lShared = lShared is bool s ? s : true;
    }
}

// family base — shared fields of TestDeptDef, TestBranchDef
public class TestFamBase : OrmTable
{
    public TestFamBase(dynamic? aFileDefinition = default,
                       dynamic? lReadOnly = default, dynamic? lShared = default,
                       dynamic? lRestructure = default)
        : base((object?)aFileDefinition, (object?)lReadOnly, (object?)lShared,
               (object?)lRestructure) { }

    public long nNo;           // DEPTNO (5,0)
    public string cName = "";  // NAME (30,0)
}

public class TestDeptDef : TestFamBase
{
    public TestDeptDef(dynamic? aFileDefinition = default,
                       dynamic? lReadOnly = default, dynamic? lShared = default,
                       dynamic? lRestructure = default)
        : base((object?)aFileDefinition, (object?)lReadOnly, (object?)lShared,
               (object?)lRestructure) { }

    public long nClerkNo;      // CLERKNO (10,0)
    public bool lDisable;      // DISABLE (1,0)
    public decimal nRate;      // RATE (6,2)
    public DateOnly dSince;    // SINCE (8,0)
}

public class TestBranchDef : TestFamBase
{
    public TestBranchDef(dynamic? aFileDefinition = default,
                         dynamic? lReadOnly = default, dynamic? lShared = default,
                         dynamic? lRestructure = default)
        : base((object?)aFileDefinition, (object?)lReadOnly, (object?)lShared,
               (object?)lRestructure) { }

    public long nRegion;       // REGION (3,0)
}
