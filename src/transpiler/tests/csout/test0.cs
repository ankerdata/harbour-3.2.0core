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
        QOut("x=" + Str(x));
        // Star comment
        // Old comment
        /*x for sex is NB*/
        // dBASE commentd
        x = x + 1;
        QOut("x=" + Str(x));

        /* Standalone
      multi-line comment */
        SetColor("\"W/B\"");

        QOut("Amazing success!");
    }
}
