#include "astype.ch"
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

   DATA cName AS STRING INIT ""
   DATA nTotal AS NUMERIC INIT 0
   METHOD New( cName )
   METHOD Add( nAmount )
   METHOD Describe()
   METHOD Maybe117( lWant )
   METHOD Mixed117( lWant )

ENDCLASS

CLASS TaxLedger117 INHERIT Ledger117

   DATA nFee AS NUMERIC INIT 0
   METHOD New( cName, nFee )
   METHOD Add( nAmount )

ENDCLASS

CLASS PlainLedger117 INHERIT Ledger117


ENDCLASS

METHOD New( cName AS STRING ) AS OBJECT CLASS Ledger117
   ::cName := cName
RETURN Self

METHOD Add( nAmount AS NUMERIC ) AS OBJECT CLASS Ledger117
   ::nTotal += nAmount
RETURN Self

METHOD Describe() AS STRING CLASS Ledger117
RETURN ::cName + " " + hb_ntos(::nTotal)

METHOD Maybe117( lWant AS LOGICAL ) AS USUAL CLASS Ledger117
   IF lWant
   RETURN Self
   ENDIF

RETURN NIL

METHOD Mixed117( lWant AS LOGICAL ) AS USUAL CLASS Ledger117
   IF lWant
   RETURN Self
   ENDIF

RETURN "gone"

METHOD New( cName AS STRING, nFee AS NUMERIC ) AS OBJECT CLASS TaxLedger117
   ::Super:New(cName)
   ::nFee := nFee
RETURN Self

METHOD Add( nAmount AS NUMERIC ) AS OBJECT CLASS TaxLedger117
   ::Super:Add(nAmount + ::nFee)
RETURN Self

PROCEDURE Main()

   LOCAL oLedger := Ledger117():New("cash") AS OBJECT
   LOCAL oTax := TaxLedger117():New("vat", 3) AS OBJECT
   LOCAL oPlain := PlainLedger117():New("plain") AS OBJECT

   QOut("chain:", oLedger:Add(5):Add(7):Describe())
   QOut("tax:", oTax:Add(100):Add(10):Describe())
   QOut("inherited New:", oPlain:Add(2):Describe())
   QOut("maybe:", ValType(oLedger:Maybe117(.T.)), ValType(oLedger:Maybe117(.F.)))
   QOut("maybe nil:", oLedger:Maybe117(.F.) == NIL, oLedger:Maybe117(.T.):Describe())
   QOut("mixed:", ValType(oLedger:Mixed117(.T.)), oLedger:Mixed117(.F.))

RETURN
