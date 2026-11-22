// Test 185: a method that answers a value of its default's type.
// A method declares a type parameter as a function does: Take185() is
// `T Take185<T>( string cKey, T xDefault )` in a class that is not
// generic itself, and a subclass's method that calls it through Self or
// through an object gets a string for "" and a whole number for 0.

#include "hbclass.ch"
#include "../include/astype.ch"

CLASS Answers185
   VAR hcResult185
   METHOD New()
   METHOD Take185( cKey, xDefault )
   METHOD Name185()
   METHOD Count185()
ENDCLASS

METHOD New() CLASS Answers185

   ::hcResult185 := { "name" => "Ann", "count" => 3, "rate" => 1.25, "ids" => { 4, 5 } }

   RETURN Self

METHOD Take185( cKey, xDefault AS T ) AS T CLASS Answers185

   IF hb_HHasKey( ::hcResult185, cKey )
      RETURN ::hcResult185[ cKey ]
   ENDIF

   RETURN xDefault

METHOD Name185() AS STRING CLASS Answers185

   RETURN ::Take185( "name", "" )

METHOD Count185() AS INTEGER CLASS Answers185

   RETURN ::Take185( "count", 0 )

PROCEDURE Main()

   LOCAL oAnswers185 := Answers185():New()
   LOCAL cName185
   LOCAL nRate185
   LOCAL aIds185

   ? Upper( oAnswers185:Name185() ), oAnswers185:Count185() + 1

   cName185 := oAnswers185:Take185( "name", "" )
   nRate185 := oAnswers185:Take185( "rate", 0.00 )
   aIds185 := oAnswers185:Take185( "ids", {} )
   ? Len( cName185 ), nRate185 * 4, Len( aIds185 )

   ? oAnswers185:Take185( "missing", "none" ), oAnswers185:Take185( "missing", -1 ), ;
     oAnswers185:Take185( "missing", .T. )

   RETURN
