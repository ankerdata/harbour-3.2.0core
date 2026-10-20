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
public class Record : HbDynamicObject
{
    public string cName = "";
    public decimal nValue = 0;
    public bool lActive = false;

    public virtual Record SetByName(string cField = default, dynamic xValue = default)
    {
        SETMEMBER(this, cField, xValue);
        return this;
    }

    public virtual dynamic GetByName(string cField = default)
    {
        return GETMEMBER(this, cField);
    }

    public virtual dynamic DumpFields(List<dynamic> aFields = default)
    {
        long i = default;
        dynamic xVal = default;
        string cType = default;
        for (i = 1; i <= Len(aFields); i++)
        {
            xVal = GETMEMBER(this, aFields[Ix(i)]);
            cType = ValType(xVal);
            if (cType == "C")
            {
                QOut(aFields[Ix(i)] + ": " + xVal);
            }
            else if (cType == "N")
            {
                QOut(aFields[Ix(i)] + ": " + Str(xVal, 4));
            }
            else if (cType == "L")
            {
                QOut(aFields[Ix(i)] + ": " + (xVal ? "T" : "F"));
            }
            else
            {
                QOut(aFields[Ix(i)] + ": ?");
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
        List<dynamic> aFields = new List<dynamic> { "cName", "nValue", "lActive" };

        // Direct property access — baseline
        oRec.cName = "hello";
        oRec.nValue = 42;
        oRec.lActive = true;
        QOut("direct: " + oRec.cName + ", " + Str(oRec.nValue, 4) + ", " + (oRec.lActive ? "T" : "F"));

        // Write via dynamic member (SETMEMBER)
        oRec.SetByName("cName", "world");
        oRec.SetByName("nValue", 99);
        oRec.SetByName("lActive", false);

        // Read via dynamic member (GETMEMBER)
        QOut("dynamic: " + oRec.GetByName("cName") + ", " + Str(oRec.GetByName("nValue"), 4) + ", " + (oRec.GetByName("lActive") ? "T" : "F"));

        // Iterate fields by name (DumpFields uses GETMEMBER in a loop)
        oRec.DumpFields(aFields);

        return;
    }
}
