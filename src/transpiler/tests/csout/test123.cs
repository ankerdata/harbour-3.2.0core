using System;
using static HbRuntime;
using static Program;

// Test 123: a subscript in an INLINE method is rebased as one in a method
// body is.
//
// An INLINE body reaches the emitter as text, not as an expression tree,
// and the text translator copied a subscript through as it stood, so
// `INLINE ::aData[1]` read the C# list's element 1, Harbour's second, and
// the last element read past the end (easipos daterangedialog.prg's
// StartFile() / EndFile()). It went unnoticed while arrays were C#
// arrays, whose indexer takes a long; a List<dynamic>'s takes an int.
//
// The translator decides as the AST emitter does: a string or c-named
// index, or a receiver whose name says hash, is a hash key and passes as
// it is; anything else is an array index, rebased.

// #include "hbclass.ch"
public class Shelf123
{
    public List<dynamic> aSlots123;
    public List<dynamic> aGrid123;
    public OrderedDictionary<string, dynamic> hTags123;
    public decimal nPos123;

    public dynamic First123() => aSlots123[0];
    public dynamic Last123() => aSlots123[(int)(HbRuntime.Len(aSlots123)) - 1];
    public dynamic At123(dynamic nAt = default) => aSlots123[(int)(nAt) - 1];
    public dynamic Current123() => aSlots123[(int)(nPos123) - 1];
    public dynamic Cell123(dynamic nRow = default, dynamic nCol = default) => aGrid123[(int)(nRow) - 1][(int)(nCol) - 1];
    public dynamic Tag123(dynamic cKey = default) => hTags123[cKey];
    public dynamic Red123() => hTags123["red"];
    public dynamic Put123(dynamic nAt = default, dynamic cVal = default) => aSlots123[(int)(nAt) - 1] = cVal;
    public virtual Shelf123 Fill123()
    {
        aSlots123 = new List<dynamic> { "first", "second", "third" };
        aGrid123 = new List<dynamic> { new List<dynamic> { "a1", "a2" }, new List<dynamic> { "b1", "b2" } };
        hTags123 = new OrderedDictionary<string, dynamic> { { "red", "warm" }, { "blue", "cold" } };
        nPos123 = 2;
        return this;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        dynamic oStore123 = new Shelf123().Fill123();

        HbRuntime.QOut(oStore123.First123(), oStore123.Last123(), oStore123.At123(2), oStore123.Current123());
        HbRuntime.QOut(oStore123.Cell123(2, 1), oStore123.Cell123(1, 2));
        HbRuntime.QOut(oStore123.Tag123("blue"), oStore123.Red123());
        oStore123.Put123(3, "changed");
        HbRuntime.QOut(oStore123.At123(3));

        return;
    }
}
