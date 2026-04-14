using System;

// Test 29: SWITCH ... CASE against #define symbols.
//
// Standard Harbour requires every CASE label to reduce to a numeric
// or string literal at parse time (E0055 "CASE requires either numeric
// or string constant"). The vanilla PP expands `#define` symbols in
// the token stream before the parser sees them, so `CASE SHUT`
// arrives as `CASE "SHUTDOWN"` and passes.
//
// The transpiler intentionally preserves `#define` symbols so they can
// be re-emitted as C# `const` fields with the original name. That means
// `CASE SHUT` arrives at the grammar rule as an identifier, not a
// literal, and the E0055 check trips on every CASE.
//
// The C# emitter handles the symbolic reference fine — C# `switch`
// accepts const references as case labels. The fix is to skip the
// literal-only check under HB_TRANSPILER (src/transpiler/harbour.yyc).
//
// Hit in easiposx: bohpysvr, easiglory, easiudm, inspectmessage,
// keydebug, posdialog, rwreps, socketloop, postexcp — 43 CASE lines
// across 9 files.
public static partial class Program
{
    const string SHUT = @"SHUTDOWN";
    const string HALT = @"HALT";
    const string RUN = @"RUN";
    public static void Main(string[] args)
    {
        Dispatch(SHUT);
        Dispatch(HALT);
        Dispatch(RUN);
        Dispatch("UNKNOWN");
        return;
    }

    public static void Dispatch(string cCmd = default)
    {
        switch (cCmd)
        {
            case SHUT:
                HbRuntime.QOUT("shutting down");
                break;
            case HALT:
                HbRuntime.QOUT("halting");
                break;
            case RUN:
                HbRuntime.QOUT("running");
                break;
            default:
                HbRuntime.QOUT("unknown: " + cCmd);
                break;
        }

        return;
    }
}
