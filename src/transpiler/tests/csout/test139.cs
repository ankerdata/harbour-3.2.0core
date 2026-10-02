using System;
using static HbRuntime;
using static Program;

// Test 139: `!` over a relational or arithmetic operand.
//
// Harbour's `!` binds looser than every relational and arithmetic
// operator: `!Left( c, 3 ) == "TH-"` is `!( Left( c, 3 ) == "TH-" )`.
// C#'s `!` binds tighter than all of them, and the AST keeps only the
// parentheses the source wrote, so the emitter wraps a binary operand
// itself. Without it the C# was `!Left( c, 3 ) == "TH-"`, `!` applied to
// a string: CS0023 once the operand was typed, a run-time binder error
// while it was `dynamic` (easiwig.prg's Transaction History test). `$`
// and `^` emit as calls and need nothing; source parentheses stay as
// written; `!` still binds tighter than `.AND.` and `.OR.`.

// #include "hbclass.ch"
public class Ticket139
{
    public string cRef = "";
    public decimal nCount = 0;

    public virtual Ticket139 New(string cRef = default, decimal nCount = default)
    {
        this.cRef = cRef;
        this.nCount = nCount;
        return this;
    }
}

public static partial class Program
{
    public static bool test139_PayAgain139(Ticket139 oTicket = default)
    {
        // Transaction History must not be paid again
        if (!(HbRuntime.Left(oTicket.cRef, 3) == "TH-"))
        {
            return true;
        }

        return false;
    }

    public static void Main(string[] args)
    {
        Ticket139 oFresh = (Ticket139)new Ticket139().New("AB-1", 2);
        Ticket139 oHistory = (Ticket139)new Ticket139().New("TH-9", 0);
        decimal nCount = 3;
        string cName = "Lisbon";
        bool lOpen = false;

        HbRuntime.QOut(test139_PayAgain139(oFresh), test139_PayAgain139(oHistory));
        HbRuntime.QOut(!(oFresh.nCount == 0), !(oHistory.nCount == 0));
        HbRuntime.QOut(!(nCount > 5), !(nCount + 1 == 4));
        HbRuntime.QOut(!(HbRuntime.StrCmp(cName, "M") < 0), !(HbRuntime.StrCmp(cName, "Paris") >= 0));
        HbRuntime.QOut(!HbRuntime.HbIn("bon", cName), !(nCount == 3));
        HbRuntime.QOut(!lOpen && nCount == 3, !lOpen || nCount == 0);
        HbRuntime.QOut(!(oHistory.nCount != 0));
        return;
    }
}
