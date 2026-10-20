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
    public static void Main(string[] args)
    {
        Dispatch(Test29PrgConst.SHUT);
        Dispatch(Test29PrgConst.HALT);
        Dispatch(Test29PrgConst.RUN);
        Dispatch("UNKNOWN");
    }

    public static void Dispatch(string cCmd = default)
    {
        switch (cCmd)
        {
            case Test29PrgConst.SHUT:
                QOut("shutting down");
                break;
            case Test29PrgConst.HALT:
                QOut("halting");
                break;
            case Test29PrgConst.RUN:
                QOut("running");
                break;
            default:
                QOut("unknown: " + cCmd);
                break;
        }
    }
}
