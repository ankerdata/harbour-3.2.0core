// Test 172: a function the program defines keeps its own return type.
//
// hbfuncs.tab types the functions the transpiler knows from outside the
// program, hbnf's FT_Elapsed() among them: an array. A program that
// defines FT_Elapsed() itself calls its own (test108), and its own return
// type is the call's: here a string and, through a second function, a
// whole number, where the table's ARRAY made the caller's local a list.

PROCEDURE Main()

   LOCAL cSpan := FT_Elapsed( 3, 5 )

   ? Upper( cSpan )
   ? Len( FT_Elapsed( 1, 12 ) )
   ? SpanDays172( 3, 5 ) + 1

RETURN

FUNCTION FT_Elapsed( iFrom, iTo )
RETURN hb_ntos( iTo - iFrom ) + " days"

FUNCTION SpanDays172( iFrom, iTo )
RETURN Val( FT_Elapsed( iFrom, iTo ) )
