// Test 181: `#pragma TEXTHIDDEN`. A string literal of a statement written
// while it is on is emitted as encoded bytes and decoded when it is used,
// so the text is not in the assembly's string table; a statement after
// the pragma is switched off keeps its literal. The program's output is
// the same either way.
public static partial class Program
{
    public static void Main(string[] args)
    {
        string cPlain = "plain text 181";

        QOut(HiddenKey181());
        QOut(Len(HiddenKey181()));
        QOut(HiddenJoin181("-"));
        QOut(HiddenAccent181());
        QOut(cPlain);
    }

    public static string HiddenKey181()
    {
        return HbHiddenText(new byte[] { 0xF4, 0xA3, 0x96, 0x65, 0x4E, 0x27, 0x40, 0xB1, 0xA7, 0x8F });
    }

    public static string HiddenJoin181(string cSep = default)
    {
        string cRight = "shown";

        string cLeft = HbHiddenText(new byte[] { 0xCC, 0xA3, 0x95, 0x70 }) + cSep + HbHiddenText(new byte[] { 0xC5, 0xA7, 0x86, 0x6F });

        return cLeft + cSep + cRight;
    }

    public static string HiddenAccent181()
    {
        return HbHiddenText(new byte[] { 0xFD, 0x05, 0x41, 0x6C, 0x4F, 0x27, 0x13, 0xA0, 0x7D, 0x3C, 0x71 });
    }
}
