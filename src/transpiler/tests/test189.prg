// Test 189: a shape's key that takes anything, `Key AS USUAL`. Wrap189 is
// an envelope: what it is of is text, and what it carries is whatever that
// says: a shape, a list, a number, text, nothing. The key is a field that
// holds any of them, written as JSON as what it holds and read back by key;
// a function returns the envelope, built from its parameter, and an
// OPTIONAL key after it is still the last on the wire.

#include "../include/astype.ch"

SHAPE Note189( Title AS STRING, Count AS INTEGER )
SHAPE Wrap189( Kind AS STRING, Body AS USUAL, Serial AS INTEGER OPTIONAL )

PROCEDURE Main()

   LOCAL hcNote AS SHAPE Note189 := { "Title" => "first", "Count" => 2 }
   LOCAL hcWrap AS SHAPE Wrap189
   LOCAL acWords AS ARRAY OF STRING := { "one", "two" }

   hcWrap := MakeWrap189( "note", hcNote )
   ? hb_jsonEncode( hcWrap )
   ? Len( hcWrap ), hb_HGet( hcWrap[ "Body" ], "Title" ), hb_HHasKey( hcWrap, "Serial" )

   hcWrap := MakeWrap189( "words", acWords )
   ? hb_jsonEncode( hcWrap ), Len( hcWrap[ "Body" ] )

   hcWrap := MakeWrap189( "number", 12.5 )
   ? hb_jsonEncode( hcWrap ), hcWrap[ "Body" ] * 2

   hcWrap := MakeWrap189( "text", "plain" )
   ? hb_jsonEncode( hcWrap ), Upper( hcWrap[ "Body" ] )

   hcWrap := MakeWrap189( "nothing" )
   ? hb_jsonEncode( hcWrap ), hcWrap[ "Body" ] == NIL

   hcWrap[ "Body" ] := .T.
   hcWrap[ "Serial" ] := 7
   ? hb_jsonEncode( hcWrap ), Len( hcWrap ), hb_HKeyAt( hcWrap, 3 )

RETURN

FUNCTION MakeWrap189( cKind, xBody ) AS SHAPE Wrap189

   LOCAL hcWrap AS SHAPE Wrap189 := { "Kind" => cKind, "Body" => xBody }

RETURN hcWrap
