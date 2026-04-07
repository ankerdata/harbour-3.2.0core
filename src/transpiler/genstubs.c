/*
 * Stub implementations for removed code generators
 * These satisfy the linker for functions still referenced by hbmain.c
 */

#include "hbcomp.h"

void hb_compGenCCode( HB_COMP_DECL, PHB_FNAME pFileName )
{
   HB_SYMBOL_UNUSED( HB_COMP_PARAM );
   HB_SYMBOL_UNUSED( pFileName );
}

void hb_compGenPortObj( HB_COMP_DECL, PHB_FNAME pFileName )
{
   HB_SYMBOL_UNUSED( HB_COMP_PARAM );
   HB_SYMBOL_UNUSED( pFileName );
}

void hb_compGenBufPortObj( HB_COMP_DECL, HB_BYTE ** pBufPtr, HB_SIZE * pnSize )
{
   HB_SYMBOL_UNUSED( HB_COMP_PARAM );
   HB_SYMBOL_UNUSED( pBufPtr );
   HB_SYMBOL_UNUSED( pnSize );
}
