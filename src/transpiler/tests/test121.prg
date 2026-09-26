// Test 121: `i` names beyond locals, and what returns long.
//
// test120 pinned `i` locals, statics, parameters and returns. This pins the
// rest of where an `i` name can stand - a class member with and without an
// INIT, written by the compound operators; an `@iX` argument to an `n`
// parameter and an `@nX` argument to an `i` parameter - and what a routine
// returning long does to the arithmetic around it: a division of a call
// that returns long (an `i` local, a bit operation) is still Harbour's
// float division, and an `n` local initialised from a bit operation still
// takes a fraction later, as one initialised from an integral #define does. A bit setter, RETURN IIF( lx, hb_bitOr(...),
// hb_bitAnd(...) ), returns long into an AS INTEGER member. A decimal
// written into an `i` member is cast, from inside the class or outside it.
// A member is written bare in C# unless a parameter, a local or a codeblock
// parameter of the same name makes `this.` necessary (Alex), in a method
// body and in an INLINE one.

#include "hbclass.ch"

#define FLAG121_BIT  2

CLASS Tally121
   VAR iCount INIT 0
   VAR iTotal
   VAR nFlags AS INTEGER INIT 0
   VAR cName INIT ""
   METHOD Add121( iStep )
   METHOD Count121()
   METHOD Rename121( cName )
   METHOD Spread121( aSteps )
   METHOD Label121() INLINE ::cName + "=" + hb_ntos( ::iCount )
   METHOD Relabel121( cName ) INLINE ::cName := cName
ENDCLASS

METHOD Add121( iStep ) CLASS Tally121
   ::iCount += iStep
   ::iTotal := ::iCount * 2
   ::iTotal := Len( "abc" ) + ::iTotal
   ::nFlags := SetFlag121( ::nFlags, ::iCount > 3 )
   RETURN Self

METHOD Count121() CLASS Tally121
   RETURN ::iCount

METHOD Rename121( cName ) CLASS Tally121
   // a parameter named as the member: `this.` is what tells them apart
   ::cName := cName
   RETURN Self

METHOD Spread121( aSteps ) CLASS Tally121
   // ...and a codeblock parameter named as one
   AEval( aSteps, {| iCount | ::iTotal += iCount + ::iCount } )
   RETURN Self

PROCEDURE Main()

   LOCAL oTally := Tally121():New()
   LOCAL iWhole := 7
   LOCAL nPlain := 9
   LOCAL nMask := hb_bitAnd( 7, 3 )
   LOCAL nHalf := FLAG121_BIT

   oTally:Add121( 2 )
   oTally:Add121( 3 )
   ? "tally:", oTally:Count121(), oTally:iTotal, oTally:nFlags

   Bump121( @iWhole )
   Halve121( @nPlain )
   ? "by ref:", iWhole, nPlain

   ? "divided:", Str( Thrice121( 7 ) / 4, 6, 2 ), Str( hb_bitAnd( 7, 6 ) / 4, 6, 2 ), Str( oTally:Count121() / 2, 6, 2 )
   nMask := nMask / 2
   nHalf := nHalf / 4
   ? "mask:", Str( nMask, 6, 2 ), Str( nHalf, 6, 2 )

   oTally:iTotal := nPlain
   ? "member:", oTally:iTotal

   oTally:Rename121( "t" )
   oTally:Spread121( { 1, 2 } )
   ? "names:", oTally:Label121(), oTally:iTotal
   oTally:Relabel121( "u" )
   ? "inline:", oTally:Label121()

   RETURN

PROCEDURE Bump121( nValue )
   nValue += 1
   RETURN

PROCEDURE Halve121( iValue )
   iValue := Int( iValue / 2 )
   RETURN

FUNCTION Thrice121( iValue )
   LOCAL iResult := iValue * 3
   RETURN iResult

FUNCTION SetFlag121( nFlags, lx )
   RETURN IIF( lx, hb_bitOr( nFlags, FLAG121_BIT ), hb_bitAnd( nFlags, hb_bitNot( FLAG121_BIT ) ) )
