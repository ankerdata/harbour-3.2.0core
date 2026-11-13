// Test 174: a whole-number parameter's default from an untyped value.
//
// `DEFAULT iNo TO saSettings174[ 1 ]` gives an `i` parameter, a C# long,
// an element of a plain array, which holds whatever was put there: here a
// decimal, as a number read from a file is. C# takes an untyped whole
// number into a long by itself and refuses an untyped decimal at run time,
// so the default is cast as an assignment to an `i` local is. A default
// the scan knows whole (a literal, another `i` name) gets no cast.
public static partial class Program
{
    public static List<dynamic> test174_saSettings174 = new List<dynamic>();
    public static string Label174(long? iNo = null, long iWidth = 4)
    {
        long iNo_ = iNo ?? (long)(test174_saSettings174[I(1)]);

        return PadL(hb_ntos(iNo_), iWidth, "0");
    }

    public static void Main(string[] args)
    {
        AAdd(test174_saSettings174, Val("7"));

        QOut(Label174());
        QOut(Label174(12));
        QOut(Label174(iWidth: 2));
    }
}
