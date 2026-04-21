using System;
using static HbRuntime;
using static Program;

// Test 45b: Main that calls LoadFlags (from test45a.prg) at full
// arity. A 1-arg call to LoadFlags lives in a dead `IF .F.` branch
// below: the scanner sees the call site in source (records arity 1
// in the observed-call-arity bitmap) so the emitter generates the
// short overload, but Harbour and C# both skip the body at runtime
// so neither tries to run LoadFlags("short.dat") — which would
// crash both pipelines (can't ASize NIL) and, worse, crash them
// with DIFFERENT errors (BASE/2023 vs IndexOutOfRangeException)
// that the compare step would flag as a bogus transpilation
// divergence. The short-overload *emission* remains directly
// asserted by scripts/verify_shortoverload.sh via grep.
public static partial class Program
{
    public static void Main(string[] args)
    {
        dynamic[] aArr = new dynamic[] {  };

        LoadFlags("full.dat", ref aArr, 4);
        HbRuntime.QOut("full[1]=" + aArr[0]);
        HbRuntime.QOut("full[4]=" + aArr[3]);

        if (false)
        {
            LoadFlags("short.dat");
        }

        return;
    }
}
