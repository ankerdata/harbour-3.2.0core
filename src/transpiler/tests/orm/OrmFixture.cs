// Test fixture for test83 — mirrors what gen-fieldtypes.py --emit-models
// generates for the real corpus: an OrmTable base stub plus one
// strongly-typed model per def class listed in tests/orm/fieldtypes.tsv.
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

public class TestDeptDef : OrmTable
{
    public TestDeptDef(dynamic? aFileDefinition = default,
                       dynamic? lReadOnly = default, dynamic? lShared = default,
                       dynamic? lRestructure = default)
        : base((object?)aFileDefinition, (object?)lReadOnly, (object?)lShared,
               (object?)lRestructure) { }

    public int nNo;            // DEPTNO (5,0)
    public long nClerkNo;      // CLERKNO (10,0)
    public string cName = "";  // NAME (30,0)
    public bool lDisable;      // DISABLE (1,0)
    public decimal nRate;      // RATE (6,2)
    public DateOnly dSince;    // SINCE (8,0)
}
