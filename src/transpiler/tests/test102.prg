// Test 102: a file-static variadic function keeps its named parameters,
// and PCount() inside a widened function is the argument count.
//
// A function that reads PCount() is flagged variadic in the reftab and
// its C# signature widens to `params dynamic[] hbva`, the named
// parameters re-bound from the array at the top of the body. That
// re-bind looked the row up again by the bare name, and a file-static
// function's row is `file::func`, so trace.prg's PlatformDebug got the
// widened signature and none of its ten names (CS0103 ×10). PCount()
// itself emitted HbRuntime.PCount(), a stub returning 0 — C# has no
// caller argument count — so the ladder it guards never ran; inside a
// widened function it is now `hbva.Length`. Muster is the file-static
// shape, Convoy the public one.
STATIC FUNCTION Muster( cA, cB, cC )
   LOCAL cOut := "muster=" + LTrim( Str( PCount() ) ) + ":" + cA
   IF PCount() >= 2
      cOut += "," + cB
      IF PCount() >= 3
         cOut += "," + cC
      ENDIF
   ENDIF
RETURN cOut

FUNCTION Convoy( cFirst, cSecond )
RETURN "convoy=" + LTrim( Str( PCount() ) ) + ":" + ;
       IIf( PCount() >= 2, cSecond, cFirst )

PROCEDURE Main()
   ? Muster( "one" )
   ? Muster( "one", "two" )
   ? Muster( "one", "two", "three" )
   ? Convoy( "solo" )
   ? Convoy( "a", "b" )
RETURN
