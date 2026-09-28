using System;
using static HbRuntime;
using static Program;

// Test 131 (multi-file pair): the subclass, in a file of its own, sends its
// parent's member and methods through Self (test131a.prg says why).

// #include "hbclass.ch"
public class Child131 : Base131
{

    public virtual dynamic Run131()
    {
        Bump131();
        Bump131(2);
        nCount131 += 10;
        return Label131();
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        HbRuntime.QOut(new Child131().Run131());
        return;
    }
}
