// Test 97: MESSAGE aliases, bare method sends, INLINE method rows.
//
// `MESSAGE Len METHOD StackLen` (hbclass.ch) declares Len as another
// name for StackLen. The class parser turned that line into nothing,
// so `oQueue:Len` was CS1061 on a typed receiver and a runtime miss on
// a dynamic one (easiutil's Queue and Stack, five call sites). The
// parser now reads it as `METHOD Len INLINE ::StackLen()`, with or
// without the parentheses hbclass.ch allows on either name, and an
// INLINE method gets a reftab row like any other, so a cross-file send
// resolves it. A method sent WITHOUT parentheses — `oShelf:Len`, legal
// Harbour — is emitted as the call it is. (An ACCESS is a property and
// gets no parentheses; its body is a known gap — see gencsharp.c — so
// none is exercised here.)
#include "hbclass.ch"

CLASS Shelf
   PROTECT aItems INIT {}
   MESSAGE Len METHOD StackLen
   MESSAGE Size() METHOD StackLen()
   METHOD Stow( xItem )
   METHOD StackLen()
   METHOD Init INLINE ( ::aItems := {}, Self )
ENDCLASS

METHOD Stow( xItem ) CLASS Shelf
   AAdd( ::aItems, xItem )
RETURN xItem

METHOD StackLen() CLASS Shelf
RETURN Len( ::aItems )

PROCEDURE Main()
   LOCAL oShelf := Shelf():New()
   oShelf:Stow( "jar" )
   oShelf:Stow( "tin" )
   ? "len=" + LTrim( Str( oShelf:Len ) ) + " size=" + LTrim( Str( oShelf:Size() ) )
   ? "bare=" + LTrim( Str( oShelf:StackLen ) )
RETURN
