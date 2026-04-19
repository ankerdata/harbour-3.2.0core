using System;
using static HbRuntime;
using static Program;

// Test 37: class DATA INIT with trailing line comment.
//
// Copied from sharedx/transaction.prg style: a VAR with INIT value
// followed by a `//` comment on the same line. Before the fix, the
// emitter pasted the raw INIT text — comment and all — into
// `= <szInit>;`, producing `= 0 // Owner;` which made the `;` part
// of the line comment and broke C#. Same path prevented `.F.`/`.T.`/
// `NIL` from matching the canonical conversions because the trailing
// whitespace + comment meant the exact-string compare failed.

// #include "hbclass.ch"
public class Widget
{
    public decimal nCount { get; set; } = 0;
    public bool lEnabled { get; set; } = true;
    public bool lHidden { get; set; } = false;
    public string cName { get; set; } = "";

}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Widget oW = new Widget();

        HbRuntime.QOut("count: " + HbRuntime.Str(oW.nCount, 4));
        HbRuntime.QOut("name:  " + "[" + oW.cName + "]");
        if (oW.lEnabled)
        {
            HbRuntime.QOut("enabled: yes");
        }
        else
        {
            HbRuntime.QOut("enabled: no");
        }

        if (oW.lHidden)
        {
            HbRuntime.QOut("hidden: yes");
        }
        else
        {
            HbRuntime.QOut("hidden: no");
        }

        return;
    }
}
