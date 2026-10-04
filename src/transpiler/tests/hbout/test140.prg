#include "astype.ch"
// Test 140: a write through a typed member into a whole-number field.
//
// A member named for a class (`VAR oGauge140`, no AS) is declared as that
// class in C#, so every send through it is checked. A Harbour number
// written into one of its INTEGER members (`::oGauge140:nLevel :=
// nAmount`) needs the (long) a write through a local gets: C# will not
// narrow a decimal on its own. The emitter's check read only a variable
// receiver, so a member receiver got none and the C# was CS0266
// (ormtestsuite.prg's `::oTestOrmTable:nTestNo := nOrigTestNo`, once the
// member was named for its table). The receiver probe also reads such a
// member as its class now, so a division of the whole-number member
// keeps its decimal.

#include "hbclass.ch"

CLASS Gauge140

   DATA nLevel AS INTEGER INIT 0

ENDCLASS

CLASS Panel140

   DATA oGauge140 AS OBJECT
   METHOD New()
   METHOD FillGauge140( nAmount )
   METHOD HalfLevel140()

ENDCLASS

METHOD New() AS OBJECT CLASS Panel140
   ::oGauge140 := Gauge140():New()
RETURN Self

METHOD FillGauge140( nAmount AS NUMERIC ) AS OBJECT CLASS Panel140
   ::oGauge140:nLevel := nAmount
RETURN Self

METHOD HalfLevel140() AS NUMERIC CLASS Panel140
RETURN ::oGauge140:nLevel / 2

PROCEDURE Main()
   LOCAL oPanel := Panel140():New() AS OBJECT
   LOCAL nTotal := 14 AS NUMERIC
   LOCAL nAmount := nTotal / 2 AS NUMERIC

   oPanel:FillGauge140(nAmount)
   QOut(Str(oPanel:oGauge140:nLevel, 4))
   QOut(Str(oPanel:HalfLevel140(), 6, 2))
RETURN
