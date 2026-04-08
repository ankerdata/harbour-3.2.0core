// Test 9: Type inference from initializers and Hungarian notation
#include "hbclass.ch"

CLASS Inherited

   CLASSDATA nVersion INIT 1.0

ENDCLASS

CLASS Person INHERIT Inherited

   DATA nAge      INIT 0
   DATA cName     INIT ""
   DATA dBirth
   DATA lActive   INIT .T.
   DATA aItems    INIT {}
   DATA oParent
   DATA hConfig
   DATA bCallback
   DATA xUnknown

   CLASSDATA nCount INIT 0

   METHOD New()

ENDCLASS

METHOD New() CLASS Person
   ? "New called"
RETURN Self

FUNCTION Main()

   // Type from initializer (most specific)
   LOCAL nCount := 0
   LOCAL nPrice := 9.99
   LOCAL cName := "hello"
   LOCAL lFound := .T.
   LOCAL aList := { 1, 2, 3 }
   LOCAL hMap := { "key" => "value" }

   // Type from Hungarian prefix (no initializer)
   LOCAL nTotal
   LOCAL cResult
   LOCAL lDone
   LOCAL aBuffer
   LOCAL oConnection
   LOCAL dToday
   LOCAL hSettings
   LOCAL bAction

   // No prefix, no initializer — fallback
   LOCAL x
   LOCAL counter
   LOCAL Temp

   ? "nCount=" + Str( nCount )
   ? "nPrice=" + Str( nPrice, 10, 2 )
   ? "cName=" + cName
   ? "lFound=" + IIF( lFound, ".T.", ".F." )

RETURN NIL
