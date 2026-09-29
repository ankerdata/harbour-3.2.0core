// Test 135: a FOR EACH's C# temporary takes its loop variable's type.
//
// Harbour's loop variable is an ordinary local that lives on after the
// loop, and a C# foreach cannot loop on an existing local, so the loop
// runs on a temporary and assigns the local: `foreach (dynamic
// __hb_fe_cName in …) { cName = __hb_fe_cName; …`. The local was typed by
// its name already; the temporary was `dynamic`. It takes the local's type
// now (string, a hash, an array, decimal, long), as a C# author would
// write the loop. A typed foreach converts each element with an explicit
// cast, so the integer iCount135 acts as an integer (Alex): a decimal
// element is cast (long), as a decimal written into an `i` name is
// anywhere else, where the dynamic temporary's implicit assignment threw
// on any decimal, a whole one (aWhole135's 5.0) too.

PROCEDURE Main()
   LOCAL aNames135 := { "tin", "cup" }
   LOCAL aRows135 := { { "a", 1 }, { "b", 2 } }
   LOCAL aHashes135 := { { "k" => "v1" }, { "k" => "v2" } }
   LOCAL aAmounts135 := { 1.5, 2.25 }
   LOCAL aCounts135 := { 3, 4 }
   LOCAL aWhole135 := { 10 / 2 }
   LOCAL cName135, aRow135, hField135, nAmount135, iCount135
   LOCAL cOut := ""
   LOCAL nSum := 0
   LOCAL iTotal := 0

   FOR EACH cName135 IN aNames135
      cOut += cName135 + ";"
   NEXT
   FOR EACH aRow135 IN aRows135
      cOut += aRow135[ 1 ] + hb_ntos( aRow135[ 2 ] ) + ";"
   NEXT
   FOR EACH hField135 IN aHashes135
      cOut += hField135[ "k" ] + ";"
   NEXT
   FOR EACH nAmount135 IN aAmounts135
      nSum += nAmount135
   NEXT
   FOR EACH iCount135 IN aCounts135
      iTotal += iCount135
   NEXT
   FOR EACH iCount135 IN aWhole135
      iTotal += iCount135
   NEXT

   ? cOut
   ? nSum
   ? iTotal
RETURN
