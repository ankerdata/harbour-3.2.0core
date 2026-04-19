using System;
using static HbRuntime;
using static Program;

// Test 0: Comments (all types), SET COLOR, basic LOCAL assignment
public static partial class Program
{
    public static void Main(string[] args)
    {
        // /* some comment*/
        // procedure main()
        //     ? "hello"
        // return

        // Single line comment
        // trailing comment
        decimal x = 5;
        HbRuntime.QOUT("x=" + HbRuntime.STR(x));
        // Star comment
        // Old comment
        /*x for sex is NB*/
        // dBASE commentd
        x = x + 1;
        HbRuntime.QOUT("x=" + HbRuntime.STR(x));

        /* Standalone
      multi-line comment */
        HbRuntime.SETCOLOR("\"W/B\"");

        HbRuntime.QOUT("Amazing success!");

        return;
    }
}
