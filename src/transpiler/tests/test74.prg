// Test 74: `o<ClassName>` (and easipos STATIC `so<ClassName>`) infer
// the variable as that specific class, not generic OBJECT.
//
// Prior to the prefix-from-name inference, a local declared
//
//    LOCAL oFooThing
//
// was seeded as OBJECT (Hungarian `o`) and emitted as C# `dynamic` —
// member access went through the DLR, no static-type benefit. With
// the inference, when the post-`o` suffix matches a class registered
// in the reftab, the local takes that class as its static type —
// authors get the static C# binding for free, no `:= FooThing():New()`
// seed allocation needed.
//
// The legs are built so the type can come ONLY from the name: no
// declaration initializer, and every value arrives through SeedObject,
// whose mixed RETURN types pin its inferred return type to USUAL — so
// neither the initializer inference nor assignment propagation ever
// sees a `Test74Holder():New()`. This is what separates the feature
// from the pre-existing constructor-initializer typing.
//
//   • LOCAL oTest74Holder       → typed Test74Holder purely by name
//   • STATIC soTest74Holder     → file-static, same inference via the
//                                  easipos `so<ClassName>` STATIC form
//   • LOCAL oNotAClass          → suffix matches no class; falls
//                                  through to OBJECT / dynamic
//
// All three pipelines (.prg / .hb / .cs) must produce identical
// output. The .cs side is where the inference matters — the .prg /
// .hb pipelines are dynamic-typed throughout and run regardless.

#include "hbclass.ch"

STATIC soTest74Holder

PROCEDURE Main()
   LOCAL oTest74Holder
   LOCAL oNotAClass

   oTest74Holder  := SeedObject( .T. )
   oNotAClass     := SeedObject( .T. )
   soTest74Holder := SeedObject( .T. )

   oTest74Holder:cTag  := "first"
   soTest74Holder:cTag := "static"
   oNotAClass:cTag     := "fallback"

   ? "local:  " + oTest74Holder:Identify()
   ? "static: " + soTest74Holder:Identify()
   ? "fallthrough: " + oNotAClass:Identify()
RETURN

// Polymorphic on purpose: the mixed RETURN types (object vs string)
// make the inferred return type USUAL, so no constructor evidence
// reaches the call sites above.
FUNCTION SeedObject( lReal )
   IF lReal
      RETURN Test74Holder():New()
   ENDIF
RETURN "never"

CLASS Test74Holder
   VAR cTag AS STRING INIT "default"
   METHOD Identify()
ENDCLASS

METHOD Identify() CLASS Test74Holder
   RETURN "Test74Holder:" + ::cTag
