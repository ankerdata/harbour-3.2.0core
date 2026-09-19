/*
 * Our tests for the string functions hbtest does not cover, or covers
 * only partly: the forms EasiPOS uses and the edges Harbour's C code
 * decides. Written as hbtest writes its assertions; Harbour runs them
 * first, so every expected value here is Harbour's own.
 */

#include "rt_main.ch"

PROCEDURE Main_STRINGS()

   LOCAL hRepl := { "/" => "\", "a" => "A" }

   /* hb_ATokens: EasiPOS splits on "," */
   HBTEST Len( hb_ATokens( "a,b,c", "," ) )              IS 3
   HBTEST hb_ATokens( "a,b,c", "," )[ 2 ]                 IS "b"
   HBTEST Len( hb_ATokens( "a,,c", "," ) )               IS 3
   HBTEST hb_ATokens( "a,,c", "," )[ 2 ]                  IS ""
   HBTEST Len( hb_ATokens( ",a,", "," ) )                IS 3
   HBTEST Len( hb_ATokens( "", "," ) )                   IS 1
   HBTEST Len( hb_ATokens( "  one   two  " ) )           IS 2
   HBTEST hb_ATokens( "  one   two  " )[ 2 ]              IS "two"
   HBTEST Len( hb_ATokens( "a::b::c", "::" ) )           IS 3
   HBTEST hb_ATokens( "a::b::c", "::" )[ 3 ]              IS "c"
   HBTEST Len( hb_ATokens( 'x,"a,b",y', ",", .T. ) )     IS 3
   HBTEST hb_ATokens( 'x,"a,b",y', "," , .T. )[ 2 ]       IS '"a,b"'
   HBTEST Len( hb_ATokens( "l1" + Chr( 13 ) + Chr( 10 ) + "l2" + Chr( 10 ) + "l3", .T. ) ) IS 3

   /* hb_StrReplace: EasiPOS passes a hash */
   HBTEST hb_StrReplace( "a/b/c", hRepl )                 IS "A\b\c"
   HBTEST hb_StrReplace( "abcabc", "ab", "xy" )           IS "xycxyc"
   HBTEST hb_StrReplace( "abcabc", "ab", "x" )            IS "xcxc"
   HBTEST hb_StrReplace( "abcabc", "ab" )                 IS "cc"
   HBTEST hb_StrReplace( "one two", { "one", "two" }, { "1", "22" } ) IS "1 22"
   HBTEST hb_StrReplace( "one two", { "one", "two" }, { "1" } )       IS "1 "
   HBTEST hb_StrReplace( "", "a", "b" )                   IS ""

   /* hb_StrFormat */
   HBTEST hb_StrFormat( "line %s: %s", "12", "bad" )      IS "line 12: bad"
   HBTEST hb_StrFormat( "%d items", 42 )                  IS "42 items"
   HBTEST hb_StrFormat( "%5d|", 42 )                      IS "   42|"
   HBTEST hb_StrFormat( "%-5d|", 42 )                     IS "42   |"
   HBTEST hb_StrFormat( "%05d|", -42 )                    IS "-0042|"
   HBTEST hb_StrFormat( "%+d", 7 )                        IS "+7"
   HBTEST hb_StrFormat( "%d", 2.5 )                       IS "3"
   HBTEST hb_StrFormat( "%x %X", 255, 255 )               IS "ff FF"
   HBTEST hb_StrFormat( "%.2f", 3.14159 )                 IS "3.14"
   HBTEST hb_StrFormat( "%8.2f|", -3.14159 )              IS "   -3.14|"
   HBTEST hb_StrFormat( "%f", 1.50 )                      IS "1.50"
   HBTEST hb_StrFormat( "%.3s|%5s|", "abcdef", "ab" )     IS "abc|   ab|"
   HBTEST hb_StrFormat( "%2$s %1$s", "a", "b" )           IS "b a"
   HBTEST hb_StrFormat( "100%% %s", "done" )              IS "100% done"
   HBTEST hb_StrFormat( "%s|%d", "only" )                 IS "only|0"
   HBTEST hb_StrFormat( "%q %s", "x" )                    IS "%q x"
   HBTEST hb_StrFormat( "%c%c", 72, 105 )                 IS "Hi"

   /* MemoLine / MLCount: EasiPOS wraps recipe text and file lists */
   HBTEST MLCount( "one two three four", 9 )              IS 3
   HBTEST MemoLine( "one two three four", 9, 1 )          IS "one two  "
   HBTEST MemoLine( "one two three four", 9, 2 )          IS "three    "
   HBTEST MemoLine( "one two three four", 9, 3 )          IS "four     "
   HBTEST MemoLine( "one two three four", 9, 4 )          IS "         "
   HBTEST MLCount( "a" + Chr( 13 ) + Chr( 10 ) + "b" )    IS 2
   HBTEST MemoLine( "a" + Chr( 13 ) + Chr( 10 ) + "b", 5, 2 ) IS "b    "
   HBTEST MLCount( "abcdefghij", 4 )                      IS 3
   HBTEST MemoLine( "abcdefghij", 4, 3 )                  IS "ij  "
   HBTEST MemoLine( "a" + Chr( 9 ) + "b", 10, 1 )         IS "a   b     "
   HBTEST MLCount( "" )                                   IS 0
   HBTEST MemoLine( "abc", 0, 1 )                         IS ""
   HBTEST MLCount( "a" + Chr( 10 ) + "b", 10 )            IS 1

   /* hex */
   HBTEST hb_StrToHex( "AB" + Chr( 0 ) + Chr( 255 ) )     IS "414200FF"
   HBTEST hb_StrToHex( "ab", "-" )                        IS "61-62"
   HBTEST hb_HexToStr( "414200ff" )                       IS "AB" + Chr( 0 ) + Chr( 255 )
   HBTEST hb_HexToStr( "41 42 4" )                        IS "AB"
   HBTEST hb_NumToHex( 255 )                              IS "FF"
   HBTEST hb_NumToHex( 255, 4 )                           IS "00FF"
   HBTEST hb_NumToHex( 0 )                                IS "0"
   HBTEST hb_NumToHex( -1, 4 )                            IS "FFFF"

   /* hb_ntos, hb_eol, the byte functions */
   HBTEST hb_ntos( 42 )                                   IS "42"
   HBTEST hb_ntos( -1.50 )                                IS "-1.50"
   HBTEST hb_eol()                                        IS Chr( 13 ) + Chr( 10 )
   HBTEST hb_BLen( "abc" )                                IS 3
   HBTEST hb_BLeft( "abcdef", 2 )                         IS "ab"
   HBTEST hb_BSubStr( "abcdef", 3, 2 )                    IS "cd"
   HBTEST hb_BSubStr( "abcdef", 5 )                       IS "ef"

   /* hb_At */
   HBTEST hb_At( "&", "a&b&c" )                           IS 2
   HBTEST hb_At( "&", "a&b&c", 3 )                        IS 4
   HBTEST hb_At( "&", "a&b&c", 3, 3 )                     IS 0
   HBTEST hb_At( "", "abc" )                              IS 0

   /* StrTran's start and count */
   HBTEST StrTran( "aaaa", "a", "b", 2 )                  IS "abbb"
   HBTEST StrTran( "aaaa", "a", "b", 2, 1 )               IS "abaa"
   HBTEST StrTran( "aaaa", "a", "b", 0 )                  IS ""
   HBTEST StrTran( "aaaa", "", "b" )                      IS "aaaa"
   HBTEST StrTran( "abcabc", "bc" )                       IS "aa"

   /* the pads' fill */
   HBTEST PadL( "ab", 5, "*" )                            IS "***ab"
   HBTEST PadC( "ab", 7, "-" )                            IS "--ab---"
   HBTEST PadR( "ab", 4, "xyz" )                          IS "abxx"
   HBTEST PadR( "ab", 4, "" )                             IS "ab" + Chr( 0 ) + Chr( 0 )

   /* hb_CStr and hb_ValToExp */
   HBTEST hb_CStr( "x" )                                  IS "x"
   HBTEST hb_CStr( 12 )                                   IS "        12"
   HBTEST hb_CStr( .T. )                                  IS ".T."
   HBTEST hb_CStr( NIL )                                  IS "NIL"
   HBTEST hb_CStr( { 1, 2 } )                             IS "{ Array of 2 Items }"
   HBTEST hb_CStr( hb_SToD( "20240131" ) )                IS "0d20240131"
   HBTEST hb_ValToExp( { 1, "a", .F., NIL } )              IS '{1, "a", .F., NIL}'
   HBTEST hb_ValToExp( { "k" => 2 } )                     IS '{"k"=>2}'
   HBTEST hb_ValToExp( { => } )                           IS "{=>}"
   HBTEST hb_ValToExp( 'say "hi"' )                       IS ['say "hi"']
   HBTEST hb_ValToExp( {} )                               IS "{}"
   HBTEST hb_ValToExp( 1.50 )                             IS "1.50"

   RETURN
