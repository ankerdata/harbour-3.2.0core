#include "astype.ch"
// Test 39: initializers that need to survive expression reduction.
//
// Three separate fixes are exercised here:
//
//   a) LOCAL init with adjacent string literals ("ab" + "cd"). Before
//      the fix, hb_compExprGenStatement called HB_EA_REDUCE, which
//      rewrote the HB_EO_PLUS node in place and freed the original
//      pointer — the one the AST had already captured. Emitted .cs
//      came out with an empty init: `string cLocal = ;`.
//
//   b) LOCAL init with CHR() concatenation (CHR(16)+CHR(4)). Same
//      root cause — the reduction folds adjacent HB_ET_STRING results
//      and frees the parent.
//
//   c) Empty hash literal `{ => }` in a class DATA INIT slot. The
//      raw init text is passed through hb_csTranslateInit, which
//      now recognises `{ => }` and emits `new Dictionary<>()`.
//
// Real-code analogues: sharedx/transaction.prg (hash-backed class
// DATAs), easiposx/spoolprt.prg (DLE+EOT CHR-string LOCALs).

#include "hbclass.ch"

STATIC s_cStatic := Chr(27) + "02" + Chr(1) AS STRING

CLASS Bag

   DATA cHello AS STRING INIT "ab" + "cd"
   DATA hTable AS HASH INIT { => }

ENDCLASS

PROCEDURE Main()
   LOCAL cLocal := "ab" + "cd" AS STRING
   LOCAL cPayload := Chr(16) + Chr(4) AS STRING
   LOCAL hEmpty := {} AS HASH
   LOCAL oB := Bag():New() AS OBJECT

   QOut("local:  " + cLocal)
   QOut("llen:   " + Str(Len(cPayload), 4))
   QOut("static: " + Str(Len(s_cStatic), 4))
   QOut("class:  " + oB:cHello)
   QOut("hsize:  " + Str(Len(hEmpty), 4))
   QOut("hclas:  " + Str(Len(oB:hTable), 4))

RETURN
