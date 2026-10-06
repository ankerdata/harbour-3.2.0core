#include "astype.ch"
// Test 143: #ifdef, #ifndef and defined() answered as Harbour answers
// them. The transpiler's preprocessor registers none of the file's own
// #defines (it keeps them for the emitter) and opens no #include, so it
// used to take every name they define for undefined and keep the other
// branch. Each block prints the branch it took; Harbour's output is the
// reference.

#define T143_LOCAL
#define T143_LOCAL_VALUE  5
#define T143_BEFORE_INCLUDE

#include "test143.ch"

PROCEDURE Main()

   QOut("the file's own define: defined")

   QOut("the file's own define with a value: defined")

   QOut("a header's define: defined")

   QOut("the header saw the file's define before the #include")

   QOut("a header's define under its own #ifdef __HARBOUR__: defined")

   QOut("a define of a header the header includes: defined")

   QOut("defined(): both")

   QOut("after #undef: undefined")

   QOut("a name nothing defines: undefined")

RETURN
