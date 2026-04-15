using System;
using static HbRuntime;

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
        dynamic oError = HbRuntime.ERRORNEW();

        oError.severity = 3;
        oError.genCode = 42;
        oError.subSystem = "BASE";
        oError.subCode = 1001;
        oError.description = "sample failure";
        oError.canRetry = false;
        oError.canDefault = true;

        HbRuntime.QOUT("severity:    " + HbRuntime.STR(oError.severity, 4));
        HbRuntime.QOUT("genCode:     " + HbRuntime.STR(oError.genCode, 4));
        HbRuntime.QOUT("subSystem:   " + oError.subSystem);
        HbRuntime.QOUT("description: " + oError.description);
        HbRuntime.QOUT("canRetry:    " + (oError.canRetry ? "yes" : "no"));
        HbRuntime.QOUT("canDefault:  " + (oError.canDefault ? "yes" : "no"));
        HbRuntime.QOUT("classname:   " + oError.classname());
        return;
    }
}
