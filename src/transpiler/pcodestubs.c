/*
 * Harbour Transpiler - PCODE stub implementations
 *
 * Lightweight replacements for PCODE generation. The grammar and hbmain.c
 * still emit PCODE during parsing (jump tracking, NOOP filling, etc.).
 * We allocate the pCode buffer and write bytes into it so that all existing
 * PCODE-accessing code remains functional, but the generated PCODE is
 * never optimized or output — it's discarded at cleanup.
 *
 * This replaces hbpcode.c and the 5 optimization files (hbopt.c, hbdead.c,
 * hbfix.c, hbstripl.c, hblbl.c).
 *
 * Copyright 2026 harbour.github.io
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 */

#include "hbcomp.h"

/* Keep the full PCODE generation — the grammar and hbmain.c access the
   pCode buffer directly for jump patching, NOOP filling, etc. The only
   difference from hbpcode.c is that we don't include the optimization
   infrastructure. */

#define HB_PCODE_CHUNK  512

void hb_compGenPCode1( HB_BYTE byte, HB_COMP_DECL )
{
   PHB_HFUNC pFunc = HB_COMP_PARAM->functions.pLast;
   if( ! pFunc->pCode )
   {
      pFunc->pCode      = ( HB_BYTE * ) hb_xgrab( HB_PCODE_CHUNK );
      pFunc->nPCodeSize = HB_PCODE_CHUNK;
      pFunc->nPCodePos  = 0;
   }
   else if( ( pFunc->nPCodeSize - pFunc->nPCodePos ) < 1 )
      pFunc->pCode = ( HB_BYTE * ) hb_xrealloc( pFunc->pCode, pFunc->nPCodeSize += HB_PCODE_CHUNK );
   pFunc->pCode[ pFunc->nPCodePos++ ] = byte;
}

void hb_compGenPCode2( HB_BYTE byte1, HB_BYTE byte2, HB_COMP_DECL )
{
   PHB_HFUNC pFunc = HB_COMP_PARAM->functions.pLast;
   if( ! pFunc->pCode )
   {
      pFunc->pCode      = ( HB_BYTE * ) hb_xgrab( HB_PCODE_CHUNK );
      pFunc->nPCodeSize = HB_PCODE_CHUNK;
      pFunc->nPCodePos  = 0;
   }
   else if( ( pFunc->nPCodeSize - pFunc->nPCodePos ) < 2 )
      pFunc->pCode = ( HB_BYTE * ) hb_xrealloc( pFunc->pCode, pFunc->nPCodeSize += HB_PCODE_CHUNK );
   pFunc->pCode[ pFunc->nPCodePos++ ] = byte1;
   pFunc->pCode[ pFunc->nPCodePos++ ] = byte2;
}

void hb_compGenPCode3( HB_BYTE byte1, HB_BYTE byte2, HB_BYTE byte3, HB_COMP_DECL )
{
   PHB_HFUNC pFunc = HB_COMP_PARAM->functions.pLast;
   if( ! pFunc->pCode )
   {
      pFunc->pCode      = ( HB_BYTE * ) hb_xgrab( HB_PCODE_CHUNK );
      pFunc->nPCodeSize = HB_PCODE_CHUNK;
      pFunc->nPCodePos  = 0;
   }
   else if( ( pFunc->nPCodeSize - pFunc->nPCodePos ) < 3 )
      pFunc->pCode = ( HB_BYTE * ) hb_xrealloc( pFunc->pCode, pFunc->nPCodeSize += HB_PCODE_CHUNK );
   pFunc->pCode[ pFunc->nPCodePos++ ] = byte1;
   pFunc->pCode[ pFunc->nPCodePos++ ] = byte2;
   pFunc->pCode[ pFunc->nPCodePos++ ] = byte3;
}

void hb_compGenPCode4( HB_BYTE byte1, HB_BYTE byte2, HB_BYTE byte3, HB_BYTE byte4, HB_COMP_DECL )
{
   PHB_HFUNC pFunc = HB_COMP_PARAM->functions.pLast;
   if( ! pFunc->pCode )
   {
      pFunc->pCode      = ( HB_BYTE * ) hb_xgrab( HB_PCODE_CHUNK );
      pFunc->nPCodeSize = HB_PCODE_CHUNK;
      pFunc->nPCodePos  = 0;
   }
   else if( ( pFunc->nPCodeSize - pFunc->nPCodePos ) < 4 )
      pFunc->pCode = ( HB_BYTE * ) hb_xrealloc( pFunc->pCode, pFunc->nPCodeSize += HB_PCODE_CHUNK );
   pFunc->pCode[ pFunc->nPCodePos++ ] = byte1;
   pFunc->pCode[ pFunc->nPCodePos++ ] = byte2;
   pFunc->pCode[ pFunc->nPCodePos++ ] = byte3;
   pFunc->pCode[ pFunc->nPCodePos++ ] = byte4;
}

void hb_compGenPCodeN( const HB_BYTE * pBuffer, HB_SIZE nSize, HB_COMP_DECL )
{
   PHB_HFUNC pFunc = HB_COMP_PARAM->functions.pLast;
   if( ! pFunc->pCode )
   {
      pFunc->nPCodeSize = ( ( nSize / HB_PCODE_CHUNK ) + 1 ) * HB_PCODE_CHUNK;
      pFunc->pCode      = ( HB_BYTE * ) hb_xgrab( pFunc->nPCodeSize );
      pFunc->nPCodePos  = 0;
   }
   else if( pFunc->nPCodeSize - pFunc->nPCodePos < nSize )
   {
      pFunc->nPCodeSize += ( ( ( nSize - ( pFunc->nPCodeSize - pFunc->nPCodePos ) ) / HB_PCODE_CHUNK ) + 1 ) * HB_PCODE_CHUNK;
      pFunc->pCode = ( HB_BYTE * ) hb_xrealloc( pFunc->pCode, pFunc->nPCodeSize );
   }
   memcpy( pFunc->pCode + pFunc->nPCodePos, pBuffer, nSize );
   pFunc->nPCodePos += nSize;
}

/* Stub for hb_compPCodeSize — called from hbdbginf.c.
   Returns 0 since there's no real PCODE buffer to measure. */
HB_ISIZ hb_compPCodeSize( PHB_HFUNC pFunc, HB_SIZE nOffset )
{
   HB_SYMBOL_UNUSED( pFunc );
   HB_SYMBOL_UNUSED( nOffset );
   return 0;
}

/* Stub for hb_compPCodeEval — walks PCODE buffer calling handler functions.
   No-op since there's no PCODE buffer. */
void hb_compPCodeEval( PHB_HFUNC pFunc, const PHB_PCODE_FUNC * pFunctions, void * cargo )
{
   HB_SYMBOL_UNUSED( pFunc );
   HB_SYMBOL_UNUSED( pFunctions );
   HB_SYMBOL_UNUSED( cargo );
}

/* Stub for hb_compPCodeTrace — similar to PCodeEval but for tracing.
   No-op since there's no PCODE buffer. */
void hb_compPCodeTrace( PHB_HFUNC pFunc, const PHB_PCODE_FUNC * pFunctions, void * cargo )
{
   HB_SYMBOL_UNUSED( pFunc );
   HB_SYMBOL_UNUSED( pFunctions );
   HB_SYMBOL_UNUSED( cargo );
}
