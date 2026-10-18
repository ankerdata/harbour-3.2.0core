#include "astype.ch"
// Test 157: a PUBLIC named after a class is that class (plan C13).
//
// `PUBLIC oGauge157 := Gauge157():New()` with CLASS Gauge157: the field on
// the owning file's Program partial is `public static Gauge157 oGauge157;`,
// so a reader in another routine (Show157) types `oGauge157:nLevel`
// statically, as a local so named would be. A PUBLIC whose name says
// nothing stays `dynamic` (xBag157), and the sized form `List<dynamic>`
// (aSlots157): a PUBLIC's value is whatever its writers put there, and only
// the name is evidence. Until 2026-10-18 every PUBLIC was dynamic; EasiPOS's
// hardware settings, the array aHWFlag, became the class HWFlags held by
// the PUBLIC oHWFlags, read 1,300 times across the products.

#include "hbclass.ch"

MEMVAR oGauge157
MEMVAR xBag157
MEMVAR aSlots157

CLASS Gauge157


   DATA nLevel AS NUMERIC INIT 0
   DATA cUnit AS STRING INIT "mm"

ENDCLASS

PROCEDURE Main()

   PUBLIC oGauge157 := Gauge157():New() AS OBJECT
   PUBLIC xBag157 := "anything" AS STRING
   PUBLIC aSlots157[3] AS ARRAY

   oGauge157:nLevel := 42
   aSlots157[1] := "x"
   Show157()

RETURN

PROCEDURE Show157()

   QOut(oGauge157:nLevel, oGauge157:cUnit, xBag157, Len(aSlots157))

RETURN
