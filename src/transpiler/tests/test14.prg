// Test 14: Two classes with inheritance, constructor params, method chaining

#include "hbclass.ch"

CLASS Animal

   DATA cName INIT ""
   DATA nLegs INIT 4
   DATA cSound INIT ""

   METHOD New( cName, cSound )
   METHOD Speak()
   METHOD Describe()

ENDCLASS

METHOD New( cName, cSound ) CLASS Animal
   ::cName := cName
   ? "cName=" + ::cName
   ::cSound := cSound
   ? "cSound=" + ::cSound
RETURN Self

METHOD Speak() CLASS Animal
RETURN ::cName + " says: " + ::cSound

METHOD Describe() CLASS Animal
RETURN ::cName + " has " + Str( ::nLegs ) + " legs"

CLASS Dog INHERIT Animal

   DATA cBreed INIT ""

   METHOD Init( cName, cBreed )
   METHOD DescribeFull()

ENDCLASS

METHOD Init( cName, cBreed ) CLASS Dog
   ::cName := cName
   ? "cName=" + ::cName
   ::cBreed := cBreed
   ? "cBreed=" + ::cBreed
RETURN Self

METHOD DescribeFull() CLASS Dog
RETURN ::Describe() + " (" + ::cBreed + ")"

PROCEDURE Main()

   LOCAL oAnimal := Animal():New( "Cat", "Meow" )
   LOCAL oDog := Dog():Init( "Rex", "Labrador" )

   ? "Speak=" + oAnimal:Speak()
   ? "Describe=" + oAnimal:Describe()
   ? "Speak=" + oDog:Speak()
   ? "DescribeFull=" + oDog:DescribeFull()

RETURN
