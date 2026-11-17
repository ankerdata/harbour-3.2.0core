// Test 179: `@Name()` names its function to the C# compiler. One the
// program defines is FuncPtr( nameof( Name ) ), so a reference to a
// function that is not there fails the build; one of the runtime
// (`@Upper()`) is looked up in HbRuntime, where FuncPtr() by a name alone
// looks only in the program and found nothing.

PROCEDURE Main()

   LOCAL fOwn := @Double179()
   LOCAL fCore := @Upper()

   ? fOwn:exec( 4 ), fCore:exec( "abc" ), Apply179( @Double179(), 5 )

   RETURN

FUNCTION Double179( nValue )

RETURN nValue * 2

FUNCTION Apply179( fAny, nValue )

RETURN fAny:exec( nValue )
