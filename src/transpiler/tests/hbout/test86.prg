#include "astype.ch"
// Test 86: `@` array parameter forwarded into a typed-receiver method.
//
// Store:Fill() reassigns its by-ref parameter (aOut := {...}). Wrap()
// never touches aOut itself — it only forwards it with `@` into
// GetStore():Fill(). For the caller to see the new array, Wrap's own
// parameter must still emit `ref`.
//
// Getting that right means resolving GetStore():Fill(@aOut) to the
// reftab key Store::Store__Fill. The scanner used to key only Self:
// and ::method() sends, so Wrap's parameter went unmarked, its `ref`
// was elided as redundant (W0023), and Fill's new array landed in
// Wrap's local where the caller never saw it: len= printed 0 instead
// of 3, and only the C# side was wrong — the Harbour output was right
// throughout, so nothing but a prg/cs comparison catches it.

#include "hbclass.ch"

STATIC soStore AS OBJECT

CLASS Store

   METHOD Fill()

ENDCLASS

METHOD Fill( aOut AS ARRAY ) AS NUMERIC CLASS Store
   aOut := {"x", "y", "z"}

RETURN LEN(aOut)

FUNCTION GetStore() AS OBJECT

RETURN soStore

FUNCTION Wrap( /*@*/aOut AS ARRAY )

RETURN GetStore():Fill(@aOut)

PROCEDURE Main()

   LOCAL aData := {} AS ARRAY
   LOCAL nCount AS NUMERIC

   soStore := Store():New()

   nCount := Wrap(@aData)
   QOut("n=", nCount)
   QOut("len=", LEN(aData))
   IF LEN(aData) >= 3
      QOut("vals=", aData[1] + aData[2] + aData[3])
   ENDIF

RETURN
