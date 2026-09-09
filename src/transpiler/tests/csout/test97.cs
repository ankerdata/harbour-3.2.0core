using System;
using static HbRuntime;
using static Program;

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
// #include "hbclass.ch"
public class Shelf
{
    protected dynamic[] aItems = System.Array.Empty<dynamic>();

    public dynamic Len => this.StackLen();
    public dynamic Size => this.StackLen();
    public dynamic Init() { this.aItems = System.Array.Empty<dynamic>(); return this ; }
    public dynamic Stow(dynamic xItem = default)
    {
        HbRuntime.AAdd(ref this.aItems, xItem);
        return xItem;
    }

    public decimal StackLen()
    {
        return HbRuntime.Len(this.aItems);
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Shelf oShelf = (Shelf)new Shelf().Init();
        oShelf.Stow("jar");
        oShelf.Stow("tin");
        HbRuntime.QOut("len=" + HbRuntime.LTrim(HbRuntime.Str(oShelf.Len)) + " size=" + HbRuntime.LTrim(HbRuntime.Str(oShelf.Size)));
        HbRuntime.QOut("bare=" + HbRuntime.LTrim(HbRuntime.Str(oShelf.StackLen())));
        return;
    }
}
