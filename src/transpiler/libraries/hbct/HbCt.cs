// HbCt — the hbct contrib library (contrib/hbct/hbct.hbx) for
// transpiled Harbour code. The emitter writes `HbCt.<name>(...)` for
// every name that .hbx lists. Real implementations go here; HbCt.Stubs.cs
// holds NotImplementedException stubs for the rest the application calls
// (scripts/gen_library_stubs.py in easipos-transpiled).

public static partial class HbCt
{
    // Ceiling( <nNumber> ) (ctmath2.c): the smallest whole number not less
    // than nNumber, with no decimals, as C's ceil() and hb_retnlen( d, 0, 0 )
    // give it. EasiPOS asks how many tabs a list of filters fills.
    public static decimal Ceiling(decimal nNumber) => decimal.Ceiling(nNumber);
}
