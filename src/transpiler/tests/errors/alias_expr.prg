// Regression: ALIAS expression context — must fail -GS codegen.
// Patterns copied from sharedx/arrfinit.prg (lAppend := BTNFlags->(eof()))
// and sharedx/loadflag.prg (while !Flags->(eof())).

PROCEDURE Main()
   LOCAL lAppend

   lAppend := BTNFlags->( eof() )

   DO WHILE !Flags->( eof() )
      Flags->( dbSkip() )
   ENDDO

   RETURN
