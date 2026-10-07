// Test 145: a codeblock whose last expression calls a PROCEDURE yields NIL.
//
// A PROCEDURE is void in C#, and the emitter wrote every block as `=> call`.
// With an argument dynamic the call bound at run time and failed there
// ("Cannot implicitly convert type 'void' to 'object'"), as EasiPOS's
// XZFirst() did with AEval( aMore, {|x| XZLine( …, RTrim( x ), .T. )} );
// with every argument typed it would not have compiled. The scan records a
// PROCEDURE in the reftab (flag N), and such a block is { call; return null; }.

STATIC s_cLog145 := ""

PROCEDURE Note145( cText )
   s_cLog145 += "[" + cText + "]"
   RETURN

STATIC PROCEDURE Quiet145( cText )
   s_cLog145 += "(" + cText + ")"
   RETURN

PROCEDURE Main()

   LOCAL aWords145 := { "one", "two ", "three" }
   LOCAL bNote145 := {|x| Note145( RTrim( x ) ) }
   LOCAL xResult

   // x says nothing of its type: the call binds at run time
   AEval( aWords145, {|x| Note145( RTrim( x ) ) } )
   ? "AEval, bound at run time:", s_cLog145

   // cWord is a string: the call binds when C# compiles
   s_cLog145 := ""
   AEval( aWords145, {|cWord| Note145( cWord ) } )
   ? "AEval, bound when compiled:", s_cLog145

   s_cLog145 := ""
   xResult := Eval( bNote145, "four " )
   ? "Eval:", s_cLog145, "value", iif( xResult == NIL, "NIL", "not NIL" )

   s_cLog145 := ""
   AEval( aWords145, {|x| Quiet145( x ) } )
   ? "a STATIC PROCEDURE:", s_cLog145

   // several expressions, the last a procedure
   s_cLog145 := ""
   xResult := Eval( {|x| Note145( "a" ), Note145( x ) }, "b" )
   ? "a list:", s_cLog145, "value", iif( xResult == NIL, "NIL", "not NIL" )

   RETURN
