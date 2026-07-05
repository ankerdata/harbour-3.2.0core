// Test 75: hb_csTranslateInline coverage for constructs that used to
// leak raw Harbour into C# expression-bodied members: iif() (must
// become a lazy ternary), NIL, <> (the PP-canonical form of !=),
// .AND. / .OR. / .NOT., and the empty-hash literal { => }.
//
// The iif→ternary rewrite is streaming: the translator emits `((`,
// then rewrites that call's two top-level commas to `) ? (` / `) : (`
// and its closing paren to `))`, tracking (/[/{ depth so commas nested
// in inner calls or subscripts pass through untouched. Laziness is the
// point of using a real ternary: in ResultValue below, the hash
// subscript must not evaluate while hResultData is NIL — an eager
// IIF() helper function would throw where Harbour's iif does not.

#include "hbclass.ch"

CLASS Test75Dialog
   VAR hResultData

   METHOD ResultValue(cKey, xDefault) INLINE (iif(::hResultData != NIL .AND. hb_HHasKey(::hResultData, cKey), ::hResultData[cKey], xDefault))
   METHOD GetOrEmpty(cKey) INLINE (hb_HGetDef(::hResultData, cKey, { => }))
   METHOD IsEmptyish(nVal) INLINE (nVal == 0 .OR. .NOT. (nVal <> -1))
ENDCLASS

PROCEDURE Main()
   LOCAL oDlg := Test75Dialog():New()

   // hResultData is NIL here: iif's condition must short-circuit at
   // `!= NIL` and take the false branch without touching the hash
   // subscript. Exercises iif + NIL + .AND. + lazy branches.
   ? "a=", oDlg:ResultValue("result", "fallback")

   oDlg:hResultData := { "result" => "done", "count" => 2 }
   ? "b=", oDlg:ResultValue("result", "fallback")
   ? "c=", oDlg:ResultValue("missing", 42)

   // { => } as an INLINE argument: missing key falls back to an empty
   // hash, which must be a real (empty) hash, not a syntax error.
   ? "d=", hb_HHasKey(oDlg:GetOrEmpty("nothere"), "x")
   ? "e=", Len(oDlg:GetOrEmpty("nothere"))

   // .OR. / .NOT. / <> in one body.
   ? "f=", oDlg:IsEmptyish(0)
   ? "g=", oDlg:IsEmptyish(-1)
   ? "h=", oDlg:IsEmptyish(7)

RETURN
