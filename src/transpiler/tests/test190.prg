// Test 190: a wrapper's method calls the method of the same name on what
// it wraps, an object whose class nothing here declares (a connector's
// proxy). Till190:Ring190() takes a whole number first and text second;
// what it wraps takes text first and an amount second. The arguments of
// the inner call are the inner method's, so they are passed as they are
// and not cast after the till's own parameters. Nothing is wrapped in
// this run, so the till answers itself.

#include "hbclass.ch"

PROCEDURE Main()

   LOCAL oTill190 := Till190():New()

   ? oTill190:Ring190( 7, "tea" )
   ? oTill190:Ring190( 2, "milk" )

RETURN

CLASS Till190
   VAR oInside
   METHOD New() CONSTRUCTOR
   METHOD Ring190( iTimes, cWhat )
ENDCLASS

METHOD New() CLASS Till190
RETURN Self

METHOD Ring190( iTimes, cWhat ) CLASS Till190

   IF ::oInside != NIL
      RETURN ::oInside:Ring190( cWhat, iTimes * 1.25 )
   ENDIF

RETURN cWhat + " x" + LTrim( Str( iTimes ) )
