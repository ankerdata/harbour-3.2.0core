#include "astype.ch"
// Test 104: W0018 — the extra-argument check — in the scan walk.
//
// Harbour drops arguments beyond the declaration silently; C# refuses
// them (CS1501). The check lived in the -GS emitter only, so the -GF
// scan, which is the pipeline's gate, never saw it: a merge brought a
// four-argument call to the three-parameter MakeSQLiteIndexes, the
// scan passed clean and only gen caught it. The walk visits every
// call site with the callee key resolved anyway, so the count compare
// now happens there, in both modes: `Tariff( 10, 2, 99 )` warns
// against the free function, `oT:Post( 5, "extra" )` against a method
// Treasurer only inherits from Bursar, found through the parent link.
// The emitter still drops the extras, so both programs run and agree;
// the warnings are on stderr, not in the output.
//
// The drop counts against the row the call resolves to, and a
// file-static function shadowing a public one elsewhere has its own
// row: `nLevy` here is STATIC with three parameters while test100's
// public `nLevy` takes one — the deliberate collision. Counting
// against the bare name once dropped 23 of rmio2700's 27 RoomCharge
// arguments (the public RoomCharge takes four).
#include "hbclass.ch"

CLASS Bursar

   DATA nTotal AS NUMERIC INIT 0
   METHOD Post( nAmount )

ENDCLASS

CLASS Treasurer INHERIT Bursar

   DATA cName AS STRING INIT "t"

ENDCLASS

STATIC FUNCTION nLevy( nA AS NUMERIC, nB AS NUMERIC, nC AS NUMERIC ) AS NUMERIC
RETURN nA + nB + nC

FUNCTION Tariff( nBase AS NUMERIC, nRate AS NUMERIC ) AS NUMERIC
RETURN nBase * nRate

METHOD Post( nAmount AS NUMERIC ) AS OBJECT CLASS Bursar
   ::nTotal += nAmount
RETURN Self

PROCEDURE Main()
   LOCAL oT := Treasurer():New() AS OBJECT
   QOut("tariff=" + LTrim(Str(Tariff(10, 2, 99))))
   oT:Post(5, "extra")
   QOut("total=" + LTrim(Str(oT:nTotal)))
   QOut("plain=" + LTrim(Str(Tariff(3, 4))))
   QOut("levy=" + LTrim(Str(nLevy(1, 2, 3))))
RETURN
