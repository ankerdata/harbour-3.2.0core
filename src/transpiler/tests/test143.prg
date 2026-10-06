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

#ifdef T143_LOCAL
   ? "the file's own define: defined"
#else
   ? "the file's own define: undefined"
#endif

#ifndef T143_LOCAL_VALUE
   ? "the file's own define with a value: undefined"
#else
   ? "the file's own define with a value: defined"
#endif

#ifdef T143_HEADER_FLAG
   ? "a header's define: defined"
#else
   ? "a header's define: undefined"
#endif

#ifdef T143_SEEN_BEFORE
   ? "the header saw the file's define before the #include"
#else
   ? "the header did not see the file's define before the #include"
#endif

#ifdef T143_UNDER_HARBOUR
   ? "a header's define under its own #ifdef __HARBOUR__: defined"
#else
   ? "a header's define under its own #ifdef __HARBOUR__: undefined"
#endif

#ifdef T143_NESTED
   ? "a define of a header the header includes: defined"
#else
   ? "a define of a header the header includes: undefined"
#endif

#if defined( T143_LOCAL ) .and. defined( T143_HEADER_FLAG )
   ? "defined(): both"
#else
   ? "defined(): not both"
#endif

#undef T143_LOCAL
#ifdef T143_LOCAL
   ? "after #undef: defined"
#else
   ? "after #undef: undefined"
#endif

#ifdef T143_NOWHERE
   ? "a name nothing defines: defined"
#else
   ? "a name nothing defines: undefined"
#endif

RETURN
