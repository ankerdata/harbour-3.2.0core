#include "astype.ch"
// Test 16: STATIC, PUBLIC, and PRIVATE declarations with type inference

FUNCTION Counter() AS NUMERIC

   STATIC nCount := 0 AS NUMERIC
   STATIC cLabel := "count" AS STRING

   nCount := nCount + 1
   QOut(cLabel + "=" + Str(nCount))

RETURN nCount

PROCEDURE Main()

   MEMVAR nPriv
   MEMVAR cPrivName
   MEMVAR nPub
   MEMVAR cPubName

   STATIC nStatic := 10 AS NUMERIC
   STATIC cStaticLabel := "static" AS STRING
   PRIVATE nPriv := 42 AS NUMERIC
   PRIVATE cPrivName := "secret" AS STRING
   PUBLIC nPub := 100 AS NUMERIC
   PUBLIC cPubName := "global" AS STRING

   QOut("nStatic=" + Str(nStatic))
   QOut("cStaticLabel=" + cStaticLabel)
   QOut("nPriv=" + Str(nPriv))
   QOut("cPrivName=" + cPrivName)
   QOut("nPub=" + Str(nPub))
   QOut("cPubName=" + cPubName)

   Counter()
   Counter()
   Counter()

   nStatic := nStatic + 5
   QOut("nStatic=" + Str(nStatic))

   nPriv := nPriv + 1
   QOut("nPriv=" + Str(nPriv))

   nPub := nPub - 10
   QOut("nPub=" + Str(nPub))

RETURN
