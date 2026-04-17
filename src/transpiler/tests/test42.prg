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
   VAR cName   AS STRING  INIT ""
   VAR nValue  AS NUMERIC INIT 0
   VAR lActive AS LOGICAL INIT .F.

   METHOD SetByName( cField, xValue )
   METHOD GetByName( cField )
   METHOD DumpFields( aFields )
ENDCLASS

METHOD SetByName( cField, xValue ) CLASS Record
   ::&(cField) := xValue
RETURN Self

METHOD GetByName( cField ) CLASS Record
RETURN ::&(cField)

METHOD DumpFields( aFields ) CLASS Record
   LOCAL i, xVal, cType
   FOR i := 1 TO Len( aFields )
      xVal  := ::&(aFields[i])
      cType := ValType( xVal )
      IF cType == "C"
         ? aFields[i] + ": " + xVal
      ELSEIF cType == "N"
         ? aFields[i] + ": " + Str( xVal, 4 )
      ELSEIF cType == "L"
         ? aFields[i] + ": " + iif( xVal, "T", "F" )
      ELSE
         ? aFields[i] + ": ?"
      ENDIF
   NEXT
RETURN NIL

PROCEDURE Main()
   LOCAL oRec := Record():New()
   LOCAL aFields := { "cName", "nValue", "lActive" }

   // Direct property access — baseline
   oRec:cName   := "hello"
   oRec:nValue  := 42
   oRec:lActive := .T.
   ? "direct: " + oRec:cName + ", " + Str( oRec:nValue, 4 ) + ", " + iif( oRec:lActive, "T", "F" )

   // Write via dynamic member (SETMEMBER)
   oRec:SetByName( "cName", "world" )
   oRec:SetByName( "nValue", 99 )
   oRec:SetByName( "lActive", .F. )

   // Read via dynamic member (GETMEMBER)
   ? "dynamic: " + oRec:GetByName( "cName" ) + ", " + Str( oRec:GetByName( "nValue" ), 4 ) + ", " + iif( oRec:GetByName( "lActive" ), "T", "F" )

   // Iterate fields by name (DumpFields uses GETMEMBER in a loop)
   oRec:DumpFields( aFields )

RETURN
