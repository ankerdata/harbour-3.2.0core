/*
 * Harbour Transpiler - Function-table scan mode (-GF)
 *
 * This is the entry point invoked from hb_compGenOutput when the user
 * passes -GF on the command line. Instead of generating code, it
 * walks the parsed AST and accumulates information about every
 * function/procedure defined in this source file plus every by-ref
 * call site, into HB_REFTAB_PATH.
 *
 * The table is loaded before scanning and saved afterwards so multiple
 * invocations of -GF over different files accumulate into one table.
 * This is the whole-codebase pre-scan step that the C# emitter relies
 * on for correct cross-file by-ref handling and named-argument call
 * rewriting.
 *
 * Copyright 2026 harbour.github.io
 */

#include "hbcomp.h"
#include "hbast.h"
#include "hbreftab.h"

void hb_compGenScan( HB_COMP_DECL, PHB_FNAME pFileName )
{
   PHB_REFTAB   pTab;
   const char * szPath = hb_refTabGetPath();

   HB_SYMBOL_UNUSED( pFileName );

   pTab = hb_refTabNew();

   /* Merge with whatever's already on disk so multiple -GF invocations
      accumulate. Failure to load is fine — first run starts empty. */
   hb_refTabLoad( pTab, szPath );

   /* Scan this file's AST and add its functions / by-ref usage. */
   hb_refTabCollect( pTab, HB_COMP_PARAM );

   /* Persist. */
   if( ! hb_refTabSave( pTab, szPath ) )
   {
      char buffer[ 256 ];
      hb_snprintf( buffer, sizeof( buffer ),
                   "hbtranspiler: warning: cannot write %s\n", szPath );
      hb_compOutStd( HB_COMP_PARAM, buffer );
   }
   else if( ! HB_COMP_PARAM->fQuiet )
   {
      char buffer[ 256 ];
      hb_snprintf( buffer, sizeof( buffer ),
                   "Scanned '%s' into %s\n",
                   HB_COMP_PARAM->szFile, szPath );
      hb_compOutStd( HB_COMP_PARAM, buffer );
   }

   hb_refTabFree( pTab );
}
