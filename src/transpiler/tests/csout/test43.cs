using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 43: multiple classes in one .prg file.

// #include "hbclass.ch"
public class Foo
{
    public decimal nA { get; set; } = 1;

}

public class Bar
{
    public decimal nB { get; set; } = 2;

}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Foo oF = new Foo();
        Bar oB = new Bar();

        HbRuntime.QOut("Foo:nA = " + HbRuntime.Str(oF.nA, 4));
        HbRuntime.QOut("Bar:nB = " + HbRuntime.Str(oB.nB, 4));

        return;
    }
}
