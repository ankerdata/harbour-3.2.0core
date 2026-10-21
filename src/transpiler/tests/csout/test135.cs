// Test 135: a FOR EACH's C# temporary takes its loop variable's type.
//
// Harbour's loop variable is an ordinary local that lives on after the
// loop, and a C# foreach cannot loop on an existing local, so the loop
// runs on a temporary and assigns the local: `foreach (dynamic
// __hb_fe_cName in …) { cName = __hb_fe_cName; …`. The local was typed by
// its name already; the temporary was `dynamic`. It takes the local's type
// now (string, a hash, an array, decimal, long), as a C# author would
// write the loop. A typed foreach converts each element with an explicit
// cast, so the integer iCount135 acts as an integer (Alex): a decimal
// element is cast (long), as a decimal written into an `i` name is
// anywhere else, where the dynamic temporary's implicit assignment threw
// on any decimal, a whole one (aWhole135's 5.0) too.
public static partial class Program
{
    public static void Main(string[] args)
    {
        var aNames135 = new List<dynamic> { "tin", "cup" };
        var aRows135 = new List<dynamic>
        {
            new List<dynamic> { "a", 1 },
            new List<dynamic> { "b", 2 }
        };
        var aHashes135 = new List<dynamic>
        {
            new OrderedDictionary<string, dynamic> { { "k", "v1" } },
            new OrderedDictionary<string, dynamic> { { "k", "v2" } }
        };
        var aAmounts135 = new List<dynamic> { 1.5m, 2.25m };
        var aCounts135 = new List<dynamic> { 3, 4 };
        var aWhole135 = new List<dynamic> { (decimal)(10) / 2 };
        string cName135;
        List<dynamic> aRow135;
        OrderedDictionary<string, dynamic> hField135;
        decimal nAmount135;
        long iCount135;
        string cOut = "";
        decimal nSum = 0;
        long iTotal = 0;

        foreach (string __hb_fe_cName135 in HbEnumValues(aNames135))
        {
            cName135 = __hb_fe_cName135;
            cOut += cName135 + ";";
        }

        foreach (List<dynamic> __hb_fe_aRow135 in HbEnumValues(aRows135))
        {
            aRow135 = __hb_fe_aRow135;
            cOut += aRow135[I(1)] + hb_ntos(aRow135[I(2)]) + ";";
        }

        foreach (OrderedDictionary<string, dynamic> __hb_fe_hField135 in HbEnumValues(aHashes135))
        {
            hField135 = __hb_fe_hField135;
            cOut += hField135["k"] + ";";
        }

        foreach (decimal __hb_fe_nAmount135 in HbEnumValues(aAmounts135))
        {
            nAmount135 = __hb_fe_nAmount135;
            nSum += nAmount135;
        }

        foreach (long __hb_fe_iCount135 in HbEnumValues(aCounts135))
        {
            iCount135 = __hb_fe_iCount135;
            iTotal += iCount135;
        }

        foreach (long __hb_fe_iCount135 in HbEnumValues(aWhole135))
        {
            iCount135 = __hb_fe_iCount135;
            iTotal += iCount135;
        }

        QOut(cOut);
        QOut(nSum);
        QOut(iTotal);
    }
}
