#include "astype.ch"
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

CLASS Thing

   DATA nCount AS NUMERIC INIT 0
   METHOD Bump( nBy )

ENDCLASS

PROCEDURE Main()
   LOCAL nTot := 10 AS NUMERIC
   LOCAL oThing := Thing():New() AS OBJECT

   Show(1)
   Show(2, .F.)
   Show(3, .T., 7)
   Width()
   Width(10)
   QOut("neg=" + LTrim(Str(Neg())))
   QOut("acc1=" + LTrim(Str(Accum())))
   Accum(@nTot)
   QOut("acc2=" + LTrim(Str(nTot)))
   Accum(@nTot, .F.)
   QOut("acc3=" + LTrim(Str(nTot)))
   oThing:Bump()
   oThing:Bump(5)
   QOut("count=" + LTrim(Str(oThing:nCount)))
   QOut("name1=" + Name())
   QOut("name2=" + Name("x"))
RETURN

   /* Both DEFAULTs lift: `bool lLoud = true, decimal nTimes = 2`. */
PROCEDURE Show( nId AS NUMERIC, lLoud AS LOGICAL, nTimes AS NUMERIC )
   IF lLoud == NIL
      lLoud := .T.
   ENDIF
   IF nTimes == NIL
      nTimes := 2
   ENDIF
   QOut("show " + LTrim(Str(nId)) + " " + IIF(lLoud, "T", "F") + " " + LTrim(Str(nTimes)))
RETURN

   /* hb_default form, defaulting to a header #define — a Const-class
   member, so still a C# constant: `decimal nW = Test87Const.TEST87_WIDTH`. */
PROCEDURE Width( nW AS NUMERIC )
   hb_default(@nW, TEST87_WIDTH)
   QOut("width=" + LTrim(Str(nW)))
RETURN

   /* A negative literal. */
FUNCTION Neg( nN AS NUMERIC ) AS NUMERIC
   IF nN == NIL
      nN := -1
   ENDIF
RETURN nN

   /* nTotal is by-ref (the `@nTot` call sites) and sits first, so the
   canonical takes `ref decimal nTotal` and cannot carry a default; the
   parameterless short overload forwards the lifted 0 instead. lDouble
   follows the ref and carries `= true` on the canonical, which is what
   `Accum( @nTot )` relies on. */
FUNCTION Accum( /*@*/nTotal AS NUMERIC, lDouble AS LOGICAL ) AS NUMERIC
   IF nTotal == NIL
      nTotal := 0
   ENDIF
   IF lDouble == NIL
      lDouble := .T.
   ENDIF
   nTotal += IIF(lDouble, 2, 1)
RETURN nTotal

   /* A reference-typed slot: cName is nilable, `string? cName = null`, and
   its guard stays exactly as before. */
FUNCTION Name( cName AS STRING ) AS STRING
   IF cName == NIL
      cName := "anon"
   ENDIF
RETURN cName

   /* The method path: `decimal nBy = 1`. */
METHOD Bump( nBy AS NUMERIC ) AS OBJECT CLASS Thing
   IF nBy == NIL
      nBy := 1
   ENDIF
   ::nCount += nBy
RETURN Self
