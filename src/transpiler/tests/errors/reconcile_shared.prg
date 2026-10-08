// --reconcile (plan H3): two builds compile this file, each with a caller
// of its own (reconcile_a.prg, reconcile_b.prg), and each scans into its
// own reftab, so each types these routines from its own callers alone.
// One C# tree has one signature per routine; the reconcile gives both
// tables what the other build's callers recorded, and the emitted
// signatures must come out identical.

#include "hbclass.ch"

CLASS Fruit46
   VAR cName INIT ""
ENDCLASS

CLASS Apple46 INHERIT Fruit46
ENDCLASS

CLASS Pear46 INHERIT Fruit46
ENDCLASS

CLASS Stone46
   VAR nWeight INIT 0
ENDCLASS

// build A passes the total by reference, build B leaves it out: `ref`
// in both, and the short overload B's call needs in both
PROCEDURE Count46( cKey, nTotal )
   nTotal := Len( cKey )
RETURN

// build A passes an apple, build B a pear: a Fruit46 in both
PROCEDURE Weigh46( oThing46 )
   ? oThing46:cName
RETURN

// build A passes an apple, build B a stone, which share no class: W0045,
// and dynamic in both
PROCEDURE Show46( oShown46 )
   ? oShown46:ClassName()
RETURN
