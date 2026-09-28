#include "astype.ch"
// Test 132: a method's return type reaches its caller through a send.
//
// The inference read only member rows (`Class::member`) for a send, never a
// method's row (`Class::Class__Method`), so `RETURN oObj:Method()` and
// `x := oObj:Method()` learned nothing of what the method returns, where a
// call to a function passes its return type on. Read132() now returns
// string and its local is decimal.

#include "hbclass.ch"

CLASS Meter132

   DATA nReading132 AS NUMERIC INIT 41
   METHOD Reading132()
   METHOD Label132()

ENDCLASS

METHOD Reading132() AS NUMERIC CLASS Meter132
RETURN ::nReading132 + 1

METHOD Label132() AS STRING CLASS Meter132
RETURN "meter"

FUNCTION Read132( oMeter132 AS OBJECT )
   LOCAL xReading := oMeter132:Reading132() AS NUMERIC
RETURN oMeter132:Label132() + " " + hb_ntos(xReading)

PROCEDURE Main()
   QOut(Read132(Meter132():New()))
RETURN
