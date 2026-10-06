using System;
using static HbRuntime;
using static Program;

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

// #include "../include/astype.ch"
// #include "hbclass.ch"
public class Parcel141
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
        List<dynamic> aParcels = new List<dynamic> {  };
        List<dynamic> aLoose = new List<dynamic> { (Parcel141)new Parcel141().New("Crate", 3) };
        List<dynamic> aCounts = new List<dynamic> {  };
        long iCount = default;
        decimal nTotal = 0;
        long iSum = 0;
        Func<string, string> bShout = ((Func<string, string>)((cWord) => HbRuntime.Upper(cWord) + "!"));
        decimal nPos = default;

        HbRuntime.AAdd(aParcels, (Parcel141)new Parcel141().New("Books", 4));
        HbRuntime.AAdd(aParcels, (Parcel141)new Parcel141().New("Lamp", 2));
        HbRuntime.AAdd(aParcels, (Parcel141)new Parcel141().New("Rug", 7));

        // an element of the declared array: x is a Parcel141, the result bool
        nPos = HbRuntime.AScan(aParcels, ((Func<Parcel141, bool>)((x) => x.nWeight > 5)));
        HbRuntime.QOut(nPos, ((Parcel141)aParcels[(int)(nPos) - 1]).cLabel);

        // ASort's two parameters, both elements
        HbRuntime.ASort(aParcels, null, null, ((Func<Parcel141, Parcel141, bool>)((x, y) => x.nWeight < y.nWeight)));
        HbRuntime.AEval(aParcels, ((Func<Parcel141, dynamic>)((x) => HbRuntime.QOut(x.cLabel))));

        // an o<Class> name types its parameter over an undeclared array
        HbRuntime.QOut(HbRuntime.AScan(aLoose, ((Func<Parcel141, bool>)((oParcel141) => oParcel141.nWeight == 3))));

        // numbers held as long reach an n parameter
        for (iCount = 1; iCount <= 3; iCount++)
        {
            HbRuntime.AAdd(aCounts, iCount * 10);
        }

        HbRuntime.AEval(aCounts, ((Func<decimal, dynamic>)((nValue) => nTotal += nValue)));
        HbRuntime.QOut(nTotal);

        // numbers held as decimal reach an i parameter
        HbRuntime.AEval(new List<dynamic> { HbRuntime.Val("3"), HbRuntime.Val("4") }, ((Func<long, dynamic>)((iWhole) => iSum += iWhole)));
        HbRuntime.QOut(iSum);

        // a string parameter, a local's block, a parameter left out
        HbRuntime.QOut(HbRuntime.Eval(bShout, "hello"));
        HbRuntime.QOut(HbRuntime.Eval(((Func<string, string, bool>)((cFirst, cSecond) => cSecond == null)), "only"));
        return;
    }
}
