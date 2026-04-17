using System;
using static HbRuntime;

// Test 42: dynamic member access via obj:&(name).
//
// Harbour's `::&(cMemberName)` reads/writes a property whose name is
// only known at runtime. The transpiler emits HbRuntime.GETMEMBER /
// SETMEMBER / SENDMSG calls backed by reflection. This test builds
// a small class with typed DATAs, then exercises:
//   a) read via GETMEMBER
//   b) write via SETMEMBER
//   c) iteration over a list of member names (the real-world ORM
//      pattern from sharedx/ormsql.prg)
//
// The class mirrors the shape of POSClass-derived objects in easipos
// where a list of field names drives get/set loops.

// #include "hbclass.ch"
public class Record
{
    public string cName { get; set; } = "";
    public decimal nValue { get; set; } = 0;
    public bool lActive { get; set; } = false;

    public object SetByName(string cField = default, dynamic xValue = default)
    {
        HbRuntime.SETMEMBER(this, cField, xValue);
        return this;
    }

    public dynamic GetByName(string cField = default)
    {
        return HbRuntime.GETMEMBER(this, cField);
    }

    public dynamic DumpFields(dynamic[] aFields = default)
    {
        decimal i = default;
        dynamic xVal = default;
        string cType = default;
        for (i = 1; i <= HbRuntime.LEN(aFields); i++)
        {
            xVal = HbRuntime.GETMEMBER(this, aFields[(int)(i) - 1]);
            cType = HbRuntime.VALTYPE(xVal);
            if (cType == "C")
            {
                HbRuntime.QOUT(aFields[(int)(i) - 1] + ": " + xVal);
            }
            else if (cType == "N")
            {
                HbRuntime.QOUT(aFields[(int)(i) - 1] + ": " + HbRuntime.STR(xVal, 4));
            }
            else if (cType == "L")
            {
                HbRuntime.QOUT(aFields[(int)(i) - 1] + ": " + (xVal ? "T" : "F"));
            }
            else
            {
                HbRuntime.QOUT(aFields[(int)(i) - 1] + ": ?");
            }
        }

        return null;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Record oRec = new Record();
        dynamic[] aFields = new dynamic[] { "cName", "nValue", "lActive" };

        // Direct property access — baseline
        oRec.cName = "hello";
        oRec.nValue = 42;
        oRec.lActive = true;
        HbRuntime.QOUT("direct: " + oRec.cName + ", " + HbRuntime.STR(oRec.nValue, 4) + ", " + (oRec.lActive ? "T" : "F"));

        // Write via dynamic member (SETMEMBER)
        oRec.SetByName("cName", "world");
        oRec.SetByName("nValue", 99);
        oRec.SetByName("lActive", false);

        // Read via dynamic member (GETMEMBER)
        HbRuntime.QOUT("dynamic: " + oRec.GetByName("cName") + ", " + HbRuntime.STR(oRec.GetByName("nValue"), 4) + ", " + (oRec.GetByName("lActive") ? "T" : "F"));

        // Iterate fields by name (DumpFields uses GETMEMBER in a loop)
        oRec.DumpFields(aFields);

        return;
    }
}
