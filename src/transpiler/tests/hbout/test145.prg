#include "astype.ch"
// Test 145: a local that holds the only reference to a new object is
// disposed where Harbour's reference count reaches zero (hbown.c).
//
// Harbour runs an object's DESTRUCTOR the moment its last reference goes:
// at `o := NIL`, when o is given another object (after the new one is
// built), or when the routine holding o returns. C# counts nothing, and a
// Debug build keeps a routine's references alive until it returns. Where
// the transpiler proves a local holds the only reference, the C# disposes
// it at those points: a `using` declaration for one new object given at
// the top level; otherwise HbRuntime.Replace at reassignment, `?.Dispose()`
// at := NIL and a finally at the routine's exit. A value that may outlive
// the routine (a STATIC, a routine that keeps what it is given) and a
// parameter, which is its caller's, are left to the finalizer, which
// hb_gcAll() waits for. Every Probe145 says when it goes, so the order of
// the lines is the test.

#include "hbclass.ch"

STATIC soKept145 AS OBJECT

CLASS Probe145

   DATA cName145 AS STRING
   METHOD New( cName145 )
   METHOD Touch145()

ENDCLASS

METHOD New( cName145 AS STRING ) AS OBJECT CLASS Probe145
   ::cName145 := cName145
   QOut("built", cName145)
RETURN Self

METHOD Touch145() AS NUMERIC CLASS Probe145
RETURN Len(::cName145)

PROCEDURE Gone145() CLASS Probe145
   QOut("gone", ::cName145)
RETURN

   // one new object, kept to the end: a `using` declaration
STATIC PROCEDURE AtEnd145()

   LOCAL oProbe145 := Probe145():New("at end") AS OBJECT

   QOut("using", oProbe145:Touch145())

RETURN

   // := NIL, and another object given to it
STATIC PROCEDURE Released145()

   LOCAL oProbe145 := Probe145():New("first") AS OBJECT

   QOut("touched", oProbe145:Touch145())
   oProbe145 := NIL
   QOut("after := NIL")
   oProbe145 := Probe145():New("second")
   oProbe145 := Probe145():New("third")
   QOut("after reassignment")

RETURN

   // a function that returns the new object it made: its caller owns it
STATIC FUNCTION Make145( cName AS STRING ) AS OBJECT

   LOCAL oProbe145 := Probe145():New(cName) AS OBJECT

RETURN oProbe145

STATIC PROCEDURE Returned145()

   LOCAL oProbe145 AS OBJECT

   oProbe145 := Make145("returned")
   QOut("used", oProbe145:Touch145())

RETURN

   // passed to a routine that only reads it: still owned
STATIC FUNCTION Read145( oProbe145 AS OBJECT )
RETURN oProbe145:Touch145()

STATIC PROCEDURE Lent145()

   LOCAL oProbe145 := Probe145():New("lent") AS OBJECT

   QOut("read", Read145(oProbe145))

RETURN

   // passed to a routine that keeps it: left to the collector
STATIC PROCEDURE Keep145( oProbe145 AS OBJECT )
   soKept145 := oProbe145
RETURN

STATIC PROCEDURE Kept145()

   LOCAL oProbe145 := Probe145():New("kept") AS OBJECT

   Keep145(oProbe145)
   QOut("kept, not gone")

RETURN

   // a parameter is its caller's: := NIL in the callee lets go of the
   // callee's reference only
STATIC PROCEDURE Drop145( oProbe145 AS OBJECT )
   oProbe145 := NIL
   QOut("dropped the parameter")
RETURN

STATIC PROCEDURE Borrowed145()

   LOCAL oProbe145 := Probe145():New("borrowed") AS OBJECT

   Drop145(oProbe145)
   QOut("still here", oProbe145:Touch145())

RETURN

PROCEDURE Main()

   AtEnd145()
   QOut("after AtEnd145")
   Released145()
   QOut("after Released145")
   Returned145()
   QOut("after Returned145")
   Lent145()
   QOut("after Lent145")
   Kept145()
   QOut("after Kept145")
   soKept145 := NIL
   hb_gcAll(.T.)
   QOut("after releasing the kept one")
   Borrowed145()
   QOut("after Borrowed145")

RETURN
