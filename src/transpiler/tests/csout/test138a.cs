using System;
using static HbRuntime;
using static Program;

// Test 138 (multi-file pair): downcasts across files, and an element's
// subclass member left uncast (plan C11).
//
// A parameter named for its class (`oPrintLine138`) keeps the class when a
// caller passes its parent. The name used to be taken only once the slot
// held the class: here the classes are in test138b.prg, which the scan
// reaches after this file, so on the first pass the name names nothing
// and the slot is a plain object; on the next, FirstTotal138's Line138
// came first and the slot became Line138, which the later subclass callers
// fit under. EasiPOS's GetPLULineColor( oTransaction, oItemTranLine )
// came out TranLine that way. Now a parent given to a name reads as the
// name's class: the slot is PrintLine138 and the parent's call casts.
//
// A member only a subclass declares, read off an element of a declared
// array (`oTill138:aLines[ i ]:nValue`, aLines AS ARRAY OF CLASS Line138),
// is written as it was before the declaration: the element is `dynamic`
// in C# (a List<dynamic> slot), so `((dynamic)...)` added nothing but
// noise, 2,516 times in EasiPOS's sale buffer.
//
// This file has the callers and the named parameter.

// the first caller passes the parent: an element of a declared array
public static partial class Program
{
    public static decimal FirstTotal138(Till138 oTill138 = default)
    {
        Line138 oLine = oTill138.aLines[0];
        return Amount138((PrintLine138)oLine);

        // later callers pass subclasses
    }
    public static decimal LaterTotal138(FcnLine138 oFcnLine138 = default, GenLine138 oGenLine138 = default)
    {
        return Amount138(oFcnLine138) + Amount138(oGenLine138);

        // named for the middle class, which it keeps
    }
    public static decimal Amount138(PrintLine138 oPrintLine138 = default)
    {
        return oPrintLine138.nValue;

        // a member only a subclass declares, read off an element: no cast
    }
    public static decimal SumValues138(Till138 oTill138 = default)
    {
        decimal nTotal = 0;
        long iPos = default;
        for (iPos = 1; iPos <= HbRuntime.Len(oTill138.aLines); iPos++)
        {
            if (((Line138)oTill138.aLines[(int)iPos - 1]).nKind != 0)
            {
                nTotal += oTill138.aLines[(int)iPos - 1].nValue;
            }
        }

        return nTotal;
    }
}
