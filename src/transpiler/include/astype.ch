/*
 * astype.ch — Stub out transpiler AS type annotations
 *
 * A test includes this so that Harbour compiles its declarations. The
 * AS TYPE annotations are used by the transpiler for C# type inference
 * but are not valid Harbour syntax in all contexts (e.g. LOCAL x := 5
 * AS INTEGER).
 *
 * Usage: #include "../include/astype.ch" at the top of the test
 */

#xtranslate AS INTEGER   =>
#xtranslate AS DECIMAL   =>
#xtranslate AS STRING    =>
#xtranslate AS LOGICAL   =>
#xtranslate AS NUMERIC   =>
#xtranslate AS ARRAY     =>
#xtranslate AS HASH      =>
#xtranslate AS BLOCK     =>
#xtranslate AS OBJECT    =>
#xtranslate AS DATE      =>
#xtranslate AS TIMESTAMP =>
#xtranslate AS USUAL     =>

/* Declared classes and element types (`LOCAL o AS CLASS X`, `VAR a AS
   ARRAY OF CLASS X`, `FUNCTION f() AS ARRAY OF CHARACTER`), removed as
   hbclass.ch's HB_CLS_NO_DECLARATIONS removes them. Harbour tries the
   most recently defined rule first, so the longer forms come last. */
#xtranslate AS CHARACTER          =>
#xtranslate AS CLASS <!name!>      =>
#xtranslate AS ARRAY OF <!type!>   =>
#xtranslate AS ARRAY OF CLASS <!name!> =>

/* Records and fixed arrays (plan C12): `RECORD X( f1, f2 AS … )` declares
   an array's positions, `AS RECORD X` holds one, `AS FIXED ARRAY OF …` an
   array that never grows or shrinks. Arrays to Harbour: the declarations
   go, as the others do, and the RECORD line with them. */
#xtranslate AS RECORD <!name!>               =>
#xtranslate AS ARRAY OF RECORD <!name!>      =>
#xtranslate AS FIXED ARRAY OF <!type!>       =>
#xtranslate AS FIXED ARRAY OF RECORD <!name!> =>
#xcommand RECORD <*rest*> =>

/* Typed hashes: `AS INTEGER HASH`, `AS STRING HASH`, `AS HASH OF …`, keyed
   and valued for the transpiler, a hash to Harbour. Longest forms last. */
#xtranslate AS <k: INTEGER, STRING> HASH                        =>
#xtranslate AS <k: INTEGER, STRING> HASH OF <!type!>            =>
#xtranslate AS <k: INTEGER, STRING> HASH OF <c: CLASS, RECORD> <!name!> =>
#xtranslate AS HASH OF <!type!>                                  =>
#xtranslate AS HASH OF <c: CLASS, RECORD> <!name!>               =>
#xtranslate AS HASH OF ARRAY OF <!type!>                         =>
#xtranslate AS HASH OF ARRAY OF <c: CLASS, RECORD> <!name!>      =>
#xtranslate AS HASH OF FIXED ARRAY OF <!type!>                   =>
#xtranslate AS HASH OF FIXED ARRAY OF <c: CLASS, RECORD> <!name!> =>
