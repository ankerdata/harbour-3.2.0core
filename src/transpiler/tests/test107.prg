// Test 107: `#pragma BEGINCSHARP` inside a routine — C# statements in place.
//
// test105's blocks are file scope: type declarations, flushed at namespace
// level wherever they stand. A block whose text is NOT a type declaration
// and that stands inside a routine is C# statements instead: it lands in
// the method body where it stands, re-indented. That lets a routine carry
// its Harbour (9.0 keeps running it) and its C# side by side, one guarded
// stretch at a time:
//
//    #ifndef __HB_TRANSPILER__
//       <Harbour>
//    #else
//    #pragma BEGINCSHARP
//       <C#>
//    #pragma ENDCSHARP
//    #endif
//
// Position alone cannot tell the two kinds apart — Harbour has no end of
// routine, so a block after a routine's last statement is "inside" it
// until the next routine begins — but C# can: only type declarations are
// legal at namespace level, and none inside a method. A block of nothing
// but comments stays file scope (test105 keeps one in Main()).
//
// The block sees the method's emitted C# names: members bare (`nCount`),
// parameters and locals as declared. A block ending in `return …;` or
// `throw …;` never falls through, so the RETURN Harbour needs after the
// guard is dropped from the C# as unreachable (CS0162) — `Polarity` and
// the method `Tenfold` below.
// Every case prints the same from both branches. The suite scans all its
// tests into one reftab, so these names are unique across it.
#include "hbclass.ch"

CLASS Meter
   VAR nCount INIT 0
   METHOD Raise( nBy )
   METHOD Tenfold()
ENDCLASS

// a method: the member written as the C# member
METHOD Raise( nBy ) CLASS Meter
#ifndef __HB_TRANSPILER__
   ::nCount += nBy
#else
#pragma BEGINCSHARP
   nCount += nBy;
#pragma ENDCSHARP
#endif
RETURN Self

// a method whose C# returns: its trailing RETURN is unreachable in C#
METHOD Tenfold() CLASS Meter
#ifndef __HB_TRANSPILER__
   RETURN ::nCount * 10
#else
#pragma BEGINCSHARP
   return nCount * 10;
#pragma ENDCSHARP
#endif
RETURN 0

// a function: a local assigned in C#, read by the Harbour RETURN
FUNCTION Twice( nValue )
   LOCAL nResult
#ifndef __HB_TRANSPILER__
   nResult := nValue * 2
#else
#pragma BEGINCSHARP
   nResult = nValue * 2;
#pragma ENDCSHARP
#endif
RETURN nResult

// C# that returns: the RETURN after the guard is unreachable there
FUNCTION Polarity( nValue )
#ifndef __HB_TRANSPILER__
   IF nValue < 0
      RETURN -1
   ENDIF
   RETURN 1
#else
#pragma BEGINCSHARP
   return nValue < 0 ? -1 : 1;
#pragma ENDCSHARP
#endif
RETURN 0

// a block inside an IF: it lands in the IF's body
FUNCTION Grade( nValue )
   LOCAL cText := "zero"
   IF nValue > 0
#ifndef __HB_TRANSPILER__
      cText := "positive"
#else
#pragma BEGINCSHARP
      cText = "positive";
#pragma ENDCSHARP
#endif
   ENDIF
RETURN cText

PROCEDURE Main()
   LOCAL oCounter := Meter():New()
   oCounter:Raise( 2 )
   oCounter:Raise( 3 )
   ? oCounter:nCount
   ? oCounter:Tenfold()
   ? Twice( 21 )
   ? Polarity( -5 )
   ? Polarity( 5 )
   ? Grade( 1 )
   ? Grade( 0 )
RETURN
