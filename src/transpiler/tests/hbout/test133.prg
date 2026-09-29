#include "astype.ch"
// Test 133: a model named after its factory's stem, and an o<Class>
// parameter nothing refines, typed by its name.
//
// The ORM models are named <Stem>Table after their definition factories
// <Stem>Def (Alex, 2026-09-28): the map's `TestShelf133Def =model
// TestShelf133Table` row makes `ConstructORMTable( TestShelf133Def() )`
// emit `new TestShelf133Table(...)`, and a variable named
// oTestShelf133Table is that class by its name, as oTransaction is a
// Transaction. A parameter
// whose callers pass only untyped values (here, values out of a hash) was
// declared dynamic; one named after a class, or after a model, is now
// declared as it: ShelfLabel133( oTestShelf133Table ) and
// Bump133( oCounter133 ).
//
// The Harbour side, as test83's: a stand-in record class, and the factory
// and ConstructORMTable() as plain functions.

#include "hbclass.ch"

CLASS ShelfRec133

   DATA nNo AS NUMERIC
   DATA cName AS STRING
   METHOD New()

ENDCLASS

CLASS Counter133

   DATA nCount133 AS NUMERIC INIT 0

ENDCLASS

METHOD New() AS OBJECT CLASS ShelfRec133
RETURN Self

FUNCTION TestShelf133Def( cPath AS STRING ) AS ARRAY
RETURN {{"Shelf", "SHELF"}, IIF(cPath != NIL, cPath, ""), {}, {}, {=>}, 1}

FUNCTION ConstructORMTable( aFileDefinition AS ARRAY, lReadOnly AS LOGICAL, lShared AS LOGICAL ) AS OBJECT
RETURN ShelfRec133():New()

   // both parameters' callers pass values out of a hash: untyped
FUNCTION ShelfLabel133( oTestShelf133Table AS OBJECT ) AS STRING
RETURN oTestShelf133Table:cName + " " + hb_ntos(oTestShelf133Table:nNo)

PROCEDURE Bump133( oCounter133 AS OBJECT )
   oCounter133:nCount133 += 1
RETURN

PROCEDURE Main()
   LOCAL oShelf := ConstructORMTable(TestShelf133Def()) AS OBJECT
   LOCAL hBox := {=>} AS HASH

   oShelf:nNo := 7
   oShelf:cName := "top"
   hBox["shelf"] := oShelf
   hBox["counter"] := Counter133():New()

   QOut(ShelfLabel133(hBox["shelf"]))
   Bump133(hBox["counter"])
   Bump133(hBox["counter"])
   QOut(hBox["counter"]:nCount133)
RETURN
