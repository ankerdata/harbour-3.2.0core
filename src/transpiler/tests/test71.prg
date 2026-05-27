// Test 71: non-`@` args to by-ref params get a value-in / no-writeback shim.
//
// Harbour treats a parameter as a local copy at the *call* site —
// reassigning it inside the function only affects the caller's
// variable when the caller wrote `@` at the call site (only valid on
// `@var` and `@obj:member`; the parser silently drops a stray `@` on
// `aArr[i]` or `hash["k"]`). The reftab however tags a parameter as
// RW any time the body writes to it, so from a C# binding perspective
// every caller would need `ref`.
//
// The transpiler reconciles this: HB_ET_VARREF (`@var`) and
// HB_ET_REFERENCE (`@obj:member`) write back through the shim;
// every other shape — field access, array element, literal,
// expression, or a plain variable whose static type differs from the
// parameter's — gets a fresh temp initialized from the value, passed
// by `ref`, and *no* writeback. The caller's original storage stays
// put exactly as Harbour value semantics promise.
//
// Consume() splits its input at the first separator, returning the
// prefix and (by side effect) advancing the input past it. We call
// Consume from the four shapes that exercise different shim paths:
//   • `@cVar`         — VARREF, writes back
//   • aArr[1]         — ARRAYAT, shim with no writeback
//   • oObj["cField"]  — hash lookup, shim with no writeback
//   • literal         — STRING, shim with no writeback
// The before/after `?` outputs make the writeback contrast directly
// observable across .prg / .hb / .cs.

FUNCTION Consume( cString, cSep )
   LOCAL cFirst := ""
   IF !empty( cSep ) .AND. cSep $ cString
      cFirst := LEFT( cString, AT( cSep, cString ) - 1 )
      cString := SUBSTR( cString, AT( cSep, cString ) + LEN( cSep ) )
   ENDIF
RETURN cFirst

PROCEDURE Main()
   LOCAL cVar  := "alpha,one"
   LOCAL aArr  := { "beta,two" }
   LOCAL oObj  := { => }
   LOCAL cTaken
   oObj[ "cField" ] := "gamma,three"

   /* @cVar — writeback expected: cVar advances past the comma */
   cTaken := Consume( @cVar, "," )
   ? "@var   in=alpha,one  out=" + cVar              + "  took=" + cTaken

   /* aArr[1] — value-in only: aArr[1] must still read "beta,two" after */
   cTaken := Consume( aArr[ 1 ], "," )
   ? " elem  in=beta,two   out=" + aArr[ 1 ]         + "  took=" + cTaken

   /* oObj["cField"] — value-in only: the hash entry stays "gamma,three" */
   cTaken := Consume( oObj[ "cField" ], "," )
   ? " hash  in=gamma,three out=" + oObj[ "cField" ] + "  took=" + cTaken

   /* literal — there is no storage to write back to */
   cTaken := Consume( "delta,four", "," )
   ? " lit                  out=(literal)        took=" + cTaken
RETURN
