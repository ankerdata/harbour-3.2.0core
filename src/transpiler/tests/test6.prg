// Test 6: CLASS inheritance, DATA/CLASSDATA, ACCESS/ASSIGN, scope modifiers
//
// Also exercises two regression paths:
//
//  - `EXPORT <name>` and `PROTECT <name>` shorthand from hbclass.ch.
//    The parser used to mis-read bare `EXPORT <name>` as the EXPORTED
//    section header (which swallows the line), so every EXPORT-prefixed
//    data member went missing. The fix distinguishes the header form
//    (followed by `:` or EOL) from the shorthand (followed by an
//    identifier) — see hbclsparse.c.
//
//  - INIT expressions that contain Harbour built-in function calls.
//    `INIT Space(3)` and `INIT CToD("")` are raw PP text, so they
//    bypass the normal HB_ET_FUNCALL remap. hb_csTranslateInit now
//    falls through to the INLINE translator, which walks identifiers
//    and prefixes built-ins from hbfuncs.tab using canonical casing
//    (→ `HbRuntime.Space`, `HbRuntime.CToD`). Without the canonical-
//    casing path the emit was `HbRuntime.SPACE` / `HbRuntime.CTOD`
//    which only compiled thanks to NIE stubs — throwing at runtime.
#include "hbclass.ch"

CLASS Inherited

   CLASSDATA nVersion INIT 1.0

ENDCLASS

CLASS Person INHERIT Inherited

   DATA nAge      AS NUMERIC  INIT 0
   DATA cName     AS STRING   INIT ""
   DATA dBirth    AS DATE     INIT CToD( "" )        // INIT with Harbour func call
   DATA cInitials AS STRING   INIT Space( 3 )        // INIT with Harbour func call

   CLASSDATA nCount AS NUMERIC INIT 0

   ACCESS FullName
   ASSIGN FullName

   EXPORT  cPublicNotes                              // hbclass.ch shorthand
   PROTECT oContext                                  // hbclass.ch shorthand

   EXPORTED:
   METHOD New()
   METHOD SetAge()

   PROTECTED:
   DATA cSecret   AS STRING  INIT "hidden" READONLY

   HIDDEN:
   METHOD InternalCalc()

ENDCLASS

METHOD New() CLASS Person
RETURN Self

METHOD SetAge( nAge ) CLASS Person
   ::nAge := nAge
   ? "nAge=" + Str( ::nAge )
RETURN Self

METHOD FullName() CLASS Person
RETURN ::cName

METHOD _FullName( cVal ) CLASS Person
   ::cName := cVal
   ? "cName=" + ::cName
RETURN cVal

METHOD InternalCalc() CLASS Person
RETURN ::nAge * 2

FUNCTION Main()

   LOCAL oPerson := Person():New()
   ? "oPerson created"

   oPerson:SetAge( 25 )
   ? "FullName=" + oPerson:FullName

   // Touch the EXPORT-shorthand member so it's visibly present at
   // compile AND runtime (the PROTECT counterpart `oContext` is
   // deliberately only accessed from inside the class).
   oPerson:cPublicNotes := "hello"
   ? "cPublicNotes=" + oPerson:cPublicNotes
   ? "cInitials=[" + oPerson:cInitials + "]"

RETURN oPerson:FullName
