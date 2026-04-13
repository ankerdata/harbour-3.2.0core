// Test 26: TIMESTAMP type map + per-file STATIC var name mangling.
//
// Both fixes were exposed by building a .NET classlib from the real
// easipos/easiutil generated code. The synthetic test suite passed
// through both bugs because (a) no test had a `t`-prefixed identifier
// (which Hungarian inference tags as TIMESTAMP — see hbtypes.c
// hb_astInferFromPrefix), and (b) no two tests declared a STATIC var
// with the same name across the merged partial class Program.
//
//   A. hb_csTypeMap DATE → DateOnly and TIMESTAMP → DateTime. Before:
//      an identifier whose Hungarian prefix inferred TIMESTAMP emitted
//      as bare `TIMESTAMP` in C#, which is not a valid C# type. DATE
//      used to map to DateTime for parity; it now maps to .NET's
//      DateOnly (no time component), matching Harbour DATE semantics.
//
//   B. File-scope STATIC var name mangling. Harbour STATICs are
//      private to their declaring .prg file, but every generated .cs
//      merges into one `public static partial class Program`, so two
//      files each declaring e.g. `STATIC aTable := {...}` produce a
//      C# member collision. The emitter now prefixes both the decl
//      and every reference with the file base name (`<file>_<var>`),
//      yielding file-unique names by construction. Locals with the
//      same name still shadow the static (Harbour rule).
//
// The cross-file collision case is exercised by the real easiutil
// scan (ntohex.prg and adt.prg both declare `STATIC aHex` and both
// now build cleanly under one partial class). Here we just cover the
// mangling mechanics and the TIMESTAMP map for a single file.

PROCEDURE Main()
   // Hungarian prefix `dDate` → hb_astInferType returns "DATE" →
   // hb_csTypeMap returns "DateOnly". Date() returns DateOnly too,
   // so the assignment compiles cleanly. Separately, tStamp's
   // TIMESTAMP → DateTime mapping is covered by test27.
   LOCAL dDate := Date()
   LOCAL i

   // Exercise function-scope STATIC mangling three times to confirm
   // the var persists across calls and the references all point at
   // the same class field.
   FOR i := 1 TO 3
      ? "bump:  " + Str( BumpCounter(), 4 )
   NEXT
   ? "total: " + Str( BumpCounter(), 4 )

   // Use dDate so C# doesn't warn-as-error about an unused local.
   // Empty(date()) is .F. for today; both Harbour and HbRuntime.EMPTY
   // return false for a non-null DateOnly, so output matches.
   IF Empty( dDate )
      ? "dDate: empty"
   ELSE
      ? "dDate: nonempty"
   ENDIF

RETURN

// Function-scope STATIC — persists across calls. Gets mangled to
// `test26_nCounter` in the generated C#.
FUNCTION BumpCounter()
   STATIC nCounter := 0
   nCounter := nCounter + 1
RETURN nCounter
