#include "astype.ch"
// Test 122: DESTRUCTOR runs, and a READONLY member is assigned by its class.
//
// A DESTRUCTOR was emitted as an ordinary method nothing called: C# needs a
// finalizer that calls it, and hb_gcAll() has to wait for finalizers, as
// Harbour's runs destructors before it returns. ormsql.prg's registry of
// open tables depends on it: an exclusive table is released by its
// destructor.
//
// When: Harbour runs a destructor as soon as the last reference goes
// (reference counting). C# runs it when the collector reclaims the object,
// and unoptimised code (a Debug build, and every method's first, tier-0
// compilation) keeps each reference in a routine's frame alive until the
// routine returns, so `o := NIL; hb_gcAll()` inside one routine frees
// nothing there. The objects here are dropped by a routine returning,
// where both agree.
//
// A READONLY member was a get-only property, which C# lets only a
// constructor assign; Harbour lets the class's own methods assign it
// (ormsql.prg's InitInstance sets cTableName). It is `{ get; protected set; }`,
// and a PROTECTED one `{ get; set; }` (C# rejects `protected ... protected set`).
//
// A MODULE FRIENDLY class lets the functions of its own file use its
// PROTECTED and HIDDEN members (ormsql.prg's PreparedSeek() reads SQLtTable's
// hSeekStmts): `protected internal` / `internal` in C#.

#include "hbclass.ch"

STATIC snFreed122 := 0 AS NUMERIC

CLASS Handle122

   DATA cLabel122 AS STRING READONLY
   METHOD Label122( cLabel )

ENDCLASS

CLASS Vault122


   METHOD Peek122()

   PROTECTED:
   DATA nInside122 AS NUMERIC INIT 0
   DATA cSealed122 AS STRING READONLY

ENDCLASS

METHOD Label122( cLabel AS STRING ) AS OBJECT CLASS Handle122
   ::cLabel122 := cLabel
RETURN Self

   // a destructor that returns a value is called through a lambda
METHOD Release122() AS OBJECT CLASS Handle122
   snFreed122++
RETURN Self

   // spelled otherwise than its declaration: the definition takes the declared Peek122
METHOD PEEK122() AS STRING CLASS Vault122
RETURN hb_ntos(::nInside122) + " " + ::cSealed122

   // a function of the file, reaching the FRIENDLY class's protected members
STATIC PROCEDURE Fill122( oVault AS OBJECT )
   oVault:nInside122 := 5
   oVault:cSealed122 := "sealed"
RETURN

PROCEDURE Main()

   Open122("first")
   hb_gcAll(.T.)
   QOut("freed:", snFreed122)

   Open122("second")
   hb_gcAll(.T.)
   QOut("freed:", snFreed122)

   Vault122Check()

RETURN

   // an object that goes with its routine
STATIC PROCEDURE Open122( cLabel AS STRING )

   LOCAL oHandle := Handle122():New() AS OBJECT

   oHandle:Label122(cLabel)
   QOut("label:", oHandle:cLabel122)

RETURN

STATIC PROCEDURE Vault122Check()

   LOCAL oVault := Vault122():New() AS OBJECT

   Fill122(oVault)
   QOut("vault:", oVault:Peek122())

RETURN
