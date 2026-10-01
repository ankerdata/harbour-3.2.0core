// W0040: a declaration (`AS CLASS X`, `AS ARRAY OF CLASS X`) naming a
// class nothing defines. W0041: a value of another type put where a
// declaration says otherwise — assigned, put into a declared array,
// passed to a declared parameter, returned from a declared function.
// A subclass is accepted where its parent is declared, and a value of
// unknown type passes (C# converts it at run time). The lines marked
// "quiet" must not warn.

#include "hbclass.ch"

CLASS Shape40
   VAR nSide INIT 1
   VAR oPart AS CLASS Missing40               // fires W0040: nothing defines Missing40
ENDCLASS

CLASS Square40 INHERIT Shape40
ENDCLASS

CLASS Other40
ENDCLASS

FUNCTION Use40( xAnything )
   LOCAL oShape AS CLASS Shape40
   LOCAL oGhost AS CLASS Ghost40              // fires W0040: nothing defines Ghost40
   LOCAL aShapes AS ARRAY OF CLASS Shape40 := {}
   oShape := Square40():New()                 // quiet: a Square40 is a Shape40
   oShape := xAnything                        // quiet: its type is unknown
   oShape := Other40():New()                  // fires W0041: an Other40 is not
   oShape := "box"                            // fires W0041
   AAdd( aShapes, Square40():New() )          // quiet
   AAdd( aShapes, 5 )                         // fires W0041
   aShapes[ 1 ] := Other40():New()            // fires W0041
   Take40( Square40():New() )                 // quiet
   Take40( "box" )                            // fires W0041
   HB_SYMBOL_UNUSED( oGhost )
   RETURN Make40()

FUNCTION Take40( oShape AS CLASS Shape40 )
   RETURN oShape:nSide

FUNCTION Make40() AS CLASS Shape40
   IF Seconds() > 0
      RETURN Square40():New()                 // quiet
   ENDIF
   RETURN "not a shape"                       // fires W0041
