using System;
using static HbRuntime;
using static Program;

// Test 99: sends on a DYNAMIC receiver; ACCESS / ASSIGN INLINE bodies.
//
// A receiver the transpiler cannot type is `dynamic` in C#, and the
// DLR reads `d.Name` as a field or property: a method sent bare
// (`xBin:Heft`, legal Harbour) throws at runtime, and so does `d.Len()`
// on a property (a parameterless MESSAGE alias, test97). When the
// receiver's class is unknown the emitter asks the reftab whether the
// name is a method on every class declaring it, or a member on every
// one, and adds or drops the parentheses accordingly; a name that is
// both somewhere is left as written.
// Second: an ACCESS / ASSIGN INLINE body is now the accessor's body —
// it used to be dropped into an auto-property reading default. The
// body goes through the inline text translator (as an INLINE method's
// does), which now maps a single-colon send (`::oProxy:member`); it
// does not rebase a 1-based subscript, so those stay in a method.
// #include "hbclass.ch"
public class Bin
{
    protected dynamic[] aItems = System.Array.Empty<dynamic>();
    public dynamic Peak { get { return this.Last(); } set { dynamic xNew = value; this.Pop(); this.Push( xNew ) ; } }
    
    public dynamic Len => this.Heft();
    public dynamic Push(dynamic xItem = default)
    {
        HbRuntime.AAdd(ref this.aItems, xItem);
        return xItem;
    }

    public dynamic Pop()
    {
        dynamic xLast = this.Last();
        HbRuntime.ASize(ref this.aItems, HbRuntime.Len(this.aItems) - 1);
        return xLast;
    }

    public dynamic Last()
    {
        return this.aItems[(long)(HbRuntime.Len(this.aItems)) - 1];
    }

    public decimal Heft()
    {
        return HbRuntime.Len(this.aItems);
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        // x: the receiver is dynamic
        Bin xBin = new Bin();
        xBin.Push("jar");
        xBin.Push("tin");
        HbRuntime.QOut("heft=" + HbRuntime.LTrim(HbRuntime.Str(xBin.Heft())) + " len=" + HbRuntime.LTrim(HbRuntime.Str(xBin.Len)));
        HbRuntime.QOut("top=" + xBin.Peak);
        xBin.Peak = "box";
        HbRuntime.QOut("top=" + xBin.Peak + " heft=" + HbRuntime.LTrim(HbRuntime.Str(xBin.Heft())));
        return;
    }
}
