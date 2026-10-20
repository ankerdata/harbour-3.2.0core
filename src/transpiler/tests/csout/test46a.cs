// Test 46a + 46b: an array passed with @ to a function in another file,
// every caller at the full arity.
//
//    test46a.prg  defines  PROCEDURE SaveFlags( cFile, aFlag, nCount )
//    test46b.prg  calls    SaveFlags( "out.dat", @aArr, 3 )  // full only
//
// The array's parameter is the List itself in C#, a reference without
// `ref`, so the call passes aArr as written, and SaveFlags is declared
// once.
public static partial class Program
{
    public static void SaveFlags(string cFile, List<dynamic> aFlag, decimal nCount = default)
    {
        long i;

        ASize(aFlag, nCount);
        for (i = 1; i <= nCount; i++)
        {
            aFlag[I(i)] = cFile + "#" + Str(i, 1);
        }
    }
}
