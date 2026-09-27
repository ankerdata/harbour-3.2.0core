using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 130: inside a method, a call to a function that shares a member's
// name is the function.
//
// Harbour's `name( ... )` is always a function call; a method is reached
// only by a send. C# binds an unqualified call inside a class to the member
// first, so easipos dialog.prg's `method EnableScanners()`, whose body calls
// scanio.prg's procedure EnableScanners(), called itself until the stack
// overflowed. The call is emitted `Program.EnableScanners()`, and so is one
// from a subclass, where the inherited method would take it.

// #include "hbclass.ch"
public class Panel130
{
    public bool lOn130 = true;

    public virtual Panel130 Wake130()
    {
        if (lOn130)
        {
            Program.Wake130();
        }

        return this;
    }
}

public class Door130 : Panel130
{

    public virtual Door130 Open130()
    {
        Program.Wake130();
        return this;
    }
}

public static partial class Program
{
    public static decimal test130_snCalls130 = 0;
    public static void Wake130()
    {
        test130_snCalls130++;
        return;
    }

    public static void Main(string[] args)
    {
        new Panel130().Wake130();
        new Door130().Open130();
        HbRuntime.QOut(test130_snCalls130);
        return;
    }
}
