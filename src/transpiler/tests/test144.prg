// Test 144: `X():Method()` with no arguments sends the method to the new
// instance and answers its result; only New() is the constructor.
//
// The emitter wrote every `X():Method()` without arguments as `X():New()`
// would be: `X():Init()` became HbRuntime.Initialised( new X(), … ), which
// answers the object whatever Init() returned, so a device class's NIL
// ("could not start") was never seen; and `X():Start144()` on a class with
// neither New nor Init became a bare `new X()`, the method never sent.
// test124 covers the same messages with arguments.

#include "hbclass.ch"

STATIC s_lWorks144 := .F.

// NIL on failure, Self on success, as the device classes do
CLASS Device144
   VAR lUp144 INIT .F.
   METHOD Init()
ENDCLASS

METHOD Init() CLASS Device144
   IF ! s_lWorks144
      RETURN NIL
   ENDIF
   ::lUp144 := .T.
   RETURN Self

// neither New nor Init: the class function's instance, then the method
CLASS Remote144
   VAR nStarts144 INIT 0
   METHOD Start144()
ENDCLASS

METHOD Start144() CLASS Remote144
   ::nStarts144++
   RETURN Self

PROCEDURE Main()

   LOCAL oDevice144
   LOCAL oRemote144

   oDevice144 := Device144():Init()
   ? "Init() of a device that cannot start:", iif( oDevice144 == NIL, "NIL", "an object" )

   oDevice144 := Device144():New()
   ? "New() of the same device:", iif( oDevice144 == NIL, "NIL", "an object" ), oDevice144:lUp144

   s_lWorks144 := .T.
   oDevice144 := Device144():Init()
   ? "Init() of a device that starts:", iif( oDevice144 == NIL, "NIL", "an object" ), oDevice144:lUp144

   ? "Init() as a receiver:", Device144():Init():lUp144

   oRemote144 := Remote144():Start144()
   ? "Start144() ran:", oRemote144:nStarts144

   RETURN
