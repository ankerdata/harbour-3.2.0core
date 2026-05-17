#include "astype.ch"
// Test 59: a dynamic class reaching a member it does not declare.
//
// A class that uses `::&(name)` emits as a C# DynamicObject. Such a
// class often accesses members it does not statically declare — the
// ORM pattern, where a base class's methods drive columns that live
// on a runtime subclass.
//
// Here Base's methods touch `::nMark` / `::cTag`, declared on the
// Child subclass, not on Base. In Harbour this resolves at runtime
// because Self is a Child. In C# `this` is statically typed Base, so
// `this.nMark` would be CS1061 — a missing static field.
//
// The transpiler now routes a `Self:` access to a member undeclared
// on a dynamic class through `((dynamic)this)`, so the DLR reaches
// the real member via HbDynamicObject. This test pins both emit
// paths: the AST SEND emitter (Stamp) and the INLINE textual
// translator (Tag).

#include "hbclass.ch"

CLASS Base

   METHOD Stamp()
   METHOD Tag() INLINE (::cTag)
   METHOD MacroPoke( cName, xVal )

ENDCLASS

// The `::&(name)` here is what marks Base a dynamic class (so it
// emits `: HbDynamicObject`). Never called — it exists only to
// trigger that classification.

CLASS Child INHERIT Base

   DATA nMark AS NUMERIC INIT 10
   DATA cTag AS STRING INIT "child"

ENDCLASS

METHOD MacroPoke( cName AS STRING, xVal AS USUAL ) AS OBJECT CLASS Base
   ::&(cName) := xVal
RETURN Self

   // nMark is declared on Child, not Base — `::nMark` here is undeclared
   // from Base's point of view, so it emits as ((dynamic)this).nMark.
METHOD Stamp() AS NUMERIC CLASS Base
   ::nMark := ::nMark + 5
RETURN ::nMark

PROCEDURE Main()
   LOCAL o := Child():New() AS OBJECT

   // AST SEND: read + write
   QOut("stamp=" + LTrim(Str(o:Stamp())))
   // INLINE read
   QOut("tag=" + o:Tag())
RETURN
