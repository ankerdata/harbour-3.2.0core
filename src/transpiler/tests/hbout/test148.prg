#include "astype.ch"
// Test 148: a whole-number parameter of a method whose receiver's class
// is unknown at the call.
//
// An `i` parameter is a C# long, and C# will not narrow a decimal to it on
// its own. A call to a function or through a known class has the argument
// cast already. A receiver the reftab cannot place (an `x` variable here; in
// EasiPOS a table model, `oTableIndexTable:GoTo( nRecNo )`) emitted the
// argument plain: through a typed receiver that is CS1503, through a
// dynamic one the binder throws at run time. The method row every class
// declaring the method shares now types the slot, as it names a gap.

#include "hbclass.ch"

CLASS RowCursor

   DATA nLastRow AS NUMERIC INIT 7
   METHOD SeekRow( iRow, lQuiet )

ENDCLASS

METHOD SeekRow( iRow AS NUMERIC, lQuiet AS LOGICAL ) AS NUMERIC CLASS RowCursor
   hb_default(@lQuiet, .F.)
   IF iRow > ::nLastRow
      iRow := ::nLastRow
   ENDIF

   IF !lQuiet
      QOut("row", iRow)
   ENDIF

RETURN iRow

PROCEDURE Main()
   LOCAL xRowCursor := RowCursor():New() AS OBJECT
   LOCAL nWanted := 3 AS NUMERIC
   xRowCursor:SeekRow(nWanted)
   QOut(xRowCursor:SeekRow(nWanted + 9, .T.))
RETURN
