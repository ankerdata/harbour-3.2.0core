// Test 137: downcasts (plan C11).
//
// A name or a declaration of a subclass, given a value of its parent,
// keeps its class: the typed view after a test of the kind
// (`oItemLine137 := oLine` after `oLine:nKind == KIND_ITEM`), a parameter
// named or declared the subclass, a local initialised with the parent, a
// function declared returning the subclass, a member declared it. The
// name or the declaration is the claim, and C# needs the cast Harbour has
// no way to write, so the emitter writes it (`oItemLine137 =
// (ItemLine137)oLine;`). A wrong claim throws InvalidCastException there
// in C#, where Harbour goes on to the first message the object does not
// answer. An element of a declared array is `dynamic` in C# and needs no
// cast. A class unrelated to the name still wins over it, and is W0041
// against a declaration (tests/errors/declared_types.prg).
public class Line137 : IHbObject
{
    public decimal nKind = 0;
    public string cText = "";

}

public class FcnLine137 : Line137
{
    public decimal nFixedNo = 0;

    public virtual FcnLine137 New(string cText = default, decimal nFixedNo = default)
    {
        nKind = Test137PrgConst.KIND_FCN;
        this.cText = cText;
        this.nFixedNo = nFixedNo;
        return this;
    }
}

public class ItemLine137 : Line137
{
    public decimal nPrice = 0;
    public decimal nQty = 0;

    public virtual ItemLine137 New(string cText = default, decimal nPrice = default, decimal nQty = default)
    {
        nKind = Test137PrgConst.KIND_ITEM;
        this.cText = cText;
        this.nPrice = nPrice;
        this.nQty = nQty;
        return this;
    }

    public virtual decimal Value()
    {
        return nPrice * nQty;
    }
}

public class Till137 : IHbObject
{
    public List<dynamic> aLines = new List<dynamic>();
    public ItemLine137 oLastItem;

    public virtual Till137 Add(Line137 oLine = default)
    {
        AAdd(aLines, oLine);
        if (oLine.nKind == Test137PrgConst.KIND_ITEM)
        {
            // a member declared the subclass
            oLastItem = (ItemLine137)oLine;
        }

        return this;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        var oTill137 = new Till137();
        Line137 oLine;
        decimal nTotal = 0;

        oTill137.Add(new ItemLine137().New("tea", 3, 2));
        oTill137.Add(new FcnLine137().New("discount", 7));
        oTill137.Add(new ItemLine137().New("cake", 5, 1));

        foreach (Line137 __hb_fe_oLine in HbEnumValues(oTill137.aLines))
        {
            oLine = __hb_fe_oLine;
            if (oLine.nKind == Test137PrgConst.KIND_ITEM)
            {
                // a view by its name
                ItemLine137 oItemLine137 = (ItemLine137)oLine;
                nTotal += oItemLine137.Value();
            }
            else
            {
                // a view by its declaration
                FcnLine137 oFcn = (FcnLine137)oLine;
                QOut(oFcn.cText, oFcn.nFixedNo);
            }
        }

        QOut(nTotal);

        for (long iPos = 1; iPos <= Len(oTill137.aLines); iPos++)
        {
            oLine = oTill137.aLines[I(iPos)];
            if (oLine.nKind == Test137PrgConst.KIND_ITEM)
            {
                QOut(Describe137((ItemLine137)oLine), Price137((ItemLine137)oLine), Qty137(oLine));
            }
        }

        QOut(oTill137.oLastItem.cText, ItemAt137(oTill137, 1).nPrice);

        // a parameter named for the subclass
    }
    public static string Describe137(ItemLine137 oItemLine137 = default)
    {
        return oItemLine137.cText + " x" + hb_ntos(oItemLine137.nQty);

        // a parameter declared the subclass
    }
    public static decimal Price137(ItemLine137 oAny = default)
    {
        return oAny.nPrice;

        // a local named for the subclass, initialised with its parent
    }
    public static decimal Qty137(Line137 oLine = default)
    {
        ItemLine137 oItemLine137 = (ItemLine137)oLine;
        return oItemLine137.nQty;

        // a function declared returning the subclass, returning its parent
    }
    public static ItemLine137 ItemAt137(Till137 oTill137 = default, long iPos = default)
    {
        Line137 oLine = oTill137.aLines[I(iPos)];
        return (ItemLine137)oLine;
    }
}
