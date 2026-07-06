#include "astype.ch"
// Test 79: INHERIT-aware type relations in the reftab.
//
// The reftab records INHERIT edges (I=<Parent> tail rows), and the
// type machinery uses them two ways:
//   - a subclass argument satisfies a superclass-typed parameter slot
//     (Describe below receives Animal AND Dog with no W0022 conflict
//     and no downgrade-to-USUAL — the slot stays Animal);
//   - RETURN statements yielding different but related classes merge
//     to their nearest common ancestor instead of degrading (Pick
//     returns Dog on one path and Cat on the other; its C# return
//     type must be Animal for both branches to compile).
// Runtime behaviour is checked across prg/hb/cs as usual.

#include "hbclass.ch"

CLASS Animal

   DATA cName AS STRING INIT "animal"
   DATA cSound AS STRING INIT "..."

ENDCLASS

CLASS Dog INHERIT Animal


ENDCLASS

CLASS Cat INHERIT Animal


ENDCLASS

PROCEDURE Main()

   LOCAL oAnimal := Animal():New() AS OBJECT
   LOCAL oDog := Dog():New() AS OBJECT
   LOCAL oPet AS OBJECT

   oDog:cName := "rex"
   oDog:cSound := "woof"

   // Same parameter slot fed both the base and the subclass.
   QOut("a=", Describe(oAnimal))
   QOut("b=", Describe(oDog))

   // Mixed-class returns: Pick's inferred return type is Animal.
   oPet := Pick(.T.)
   oPet:cSound := "WOOF"
   QOut("c=", Describe(oPet))
   oPet := Pick(.F.)
   QOut("d=", Describe(oPet))

RETURN

FUNCTION Describe( oAnimal AS OBJECT ) AS STRING
RETURN rtrim(oAnimal:cName) + " says " + rtrim(oAnimal:cSound)

   // Two RETURN paths, two sibling classes — merges to Animal via the
   // INHERIT chain rather than conflicting to USUAL.
FUNCTION Pick( lDog AS LOGICAL ) AS OBJECT

   IF lDog
   RETURN Dog():New()
   ENDIF

RETURN Cat():New()
