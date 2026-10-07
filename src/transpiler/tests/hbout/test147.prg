#include "astype.ch"
// Test 147: a string literal is its bytes.
//
// A Harbour string is bytes, and a source may hold them in any encoding:
// EasiPOS has Windows-1252 (fmde.prg's TSE label, prtinit.prg's printer
// tables) beside UTF-8 (testsetup.prg's receipt layout). The emitter copied
// a literal's bytes into the .cs, which the C# compiler reads as UTF-8: a
// lone high byte became U+FFFD and UTF-8's three bytes one char, so lengths,
// truncation and what was written moved. Each byte is its own char now,
// \u00XX. The output is byte values, which no console encoding can change.

PROCEDURE Main()

   LOCAL cEuro147 := "€/kg = €" AS STRING
   LOCAL cUmlaut147 := "z�hler" AS STRING
   LOCAL cEsc147 := "@" AS STRING

   QOut("euro:", Len(cEuro147), Asc(cEuro147), Asc(SubStr(cEuro147, 2)), Asc(SubStr(cEuro147, 3)))
   QOut("cut to 9:", Len(Left(cEuro147, 9)), Asc(Right(Left(cEuro147, 9), 1)))
   QOut("umlaut:", Len(cUmlaut147), Asc(SubStr(cUmlaut147, 2)))
   QOut("escape:", Len(cEsc147), Asc(cEsc147))

RETURN
