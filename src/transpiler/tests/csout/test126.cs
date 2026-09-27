using System;
using static HbRuntime;
using static Program;

// Test 126: a SWITCH runs on from the matching CASE until an EXIT.
//
// Harbour's SWITCH falls through: `CASE "C"; CASE "M"; x := ""; EXIT` is one
// body for both labels, and a body without EXIT runs on into the next
// CASE's, and from the last CASE into OTHERWISE. The C# ended every CASE
// with `break;`, so a label with no statements of its own did nothing:
// easipos's GetDefaultFromType() (sqlthelpers.prg) answered NIL for "C",
// "M" and "N", and every INSERT the ORM built from those defaults failed.
//
// C# falls through only between labels with nothing between them: an empty
// CASE stacks its label on the next, and a body that runs on says so with
// `goto case` / `goto default`.

// labels stacked, a comment between them, as in GetDefaultFromType()
public static partial class Program
{
    public static dynamic test126_Kind126(string cType = default)
    {
        dynamic xValue = default;

        switch (cType)
        {
            case "C":
            case "M":
                // a comment of its own
            case "B":
                xValue = "text";
                break;
            case "N":
            case "I":
                xValue = 0;
                break;
            case "L":
                xValue = false;
                break;
        }

        return xValue;

        // a body without EXIT runs on into the next body, and into OTHERWISE
    }
    public static string test126_Grade126(decimal nScore = default)
    {
        string cTrail = "";

        switch (nScore)
        {
            case 3:
                cTrail += "three ";
                goto case 2;
            case 2:
                cTrail += "two ";
                break;
            case 1:
                cTrail += "one ";
                goto default;
            default:
                cTrail += "other";
                break;
        }

        return cTrail;

        // a dynamic value switched on; the last CASE, empty, falls out
    }
    public static string test126_Tail126(dynamic xKey = default)
    {
        string cSeen = "-";

        switch (xKey)
        {
            case "a":
                cSeen = "A";
                goto case "b";
            case "b":
                cSeen += "B";
                break;
            case "z":
                break;
        }
        return cSeen;
    }

    public static void Main(string[] args)
    {
        HbRuntime.QOut(test126_Kind126("C"), test126_Kind126("M"), test126_Kind126("B"));
        HbRuntime.QOut(test126_Kind126("N"), test126_Kind126("I"), test126_Kind126("L"), test126_Kind126("D") == null);
        HbRuntime.QOut(test126_Grade126(3) + "|" + test126_Grade126(2) + "|" + test126_Grade126(1) + "|" + test126_Grade126(9));
        HbRuntime.QOut(test126_Tail126("a"), test126_Tail126("b"), test126_Tail126("z"), test126_Tail126(7));

        return;
    }
}
