// Test 129: a call to a STATIC function types what it is assigned to.
//
// A STATIC function's reftab row is keyed `<FileBase>::<Name>`, and the
// type inference asked only the bare name, so `oPlain := MakeShape129()`
// left the local dynamic. The refinement then heard only from the typed
// caller: Kind129's parameter became Square129, and the call passing a
// Shape129 failed at run time in the binder - easipos ormtestsuite.prg's
// OrmTestOrderedIds( oFirst ), oFirst from the file's SharedIdsFixtureTable().
// Typed, the two callers widen the slot to their common ancestor.

#include "hbclass.ch"

CLASS Shape129
   VAR cKind129
   METHOD New( cKind )
ENDCLASS

METHOD New( cKind ) CLASS Shape129
   ::cKind129 := cKind
   RETURN Self

CLASS Square129 INHERIT Shape129
ENDCLASS

// A class named after its file, as OrmTestSuite is in ormtestsuite.prg:
// member rows are `Class::member`, the file's STATIC functions
// `file::Name`, and the lookup still has to find the functions.
CLASS Test129
   VAR cNote129 INIT "note"
ENDCLASS

FUNCTION Kind129( oShape )
   RETURN oShape:cKind129

PROCEDURE Main()
   LOCAL oSq129 := Square129():New( "square" )
   LOCAL oPl129 := MakeShape129()

   ? Kind129( oSq129 )
   ? Kind129( oPl129 )
   ? Label129()
   ? Panel129( 1.5 ), Panel129( 0 )
   ? Flag129( "L" )

   RETURN

// A value that could be anything, taken into a name that commits to a type,
// keeps the name's type: Poly129()'s RETURNs disagree (USUAL), and Flag129()
// returns bool as its local is declared - easipos flags.prg's GetFlagL() over
// TypedFlag(), which went dynamic once TypedFlag()'s USUAL was visible.
FUNCTION Flag129( cKind )
   LOCAL lValue := Poly129( cKind )
   RETURN lValue

STATIC FUNCTION Poly129( cKind )
   IF cKind == "L"
      RETURN .T.
   ENDIF
   RETURN "text"

// A numeric parameter given an INTEGER function's result keeps its type:
// the emitter's inference seeds the parameters as the scan's does, or it
// typed the return from the assignment - long, over a decimal parameter
// (easipos screensetup.prg's ClerkPanel(), once PanelDefault() had a type).
FUNCTION Panel129( nPanel )
   IF nPanel == 0
      nPanel := Default129()
   ENDIF
   RETURN nPanel

// hb_bitAnd() makes it INTEGER, as PanelDefault()'s #defines did
FUNCTION Default129()
   RETURN hb_bitAnd( 7, 3 )

STATIC FUNCTION MakeShape129()
   RETURN Shape129():New( "plain" )

// A RETURN of a STATIC function's call takes its type too.
STATIC FUNCTION Label129()
   RETURN Tag129() + "!"

STATIC FUNCTION Tag129()
   RETURN "tag"
