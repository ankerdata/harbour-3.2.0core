/*
 * Our tests for family 13, the encodings and codecs, which hbtest does
 * not cover at all. EasiPOS speaks JSON to EasiWin and to the BOH server
 * (hb_jsonEncode 13 calls in the product, 43 in the test build,
 * hb_jsonDecode 10 / 11), compresses what it sends to EasiSvr
 * (hb_ZCompress / hb_ZUncompress), base64s bitmaps and card payloads,
 * MD5s a hotel payload, and runs two regular expressions. Every text
 * here is the text those protocols carry, so it is pinned exactly.
 * Written as hbtest writes its assertions; Harbour runs them first, so
 * every expected value is Harbour's own.
 */

#include "rt_main.ch"

REQUEST HB_CODEPAGE_DE850

PROCEDURE Main_CODECS()

   /* built at run time: the compiler folds Chr() and hb_BChar() of a
      constant, and Len() of what it folded, to bytes */
   LOCAL nE := 233
   LOCAL cE := hb_utf8Chr( nE )
   LOCAL cEuro := hb_utf8Chr( 0x20AC )
   LOCAL cByte := hb_BChar( nE - 1 + 18 )
   LOCAL cMix := "a" + cE + cByte + "z"
   LOCAL hOrder := { "Type" => "SALE", "Qty" => 2, "Price" => 19.95, "Paid" => .F. }
   LOCAL cBytes := Chr( 0 ) + Chr( 9 ) + Chr( 10 ) + Chr( 13 ) + Chr( 34 ) + Chr( 92 ) + Chr( 200 )

   /* --- JSON: the scalars, exactly as the protocol carries them --- */
   HBTEST hb_jsonEncode( "plain" )                   IS '"plain"'
   HBTEST hb_jsonEncode( "" )                        IS '""'
   HBTEST hb_jsonEncode( 42 )                        IS "42"
   HBTEST hb_jsonEncode( -7 )                        IS "-7"
   HBTEST hb_jsonEncode( 19.95 )                     IS "19.95"
   HBTEST hb_jsonEncode( 2.50 )                      IS "2.50"
   HBTEST hb_jsonEncode( .T. )                       IS "true"
   HBTEST hb_jsonEncode( .F. )                       IS "false"
   HBTEST hb_jsonEncode( NIL )                       IS "null"
   HBTEST hb_jsonEncode( hb_SToD( "20260922" ) )     IS '"20260922"'

   /* a string is bytes: control characters escape, the high half rides
      through as it stands (the caller UTF-8s the whole message) */
   HBTEST hb_jsonEncode( cBytes )                    IS '"\u0000\t\n\r\"\\' + Chr( 200 ) + '"'

   /* --- JSON: arrays and hashes, keys in the order they were added --- */
   HBTEST hb_jsonEncode( {} )                        IS "[]"
   HBTEST hb_jsonEncode( { 1, "two", .T., NIL } )    IS '[1,"two",true,null]'
   HBTEST hb_jsonEncode( { => } )                    IS "{}"
   HBTEST hb_jsonEncode( hOrder )                    IS '{"Type":"SALE","Qty":2,"Price":19.95,"Paid":false}'
   HBTEST hb_jsonEncode( { "a" => { 1, 2 }, "b" => { "c" => NIL } } ) IS '{"a":[1,2],"b":{"c":null}}'

   /* --- JSON: the human-readable form the trace writes, which ends in
          an end of line of its own --- */
   HBTEST hb_jsonEncode( { 1, 2 }, .T. )             IS "[" + hb_eol() + "  1," + hb_eol() + "  2" + hb_eol() + "]" + hb_eol()

   /* --- JSON: decode --- */
   HBTEST hb_jsonDecode( '"plain"' )                 IS "plain"
   HBTEST hb_jsonDecode( "42" )                      IS 42
   HBTEST hb_jsonDecode( "19.95" )                   IS 19.95
   HBTEST hb_jsonDecode( "true" )                    IS .T.
   HBTEST hb_jsonDecode( "null" )                    IS NIL
   HBTEST ValType( hb_jsonDecode( "[1,2,3]" ) )      IS "A"
   HBTEST Len( hb_jsonDecode( "[1,2,3]" ) )          IS 3
   HBTEST hb_jsonDecode( "[1,2,3]" )[ 2 ]            IS 2
   HBTEST ValType( hb_jsonDecode( '{"a":1}' ) )      IS "H"
   HBTEST hb_jsonDecode( '{"a":1}' )[ "a" ]          IS 1
   HBTEST hb_jsonDecode( '"A\n"' )              IS "A" + Chr( 10 )
   HBTEST hb_jsonDecode( "not json" )                IS NIL
   HBTEST hb_jsonDecode( "" )                        IS NIL

   /* what a round trip of a whole message keeps */
   HBTEST RoundTrip( hOrder )                        IS "SALE 2 19.95 .F."
   HBTEST DecodeLen( hb_jsonEncode( hOrder ) )       IS 4

   /* --- base64, the bitmaps and card payloads --- */
   HBTEST hb_base64Encode( "" )                      IS ""
   HBTEST hb_base64Encode( "f" )                     IS "Zg=="
   HBTEST hb_base64Encode( "fo" )                    IS "Zm8="
   HBTEST hb_base64Encode( "foo" )                   IS "Zm9v"
   HBTEST hb_base64Encode( "EasiPOS" )               IS "RWFzaVBPUw=="
   HBTEST hb_base64Encode( Chr( 0 ) + Chr( 255 ) )   IS "AP8="

   /* --- MD5, the hotel payload signature --- */
   HBTEST hb_MD5( "" )                               IS "d41d8cd98f00b204e9800998ecf8427e"
   HBTEST hb_MD5( "abc" )                            IS "900150983cd24fb0d6963f7d28e17f72"

   /* --- zlib, what goes to EasiSvr --- */
   HBTEST ZRoundTrip( "the quick brown fox" )        IS .T.
   HBTEST ZRoundTrip( Replicate( "AB", 5000 ) )      IS .T.
   HBTEST ZRoundTrip( cBytes )                       IS .T.
   HBTEST ZSmaller( Replicate( "AB", 5000 ) )        IS .T.
   HBTEST hb_ZUncompress( hb_ZCompress( "" ) )       IS ""
   HBTEST ValType( hb_ZLibVersion() )                IS "C"

   /* --- text and bytes: the program runs in UTF8EX, where text is UTF-8,
          Len() counts characters and the hb_B* functions bytes. A byte
          that is not UTF-8 is one character. --- */
   HBTEST hb_cdpSelect()                             IS "UTF8EX"
   HBTEST hb_cdpIsUTF8()                             IS .T.
   HBTEST hb_cdpIsUTF8( "DE850" )                    IS .F.
   HBTEST Len( cE )                                  IS 1
   HBTEST hb_BLen( cE )                              IS 2
   HBTEST Len( cEuro )                               IS 1
   HBTEST hb_BLen( cEuro )                           IS 3
   HBTEST Len( cByte )                               IS 1
   HBTEST hb_BLen( cByte )                           IS 1
   HBTEST Len( cMix )                                IS 4
   HBTEST hb_BLen( cMix )                            IS 5
   HBTEST hb_StrToHex( cMix )                        IS "61C3A9FA7A"
   HBTEST hb_StrToHex( SubStr( cMix, 2, 1 ) )        IS "C3A9"
   HBTEST hb_StrToHex( SubStr( cMix, 3, 1 ) )        IS "FA"
   HBTEST hb_StrToHex( Left( cMix, 2 ) )             IS "61C3A9"
   HBTEST hb_StrToHex( Right( cMix, 2 ) )            IS "FA7A"
   HBTEST hb_StrToHex( hb_BSubStr( cMix, 2, 1 ) )    IS "C3"
   HBTEST hb_StrToHex( hb_BSubStr( cMix, 3 ) )       IS "A9FA7A"
   HBTEST hb_StrToHex( hb_BSubStr( cMix, -2 ) )      IS "FA7A"
   HBTEST hb_StrToHex( hb_BLeft( cMix, 2 ) )         IS "61C3"
   HBTEST hb_StrToHex( hb_BLeft( cMix, 99 ) )        IS "61C3A9FA7A"
   HBTEST hb_StrToHex( hb_BRight( cMix, 2 ) )        IS "FA7A"
   HBTEST hb_BRight( cMix, 0 )                       IS ""
   HBTEST hb_BLeft( cMix, 3 ) == "a" + cE            IS .T.
   HBTEST At( cByte, cMix )                          IS 3
   HBTEST hb_BAt( cByte, cMix )                      IS 4
   HBTEST At( "z", cMix )                            IS 4
   HBTEST hb_BAt( "z", cMix )                        IS 5
   HBTEST hb_BAt( "a", "banana", 3 )                 IS 4
   HBTEST hb_BAt( "a", "banana", 3, 3 )              IS 0
   HBTEST hb_BAt( "a", "banana", 3, 4 )              IS 4
   HBTEST hb_BAt( "", cMix )                         IS 0
   HBTEST hb_BCode( cE )                             IS 195
   HBTEST hb_BCode( "" )                             IS 0
   HBTEST Asc( cE )                                  IS 233
   HBTEST Asc( cEuro )                               IS 8364
   HBTEST Asc( cByte )                               IS 65533
   HBTEST hb_StrToHex( Chr( nE ) )                   IS "C3A9"
   HBTEST hb_StrToHex( Chr( nE + 23 ) )              IS "C480"
   HBTEST hb_StrToHex( hb_BChar( nE ) )              IS "E9"
   HBTEST hb_StrToHex( hb_BChar( nE + 23 ) )         IS "00"
   HBTEST hb_StrToHex( hb_utf8Chr( 65 ) )            IS "41"
   HBTEST hb_StrToHex( PadR( cE, 3 ) )               IS "C3A92020"
   HBTEST hb_StrToHex( PadR( cByte, 3 ) )            IS "FA2020"
   HBTEST hb_StrToHex( Replicate( cE, 2 ) )          IS "C3A9C3A9"
   HBTEST hb_StrToHex( Stuff( cMix, 2, 1, "X" ) )    IS "6158FA7A"
   HBTEST hb_StrToHex( StrTran( cMix, cE, "X" ) )    IS "6158FA7A"
   HBTEST hb_StrToHex( Upper( cE ) )                 IS "C389"
   HBTEST hb_StrToHex( Lower( Upper( cE ) ) )        IS "C3A9"
   HBTEST hb_StrToHex( Upper( hb_utf8Chr( 0xDF ) ) ) IS "C39F"
   HBTEST hb_StrToHex( Upper( hb_utf8Chr( 0xFF ) ) ) IS "C5B8"
   HBTEST hb_StrToHex( Lower( hb_utf8Chr( 0x130 ) ) ) IS "69"
   HBTEST hb_StrToHex( Upper( hb_utf8Chr( 0x131 ) ) ) IS "49"
   HBTEST hb_StrToHex( AllTrim( " " + cE + " " ) )   IS "C3A9"
   HBTEST hb_StrIsUTF8( cE )                         IS .T.
   HBTEST hb_StrIsUTF8( cByte )                      IS .F.
   HBTEST hb_StrIsUTF8( cMix )                       IS .F.
   HBTEST hb_StrIsUTF8( "abc" )                      IS .F.
   HBTEST hb_StrIsUTF8( "" )                         IS .F.
   HBTEST hb_StrToHex( hb_HexToStr( "C3A9FA" ) )     IS "C3A9FA"
   HBTEST Len( hb_HexToStr( "C3A9FA" ) )             IS 2
   HBTEST hb_base64Encode( cE )                      IS "w6k="
   HBTEST hb_StrToHex( L2Bin( 250 ) )                IS "FA000000"
   HBTEST Bin2L( L2Bin( 250 ) )                      IS 250
   HBTEST Bin2L( cByte + hb_BChar( 0 ) + hb_BChar( 0 ) + hb_BChar( 0 ) ) IS 250
   HBTEST hb_StrToHex( hb_jsonEncode( cE ) )         IS "22C3A922"
   HBTEST hb_StrToHex( hb_jsonDecode( '"\u00e9"' ) ) IS "C3A9"

   /* --- order: UTF8EX's sort table, not the bytes. Space, punctuation,
          digits, then the letters as aAbB..., an accented letter beside
          its plain one; a byte that is not text below the letters. --- */
   HBTEST "a" < "A"                                  IS .T.
   HBTEST "A" < "b"                                  IS .T.
   HBTEST "B" < "a"                                  IS .F.
   HBTEST "Z" < "a"                                  IS .F.
   HBTEST "9" < "A"                                  IS .T.
   HBTEST "-" < "0"                                  IS .T.
   HBTEST " " < "-"                                  IS .T.
   HBTEST cE < "f"                                   IS .T.
   HBTEST cE > "e"                                   IS .T.
   HBTEST cE > "z"                                   IS .F.
   HBTEST cEuro < "0"                                IS .T.
   HBTEST cByte < "A"                                IS .T.
   HBTEST cByte > "9"                                IS .F.
   HBTEST "abc" < "abd"                              IS .T.
   HBTEST "abc" < "ABD"                              IS .T.
   HBTEST "Lisbon" > "Lis"                           IS .T.
   HBTEST ASort( { "b", "B", "a", "A", cE, "e", "1", " " } )[ 1 ] IS " "
   HBTEST ASort( { "b", "B", "a", "A", cE, "e", "1", " " } )[ 3 ] IS "a"
   HBTEST ASort( { "b", "B", "a", "A", cE, "e", "1", " " } )[ 4 ] IS "A"
   HBTEST hb_StrToHex( ASort( { "b", "B", "a", "A", cE, "e", "1", " " } )[ 8 ] ) IS "C3A9"

   /* --- another code page: what a character is there, '?' for one it has
          not and for a byte that was not text --- */
   HBTEST hb_StrToHex( hb_Translate( cE, "UTF8EX", "DE850" ) )    IS "82"
   HBTEST hb_StrToHex( hb_Translate( cEuro, "UTF8EX", "DE850" ) ) IS "3F"
   HBTEST hb_StrToHex( hb_Translate( cMix, "UTF8EX", "DE850" ) )  IS "61823F7A"
   HBTEST hb_StrToHex( hb_Translate( hb_BChar( 130 ), "DE850", "UTF8EX" ) ) IS "C3A9"
   HBTEST hb_StrToHex( hb_Translate( cE, hb_cdpSelect(), "DE850" ) ) IS "82"
   HBTEST hb_StrToHex( hb_StrToUTF8( cE ) )          IS "C3A9"
   HBTEST hb_StrToHex( hb_UTF8ToStr( cE ) )          IS "C3A9"
   HBTEST hb_StrToHex( hb_StrToUTF8( hb_BChar( 130 ), "DE850" ) ) IS "C3A9"
   HBTEST hb_StrToHex( hb_UTF8ToStr( cE, "DE850" ) ) IS "82"
   HBTEST hb_StrToUTF8( "plain" )                    IS "plain"
   HBTEST hb_UTF8ToStr( "plain" )                    IS "plain"

   /* --- the two regular expressions the product runs --- */
   HBTEST Len( hb_regex( "([0-9\.]+)%", "Discount 12.5% applied" ) ) IS 2
   HBTEST hb_regex( "([0-9\.]+)%", "Discount 12.5% applied" )[ 1 ]   IS "12.5%"
   HBTEST hb_regex( "([0-9\.]+)%", "Discount 12.5% applied" )[ 2 ]   IS "12.5"
   HBTEST Len( hb_regex( "([0-9\.]+)%", "nothing here" ) )           IS 0
   HBTEST Len( hb_regex( hb_regexComp( "[A-Z]+" ), "abc DEF ghi" ) ) IS 1
   HBTEST hb_regex( hb_regexComp( "[A-Z]+" ), "abc DEF ghi" )[ 1 ]   IS "DEF"

   /* --- JSON: a decoded array grows like any other, an empty one too
          (the component tests' expected "BufferLines": []) --- */
   HBTEST GrowDecoded( "[]" )                        IS 1
   HBTEST GrowDecoded( "[7]" )                       IS 2

   RETURN

/* A message through the wire and back: what the other end reads. */
STATIC FUNCTION RoundTrip( hValue )

   LOCAL hBack := hb_jsonDecode( hb_jsonEncode( hValue ) )

   RETURN hBack[ "Type" ] + " " + hb_ntos( hBack[ "Qty" ] ) + " " + ;
          hb_ntos( hBack[ "Price" ] ) + " " + hb_ValToExp( hBack[ "Paid" ] )

STATIC FUNCTION DecodeLen( cJSON )
   RETURN Len( hb_jsonDecode( cJSON ) )

STATIC FUNCTION ZRoundTrip( cText )
   RETURN hb_ZUncompress( hb_ZCompress( cText ) ) == cText

STATIC FUNCTION ZSmaller( cText )
   RETURN hb_BLen( hb_ZCompress( cText ) ) < hb_BLen( cText )

STATIC FUNCTION GrowDecoded( cJSON )
   LOCAL aValues := hb_jsonDecode( cJSON )
   AAdd( aValues, "x" )
   RETURN Len( aValues )
