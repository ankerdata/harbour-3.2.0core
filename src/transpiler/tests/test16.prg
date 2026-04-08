// Test 16: STATIC, PUBLIC, and PRIVATE declarations with type inference

FUNCTION Counter()

   STATIC nCount := 0
   STATIC cLabel := "count"

   nCount := nCount + 1
   ? cLabel + "=" + Str( nCount )

RETURN nCount

PROCEDURE Main()

   MEMVAR nPriv, cPrivName, nPub, cPubName

   STATIC nStatic := 10
   STATIC cStaticLabel := "static"
   PRIVATE nPriv := 42
   PRIVATE cPrivName := "secret"
   PUBLIC nPub := 100
   PUBLIC cPubName := "global"

   ? "nStatic=" + Str( nStatic )
   ? "cStaticLabel=" + cStaticLabel
   ? "nPriv=" + Str( nPriv )
   ? "cPrivName=" + cPrivName
   ? "nPub=" + Str( nPub )
   ? "cPubName=" + cPubName

   Counter()
   Counter()
   Counter()

   nStatic := nStatic + 5
   ? "nStatic=" + Str( nStatic )

   nPriv := nPriv + 1
   ? "nPriv=" + Str( nPriv )

   nPub := nPub - 10
   ? "nPub=" + Str( nPub )

RETURN
