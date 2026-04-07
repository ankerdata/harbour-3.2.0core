#include "astype.ch"
#include "hbclass.ch"

CLASS Person INHERIT HBObject


   DATA nAge AS INTEGER INIT 0
   DATA cName AS STRING INIT ""
   DATA dBirth AS DATE
   DATA lActive AS LOGICAL INIT .T.
   DATA aItems AS ARRAY INIT {}
   DATA oParent AS OBJECT
   DATA hConfig AS HASH
   DATA bCallback AS BLOCK
   DATA xUnknown AS USUAL

   CLASSDATA nCount AS INTEGER INIT 0

   METHOD New()

ENDCLASS

METHOD New() AS OBJECT CLASS Person
RETURN Self


FUNCTION Main()

   // Type from initializer (most specific)
   LOCAL nCount := 0 AS INTEGER
   LOCAL nPrice := 9.99 AS DECIMAL
   LOCAL cName := "hello" AS STRING
   LOCAL lFound := .T. AS LOGICAL
   LOCAL aList := {1, 2, 3} AS ARRAY
   LOCAL hMap := {"key" => "value"} AS HASH

   // Type from Hungarian prefix (no initializer)
   LOCAL nTotal AS NUMERIC
   LOCAL cResult AS STRING
   LOCAL lDone AS LOGICAL
   LOCAL aBuffer AS ARRAY
   LOCAL oConnection AS OBJECT
   LOCAL dToday AS DATE
   LOCAL hSettings AS HASH
   LOCAL bAction AS BLOCK

   // No prefix, no initializer — fallback
   LOCAL x AS USUAL
   LOCAL counter AS USUAL
   LOCAL Temp AS USUAL

RETURN NIL

