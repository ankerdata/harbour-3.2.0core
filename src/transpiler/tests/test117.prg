// Test 117: a method that only returns Self is typed as its own class.
//
// Harbour's constructors and chaining methods end in RETURN Self, which
// the inference reads as OBJECT — emitted as dynamic, so every New() and
// every chaining method returned dynamic (39 New()/Init() in EasiPOS).
// A method whose every RETURN is Self is now its class: the C# method
// returns it and the reftab row records it, so a chain binds statically.
// Four shapes are pinned here:
//   - Ledger117:New / :Add return Ledger117, so oLedger:Add():Add():Describe()
//     resolves without dynamic;
//   - TaxLedger117:Add has its parent's signature and returns TaxLedger117:
//     an override in C#, legal because a return type may narrow;
//   - TaxLedger117:New takes different parameters, so it is an overload;
//   - PlainLedger117 declares no New and inherits one returning Ledger117 —
//     the constructor pattern casts to the class named, which is what keeps
//     LOCAL oPlain typed as PlainLedger117;
//   - Maybe117 returns Self on one path and NIL on the other, which is how
//     Harbour says a constructor failed (33 device-class Init()s in EasiPOS
//     do it): still the class, since a class-typed C# return takes null,
//     and the caller's == NIL test reads as it did;
//   - Mixed117 returns Self and a string, so it is no identity: dynamic.

#include "hbclass.ch"

CLASS Ledger117
   VAR cName INIT ""
   VAR nTotal INIT 0
   METHOD New( cName )
   METHOD Add( nAmount )
   METHOD Describe()
   METHOD Maybe117( lWant )
   METHOD Mixed117( lWant )
ENDCLASS

METHOD New( cName ) CLASS Ledger117
   ::cName := cName
   RETURN Self

METHOD Add( nAmount ) CLASS Ledger117
   ::nTotal += nAmount
   RETURN Self

METHOD Describe() CLASS Ledger117
   RETURN ::cName + " " + hb_ntos( ::nTotal )

METHOD Maybe117( lWant ) CLASS Ledger117
   IF lWant
      RETURN Self
   ENDIF
   RETURN NIL

METHOD Mixed117( lWant ) CLASS Ledger117
   IF lWant
      RETURN Self
   ENDIF
   RETURN "gone"

CLASS TaxLedger117 INHERIT Ledger117
   VAR nFee INIT 0
   METHOD New( cName, nFee )
   METHOD Add( nAmount )
ENDCLASS

METHOD New( cName, nFee ) CLASS TaxLedger117
   ::Super:New( cName )
   ::nFee := nFee
   RETURN Self

METHOD Add( nAmount ) CLASS TaxLedger117
   ::Super:Add( nAmount + ::nFee )
   RETURN Self

CLASS PlainLedger117 INHERIT Ledger117
ENDCLASS

PROCEDURE Main()

   LOCAL oLedger := Ledger117():New( "cash" )
   LOCAL oTax := TaxLedger117():New( "vat", 3 )
   LOCAL oPlain := PlainLedger117():New( "plain" )

   ? "chain:", oLedger:Add( 5 ):Add( 7 ):Describe()
   ? "tax:", oTax:Add( 100 ):Add( 10 ):Describe()
   ? "inherited New:", oPlain:Add( 2 ):Describe()
   ? "maybe:", ValType( oLedger:Maybe117( .T. ) ), ValType( oLedger:Maybe117( .F. ) )
   ? "maybe nil:", oLedger:Maybe117( .F. ) == NIL, oLedger:Maybe117( .T. ):Describe()
   ? "mixed:", ValType( oLedger:Mixed117( .T. ) ), oLedger:Mixed117( .F. )

   RETURN
