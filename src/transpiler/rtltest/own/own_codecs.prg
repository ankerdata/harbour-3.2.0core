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

PROCEDURE Main_CODECS()

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

   /* --- UTF-8, the only conversion (a char is a byte) --- */
   HBTEST hb_StrToUTF8( "plain" )                    IS "plain"
   /* A byte of the high half is Latin-1 here — two bytes — where Harbour
      maps it through the process codepage, which is three (ruled
      divergent: HbRuntime has no codepage, and a char is a byte) */
   HBTEST hb_BLen( hb_StrToUTF8( Chr( 200 ) ) )      IS 2
   HBTEST hb_UTF8ToStr( hb_StrToUTF8( Chr( 200 ) ) ) IS Chr( 200 )
   HBTEST hb_UTF8ToStr( "plain" )                    IS "plain"

   /* --- the two regular expressions the product runs --- */
   HBTEST Len( hb_regex( "([0-9\.]+)%", "Discount 12.5% applied" ) ) IS 2
   HBTEST hb_regex( "([0-9\.]+)%", "Discount 12.5% applied" )[ 1 ]   IS "12.5%"
   HBTEST hb_regex( "([0-9\.]+)%", "Discount 12.5% applied" )[ 2 ]   IS "12.5"
   HBTEST Len( hb_regex( "([0-9\.]+)%", "nothing here" ) )           IS 0
   HBTEST Len( hb_regex( hb_regexComp( "[A-Z]+" ), "abc DEF ghi" ) ) IS 1
   HBTEST hb_regex( hb_regexComp( "[A-Z]+" ), "abc DEF ghi" )[ 1 ]   IS "DEF"

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
