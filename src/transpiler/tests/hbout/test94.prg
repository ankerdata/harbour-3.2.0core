#include "astype.ch"
// Test 94: sends on typed receivers — four shapes C# needs help with.
//
// (1) `Teapot():New( 2 ):Serve()` — the constructor call is cast to its
//     class, and as a receiver that cast must bind to the constructor
//     call, not to the chained member (17 CS0029 in the easipos corpus:
//     `nChoice := StockOperationsDialog():New(...):Run()`).
// (2) `Kettle():New( 1500 )` on a class that declares only Init:
//     Harbour's HBObject forwards New to Init (the "classy"
//     compatibility in hbclass.ch), so the C# call goes to Init too.
// (3) `oKettle:lisboiling` — Harbour is case-insensitive, C# is not: a
//     member on a typed receiver is emitted in its declared spelling
//     (`oTransaction:lSaleCdSLtyOnline` against `VAR lSaleCdSLtyOnLine`).
// (4) `oMug:nHandles` read through a Mug-typed parameter that holds a
//     BigMug — the member is declared on the subclass only, a downcast
//     Harbour never spells; the access goes through (dynamic), as an
//     undeclared Self member does. A member no class in the chain or
//     below declares stays a static access, so a real typo is still a
//     C# error, which is the finding wanted. The reftab only carries
//     rows for TYPED members (`AS INTEGER` here, as FcnTranLine's
//     nFixedNo in the corpus); an untyped VAR is invisible to both the
//     spelling and the subclass lookups.
#include "hbclass.ch"

CLASS Kettle

   DATA nWatts AS NUMERIC INIT 0
   DATA lIsBoiling AS LOGICAL INIT .F.
   METHOD Init( nWatts )

ENDCLASS

CLASS Teapot

   DATA nCups AS NUMERIC INIT 0
   METHOD New( nCups )
   METHOD Serve()

ENDCLASS

CLASS Mug

   DATA nSize AS NUMERIC INIT 1

ENDCLASS

CLASS BigMug INHERIT Mug

   DATA nHandles AS INTEGER INIT 2
   METHOD New()

ENDCLASS

METHOD Init( nWatts AS NUMERIC ) AS OBJECT CLASS Kettle
   ::nWatts := nWatts
RETURN Self

METHOD New( nCups AS NUMERIC ) AS OBJECT CLASS Teapot
   ::nCups := nCups
RETURN Self

METHOD Serve() AS NUMERIC CLASS Teapot
RETURN ::nCups * 2

METHOD New() AS OBJECT CLASS BigMug
   ::nSize := 2
RETURN Self

   /* Called with a Mug and a BigMug, so the slot is Mug; nHandles exists
   only on BigMug and the guard keeps the read to those. */
FUNCTION Handles( oMug AS OBJECT )
RETURN IIF(oMug:nSize > 1, oMug:nHandles, 1)

PROCEDURE Main()
   LOCAL nServed := Teapot():New(2):Serve() AS NUMERIC
   LOCAL oKettle := Kettle():New(1500) AS OBJECT
   LOCAL oBigMug := BigMug():New() AS OBJECT
   oKettle:lisboiling := .T.
   QOut("served=" + LTrim(Str(nServed)))
   QOut("watts=" + LTrim(Str(oKettle:nWatts)) + " boiling=" + IIF(oKettle:LISBOILING, "T", "F"))
   QOut("mug=" + LTrim(Str(Handles(Mug():New()))) + " big=" + LTrim(Str(Handles(oBigMug))))
RETURN
