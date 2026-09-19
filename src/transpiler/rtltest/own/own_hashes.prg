/*
 * Our tests for the hash functions, which hbtest does not cover at all.
 * EasiPOS calls hb_HHasKey 166 times, hb_HSet 110, hb_HGetDef 54,
 * hb_HKeys 49, hb_HClone 23 and the rest a handful — string-keyed
 * hashes above all, a few keyed by integer ids. Written as hbtest writes
 * its assertions; Harbour runs them first, so every expected value here
 * is Harbour's own.
 */

#include "rt_main.ch"

PROCEDURE Main_HASHES()

   LOCAL hAges := { "ann" => 31, "bob" => 42, "cy" => 7 }
   LOCAL hIds := { 10 => "ten", 20 => "twenty" }

   /* lookup; string keys compare exactly */
   HBTEST hb_HHasKey( hAges, "bob" )                 IS .T.
   HBTEST hb_HHasKey( hAges, "Bob" )                 IS .F.
   HBTEST hb_HHasKey( hAges, "bo" )                  IS .F.
   HBTEST hb_HGetDef( hAges, "cy", 0 )               IS 7
   HBTEST hb_HGetDef( hAges, "dee", 0 )              IS 0
   HBTEST hb_HGetDef( hAges, "dee" )                 IS NIL
   HBTEST hb_HGet( hAges, "ann" )                    IS 31
   HBTEST hb_HPos( hAges, "cy" )                     IS 3
   HBTEST hb_HPos( hAges, "dee" )                    IS 0
   HBTEST hb_HKeyAt( hAges, 2 )                      IS "bob"
   HBTEST Len( hAges )                               IS 3
   HBTEST "ann" $ hAges                              IS .T.
   HBTEST "dee" $ hAges                              IS .F.

   /* numeric keys compare by value */
   HBTEST hb_HHasKey( hIds, 10 )                     IS .T.
   HBTEST hb_HHasKey( hIds, 10.0 )                   IS .T.
   HBTEST hb_HHasKey( hIds, 15 )                     IS .F.
   HBTEST hb_HGetDef( hIds, 20.0, "" )               IS "twenty"
   HBTEST hb_HKeyAt( hIds, 1 ) + 5                   IS 15

   /* order: kept as added, deletes included; a replaced key stays put */
   HBTEST KeyList( DelAdd( { "a" => 1, "b" => 2, "c" => 3 }, "b", "d" ) ) IS "a c d"
   HBTEST KeyList( SetOf( { "a" => 1, "b" => 2 }, "a", 9 ) )           IS "a b"
   HBTEST hb_HGet( SetOf( { "a" => 1 }, "a", 9 ), "a" )                 IS 9
   HBTEST hb_ValToExp( hb_HValues( { "x" => 1, "y" => 2 } ) )           IS "{1, 2}"
   HBTEST Len( hb_HKeys( { => } ) )                                    IS 0
   HBTEST hb_HKeepOrder( { => } )                                      IS .T.

   /* hb_HClone copies nested arrays and hashes */
   HBTEST CloneApart()                               IS "1 1 9 5"
   HBTEST ValType( hb_HClone( { => } ) )             IS "H"

   RETURN

STATIC FUNCTION KeyList( hIn )

   LOCAL cOut := ""
   LOCAL cKey

   FOR EACH cKey IN hb_HKeys( hIn )
      cOut += iif( Empty( cOut ), "", " " ) + cKey
   NEXT

   RETURN cOut

STATIC FUNCTION DelAdd( hIn, cDel, cAdd )

   hb_HDel( hIn, cDel )
   hIn[ cAdd ] := 0

   RETURN hIn

STATIC FUNCTION SetOf( hIn, cKey, xValue )

   hb_HSet( hIn, cKey, xValue )

   RETURN hIn

STATIC FUNCTION CloneApart()

   LOCAL hSrc := { "a" => { 1, 2 }, "b" => { "x" => 1 } }
   LOCAL hCopy := hb_HClone( hSrc )

   hCopy[ "a" ][ 1 ] := 9
   hCopy[ "b" ][ "x" ] := 5

   RETURN Str( hSrc[ "a" ][ 1 ], 1 ) + " " + Str( hSrc[ "b" ][ "x" ], 1 ) + " " + ;
          Str( hCopy[ "a" ][ 1 ], 1 ) + " " + Str( hCopy[ "b" ][ "x" ], 1 )
