// Test 19b: calls Swap (defined in test19a.prg) with @ args.
// See test19a.prg for the cross-file scan workflow this test exercises.
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nA = 1;
        decimal nB = 2;

        QOut("before: a=" + Str(nA) + " b=" + Str(nB));
        Swap(ref nA, ref nB);
        QOut("after:  a=" + Str(nA) + " b=" + Str(nB));

        return;
    }
}
