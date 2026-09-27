#include "astype.ch"
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

   DATA cKind129 AS STRING
   METHOD New( cKind )

ENDCLASS

CLASS Square129 INHERIT Shape129


ENDCLASS

CLASS Test129

   DATA cNote129 AS STRING INIT "note"

ENDCLASS

METHOD New( cKind AS STRING ) AS OBJECT CLASS Shape129
   ::cKind129 := cKind
RETURN Self

   // A class named after its file, as OrmTestSuite is in ormtestsuite.prg:
   // member rows are `Class::member`, the file's STATIC functions
   // `file::Name`, and the lookup still has to find the functions.

FUNCTION Kind129( oShape AS OBJECT ) AS STRING
RETURN oShape:cKind129

PROCEDURE Main()
   LOCAL oSq129 := Square129():New("square") AS OBJECT
   LOCAL oPl129 := MakeShape129() AS OBJECT

   QOut(Kind129(oSq129))
   QOut(Kind129(oPl129))
   QOut(Label129())
   QOut(Panel129(1.5), Panel129(0))
   QOut(Flag129("L"))

RETURN

   // A value that could be anything, taken into a name that commits to a type,
   // keeps the name's type: Poly129()'s RETURNs disagree (USUAL), and Flag129()
   // returns bool as its local is declared - easipos flags.prg's GetFlagL() over
   // TypedFlag(), which went dynamic once TypedFlag()'s USUAL was visible.
FUNCTION Flag129( cKind AS STRING ) AS LOGICAL
   LOCAL lValue := Poly129(cKind) AS LOGICAL
RETURN lValue

STATIC FUNCTION Poly129( cKind AS STRING ) AS USUAL
   IF cKind == "L"
   RETURN .T.
   ENDIF

RETURN "text"

   // A numeric parameter given an INTEGER function's result keeps its type:
   // the emitter's inference seeds the parameters as the scan's does, or it
   // typed the return from the assignment - long, over a decimal parameter
   // (easipos screensetup.prg's ClerkPanel(), once PanelDefault() had a type).
FUNCTION Panel129( nPanel AS NUMERIC ) AS NUMERIC
   IF nPanel == 0
      nPanel := Default129()
   ENDIF

RETURN nPanel

   // hb_bitAnd() makes it INTEGER, as PanelDefault()'s #defines did
FUNCTION Default129() AS NUMERIC
RETURN hb_bitAnd(7, 3)

STATIC FUNCTION MakeShape129() AS OBJECT
RETURN Shape129():New("plain")

   // A RETURN of a STATIC function's call takes its type too.
STATIC FUNCTION Label129()
RETURN Tag129() + "!"

STATIC FUNCTION Tag129() AS STRING
RETURN "tag"
