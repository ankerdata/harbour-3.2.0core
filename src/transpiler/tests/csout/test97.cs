// Test 97: MESSAGE aliases, bare method sends, INLINE method rows.
//
// `MESSAGE Len METHOD StackLen` (hbclass.ch) declares Len as another
// name for StackLen. The class parser turned that line into nothing,
// so `oQueue:Len` was CS1061 on a typed receiver and a runtime miss on
// a dynamic one (easiutil's Queue and Stack, five call sites). The
// parser now reads it as `METHOD Len INLINE ::StackLen()`, with or
// without the parentheses hbclass.ch allows on either name, and an
// INLINE method gets a reftab row like any other, so a cross-file send
// resolves it. A method sent WITHOUT parentheses — `oShelf:Len`, legal
// Harbour — is emitted as the call it is. (An ACCESS is a property and
// gets no parentheses; its body is a known gap — see gencsharp.c — so
// none is exercised here.)
public class Shelf : IHbObject
{
    protected List<dynamic> aItems = new List<dynamic>();

    public dynamic Len => StackLen();
    public dynamic Size => StackLen();
    public dynamic Init() { aItems = new List<dynamic>(); return this ; }
    public virtual dynamic Stow(dynamic xItem = default)
    {
        AAdd(aItems, xItem);
        return xItem;
    }

    public virtual decimal StackLen()
    {
        return HbRuntime.Len(aItems);
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Shelf oShelf = Initialised(new Shelf(), self => self.Init());
        oShelf.Stow("jar");
        oShelf.Stow("tin");
        QOut("len=" + LTrim(Str(oShelf.Len)) + " size=" + LTrim(Str(oShelf.Size)));
        QOut("bare=" + LTrim(Str(oShelf.StackLen())));
    }
}
