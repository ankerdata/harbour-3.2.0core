// W0036: an x name, deliberately polymorphic, subscripted by a key that
// does not say array or hash (Alex, 2026-09-27). Harbour decides at run
// time; C# has to know, so the source says which by assigning the value to
// an a<...>, h<...> or hn<...> name where it knows (a ValType() branch).
// The lines marked "quiet" must not warn.

FUNCTION Main( xValue )

   LOCAL aValue
   LOCAL nPos := 1

   IF ValType( xValue ) == "A"
      ? xValue[ nPos ]                   // fires
      aValue := xValue
      ? aValue[ nPos ]                   // quiet
   ENDIF
   ? xValue[ "key" ]                     // quiet: only a hash takes a string

   RETURN NIL
