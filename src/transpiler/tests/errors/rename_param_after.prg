// rename_param_before.prg, with PriceRn's parameter renamed for the class
// it always holds. Scanned on the reftab the first file left, the row must
// say oItemLineRn:ItemLineRn: the name's class, not the LineRn its callers
// gave the old name.

#include "hbclass.ch"

CLASS LineRn
   VAR nKind INIT 0
ENDCLASS

CLASS ItemLineRn INHERIT LineRn
   VAR nPrice INIT 0
ENDCLASS

FUNCTION CallerRn( oLineRn )
   RETURN PriceRn( oLineRn )

FUNCTION PriceRn( oItemLineRn )
   RETURN oItemLineRn:nPrice
