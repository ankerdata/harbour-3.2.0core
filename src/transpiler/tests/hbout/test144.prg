#include "astype.ch"
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

STATIC s_lWorks144 := .F. AS LOGICAL

// NIL on failure, Self on success, as the device classes do

CLASS Device144

   DATA lUp144 AS LOGICAL INIT .F.
   METHOD Init()

ENDCLASS

CLASS Remote144

   DATA nStarts144 AS NUMERIC INIT 0
   METHOD Start144()

ENDCLASS

METHOD Init() AS USUAL CLASS Device144
   IF !s_lWorks144
   RETURN NIL
   ENDIF

   ::lUp144 := .T.
RETURN Self

   // neither New nor Init: the class function's instance, then the method

METHOD Start144() AS OBJECT CLASS Remote144
   ::nStarts144++
RETURN Self

PROCEDURE Main()

   LOCAL oDevice144 AS OBJECT
   LOCAL oRemote144 AS OBJECT

   oDevice144 := Device144():Init()
   QOut("Init() of a device that cannot start:", IIF(oDevice144 == NIL, "NIL", "an object"))

   oDevice144 := Device144():New()
   QOut("New() of the same device:", IIF(oDevice144 == NIL, "NIL", "an object"), oDevice144:lUp144)

   s_lWorks144 := .T.
   oDevice144 := Device144():Init()
   QOut("Init() of a device that starts:", IIF(oDevice144 == NIL, "NIL", "an object"), oDevice144:lUp144)

   QOut("Init() as a receiver:", Device144():Init():lUp144)

   oRemote144 := Remote144():Start144()
   QOut("Start144() ran:", oRemote144:nStarts144)

RETURN
