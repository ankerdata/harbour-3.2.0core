#include "astype.ch"
// Test 112: hb_ADel / hb_AIns with lAutoSize shrink and grow the array.
//
// Harbour's hb_ADel( a, n, .T. ) deletes and shortens, hb_AIns( a, n, x,
// .T. ) lengthens and inserts. A C# array cannot change length in place,
// so the resizing overloads take the array by ref, as AAdd and ASize
// do — but the emitter passed `ref` to AAdd and ASize only, so every
// EasiPOS call (all pass .T.: the sale buffer's line delete and insert)
// left the array its old length: a NIL at the end after a delete, the
// last line lost after an insert. The scan's list of array mutators
// grows with it, so a routine that resizes its array parameter keeps
// that parameter `ref`.
#include "hbclass.ch"

CLASS SaleBuffer

   DATA aLines AS ARRAY INIT {}
   DATA xQueue AS USUAL

ENDCLASS

PROCEDURE Main()

   LOCAL aNums := {1, 2, 3, 4} AS ARRAY
   LOCAL oBuf := SaleBuffer():New() AS OBJECT

   hb_ADel(aNums, 2, .T.)
   QOut(Len(aNums), aNums[2])
   hb_AIns(aNums, 1, 9, .T.)
   QOut(Len(aNums), aNums[1], aNums[4])
   hb_ADel(aNums, 1)
   QOut(Len(aNums), ValType(aNums[4]))

   oBuf:aLines := {"a", "b", "c"}
   hb_AIns(oBuf:aLines, 2, "x", .T.)
   QOut(Len(oBuf:aLines), oBuf:aLines[2], oBuf:aLines[4])
   hb_ADel(oBuf:aLines, 1, .T.)
   QOut(Len(oBuf:aLines), oBuf:aLines[1])

   oBuf:xQueue := {10, 20}
   hb_ADel(oBuf:xQueue, 1, .T.)
   QOut(Len(oBuf:xQueue), oBuf:xQueue[1])
   hb_AIns(oBuf:xQueue, 2, 30, .T.)
   QOut(Len(oBuf:xQueue), oBuf:xQueue[2])

   aNums := {5, 6, 7}
   BufferTrim(@aNums)
   QOut(Len(aNums), aNums[1])

RETURN

PROCEDURE BufferTrim( /*@*/aList AS ARRAY )
   hb_ADel(aList, 1, .T.)
RETURN
