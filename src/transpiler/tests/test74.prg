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
// This test exercises three shapes:
//   • LOCAL oTest74Holder       → typed Test74Holder
//   • STATIC soTest74Holder     → file-static, same inference via the
//                                  easipos `so<ClassName>` STATIC form
//   • LOCAL oNotAClass          → falls through to OBJECT / dynamic,
//                                  the existing behaviour
//
// All three pipelines (.prg / .hb / .cs) must produce identical
// output. The .cs side is where the inference matters — the .prg /
// .hb pipelines are dynamic-typed throughout and run regardless.

#include "hbclass.ch"

STATIC soTest74Holder

PROCEDURE Main()
   LOCAL oTest74Holder := Test74Holder():New()
   LOCAL oNotAClass    := Test74Holder():New()    // o-prefix but suffix isn't a class

   soTest74Holder := Test74Holder():New()

   oTest74Holder:cTag  := "first"
   soTest74Holder:cTag := "static"
   oNotAClass:cTag     := "fallback"

   ? "local:  " + oTest74Holder:Identify()
   ? "static: " + soTest74Holder:Identify()
   ? "fallthrough: " + oNotAClass:Identify()
RETURN

CLASS Test74Holder
   VAR cTag AS STRING INIT "default"
   METHOD Identify()
ENDCLASS

METHOD Identify() CLASS Test74Holder
   RETURN "Test74Holder:" + ::cTag
