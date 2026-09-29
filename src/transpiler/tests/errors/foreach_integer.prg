// W0039: a FOR EACH variable that is an integer only by inference. C#
// declares it long, so the loop casts each element to long and drops a
// fraction Harbour keeps. An `i` name says the elements are whole numbers
// (Alex, 2026-09-29); an `n` name the inference made integer, here from
// keying an `hn` hash (easipos jsonifystate.prg's nClerkNo), does not.
// The lines marked "quiet" must not warn.

FUNCTION Sum39( hnPrices39 )
   LOCAL nId
   LOCAL iId
   LOCAL nTotal := 0
   FOR EACH nId IN hb_HKeys( hnPrices39 )      // fires: nId is long by inference only
      nTotal += hnPrices39[ nId ]
   NEXT
   FOR EACH iId IN hb_HKeys( hnPrices39 )      // quiet: an i name says integer
      nTotal += hnPrices39[ iId ]
   NEXT
   RETURN nTotal

FUNCTION Mean39( aValues39 )
   LOCAL nValue
   LOCAL nSum := 0
   FOR EACH nValue IN aValues39                // quiet: decimal
      nSum += nValue
   NEXT
   RETURN nSum / Len( aValues39 )
