using System;
using System.Collections.Generic;
using static HbRuntime;

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

        HbRuntime.QOUT("Foo:nA = " + HbRuntime.STR(oF.nA, 4));
        HbRuntime.QOUT("Bar:nB = " + HbRuntime.STR(oB.nB, 4));

        return;
    }
}
