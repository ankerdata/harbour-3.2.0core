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
   VAR nLevel AS INTEGER INIT 0
ENDCLASS

CLASS Panel140
   VAR oGauge140
   METHOD New() CONSTRUCTOR
   METHOD FillGauge140( nAmount )
   METHOD HalfLevel140()
ENDCLASS

METHOD New() CLASS Panel140
   ::oGauge140 := Gauge140():New()
   RETURN Self

METHOD FillGauge140( nAmount ) CLASS Panel140
   ::oGauge140:nLevel := nAmount
   RETURN Self

METHOD HalfLevel140() CLASS Panel140
   RETURN ::oGauge140:nLevel / 2

PROCEDURE Main()
   LOCAL oPanel := Panel140():New()
   LOCAL nTotal := 14
   LOCAL nAmount := nTotal / 2

   oPanel:FillGauge140( nAmount )
   ? Str( oPanel:oGauge140:nLevel, 4 )
   ? Str( oPanel:HalfLevel140(), 6, 2 )
   RETURN
