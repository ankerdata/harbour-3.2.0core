using System;

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
        HbRuntime.QOut("x=" + HbRuntime.Str(x));
        // Star comment
        // Old comment
        /*x for sex is NB*/
        // dBASE commentd
        x += 1;
        HbRuntime.QOut("x=" + HbRuntime.Str(x));

        /* Standalone
      multi-line comment */
        HbRuntime.SetColor("\"W/B\"");

        HbRuntime.QOut("Amazing success!");

        return;
    }
}
