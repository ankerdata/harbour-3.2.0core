//
// Test 143 companion header. The transpiler does not preload it: it reads
// it only to answer test143.prg's #ifdef questions, as Harbour's
// preprocessor would, its own conditionals included.
//

#ifndef TEST143_CH_
#define TEST143_CH_

#define T143_HEADER_FLAG

// the file's own define, made before the #include
#ifdef T143_BEFORE_INCLUDE
#define T143_SEEN_BEFORE
#endif

// a name only the compile's preprocessor knows
#ifdef __HARBOUR__
#define T143_UNDER_HARBOUR
#endif

#include "test143b.ch"

#endif /* TEST143_CH_ */
