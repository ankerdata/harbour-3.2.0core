// Test 153: a write through `::` into an inherited AS INTEGER member is
// coerced like one through a typed receiver.
//
// A member declared AS INTEGER is a C# long, and a decimal written into
// it needs a `(long)` cast. The emitter gave one through a typed
// receiver (`oLine:nType := nValue`), walking the INHERIT chain for the
// member's declaration, but through Self it looked at the current
// class's own declarations only: TextTranLine's `::nType := nType`,
// nType declared by TranLine, compiled only while the value was a long
// constant and was CS0266 as soon as it was a decimal parameter
// (EasiPOS, plan C4's first pass). The Self rule now walks the chain
// through the reftab, as the receiver rule does.
public class Base153 : IHbObject
{
    public long nKind = 0;
    public long nLevel = 0;

}

public class Leaf153 : Base153
{

    public virtual Leaf153 New(decimal nKind = 7)
    {
        // a decimal parameter into the parent's long
        this.nKind = (long)(nKind);
        return this;
    }

    public virtual long Deeper(decimal nBy = default)
    {
        // a compound write, the same coercion
        nLevel += (long)(nBy);
        nLevel = (long)(nLevel * 2 + nBy / 1);
        return nLevel;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Leaf153 oLeaf = new Leaf153().New(3);
        long nValue = 5;

        QOut(oLeaf.nKind);
        // through the typed receiver, as before
        oLeaf.nKind = nValue;
        QOut(oLeaf.nKind);
        QOut(oLeaf.Deeper(2), oLeaf.Deeper(1));
    }
}
