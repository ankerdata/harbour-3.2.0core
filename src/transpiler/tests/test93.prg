// Test 93: a `Class():New(...)` call site refines the constructor's slots.
//
// The refinement walker types a send's receiver to pick the method row
// its arguments refine. A receiver that is the class constructor call
// itself — `Sconce():New(oLantern, "hall")` — is a FUNCALL no return-
// type table knows (a CLASS is not a function row), so the receiver
// stayed untyped and no constructor was ever refined from its callers:
// in the easipos corpus 0 of 18 Dialog `New` methods had a typed slot
// while 804 free-function slots were typed Transaction. The walker now
// types a `Class()` receiver as the class (the emitter already resolved
// the shape, test90), so oLight below is `Lantern` in C# and the member
// access in the constructor binds statically instead of through
// `dynamic`.
#include "hbclass.ch"

CLASS Lantern
   VAR nLumens INIT 800
ENDCLASS

CLASS Sconce
   VAR oLight
   VAR cLabel INIT ""
   METHOD New( oLight, cRoom )
ENDCLASS

/* oLight is OBJECT by name (no class named Light); Main's call refines
   it to Lantern, so `oLight:nLumens` is a typed member access. */
METHOD New( oLight, cRoom ) CLASS Sconce
   ::oLight := oLight
   ::cLabel := cRoom + "/" + LTrim( Str( oLight:nLumens ) )
RETURN Self

PROCEDURE Main()
   LOCAL oLantern := Lantern():New()
   LOCAL oSconce := Sconce():New( oLantern, "hall" )
   ? "label=" + oSconce:cLabel
RETURN
