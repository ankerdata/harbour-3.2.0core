#include "astype.ch"
// Test 98: integral lvalues fed a decimal; an assignment as an operand.
//
// The transpiler types a LOCAL or a file STATIC seeded from an integer
// define as C# long, and an AS INTEGER member is long by declaration.
// A decimal reaching one of those — a NUMERIC parameter, a method's
// return, a quantity — is CS0266. The emitter already coerced a plain
// `:=` into an int local or member; it now also covers a file STATIC
// on the left, the compound `+=` family, and the assignment the
// ref-shim block emits for `n := o:M( @x )` (ten sites in the easipos
// corpus). Where the fraction is real — a division, a quantity — the
// corpus says `Int()` in source so Harbour truncates too, and the
// coercion then only restates it.
// A file STATIC takes its type from its initialiser alone (Pass 2.5
// only sees the body a variable is declared in), so `snHalf` below is
// long from LAMP_LOW; the division written into it from Main — a hard
// disqualifier — demotes it to decimal in the emitter's pre-pass, and
// it holds 0.5 as Harbour does.
// Second: `FOR i := 1 TO ( nLen := Len( a ) )` — an assignment used as
// an operand keeps its parentheses in C#, where `=` binds looser than
// `<=` (CS0131).
#define LAMP_OFF 0
#define LAMP_LOW 1
#include "hbclass.ch"

STATIC snLamp := LAMP_OFF AS NUMERIC
STATIC snHalf := LAMP_LOW AS NUMERIC

CLASS Ledger

   DATA nCovers AS INTEGER INIT 0
   METHOD Add( nQty )
   METHOD Gauge( cWhat, cOut )

ENDCLASS

METHOD Add( nQty AS NUMERIC ) AS NUMERIC CLASS Ledger
   ::nCovers += Int(nQty)
RETURN ::nCovers

   /* NUMERIC return with a by-ref parameter: the caller's assignment is
   emitted inside the shim block. */
METHOD Gauge( cWhat AS STRING, cOut AS STRING ) AS NUMERIC CLASS Ledger
   cOut := "read " + cWhat
RETURN Len(cWhat) * 10

PROCEDURE SetLamp( nLamp AS NUMERIC )
   snLamp := nLamp
RETURN

PROCEDURE Main()
   LOCAL oLedger := Ledger():New() AS OBJECT
   LOCAL nResult := LAMP_OFF AS NUMERIC
   LOCAL cOut := "" AS STRING
   LOCAL aList := {"a", "b", "c"} AS ARRAY
   LOCAL cAll := "" AS STRING
   LOCAL nLen AS NUMERIC
   LOCAL i AS NUMERIC

   SetLamp(LAMP_LOW)
   snHalf := LAMP_LOW / 2
   QOut("lamp=" + LTrim(Str(snLamp)) + " half=" + IIF(snHalf * 2 == 1, "kept", "lost"))
   oLedger:Add(2.7)
   QOut("covers=" + LTrim(Str(oLedger:Add(1))))
   nResult := oLedger:Gauge("tea", @cOut)
   QOut("result=" + LTrim(Str(nResult)) + " " + cOut)
   FOR i := 1 TO nLen := Len(aList)
      cAll += aList[i]
   NEXT

   QOut("all=" + cAll + " len=" + LTrim(Str(nLen)))
RETURN
