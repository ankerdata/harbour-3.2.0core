using System;
using static HbRuntime;
using static Program;

// Test 118: a hash literal assigned to an integer-keyed hash takes its key type.
//
// A declaration's initializer already did: LOCAL hById := { => } on a hash
// indexed by numbers is an OrderedDictionary<long, dynamic>. Resetting it
// with hById := { => } emitted the string-keyed default instead, which C#
// cannot assign to it (CS0029) - easipos/jsonifystate.prg empties two such
// caches that way. Pinned for a local and for a file static, each filled,
// emptied and filled again.
public static partial class Program
{
    public static OrderedDictionary<long, dynamic> test118_shSeen118 = new OrderedDictionary<long, dynamic> {  };
    public static void Main(string[] args)
    {
        OrderedDictionary<long, dynamic> hCount118 = new OrderedDictionary<long, dynamic> {  };
        long nKey = default;

        for (nKey = 1; nKey <= 3; nKey++)
        {
            hCount118[nKey] = nKey * 10;
            test118_shSeen118[nKey] = true;
        }

        HbRuntime.QOut("filled:", HbRuntime.Len(hCount118), HbRuntime.Len(test118_shSeen118), hCount118[2]);

        hCount118 = new OrderedDictionary<long, dynamic> {  };
        test118_shSeen118 = new OrderedDictionary<long, dynamic> {  };
        HbRuntime.QOut("emptied:", HbRuntime.Len(hCount118), HbRuntime.Len(test118_shSeen118));

        hCount118[7] = 70;
        test118_shSeen118[7] = true;
        HbRuntime.QOut("again:", hCount118[7], HbRuntime.hb_HHasKey(test118_shSeen118, 7), HbRuntime.Len(hCount118), HbRuntime.Len(test118_shSeen118));

        return;
    }
}
