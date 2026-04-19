using System;
using static HbRuntime;
using static Program;

// Test 7: #define constants, #include directives
// #include "hbclass.ch"
// #include "common.ch"
public static partial class Program
{
    const string APP_VERSION = @"1.0";
    const decimal MAX_ITEMS = 100;
    public static void Main(string[] args)
    {
        string cVersion = APP_VERSION;
        HbRuntime.QOut("cVersion=" + cVersion);

        return;
    }
}
