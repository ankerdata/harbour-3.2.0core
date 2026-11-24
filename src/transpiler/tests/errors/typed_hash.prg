// W0041 and W0035 for typed hashes (plan C12 / C16): a value of another
// type into `AS HASH OF RECORD X`, a plain hash into a typed one; a key of
// the other kind, and a declaration whose key kind contradicts the name,
// which declares the keys too. The lines marked "quiet" must not warn.

RECORD Row41h( cName, iLen )

FUNCTION Use41h( xAnything, hcPlain, nPos )
   LOCAL hcRows AS HASH OF RECORD Row41h := { "a" => { "a", 1 }, "b" => 5 }   // fires W0041: a number is no Row41h
   LOCAL hnById AS INTEGER HASH OF STRING := { "k" => "v" }               // fires W0035: a hash keyed by strings
   LOCAL hnWrong AS STRING HASH                                             // fires W0035: the name says numbers
   hcRows := xAnything                               // quiet: its type is unknown
   hcRows := hcPlain                                  // fires W0041: a plain hash into a typed one
   hcRows[ "c" ] := "text"                           // fires W0041: a string is no Row41h
   hcRows[ "c" ] := { "c", 3 }                       // quiet
   hcRows[ nPos ] := { "d", 4 }                      // fires W0035: a number as a key
   hnById[ "x" ] := "y"                             // fires W0035: a string as a key
   hnById[ 7 ] := "seven"                           // quiet
   hnWrong := hcPlain
RETURN Len( hcRows ) + Len( hnWrong )
