// Test 171: a method declares its return type on its own line, before its
// class: METHOD M() AS <type> CLASS X. The CLASS of `AS CLASS Y` belongs to
// the type, not to the method, and the type is the method's as one declared
// in the class body is. Each method here returns an element of a plain
// array, which nothing else could type.

#include "hbclass.ch"
#include "../include/astype.ch"

CLASS Coin171

   VAR cName AS STRING INIT ""
   VAR iCents AS INTEGER INIT 0

   METHOD New( cName, iCents ) CONSTRUCTOR

ENDCLASS

METHOD New( cName, iCents ) CLASS Coin171

   ::cName := cName
   ::iCents := iCents

RETURN Self

CLASS Purse171

   VAR aThings INIT {}

   METHOD Fill()
   METHOD FirstCoin()
   METHOD CoinCount()
   METHOD Label()
   METHOD Describe171()

ENDCLASS

METHOD Fill() CLASS Purse171

   ::aThings := { Coin171():New( "dime", 10 ), 2, "purse" }

RETURN Self

METHOD FirstCoin() AS CLASS Coin171 CLASS Purse171

RETURN ::aThings[ 1 ]

METHOD CoinCount() AS INTEGER CLASS Purse171

RETURN ::aThings[ 2 ]

METHOD Label() AS STRING CLASS Purse171

RETURN ::aThings[ 3 ]

METHOD Describe171() AS STRING CLASS Purse171

RETURN Upper( ::Label() ) + ": " + ::FirstCoin():cName + " " + hb_ntos( ::CoinCount() + ::FirstCoin():iCents )

PROCEDURE Main()

   LOCAL oPurse171 := Purse171():New():Fill()

   ? oPurse171:FirstCoin():cName
   ? oPurse171:CoinCount() + 1
   ? Len( oPurse171:Label() )
   ? oPurse171:Describe171()

RETURN
