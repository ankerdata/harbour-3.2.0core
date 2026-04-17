#include "astype.ch"
// Test 42: dynamic member access via obj:&(name).
//
// Harbour's `::&(cMemberName)` reads/writes a property whose name is
// only known at runtime. The transpiler emits HbRuntime.GETMEMBER /
// SETMEMBER / SENDMSG calls backed by reflection. This test builds
// a small class with typed DATAs, then exercises:
//   a) read via GETMEMBER
//   b) write via SETMEMBER
//   c) iteration over a list of member names (the real-world ORM
//      pattern from sharedx/ormsql.prg)
//
// The class mirrors the shape of POSClass-derived objects in easipos
// where a list of field names drives get/set loops.

#include "hbclass.ch"

CLASS Record

   DATA cName AS STRING INIT ""
   DATA nValue AS NUMERIC INIT 0
   DATA lActive AS LOGICAL INIT .F.

   METHOD SetByName( cField, xValue )
   METHOD GetByName( cField )
   METHOD DumpFields( aFields )

ENDCLASS

METHOD SetByName( cField AS STRING, xValue AS USUAL ) AS OBJECT CLASS Record
   ::&(cField) := xValue
RETURN Self

METHOD GetByName( cField AS STRING ) CLASS Record
RETURN ::&(cField)

METHOD DumpFields( aFields AS ARRAY ) CLASS Record
   LOCAL i AS NUMERIC
   LOCAL xVal AS USUAL
   LOCAL cType AS STRING
   FOR i := 1 TO Len(aFields)
      xVal := ::&(aFields[i])
      cType := ValType(xVal)
      IF cType == "C"
         QOut(aFields[i] + ": " + xVal)
      ELSEIF cType == "N"
         QOut(aFields[i] + ": " + Str(xVal, 4))
      ELSEIF cType == "L"
         QOut(aFields[i] + ": " + IIF(xVal, "T", "F"))
      ELSE
         QOut(aFields[i] + ": ?")
      ENDIF
   NEXT

RETURN NIL

PROCEDURE Main()
   LOCAL oRec := Record():New() AS OBJECT
   LOCAL aFields := {"cName", "nValue", "lActive"} AS ARRAY

   // Direct property access — baseline
   oRec:cName := "hello"
   oRec:nValue := 42
   oRec:lActive := .T.
   QOut("direct: " + oRec:cName + ", " + Str(oRec:nValue, 4) + ", " + IIF(oRec:lActive, "T", "F"))

   // Write via dynamic member (SETMEMBER)
   oRec:SetByName("cName", "world")
   oRec:SetByName("nValue", 99)
   oRec:SetByName("lActive", .F.)

   // Read via dynamic member (GETMEMBER)
   QOut("dynamic: " + oRec:GetByName("cName") + ", " + Str(oRec:GetByName("nValue"), 4) + ", " + IIF(oRec:GetByName("lActive"), "T", "F"))

   // Iterate fields by name (DumpFields uses GETMEMBER in a loop)
   oRec:DumpFields(aFields)

RETURN
