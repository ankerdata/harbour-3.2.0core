using System;
using static HbRuntime;
using static Program;

// Test 127: a hash keyed by numbers is named hn<...> (Alex, 2026-09-27).
//
// C# commits a dictionary to one key type. The inference used to guess it
// from the keys the code happened to use, and the guess went wrong where a
// key came from a macro or an `x` value (easipos languages.prg's lang
// tables). The name declares it now: h<...> is keyed by strings, the
// default, and hn<...> / shn<...> by numbers, an OrderedDictionary<long,
// dynamic> - for a local, a static, a member and a parameter alike. A key
// of the other kind is W0035 (tests/errors/hash_key_name.prg); a key of no
// known type is taken as the name says.

// #include "hbclass.ch"
public class Ledger127
{
    public OrderedDictionary<long, dynamic> hnRows127;

    public virtual Ledger127 Fill127()
    {
        hnRows127 = new OrderedDictionary<long, dynamic> {  };
        hnRows127[12] = "twelve";
        return this;
    }
}

public static partial class Program
{
    public static OrderedDictionary<long, dynamic> test127_shnSeen127 = new OrderedDictionary<long, dynamic> {  };
    public static decimal test127_Count127(OrderedDictionary<long, dynamic> hnFrom = default)
    {
        return HbRuntime.Len(hnFrom);
    }

    public static void Main(string[] args)
    {
        OrderedDictionary<long, dynamic> hnById = new OrderedDictionary<long, dynamic> {  };
        OrderedDictionary<string, dynamic> hByName = new OrderedDictionary<string, dynamic> {  };
        Ledger127 oLedger127 = new Ledger127().Fill127();
        long nId = 7;
        // a key of no known type: Eval() answers whatever its block does
        dynamic xKey = HbRuntime.Eval(((Func<decimal>)(() => test127_Seven127() + 2)));

        hnById[nId] = "seven";
        hnById[(long)(xKey)] = "nine";
        hByName["seven"] = nId;
        test127_shnSeen127[3] = true;

        HbRuntime.QOut(hnById[7], hnById[9], hByName["seven"], test127_Count127(hnById));
        HbRuntime.QOut(oLedger127.hnRows127[12], HbRuntime.hb_HHasKey(test127_shnSeen127, 3), HbRuntime.Len(test127_shnSeen127));

        return;
    }

    public static decimal test127_Seven127()
    {
        return 7;
    }
}
