// W0050: text is declared STRING, never CHARACTER (Alex, 2026-11-24).
// Harbour reads the two, and CHARACTER's abbreviations down to CHAR, as
// one type; the scan names CHARACTER wherever a declaration writes it. A
// line marked "fires" warns once, and no other line may.

#include "hbclass.ch"

RECORD Field50( cName AS CHARACTER, cKind AS STRING )       // fires

CLASS Box50
   VAR cLabel AS CHARACTER                                   // fires
   VAR cNote AS STRING                                       // quiet
   VAR acTags AS ARRAY OF CHARACTER                          // fires
   METHOD Title50() AS CHARACTER INLINE ::cLabel             // fires
   METHOD Note50( cWith AS CHARACTER ) AS STRING             // fires
ENDCLASS

METHOD Note50( cWith AS CHARACTER ) AS STRING CLASS Box50    // fires
RETURN ::cNote + cWith

FUNCTION Names50( cFirst AS CHARACTER ) AS ARRAY OF CHARACTER    // fires
   LOCAL cSecond AS CHAR := "b"                              // fires
   LOCAL hcKinds AS STRING HASH OF CHARACTER := { => }       // fires
   LOCAL hcCodes AS CHARACTER HASH := { => }                 // fires
   LOCAL aRows AS ARRAY OF ARRAY OF CHARACTER := {}          // fires
   LOCAL acKept AS ARRAY OF STRING := {}                     // quiet
   LOCAL cKept AS STRING := "c"                              // quiet
   LOCAL cCharacter := "d"                                   // quiet: a name, not a type
   hcKinds[ "x" ] := cKept
   hcCodes[ "y" ] := cCharacter
   AAdd( aRows, { cFirst, cSecond } )
   AAdd( acKept, cKept )
RETURN { cFirst, cSecond }
