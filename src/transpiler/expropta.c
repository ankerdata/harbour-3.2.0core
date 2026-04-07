/* hbexpra.c is also included from ../macro/macro.c
 * However it produces a slightly different code if used in
 * macro compiler (there is an additional parameter passed to some functions)
 *
 * Transpiler version: defines HB_TRANSPILER to enable AST hooks
 */

#ifndef HB_TRANSPILER
#define HB_TRANSPILER
#endif
#include "hbast.h"
#include "hbexpra.c"
