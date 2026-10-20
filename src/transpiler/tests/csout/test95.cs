using System;
using static HbRuntime;
using static Program;

// Test 95: three by-ref call shapes at method sends and in conditions.
//
// (1) `GetBrewer():Measure( "tea", @nOut )` — the receiver is a
//     function returning the object, so the method row comes from the
//     function's return type; with the row known, the omitted trailing
//     by-ref slot (aLog) is padded with the shared discard instead of
//     being dropped (CS7036: `GetsoEasiCdS():GetVoucher(...)` in the
//     easipos corpus).
// (2) `WHILE Tick( @n, .F. )` / `ELSEIF Peel( oBrewer:cName ) == "t"` —
//     a literal or a member access at a by-ref slot inside a WHILE or
//     ELSEIF condition, where no shim block can be placed and hoisting
//     would evaluate the call once: the inline discard form
//     (`ref HbDiscard<T>.Seed( x )`, plain variables since test80) now
//     covers every non-@ shape (CS1620).
// (3) `Nudge( @i )` on a FOR counter the transpiler types INTEGER: the
//     shim temp is decimal and its write-back into the long takes the
//     (long) an AS INTEGER member already got (CS0266).
public class Brewer : IHbObject
{
    public string cName = "tea";

    public virtual decimal Measure(string cWhat, ref decimal nOut, ref List<dynamic> aLog)
    {
        nOut = Len(cWhat) * 10;
        aLog = new List<dynamic> { cWhat };
        return nOut;
    }
}

public static partial class Program
{
    public static Brewer test95_GetBrewer_soBrewer;
    public static Brewer test95_GetBrewer()
    {
        if (test95_GetBrewer_soBrewer == null)
        {
            test95_GetBrewer_soBrewer = new Brewer();
        }

        return test95_GetBrewer_soBrewer;
    }

    public static bool Tick(ref decimal nCount, ref bool lReset)
    {
        if (lReset)
        {
            nCount = 0;
            lReset = false;
        }

        nCount++;
        return nCount < 3;
    }

    public static string Peel(ref string cText)
    {
        cText = Left(cText, 1);
        return cText;
    }

    public static dynamic Nudge(ref decimal nValue)
    {
        nValue = nValue + 10;
        return null;
    }

    public static void Main(string[] args)
    {
        decimal nOut = 0;
        decimal n = 5;
        decimal i = default;
        bool lReset = true;
        string cWord = "tea";
        List<dynamic> aLog = new List<dynamic> {  };
        Brewer oBrewer = test95_GetBrewer();

        // @aLog: the slot is by-ref
        oBrewer.Measure("coffee", ref nOut, ref aLog);
        QOut("coffee=" + LTrim(Str(nOut)) + " log=" + aLog[Ix(1)]);
        // aLog omitted
        test95_GetBrewer().Measure("tea", ref nOut, ref HbDiscard<List<dynamic>>.Value);
        QOut("tea=" + LTrim(Str(nOut)));

        // @lReset: the slot is by-ref
        Tick(ref n, ref lReset);
        QOut("reset=" + LTrim(Str(n)) + " " + (lReset ? "T" : "F"));
        while (Tick(ref n, ref HbDiscard<bool>.Seed((bool)(false))))
        {
            QOut("tick=" + LTrim(Str(n)));
        }

        // @cWord: the slot is by-ref
        Peel(ref cWord);
        if (oBrewer.cName == "coffee")
        {
            QOut("coffee");
        }
        else if (Peel(ref HbDiscard<string>.Seed((string)(oBrewer.cName))) == "t")
        {
            QOut("tea " + cWord);
        }

        for (i = 1; i <= 2; i++)
        {
            Nudge(ref i);
        }

        QOut("i=" + LTrim(Str(i)));
        return;
    }
}
