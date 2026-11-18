// Test 182: a codeblock parameter typed from its callers' blocks.
// A `b` parameter is a delegate in C# when every caller passes a block of
// one shape: a literal, a local made from one, or the caller's own typed
// parameter. A parameter the routine gives a block itself (DEFAULT), or
// whose callers disagree, stays a plain block. A block of another shape
// reaching a typed parameter through a plain variable is called through
// a delegate of the parameter's shape.
public static partial class Program
{
    public static void Main(string[] args)
    {
        Func<decimal, bool> bBig182 = ((Func<decimal, bool>)((nValue) => nValue > 10));
        dynamic xAny182 = AnyBlock182();

        // a literal, a local, and NIL
        QOut(CountIf182(new List<dynamic> { 5, 15, 25 }, ((Func<decimal, bool>)((nValue) => nValue > 10))));
        QOut(CountIf182(new List<dynamic> { 5, 15, 25 }, bBig182));
        QOut(CountIf182(new List<dynamic> { 5, 15, 25 }));

        // a parameter passed on
        QOut(CountTwice182(new List<dynamic> { 1, 11, 21, 31 }, BlockAs<Func<decimal, bool>>(bBig182)));

        // a block of another shape, through a plain variable
        QOut(CountIf182(new List<dynamic> { 5, 15, 25 }, xAny182));

        // the routine gives itself a block
        QOut(FirstOr182(new List<dynamic> { 3, 4 }));
        QOut(FirstOr182(new List<dynamic> { 3, 4 }, ((Func<decimal, decimal>)((nValue) => nValue * 2))));

        // callers that disagree: a logical block first, then two that are not
        QOut(Describe182(((Func<decimal, bool>)((nValue) => nValue > 1)), 5));
        QOut(Describe182(((Func<string, string>)((cName) => "name " + cName)), "a"));
        QOut(Describe182(((Func<decimal, decimal>)((nValue) => nValue + 1)), 1));
    }

    public static decimal CountIf182(List<dynamic> aValues = default, dynamic? bTest182 = null)
    {
        decimal nCount = 0;

        if (bTest182 == null)
        {
            return -1;
        }

        foreach (decimal nValue in HbEnumValues(aValues))
        {
            if (Eval(bTest182, nValue))
            {
                nCount++;
            }
        }

        return nCount;
    }

    public static dynamic AnyBlock182()
    {
        return ((Func<bool>)(() => true));
    }

    public static decimal CountTwice182(List<dynamic> aValues = default, Func<decimal, bool> bPass182 = default)
    {
        return CountIf182(aValues, bPass182) * 2;
    }

    public static dynamic FirstOr182(List<dynamic> aValues = default, dynamic? bMap182 = null)
    {
        if (bMap182 == null)
        {
            bMap182 = ((Func<decimal, decimal>)((nValue) => nValue));
        }

        return Eval(bMap182, aValues[I(1)]);
    }

    public static dynamic Describe182(dynamic bWhat182 = default, dynamic xValue = default)
    {
        return Eval(bWhat182, xValue);
    }
}
