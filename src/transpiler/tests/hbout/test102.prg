#include "astype.ch"
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
STATIC FUNCTION Muster( cA AS STRING, cB AS STRING, cC AS STRING ) AS STRING
   LOCAL cOut := "muster=" + LTrim(Str(PCount())) + ":" + cA AS STRING
   IF PCount() >= 2
      cOut += "," + cB
      IF PCount() >= 3
         cOut += "," + cC
      ENDIF
   ENDIF

RETURN cOut

FUNCTION Convoy( cFirst AS STRING, cSecond AS STRING )

RETURN "convoy=" + LTrim(Str(PCount())) + ":" + IIF(PCount() >= 2, cSecond, cFirst)

PROCEDURE Main()
   QOut(Muster("one"))
   QOut(Muster("one", "two"))
   QOut(Muster("one", "two", "three"))
   QOut(Convoy("solo"))
   QOut(Convoy("a", "b"))
RETURN
