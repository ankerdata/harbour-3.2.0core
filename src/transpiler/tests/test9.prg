#include "hbclass.ch"

CLASS Person INHERIT HBObject

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

RETURN NIL
