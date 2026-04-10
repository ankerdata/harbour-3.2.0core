using System;

// Test 22a + 22b: two classes with the same method name but
// incompatible signatures, defined in separate files because the
// Harbour compiler treats METHOD bodies as globally-named functions
// and refuses to redefine `Add` in a single file.
//
// Demonstrates that hbreftab.tab keys methods by (Class, Method) so
// the two definitions stay separate. Without method-keyed entries,
// both `Numbers:Add` and `Names:Add` would share a single `add` row
// and the scanner would emit a "downgrading to USUAL" warning. With
// method-keyed entries, the two never collide:
//
//   numbers::add  -  -  1  nValue:NUMERIC:-
//   names::add    -  -  1  cValue:STRING:-
//
// and the C# emits two strongly-typed methods on two distinct classes.
// #include "hbclass.ch"
public class Numbers
{
    public decimal nTotal { get; set; } = 0;

    public object New()
    {
        this.nTotal = 0;
        return this;
    }

    public object Add(decimal nValue = default)
    {
        this.nTotal = this.nTotal + nValue;
        return this;
    }

    public object Show()
    {
        HbRuntime.QOut("total=" + HbRuntime.Str(this.nTotal));
        return this;
    }
}

