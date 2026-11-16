// Test 147: a string literal is text, and a byte that is not text its byte.
//
// A Harbour string is bytes, and EasiPOS runs in the UTF8EX code page: its
// text is UTF-8, Len() and SubStr() count characters and the hb_B*
// functions bytes. A transpiled string is text, a char to a character, so
// a literal's UTF-8 is written as that text. A byte that is not UTF-8 (a
// source still in Windows-1252, a printer's control byte) is the char
// HbRuntime carries a byte in, and is written back as that byte. Chr() is
// the character, of a constant as of a variable (the suite compiles with
// -ku, as EasiPOS does), hb_BChar() the byte; and constant bytes that
// spell a character together are that character. The output is numbers,
// which no console encoding can change.

REQUEST HB_CODEPAGE_UTF8EX

PROCEDURE Main()

   LOCAL cEuro147 := "€/kg = €"
   LOCAL cUmlaut147 := "z�hler"
   LOCAL n147 := 250
   LOCAL cEsc147 := "@"

   hb_cdpSelect( "UTF8EX" )

   ? "euro:", Len( cEuro147 ), Asc( cEuro147 ), Asc( SubStr( cEuro147, 2 ) ), Asc( SubStr( cEuro147, 3 ) )
   ? "cut to 9:", Len( Left( cEuro147, 9 ) ), Asc( Right( Left( cEuro147, 9 ), 1 ) )
   ? "umlaut:", Len( cUmlaut147 ), Asc( SubStr( cUmlaut147, 2 ) )
   ? "escape:", Len( cEsc147 ), Asc( cEsc147 )
   ? "bytes:", hb_BLen( cEuro147 ), hb_BLen( cUmlaut147 ), hb_BCode( hb_BSubStr( cEuro147, 2, 1 ) ), hb_BCode( hb_BSubStr( cUmlaut147, 2, 1 ) )
   ? "chr:", hb_BLen( Chr( 250 ) ), hb_BLen( Chr( n147 ) ), hb_BLen( hb_BChar( n147 ) ), Asc( Chr( n147 ) )
   ? "joined:", hb_BChar( 0xE2 ) + hb_BChar( 0x82 ) + hb_BChar( 0xAC ) == Left( cEuro147, 1 ), Len( hb_BChar( 0xE2 ) + hb_BChar( 0x82 ) + hb_BChar( 0xAC ) )
   ? "cut:", hb_BLen( hb_BLeft( cEuro147, 2 ) ), hb_BCode( hb_BLeft( cEuro147, 2 ) ), hb_BLeft( cEuro147, 3 ) == Left( cEuro147, 1 )
   ? "upper:", Asc( Upper( SubStr( "z" + hb_utf8Chr( 228 ), 2 ) ) ), hb_StrIsUTF8( cEuro147 ), hb_StrIsUTF8( cUmlaut147 ), hb_StrIsUTF8( "abc" )

   RETURN
