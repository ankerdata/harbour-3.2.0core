// Test 181: `#pragma TEXTHIDDEN`. A string literal of a statement written
// while it is on is emitted as encoded bytes and decoded when it is used,
// so the text is not in the assembly's string table; a statement after
// the pragma is switched off keeps its literal. The program's output is
// the same either way.

PROCEDURE Main()

   LOCAL cPlain := "plain text 181"

   ? HiddenKey181()
   ? Len( HiddenKey181() )
   ? HiddenJoin181( "-" )
   ? HiddenAccent181()
   ? cPlain

   RETURN

FUNCTION HiddenKey181()

#pragma TEXTHIDDEN=1
   RETURN "Sesame!181"
#pragma TEXTHIDDEN=0

FUNCTION HiddenJoin181( cSep )

   LOCAL cLeft
   LOCAL cRight := "shown"

#pragma TEXTHIDDEN=1
   cLeft := "kept" + cSep + "back"
#pragma TEXTHIDDEN=0

   RETURN cLeft + cSep + cRight

FUNCTION HiddenAccent181()

#pragma TEXTHIDDEN=1
   RETURN "Zähler €"
#pragma TEXTHIDDEN=0
