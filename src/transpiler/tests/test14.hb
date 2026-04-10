#include "astype.ch"
// Test 14: Two classes with inheritance, constructor params, method chaining

#include "hbclass.ch"

CLASS Animal


   DATA cName AS STRING INIT ""
   DATA nLegs AS NUMERIC INIT 4
   DATA cSound AS STRING INIT ""

   METHOD New( cName, cSound )
   METHOD Speak()
   METHOD Describe()

ENDCLASS

CLASS Dog INHERIT Animal


   DATA cBreed AS STRING INIT ""

   METHOD Init( cName, cBreed )
   METHOD DescribeFull()

ENDCLASS

METHOD New( cName AS STRING, cSound AS STRING ) AS OBJECT CLASS Animal
   ::cName := cName
   QOut("cName=" + ::cName)
   ::cSound := cSound
   QOut("cSound=" + ::cSound)
RETURN Self

METHOD Speak() AS STRING CLASS Animal
RETURN ::cName + " says: " + ::cSound

METHOD Describe() AS STRING CLASS Animal
RETURN ::cName + " has " + Str(::nLegs) + " legs"

METHOD Init( cName AS STRING, cBreed AS STRING ) AS OBJECT CLASS Dog
   ::cName := cName
   QOut("cName=" + ::cName)
   ::cBreed := cBreed
   QOut("cBreed=" + ::cBreed)
RETURN Self

METHOD DescribeFull() CLASS Dog
RETURN ::Describe() + " (" + ::cBreed + ")"

PROCEDURE Main()

   LOCAL oAnimal := Animal():New("Cat", "Meow")
   LOCAL oDog := Dog():Init("Rex", "Labrador")

   QOut("Speak=" + oAnimal:Speak())
   QOut("Describe=" + oAnimal:Describe())
   QOut("Speak=" + oDog:Speak())
   QOut("DescribeFull=" + oDog:DescribeFull())

RETURN
