#include "astype.ch"
// Test 49: Super / className handling.
//
// Covers all four forms the transpiler maps:
//   1. obj:className()        → obj.className()            (extension on object)
//   2. obj:Super():className() → obj.Super().className()    (HbSuperRef.className)
//   3. ::Super:className()    → this.Super().className()   (same as 2, colon form)
//   4. ::Super:Method(args)   → base.Method(args)          (inheritance call)
//
// The last form is the important one — Harbour's idiomatic "call the
// parent class's implementation" that C# spells as `base.Method(...)`.

#include "hbclass.ch"

CLASS Animal

   METHOD Speak()
   METHOD Kind()

ENDCLASS

CLASS Dog INHERIT Animal

   METHOD Speak()
   METHOD Identify()

ENDCLASS

METHOD Speak() AS OBJECT CLASS Animal
   QOut("Animal::Speak")
RETURN Self

METHOD Speak() AS OBJECT CLASS Dog
   // form 4: → base.Speak()
   ::Super:Speak()
   QOut("Dog::Speak")
RETURN Self

METHOD Identify() AS OBJECT CLASS Dog
   // form 1
   QOut("className=" + Upper(::className()))
   // form 3
   QOut("Super-className=" + Upper(::Super:className()))
   // form 4 (inline parent method)
   QOut("Kind=" + ::Super:Kind())
RETURN Self

PROCEDURE Main()

   LOCAL oDog := Dog():New() AS OBJECT
   LOCAL oAnimal := Animal():New() AS OBJECT

   // form 1
   QOut("oAnimal:className()=" + Upper(oAnimal:className()))
   // form 1
   QOut("oDog:className()=" + Upper(oDog:className()))
   // form 2
   QOut("oDog:Super():className()=" + Upper(oDog:Super():className()))

   oDog:Speak()
   oDog:Identify()

RETURN
