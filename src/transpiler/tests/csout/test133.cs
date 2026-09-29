using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 133: a model named after its factory's stem, and an o<Class>
// parameter nothing refines, typed by its name.
//
// The ORM models are named <Stem>Table after their definition factories
// <Stem>Def (Alex, 2026-09-28): the map's `TestShelf133Def =model
// TestShelf133Table` row makes `ConstructORMTable( TestShelf133Def() )`
// emit `new TestShelf133Table(...)`, and a variable named
// oTestShelf133Table is that class by its name, as oTransaction is a
// Transaction. A parameter
// whose callers pass only untyped values (here, values out of a hash) was
// declared dynamic; one named after a class, or after a model, is now
// declared as it: ShelfLabel133( oTestShelf133Table ) and
// Bump133( oCounter133 ).
//
// The Harbour side, as test83's: a stand-in record class, and the factory
// and ConstructORMTable() as plain functions.

// #include "hbclass.ch"
public class ShelfRec133
{
    public decimal nNo;
    public string cName;

    public virtual ShelfRec133 New()
    {
        return this;
    }
}

public class Counter133
{
    public decimal nCount133 = 0;

}

public static partial class Program
{
    public static List<dynamic> TestShelf133Def(string cPath = default)
    {
        return new List<dynamic> { new List<dynamic> { "Shelf", "SHELF" }, (cPath != null ? cPath : ""), new List<dynamic> {  }, new List<dynamic> {  }, new OrderedDictionary<string, dynamic> {  }, 1 };
    }

    public static ShelfRec133 ConstructORMTable(List<dynamic> aFileDefinition = default, bool lReadOnly = default, bool lShared = default)
    {
        return (ShelfRec133)new ShelfRec133().New();

        // both parameters' callers pass values out of a hash: untyped
    }
    public static string ShelfLabel133(TestShelf133Table oTestShelf133Table = default)
    {
        return oTestShelf133Table.cName + " " + HbRuntime.hb_ntos(oTestShelf133Table.nNo);
    }

    public static void Bump133(Counter133 oCounter133 = default)
    {
        oCounter133.nCount133 += 1;
        return;
    }

    public static void Main(string[] args)
    {
        TestShelf133Table oShelf = new TestShelf133Table(TestShelf133Def());
        OrderedDictionary<string, dynamic> hBox = new OrderedDictionary<string, dynamic> {  };

        oShelf.nNo = 7;
        oShelf.cName = "top";
        hBox["shelf"] = oShelf;
        hBox["counter"] = new Counter133();

        HbRuntime.QOut(ShelfLabel133(hBox["shelf"]));
        Bump133(hBox["counter"]);
        Bump133(hBox["counter"]);
        HbRuntime.QOut(hBox["counter"].nCount133);
        return;
    }
}
