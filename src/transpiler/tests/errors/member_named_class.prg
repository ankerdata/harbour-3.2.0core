// W0025: a member typed only by its name (::oW25Tally is a W25Tally)
// whose call contradicts that class — an argument of another scalar type
// than the parameter's — is probably not that class. A whole number for
// a number is no contradiction: INTEGER and NUMERIC are one number.

#include "hbclass.ch"

CLASS W25Tally
   METHOD Add( nCount )
ENDCLASS

METHOD Add( nCount ) CLASS W25Tally
RETURN nCount

CLASS W25Report
   VAR oW25Tally
   METHOD Count( aItems )
ENDCLASS

METHOD Count( aItems ) CLASS W25Report
   ::oW25Tally:Add( "three" )          // a string for a number — fires
   ::oW25Tally:Add( Len( aItems ) )    // a whole number for a number — quiet
RETURN NIL
