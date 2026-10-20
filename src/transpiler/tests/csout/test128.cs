// Test 128: an empty argument slot on an untyped receiver takes the
// parameter's declared default.
//
// Leaving an argument out is an empty slot, `o:Eval( b, , , , 5 )`: the
// callee declares the default (hb_default( @nNext, 0 )), C# puts it on the
// signature, and the arguments after the gap are named (`nRecord: 5`). That
// took the receiver's class; for a receiver of unknown class (C# dynamic) the
// gap went in as null, which a decimal or bool parameter cannot take - easipos
// ormtestsuite.prg's TestEval on ::oOrmTable failed that way. When every class
// declaring the method gives it the same parameters, an untyped receiver's
// call names them too.
public class Counter128 : IHbObject
{

    public virtual string Tally128(string cLabel = default, decimal nStart = 1, decimal nStep = 10, bool lShout = false)
    {
        return (lShout ? Upper(cLabel) : cLabel) + " " + hb_ntos(nStart + nStep);
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        // a hash's value is untyped in C#, so every send below is dynamic
        OrderedDictionary<string, dynamic> hBox128 = new OrderedDictionary<string, dynamic> { { "counter", new Counter128() } };

        QOut(hBox128["counter"].Tally128("a"));
        QOut(hBox128["counter"].Tally128("b", nStep: 5));
        QOut(hBox128["counter"].Tally128("c", lShout: true));
        QOut(hBox128["counter"].Tally128("d", 2, lShout: true));

        return;
    }
}
