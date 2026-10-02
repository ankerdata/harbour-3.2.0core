// A parameter renamed to a name that names a class takes that class on a
// warm scan (run.sh scans this file, then rename_param_after.prg, on one
// reftab). Here the parameter is a generic oLine, which its caller makes
// a LineRn: the row records oLine:LineRn. Not a warning test: run.sh reads
// the row.

#include "hbclass.ch"

CLASS LineRn
   VAR nKind INIT 0
ENDCLASS

CLASS ItemLineRn INHERIT LineRn
   VAR nPrice INIT 0
ENDCLASS

FUNCTION CallerRn( oLineRn )
   RETURN PriceRn( oLineRn )

FUNCTION PriceRn( oLine )
   RETURN oLine:nKind
