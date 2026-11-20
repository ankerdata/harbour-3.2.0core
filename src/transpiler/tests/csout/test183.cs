// Test 183: ordering two values the program leaves untyped.
// `<`, `<=`, `>` and `>=` between two `x` values, an element of a plain
// array, a function that answers any type or a block's result may meet
// two strings when the line runs, which C# cannot order with an operator
// through dynamic. They go through HbRuntime.Cmp(): strings by the
// program's collation, logicals .F. before .T., numbers and dates as the
// operator does. A typed operand keeps the operator.
public class Scope183 : IHbObject
{
    public dynamic xTop;

}

public static partial class Program
{
    public static void Main(string[] args)
    {
        var aMixed183 = new List<dynamic> { "pear", "Apple", "fig" };
        var aNumbers183 = new List<dynamic> { 30, 4, 17 };
        Func<dynamic, dynamic> bPick183 = ((Func<dynamic, dynamic>)((xValue) => xValue));
        var oScope183 = new Scope183();

        // two untyped parameters: strings, numbers, dates, logicals
        QOut(Lower183("pear", "fig"), Lower183("fig", "pear"));
        QOut(Lower183(3, 20), Lower183(2.5m, 2.25m));
        QOut(Lower183(Date(), Date().AddDays((int)(1))) == Date());
        QOut(Lower183(true, false), Lower183(false, true));

        // an element of a plain array against another
        QOut(Cmp(aMixed183[I(1)], aMixed183[I(2)]) > 0, Cmp(aMixed183[I(3)], aMixed183[I(1)]) <= 0);
        QOut(Cmp(aNumbers183[I(1)], aNumbers183[I(2)]) >= 0);

        // a sort block with untyped parameters, over strings and over numbers
        ASort(aMixed183, null, null, ((Func<dynamic, dynamic, bool>)((xLeft, xRight) => Cmp(xLeft, xRight) < 0)));
        QOut(aMixed183[I(1)], aMixed183[I(2)], aMixed183[I(3)]);
        ASort(aNumbers183, null, null, ((Func<dynamic, dynamic, bool>)((xLeft, xRight) => Cmp(xLeft, xRight) > 0)));
        QOut(aNumbers183[I(1)], aNumbers183[I(2)], aNumbers183[I(3)]);

        // a block's result against an `x` member, as a scope is tested
        oScope183.xTop = "b";
        QOut(Cmp(Eval(bPick183, "c"), oScope183.xTop) >= 0, Cmp(Eval(bPick183, "a"), oScope183.xTop) >= 0);
        oScope183.xTop = 10;
        QOut(Cmp(Eval(bPick183, 12), oScope183.xTop) >= 0, Cmp(Eval(bPick183, 9), oScope183.xTop) >= 0);

        // a function that answers any type
        QOut(Cmp(AnyOf183(1), AnyOf183(2)) < 0, Cmp(AnyOf183(3), AnyOf183(4)) < 0);
    }

    public static dynamic Lower183(dynamic xLeft = default, dynamic xRight = default)
    {
        return (Cmp(xLeft, xRight) <= 0 ? xLeft : xRight);
    }

    public static dynamic AnyOf183(decimal nWhich = default)
    {
        if (nWhich == 1)
        {
            return "alpha";
        }
        else if (nWhich == 2)
        {
            return "beta";
        }
        else if (nWhich == 3)
        {
            return 7;
        }

        return 5;
    }
}
