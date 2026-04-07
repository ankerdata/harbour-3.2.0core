// Test 14: Class with constructor params and method chaining

#include "hbclass.ch"

CLASS Animal

   DATA cName INIT ""
   DATA nLegs INIT 4
   DATA cSound INIT ""

   METHOD New( cName, cSound )
   METHOD Speak()
   METHOD Describe()
   METHOD SetLegs( nLegs )

ENDCLASS

METHOD New( cName, cSound ) CLASS Animal
   ::cName := cName
   ::cSound := cSound
RETURN Self

METHOD Speak() CLASS Animal
RETURN ::cName + " says: " + ::cSound

METHOD Describe() CLASS Animal
RETURN ::cName + " has " + Str( ::nLegs ) + " legs"

METHOD SetLegs( nLegs ) CLASS Animal
   ::nLegs := nLegs
RETURN Self

PROCEDURE Main()

   LOCAL oDog := Animal():New( "Rex", "Woof" )
   LOCAL oCat := Animal():New( "Whiskers", "Meow" )

   // Method chaining
   oDog:SetLegs( 4 )

   ? oDog:Speak()
   ? oDog:Describe()
   ? oCat:Speak()
   ? oCat:Describe()

RETURN
