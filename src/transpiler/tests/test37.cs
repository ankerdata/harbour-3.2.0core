using System;
using static HbRuntime;

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

        HbRuntime.QOUT("count: " + HbRuntime.STR(oW.nCount, 4));
        HbRuntime.QOUT("name:  " + "[" + oW.cName + "]");
        if (oW.lEnabled)
        {
            HbRuntime.QOUT("enabled: yes");
        }
        else
        {
            HbRuntime.QOUT("enabled: no");
        }

        if (oW.lHidden)
        {
            HbRuntime.QOUT("hidden: yes");
        }
        else
        {
            HbRuntime.QOUT("hidden: no");
        }

        return;
    }
}
