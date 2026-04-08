// Single line comment
FUNCTION Main()

   // Double-slash inside function
   LOCAL x := 5  // trailing comment on code line

   && dBASE-style comment
   LOCAL y := 10  && trailing dBASE comment

   * Star comment at start of line
   LOCAL z := x + y

   NOTE Old-style comment keyword

   /* Single-line block comment */
   LOCAL cResult := "hello"

   ? "x=" + Str( x )
   ? "y=" + Str( y )
   ? "z=" + Str( z )
   ? "cResult=" + cResult

   /* Multi-line
      block comment
      spanning three lines */
   cResult := cResult + " world"
   ? "cResult=" + cResult

   x := x /* inline block comment */ + 1
   ? "x=" + Str( x )

RETURN cResult
