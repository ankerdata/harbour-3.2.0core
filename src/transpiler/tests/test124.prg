// Test 124: `X():New( ... )` answers the object when X declares only Init().
//
// HBObject's New() (rtl/tobject.prg) runs Init( ... ) and returns Self,
// whatever Init() returned. The C# was `(X)new X().Init( ... )`, which
// answered Init()'s result: easipos's Transaction and POSStatus declare an
// Init() that returns NIL, so all 75 of their `New()` calls produced null.
// It is HbRuntime.Initialised( new X(), self => self.Init( ... ) ) now.
//
// `X():Init( ... )` is a different message: it answers what Init() returns,
// which is how a device class says it could not connect (easipos fmde.prg);
// `X():New( ... )` can never say so.

#include "hbclass.ch"

CLASS Gadget124
   VAR cPort124
   VAR lOpen124
   METHOD Init( cPort )
ENDCLASS

// NIL on failure, Self on success, as the device classes do
METHOD Init( cPort ) CLASS Gadget124
   ::cPort124 := cPort
   ::lOpen124 := ! Empty( cPort )
   IF ! ::lOpen124
      RETURN NIL
   ENDIF
   RETURN Self

CLASS Ledger124
   VAR nLines124
   METHOD Init()
ENDCLASS

// the Transaction / POSStatus shape: set up, return NIL
METHOD Init() CLASS Ledger124
   ::nLines124 := 0
   RETURN NIL

PROCEDURE Main()
   LOCAL oLedger := Ledger124():New()
   LOCAL oGadget := Gadget124():New( "" )

   ? oLedger == NIL, oLedger:nLines124
   ? oGadget == NIL, oGadget:lOpen124
   ? Gadget124():New( "COM1" ):cPort124
   ? Gadget124():Init( "" ) == NIL, Gadget124():Init( "COM2" ) == NIL

   RETURN
