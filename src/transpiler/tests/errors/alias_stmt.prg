// Regression: ALIAS statement context — must fail -GS codegen.
// Pattern copied from sharedx/loadflag.prg (Flags->( dbCloseArea() )).

PROCEDURE Main()
   LOCAL cName := "Flags"

   Flags->( dbCloseArea() )
   Flags->( dbGoTop() )

   RETURN
