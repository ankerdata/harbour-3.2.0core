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
   VAR cName INIT "animal"
   VAR cSound INIT "..."
ENDCLASS

CLASS Dog INHERIT Animal
ENDCLASS

CLASS Cat INHERIT Animal
ENDCLASS

PROCEDURE Main()

    LOCAL oAnimal := Animal():New()
    LOCAL oDog := Dog():New()
    LOCAL oPet

    oDog:cName := "rex"
    oDog:cSound := "woof"

    // Same parameter slot fed both the base and the subclass.
    ? "a=", Describe(oAnimal)
    ? "b=", Describe(oDog)

    // Mixed-class returns: Pick's inferred return type is Animal.
    oPet := Pick(.T.)
    oPet:cSound := "WOOF"
    ? "c=", Describe(oPet)
    oPet := Pick(.F.)
    ? "d=", Describe(oPet)

RETURN

function Describe(oAnimal)
return rtrim(oAnimal:cName) + " says " + rtrim(oAnimal:cSound)

// Two RETURN paths, two sibling classes — merges to Animal via the
// INHERIT chain rather than conflicting to USUAL.
function Pick(lDog)

    if lDog
        return Dog():New()
    endif

return Cat():New()
