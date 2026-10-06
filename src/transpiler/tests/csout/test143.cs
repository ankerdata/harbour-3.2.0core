using System;
using static HbRuntime;
using static Program;

// Test 143: #ifdef, #ifndef and defined() answered as Harbour answers
// them. The transpiler's preprocessor registers none of the file's own
// #defines (it keeps them for the emitter) and opens no #include, so it
// used to take every name they define for undefined and keep the other
// branch. Each block prints the branch it took; Harbour's output is the
// reference.

// #include "test143.ch"
public static partial class Program
{
    // #define T143_LOCAL
    // #define T143_BEFORE_INCLUDE
    public static void Main(string[] args)
    {
        HbRuntime.QOut("the file's own define: defined");

        HbRuntime.QOut("the file's own define with a value: defined");

        HbRuntime.QOut("a header's define: defined");

        HbRuntime.QOut("the header saw the file's define before the #include");

        HbRuntime.QOut("a header's define under its own #ifdef __HARBOUR__: defined");

        HbRuntime.QOut("a define of a header the header includes: defined");

        HbRuntime.QOut("defined(): both");

        HbRuntime.QOut("after #undef: undefined");

        HbRuntime.QOut("a name nothing defines: undefined");

        return;
    }
}
