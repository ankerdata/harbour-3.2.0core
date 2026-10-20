using System;
using static HbRuntime;
using static Program;

// Test 34: Harbour Error object stopgap round-trip.
//
// Covers the HbError POCO added to HbRuntime.cs: ErrorNew() returns
// an instance whose field surface matches what createerror.prg and
// callers poke at. The return type is marked `-` in hbfuncs.tab so
// the emitter picks `dynamic` for the LHS, letting `:severity` and
// friends late-bind (otherwise CS1061 on a static `object`).
//
// This is a stopgap — no error propagation, no SEQUENCE/Try wiring.
// Replace with idiomatic C# exceptions / Result type when the real
// error story is designed.
public static partial class Program
{
    public static void Main(string[] args)
    {
        HbError oError = ErrorNew();

        oError.severity = 3;
        oError.genCode = 42;
        oError.subSystem = "BASE";
        oError.subCode = 1001;
        oError.description = "sample failure";
        oError.canRetry = false;
        oError.canDefault = true;

        QOut("severity:    " + Str(oError.severity, 4));
        QOut("genCode:     " + Str(oError.genCode, 4));
        QOut("subSystem:   " + oError.subSystem);
        QOut("description: " + oError.description);
        QOut("canRetry:    " + (oError.canRetry ? "yes" : "no"));
        QOut("canDefault:  " + (oError.canDefault ? "yes" : "no"));
        QOut("classname:   " + CLASSNAME(oError));
        return;
    }
}
