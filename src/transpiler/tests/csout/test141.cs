// Test 141: typed codeblock parameters and results (plan C9).
//
// A codeblock is a C# lambda cast to a Func. Its parameters were all
// `dynamic`; now one takes what its Hungarian name says (`nValue` a
// decimal, `cWord` a string, `oParcel141` its class), and one whose name
// says nothing takes the element type of the declared array an AScan,
// AEval or ASort hands it. The result is typed where the body's is
// certain: a comparison is bool, `Upper( cWord ) + "!"` a string. A local
// initialised with a block is declared with the lambda's own signature.
// HbRuntime calls a block through reflection, which converts no number
// between long and decimal, so InvokeBlock does: an `n` parameter takes a
// number held as long (an element added from an `i` name), an `i`
// parameter one held as decimal (Val()'s). A string parameter left out is
// NIL, as in Harbour.
public class Parcel141 : IHbObject
{
    public string cLabel = "";
    public decimal nWeight = 0;

    public virtual Parcel141 New(string cLabel = default, decimal nWeight = default)
    {
        this.cLabel = cLabel;
        this.nWeight = nWeight;
        return this;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        var aParcels = new List<dynamic>();
        var aLoose = new List<dynamic> { new Parcel141().New("Crate", 3) };
        var aCounts = new List<dynamic>();
        long iCount;
        decimal nTotal = 0;
        long iSum = 0;
        Func<string, string> bShout = ((Func<string, string>)((cWord) => Upper(cWord) + "!"));
        decimal nPos;

        AAdd(aParcels, new Parcel141().New("Books", 4));
        AAdd(aParcels, new Parcel141().New("Lamp", 2));
        AAdd(aParcels, new Parcel141().New("Rug", 7));

        // an element of the declared array: x is a Parcel141, the result bool
        nPos = AScan(aParcels, ((Func<Parcel141, bool>)((x) => x.nWeight > 5)));
        QOut(nPos, ((Parcel141)aParcels[I(nPos)]).cLabel);

        // ASort's two parameters, both elements
        ASort(aParcels, null, null, ((Func<Parcel141, Parcel141, bool>)((x, y) => x.nWeight < y.nWeight)));
        AEval(aParcels, ((Func<Parcel141, dynamic>)((x) => QOut(x.cLabel))));

        // an o<Class> name types its parameter over an undeclared array
        QOut(AScan(aLoose, ((Func<Parcel141, bool>)((oParcel141) => oParcel141.nWeight == 3))));

        // numbers held as long reach an n parameter
        for (iCount = 1; iCount <= 3; iCount++)
        {
            AAdd(aCounts, iCount * 10);
        }

        AEval(aCounts, ((Func<decimal, dynamic>)((nValue) => nTotal += nValue)));
        QOut(nTotal);

        // numbers held as decimal reach an i parameter
        AEval(new List<dynamic> { Val("3"), Val("4") }, ((Func<long, dynamic>)((iWhole) => iSum += iWhole)));
        QOut(iSum);

        // a string parameter, a local's block, a parameter left out
        QOut(Eval(bShout, "hello"));
        QOut(Eval(((Func<string, string, bool>)((cFirst, cSecond) => cSecond == null)), "only"));
    }
}
