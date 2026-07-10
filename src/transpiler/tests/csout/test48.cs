using System;
using static HbRuntime;
using static Program;

// Test 48: local variable used both inside a FOR-loop body and later
// as a FOREACH iterator. The transpiler used to suppress the `local`
// declaration if ANY later FOREACH used the same name, walking only
// outer siblings — so a local assigned inside a FOR body (one level
// down) then reused by a FOREACH was emitted with no declaration,
// producing C# CS0103 / CS0841 at compile time.
//
// The fix always emits the method-level local and renames the FOREACH
// inner iterator via `__hb_fe_<name>`, assigning to the outer. This
// test locks that behaviour in.
public static partial class Program
{
    public static void Main(string[] args)
    {
        dynamic[] aFields = new dynamic[] { "one", "two", "three" };
        string cMemberName = default;
        long i = default;
        string cAccum = "";

        // Use cMemberName inside a FOR-loop body first (nested scope).
        for (i = 1; i <= HbRuntime.Len(aFields); i++)
        {
            cMemberName = aFields[i - 1];
            cAccum += "[" + cMemberName + "]";
        }

        HbRuntime.QOut("via-for: " + cAccum);

        // Then reuse the same name as a FOREACH iterator.
        cAccum = "";
        foreach (dynamic __hb_fe_cMemberName in aFields)
        {
            cMemberName = __hb_fe_cMemberName;
            cAccum += "<" + cMemberName + ">";
        }

        HbRuntime.QOut("via-each: " + cAccum);

        return;
    }
}
