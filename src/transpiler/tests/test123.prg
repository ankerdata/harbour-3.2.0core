// Test 123: a subscript in an INLINE method is rebased as one in a method
// body is.
//
// An INLINE body reaches the emitter as text, not as an expression tree,
// and the text translator copied a subscript through as it stood, so
// `INLINE ::aData[1]` read the C# list's element 1, Harbour's second, and
// the last element read past the end (easipos daterangedialog.prg's
// StartFile() / EndFile()). It went unnoticed while arrays were C#
// arrays, whose indexer takes a long; a List<dynamic>'s takes an int.
//
// The translator decides as the AST emitter does: a string or c-named
// index, or a receiver whose name says hash, is a hash key and passes as
// it is; anything else is an array index, rebased.

#include "hbclass.ch"

CLASS Shelf123
   VAR aSlots123
   VAR aGrid123
   VAR hTags123
   VAR nPos123
   METHOD Fill123()
   METHOD First123()          INLINE ::aSlots123[1]
   METHOD Last123()           INLINE ::aSlots123[Len(::aSlots123)]
   METHOD At123( nAt )        INLINE ::aSlots123[nAt]
   METHOD Current123()        INLINE ::aSlots123[::nPos123]
   METHOD Cell123( nRow, nCol ) INLINE ::aGrid123[nRow][nCol]
   METHOD Tag123( cKey )      INLINE ::hTags123[cKey]
   METHOD Red123()            INLINE ::hTags123["red"]
   METHOD Put123( nAt, cVal ) INLINE ::aSlots123[nAt] := cVal
ENDCLASS

METHOD Fill123() CLASS Shelf123
   ::aSlots123 := { "first", "second", "third" }
   ::aGrid123  := { { "a1", "a2" }, { "b1", "b2" } }
   ::hTags123  := { "red" => "warm", "blue" => "cold" }
   ::nPos123   := 2
   RETURN Self

PROCEDURE Main()
   LOCAL oStore123 := Shelf123():New():Fill123()

   ? oStore123:First123(), oStore123:Last123(), oStore123:At123( 2 ), oStore123:Current123()
   ? oStore123:Cell123( 2, 1 ), oStore123:Cell123( 1, 2 )
   ? oStore123:Tag123( "blue" ), oStore123:Red123()
   oStore123:Put123( 3, "changed" )
   ? oStore123:At123( 3 )

   RETURN
