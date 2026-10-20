using System;
using static HbRuntime;
using static Program;

// Test 154: a method send with a reference shim, standing in an expression.
//
// A call whose `@` argument needs a shim (the variable's type is not the
// slot's) is hoisted out of its expression into a temporary, so the shim's
// write-back can follow the call: `var _hbcall_<callee> = …`. The callee's
// name was the reftab key, which for a method is `Class::Class__Method`,
// and `var _hbcall_EasiTSE::EasiTSE__FinishTender = …` is not C#
// (CS1001): EasiPOS's fmde.prg hit it the first time a long local went
// into a method's decimal by-reference slot inside an IF. The temporary's
// name is an identifier now, whatever the key.
public class Meter154 : IHbObject
{
    public long nReadings = 0;

    public virtual decimal Read(decimal nValue, ref decimal nCount)
    {
        nCount = nReadings + 1;
        nReadings = (long)(nCount);
        return nValue * 2;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Meter154 oMeter = new Meter154();
        // a long, into the method's decimal slot: a shim
        long iCount = 0;

        // the send hoisted out of the IF
        {
            decimal _hbref_iCount = iCount;
            var _hbcall_Meter154__Meter154__Read = oMeter.Read(21, ref _hbref_iCount);
            iCount = (long)(_hbref_iCount);
            if (_hbcall_Meter154__Meter154__Read == 42)
            {
                QOut("read", iCount);
            }
        }

        // a second, its own temporary
        {
            decimal _hbref_iCount = iCount;
            var _hbcall_Meter154__Meter154__Read = oMeter.Read(5, ref _hbref_iCount);
            iCount = (long)(_hbref_iCount);
            if (_hbcall_Meter154__Meter154__Read == 10)
            {
                QOut("again", iCount);
            }
        }

        QOut(iCount, oMeter.nReadings);
        return;
    }
}
