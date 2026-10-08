// Build A's caller of reconcile_shared.prg (see there).

PROCEDURE Main()
   LOCAL nTotal := 0
   Count46( "apple", @nTotal )
   Weigh46( Apple46():New() )
   Show46( Apple46():New() )
   ? nTotal
RETURN
