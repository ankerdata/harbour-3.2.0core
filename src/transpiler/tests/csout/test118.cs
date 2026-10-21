// Test 118: a hash literal assigned to an integer-keyed hash takes its key type.
//
// A declaration's initializer already did: LOCAL hById := { => } on a hash
// indexed by numbers is an OrderedDictionary<long, dynamic>. Resetting it
// with hById := { => } emitted the string-keyed default instead, which C#
// cannot assign to it (CS0029) - easipos/jsonifystate.prg empties two such
// caches that way. Pinned for a local and for a file static, each filled,
// emptied and filled again.
//
// Since 2026-09-27 the name declares the key type (test127): the hashes
// keyed by numbers here are hn<...> / shn<...>.
public static partial class Program
{
    public static OrderedDictionary<long, dynamic> test118_shnSeen118 = new OrderedDictionary<long, dynamic>();
    public static void Main(string[] args)
    {
        var hnCount118 = new OrderedDictionary<long, dynamic>();
        long nKey;

        for (nKey = 1; nKey <= 3; nKey++)
        {
            hnCount118[nKey] = nKey * 10;
            test118_shnSeen118[nKey] = true;
        }

        QOut("filled:", Len(hnCount118), Len(test118_shnSeen118), hnCount118[2]);

        hnCount118 = new OrderedDictionary<long, dynamic>();
        test118_shnSeen118 = new OrderedDictionary<long, dynamic>();
        QOut("emptied:", Len(hnCount118), Len(test118_shnSeen118));

        hnCount118[7] = 70;
        test118_shnSeen118[7] = true;
        QOut("again:", hnCount118[7], hb_HHasKey(test118_shnSeen118, 7), Len(hnCount118), Len(test118_shnSeen118));
    }
}
