#include "astype.ch"
// Test 134: a dynamic class is its own C# type.
//
// A class that sends ::&( name ) to itself extends HbDynamicObject in C#
// (EasiPOS's SQLtTable), so an instance may hold members no declaration
// names. Every slot, local and return of such a class used to be emitted
// `dynamic`, which left each use unchecked so that the few undeclared ones
// would compile. It is its own type now: declared members and methods are
// checked when C# builds, and only a member no class declares goes through
// `((dynamic)recv)`, as Extra134() shows (it is never called: Harbour has
// no such member at run time, where SQLtTable's come from its metaclass).
//
// And only a macro send to Self makes a class dynamic. Viewer134 sends one
// to its member, which says nothing of Viewer134 itself (EasiPOS's
// BrowseDialog, `::oOrmTable:&( cField )`).

#include "hbclass.ch"

CLASS Bag134

   DATA cLabel134 AS STRING INIT ""
   DATA nCount134 AS NUMERIC INIT 0
   METHOD Put134( cName, xValue )
   METHOD Get134( cName )
   METHOD Touch134()

ENDCLASS

CLASS Viewer134

   DATA oBag134 AS OBJECT
   METHOD New( oBag134 )
   METHOD Show134( cName )

ENDCLASS

METHOD Put134( cName AS STRING, xValue AS USUAL ) AS OBJECT CLASS Bag134
   ::&(cName) := xValue
RETURN Self

METHOD Get134( cName AS STRING ) CLASS Bag134
RETURN ::&(cName)

METHOD Touch134() AS OBJECT CLASS Bag134
   ::nCount134 += 1
RETURN Self

METHOD New( oBag134 AS OBJECT ) AS OBJECT CLASS Viewer134
   ::oBag134 := oBag134
RETURN Self

METHOD Show134( cName AS STRING ) CLASS Viewer134
RETURN ::oBag134:&(cName)

   // declared members and methods on a Bag134 receiver: checked in C#
FUNCTION Label134( oBag134 AS OBJECT ) AS STRING
RETURN oBag134:Touch134():cLabel134 + "/" + hb_ntos(oBag134:nCount134)

   // a member no class declares, which only a dynamic class's bag could hold
FUNCTION Extra134( oBag134 AS OBJECT ) AS STRING
RETURN oBag134:cExtra134

PROCEDURE Main()
   LOCAL oStore134 := Bag134():New() AS OBJECT
   LOCAL oShow134 AS OBJECT

   oStore134:Put134("cLabel134", "tin")
   oShow134 := Viewer134():New(oStore134)
   QOut(oShow134:Show134("cLabel134"))
   QOut(Label134(oStore134))
   QOut(oStore134:Get134("nCount134"))
RETURN
