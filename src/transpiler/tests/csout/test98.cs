// Test 98: integral lvalues fed a decimal; an assignment as an operand.
//
// The transpiler types a LOCAL or a file STATIC seeded from an integer
// define as C# long, and an AS INTEGER member is long by declaration.
// A decimal reaching one of those — a NUMERIC parameter, a method's
// return, a quantity — is CS0266. The emitter already coerced a plain
// `:=` into an int local or member; it now also covers a file STATIC
// on the left, the compound `+=` family, and the assignment the
// ref-shim block emits for `n := o:M( @x )` (ten sites in the easipos
// corpus). Where the fraction is real — a division, a quantity — the
// corpus says `Int()` in source so Harbour truncates too, and the
// coercion then only restates it.
// A file STATIC takes its type from its initialiser alone (Pass 2.5
// only sees the body a variable is declared in), so `snHalf` below is
// long from LAMP_LOW; the division written into it from Main — a hard
// disqualifier — demotes it to decimal in the emitter's pre-pass, and
// it holds 0.5 as Harbour does.
// Second: `FOR i := 1 TO ( nLen := Len( a ) )` — an assignment used as
// an operand keeps its parentheses in C#, where `=` binds looser than
// `<=` (CS0131).
public class Ledger : IHbObject
{
    public long nCovers = 0;

    public virtual long Add(decimal nQty = default)
    {
        nCovers += (long)(Int(nQty));
        return nCovers;

        /* NUMERIC return with a by-ref parameter: the caller's assignment is
   emitted inside the shim block. */
    }

    public virtual decimal Gauge(string cWhat, ref string cOut)
    {
        cOut = "read " + cWhat;
        return Len(cWhat) * 10;
    }
}

public static partial class Program
{
    public static long test98_snLamp = Test98PrgConst.LAMP_OFF;
    public static decimal test98_snHalf = Test98PrgConst.LAMP_LOW;
    public static void SetLamp(decimal nLamp = default)
    {
        test98_snLamp = (long)(nLamp);
    }

    public static void Main(string[] args)
    {
        Ledger oLedger = new Ledger();
        long nResult = Test98PrgConst.LAMP_OFF;
        string cOut = "";
        List<dynamic> aList = new List<dynamic> { "a", "b", "c" };
        string cAll = "";
        decimal nLen;
        long i;

        SetLamp(Test98PrgConst.LAMP_LOW);
        test98_snHalf = ((decimal)(Test98PrgConst.LAMP_LOW) / 2);
        QOut("lamp=" + LTrim(Str(test98_snLamp)) + " half=" + (test98_snHalf * 2 == 1 ? "kept" : "lost"));
        oLedger.Add(2.7m);
        QOut("covers=" + LTrim(Str(oLedger.Add(1))));
        nResult = (long)(oLedger.Gauge("tea", ref cOut));
        QOut("result=" + LTrim(Str(nResult)) + " " + cOut);
        for (i = 1; i <= (nLen = Len(aList)); i++)
        {
            cAll += aList[I(i)];
        }

        QOut("all=" + cAll + " len=" + LTrim(Str(nLen)));
    }
}
