using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 129: a call to a STATIC function types what it is assigned to.
//
// A STATIC function's reftab row is keyed `<FileBase>::<Name>`, and the
// type inference asked only the bare name, so `oPlain := MakeShape129()`
// left the local dynamic. The refinement then heard only from the typed
// caller: Kind129's parameter became Square129, and the call passing a
// Shape129 failed at run time in the binder - easipos ormtestsuite.prg's
// OrmTestOrderedIds( oFirst ), oFirst from the file's SharedIdsFixtureTable().
// Typed, the two callers widen the slot to their common ancestor.

// #include "hbclass.ch"
public class Shape129
{
    public string cKind129;

    public virtual Shape129 New(string cKind = default)
    {
        cKind129 = cKind;
        return this;

        // A class named after its file, as OrmTestSuite is in ormtestsuite.prg:
        // member rows are `Class::member`, the file's STATIC functions
        // `file::Name`, and the lookup still has to find the functions.
    }
}

public class Square129 : Shape129
{

}

public class Test129
{
    public string cNote129 = "note";

}

public static partial class Program
{
    public static string Kind129(Shape129 oShape = default)
    {
        return oShape.cKind129;
    }

    public static void Main(string[] args)
    {
        Square129 oSq129 = (Square129)new Square129().New("square");
        Shape129 oPl129 = test129_MakeShape129();

        HbRuntime.QOut(Kind129(oSq129));
        HbRuntime.QOut(Kind129(oPl129));
        HbRuntime.QOut(test129_Label129());
        HbRuntime.QOut(Panel129(1.5m), Panel129(0));
        HbRuntime.QOut(Flag129("L"));

        return;

        // A value that could be anything, taken into a name that commits to a type,
        // keeps the name's type: Poly129()'s RETURNs disagree (USUAL), and Flag129()
        // returns bool as its local is declared - easipos flags.prg's GetFlagL() over
        // TypedFlag(), which went dynamic once TypedFlag()'s USUAL was visible.
    }
    public static bool Flag129(string cKind = default)
    {
        bool lValue = test129_Poly129(cKind);
        return lValue;
    }

    public static dynamic test129_Poly129(string cKind = default)
    {
        if (cKind == "L")
        {
            return true;
        }

        return "text";

        // A numeric parameter given an INTEGER function's result keeps its type:
        // the emitter's inference seeds the parameters as the scan's does, or it
        // typed the return from the assignment - long, over a decimal parameter
        // (easipos screensetup.prg's ClerkPanel(), once PanelDefault() had a type).
    }
    public static decimal Panel129(decimal nPanel = default)
    {
        if (nPanel == 0)
        {
            nPanel = Default129();
        }

        return nPanel;

        // hb_bitAnd() makes it INTEGER, as PanelDefault()'s #defines did
    }
    public static long Default129()
    {
        return HbRuntime.hb_bitAnd(7, 3);
    }

    public static Shape129 test129_MakeShape129()
    {
        return (Shape129)new Shape129().New("plain");

        // A RETURN of a STATIC function's call takes its type too.
    }
    public static string test129_Label129()
    {
        return test129_Tag129() + "!";
    }

    public static string test129_Tag129()
    {
        return "tag";
    }
}
