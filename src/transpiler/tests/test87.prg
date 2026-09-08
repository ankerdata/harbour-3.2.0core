// Test 87: declared parameter defaults on strict value slots.
//
// `DEFAULT p TO v` (common.ch: `IF p == NIL ; p := v ; END`) and
// `hb_default(@p, v)` are the optional-parameter idiom. A nilable slot
// emits `T? p = null` and its guard works as written. A strict value
// slot — n/l/d/t Hungarian, non-nullable by the test53 rule — can never
// be null in C#, so the guard was dead code (CS0472 / CS8073) and an
// omitted argument arrived as `default` (false / 0) where Harbour gives
// v. In the easipos corpus that was 223 compiler warnings plus, with no
// warning at all, 135 `hb_default(ref lX, ...)` sites whose null check
// never fires: SaleTrailer's `DEFAULT lKeepChange TO .T.` reached C# as
// `bool lKeepChange = default`.
//
// When v is a C# constant — .T./.F., a numeric literal, a defines-map
// member — it is lifted onto the declaration (`bool lLoud = true`), the
// guard is dropped, and the short overload forwards the same constant.
// Guards on reference-typed slots are untouched.
#include "common.ch"
#include "hbclass.ch"
#include "test87.ch"

PROCEDURE Main()
   LOCAL nTot := 10
   LOCAL oThing := Thing():New()

   Show( 1 )
   Show( 2, .F. )
   Show( 3, .T., 7 )
   Width()
   Width( 10 )
   ? "neg="    + LTrim( Str( Neg() ) )
   ? "acc1="   + LTrim( Str( Accum() ) )
   Accum( @nTot )
   ? "acc2="   + LTrim( Str( nTot ) )
   Accum( @nTot, .F. )
   ? "acc3="   + LTrim( Str( nTot ) )
   oThing:Bump()
   oThing:Bump( 5 )
   ? "count="  + LTrim( Str( oThing:nCount ) )
   ? "name1="  + Name()
   ? "name2="  + Name( "x" )
RETURN

/* Both DEFAULTs lift: `bool lLoud = true, decimal nTimes = 2`. */
PROCEDURE Show( nId, lLoud, nTimes )
   DEFAULT lLoud TO .T.
   DEFAULT nTimes TO 2
   ? "show " + LTrim( Str( nId ) ) + " " + IIF( lLoud, "T", "F" ) + " " + LTrim( Str( nTimes ) )
RETURN

/* hb_default form, defaulting to a header #define — a Const-class
   member, so still a C# constant: `decimal nW = Test87Const.TEST87_WIDTH`. */
PROCEDURE Width( nW )
   hb_default( @nW, TEST87_WIDTH )
   ? "width=" + LTrim( Str( nW ) )
RETURN

/* A negative literal. */
FUNCTION Neg( nN )
   DEFAULT nN TO -1
RETURN nN

/* nTotal is by-ref (the `@nTot` call sites) and sits first, so the
   canonical takes `ref decimal nTotal` and cannot carry a default; the
   parameterless short overload forwards the lifted 0 instead. lDouble
   follows the ref and carries `= true` on the canonical, which is what
   `Accum( @nTot )` relies on. */
FUNCTION Accum( nTotal, lDouble )
   DEFAULT nTotal TO 0
   DEFAULT lDouble TO .T.
   nTotal += IIF( lDouble, 2, 1 )
RETURN nTotal

/* A reference-typed slot: cName is nilable, `string? cName = null`, and
   its guard stays exactly as before. */
FUNCTION Name( cName )
   DEFAULT cName TO "anon"
RETURN cName

CLASS Thing
   VAR nCount INIT 0
   METHOD Bump( nBy )
ENDCLASS

/* The method path: `decimal nBy = 1`. */
METHOD Bump( nBy ) CLASS Thing
   DEFAULT nBy TO 1
   ::nCount += nBy
RETURN Self
