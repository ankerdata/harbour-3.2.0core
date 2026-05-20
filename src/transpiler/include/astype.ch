/*
 * astype.ch — Stub out transpiler AS type annotations
 *
 * Include this in .hb files to allow them to compile with the
 * Harbour compiler. The AS TYPE annotations are used by the
 * transpiler for C# type inference but are not valid Harbour syntax
 * in all contexts (e.g. LOCAL x := 5 AS INTEGER).
 *
 * Usage: #include "astype.ch" at the top of each .hb file
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
