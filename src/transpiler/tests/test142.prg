// Test 142: a member declared a dynamic class reaches its bag.
//
// A dynamic class (one that sends ::&( name ) to itself, EasiPOS's
// SQLtTable) is its own C# type, and a member no class declares goes
// through `((dynamic)recv)` to the instance's bag (test134). That held for
// a local and for a member named for its class, but not for a member whose
// name says nothing and whose declaration gives the class (`VAR oOrmTable
// AS CLASS SQLtTable`, EasiPOS's BrowseDialog): the send was written
// plainly on the declared type, CS1061. Peek142() reads such a field (it
// is never called: the bag only holds what Put142() put there, and Harbour
// has no such member otherwise); Count142() reads a declared one, checked.
// And a constructor whose New() answers its own class is written without
// a cast, while an inherited New() keeps one. And a method sent to a
// receiver C# cannot type (an element of an undeclared array) with an
// argument past its parameters runs, the extra dropped as Harbour drops
// it: the DLR cannot bind such a call, and HbRuntime's fallback used to
// report "Parameter count mismatch" (HbDynamicObject.TryInvokeMember).

#include "../include/astype.ch"
#include "hbclass.ch"

CLASS Bag142
   VAR cLabel142 INIT ""
   VAR nCount142 INIT 0
   METHOD Put142( cName, xValue )
   METHOD Repeat142( cWhat, nTimes )
ENDCLASS

METHOD Put142( cName, xValue ) CLASS Bag142
   ::&( cName ) := xValue
   ::nCount142 += 1
RETURN Self

METHOD Repeat142( cWhat, nTimes ) CLASS Bag142
   hb_default( @nTimes, 1 )
RETURN Replicate( cWhat, nTimes )

CLASS Holder142
   VAR oHeld AS CLASS Bag142
   METHOD New( oHeld ) CONSTRUCTOR
   METHOD Count142()
   METHOD Peek142()
ENDCLASS

METHOD New( oHeld ) CLASS Holder142
   ::oHeld := oHeld
RETURN Self

METHOD Count142() CLASS Holder142
RETURN ::oHeld:nCount142

METHOD Peek142() CLASS Holder142
RETURN ::oHeld:cUndeclared142

CLASS Keeper142 INHERIT Holder142
ENDCLASS

PROCEDURE Main()
   LOCAL oHolder := Holder142():New( Bag142():New():Put142( "cLabel142", "box" ) )
   LOCAL oKeeper := Keeper142():New( Bag142():New() )
   LOCAL aLoose := { Bag142():New() }

   ? oHolder:Count142(), oKeeper:Count142(), oHolder:oHeld:cLabel142
   ? aLoose[ 1 ]:Repeat142( "ab", 2, "extra" ), aLoose[ 1 ]:Repeat142( "c" )
   RETURN
