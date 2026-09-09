// Test 100: a local shadowing a function's name; ++ / -- on a date.
//
// Harbour resolves `name( ... )` as a function call whatever locals are
// in scope; C# sees the local and refuses (CS0149 "Method name
// expected": easipayback's `local nTax` beside the tax.prg function
// `nTax()`). A user-function call whose name a local or parameter
// shadows (here `nLevy`, a local and a function) is qualified
// `Program.name( ... )`, the class every free function lives in — the
// same qualification a user `ToString` gets against object.ToString.
// `dDate++` / `--dDate` is date arithmetic in Harbour; C# DateOnly has
// no ++, so a DATE (or TIMESTAMP) operand emits `d = d.AddDays( ±1 )`
// (xzutil's `--dStart`, CS0023).
FUNCTION nLevy( nKind )
RETURN nKind * 10

PROCEDURE Main()
   LOCAL nLevy := 5
   LOCAL dDay := SToD( "20260909" )
   ? "levy=" + LTrim( Str( nLevy( 2 ) + nLevy ) )
   dDay++
   ? "next=" + DToS( dDay )
   --dDay
   --dDay
   ? "prev=" + DToS( dDay )
RETURN
