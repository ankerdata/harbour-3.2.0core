using System;
using static HbRuntime;
using static Program;

// Test 7: #define constants, #include directives
// #include "hbclass.ch"
// #include "common.ch"
public static partial class Program
{
    public static void Main(string[] args)
    {
        string cVersion = Test7PrgConst.APP_VERSION;
        HbRuntime.QOut("cVersion=" + cVersion);

        return;
    }
}
