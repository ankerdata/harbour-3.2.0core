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
   VAR nNo
   VAR cName
   METHOD New() CONSTRUCTOR
ENDCLASS

METHOD New() CLASS ShelfRec133
RETURN Self

CLASS Counter133
   VAR nCount133 INIT 0
ENDCLASS

FUNCTION TestShelf133Def( cPath )
RETURN { { "Shelf", "SHELF" }, iif( cPath != nil, cPath, "" ), {}, {}, { => }, 1 }

FUNCTION ConstructORMTable( aFileDefinition, lReadOnly, lShared )
RETURN ShelfRec133():New()

// both parameters' callers pass values out of a hash: untyped
FUNCTION ShelfLabel133( oTestShelf133Table )
RETURN oTestShelf133Table:cName + " " + hb_ntos( oTestShelf133Table:nNo )

PROCEDURE Bump133( oCounter133 )
   oCounter133:nCount133 += 1
RETURN

PROCEDURE Main()
   LOCAL oShelf := ConstructORMTable( TestShelf133Def() )
   LOCAL hBox := { => }

   oShelf:nNo := 7
   oShelf:cName := "top"
   hBox[ "shelf" ] := oShelf
   hBox[ "counter" ] := Counter133():New()

   ? ShelfLabel133( hBox[ "shelf" ] )
   Bump133( hBox[ "counter" ] )
   Bump133( hBox[ "counter" ] )
   ? hBox[ "counter" ]:nCount133
RETURN
