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
   METHOD Kind() INLINE "animal"
ENDCLASS

METHOD Speak() CLASS Animal
   ? "Animal::Speak"
RETURN Self

CLASS Dog INHERIT Animal
   METHOD Speak()
   METHOD Identify()
ENDCLASS

METHOD Speak() CLASS Dog
   ::Super:Speak()           // form 4: → base.Speak()
   ? "Dog::Speak"
RETURN Self

METHOD Identify() CLASS Dog
   ? "className=" + ::className()              // form 1
   ? "Super-className=" + ::Super:className()  // form 3
   ? "Kind=" + ::Super:Kind()                  // form 4 (inline parent method)
RETURN Self

PROCEDURE Main()

   LOCAL oDog := Dog():New()
   LOCAL oAnimal := Animal():New()

   ? "oAnimal:className()=" + oAnimal:className()           // form 1
   ? "oDog:className()=" + oDog:className()                 // form 1
   ? "oDog:Super():className()=" + oDog:Super():className() // form 2

   oDog:Speak()
   oDog:Identify()

RETURN
