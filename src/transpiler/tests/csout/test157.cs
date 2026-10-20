// Test 157: a PUBLIC named after a class is that class (plan C13).
//
// `PUBLIC oGauge157 := Gauge157():New()` with CLASS Gauge157: the field on
// the owning file's Program partial is `public static Gauge157 oGauge157;`,
// so a reader in another routine (Show157) types `oGauge157:nLevel`
// statically, as a local so named would be. A PUBLIC whose name says
// nothing stays `dynamic` (xBag157), and the sized form `List<dynamic>`
// (aSlots157): a PUBLIC's value is whatever its writers put there, and only
// the name is evidence. Until 2026-10-18 every PUBLIC was dynamic; EasiPOS's
// hardware settings, the array aHWFlag, became the class HWFlags held by
// the PUBLIC oHWFlags, read 1,300 times across the products.
public class Gauge157 : IHbObject
{
    public decimal nLevel = 0;
    public string cUnit = "mm";

}

public static partial class Program
{
    public static Gauge157 oGauge157;
    public static dynamic xBag157;
    public static List<dynamic> aSlots157;
    public static void Main(string[] args)
    {
        oGauge157 = new Gauge157();
        xBag157 = "anything";
        aSlots157 = Array(3);

        oGauge157.nLevel = 42;
        aSlots157[I(1)] = "x";
        Show157();
    }

    public static void Show157()
    {
        QOut(oGauge157.nLevel, oGauge157.cUnit, xBag157, Len(aSlots157));
    }
}
