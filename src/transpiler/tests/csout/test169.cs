// Test 169: a list of a class that stands alone is typed storage. A class
// that inherits nothing and that nothing inherits is as closed as a record,
// so AS ARRAY OF CLASS X of it is a C# List<X>: on a local, a member, a
// parameter and a function's result, its literal and AAdd() checked by the
// C#. A list of a class with a family stays a List<dynamic>, since a
// List<Child> is no List<Parent>.
public class Knob169 : IHbObject
{
    public string cText = "";
    public long iCode = 0;

    public virtual Knob169 New(string cText = default, long iCode = default)
    {
        this.cText = cText;
        this.iCode = iCode;

        return this;
    }
}

public class Panel169 : IHbObject
{
    public List<Knob169> aKnobs = new List<Knob169>();

    public virtual Panel169 Add(Knob169 oKnob169 = default)
    {
        AAdd(aKnobs, oKnob169);

        return this;
    }

    public virtual string Texts()
    {
        string cTexts = "";

        foreach (Knob169 oKnob169 in HbEnumValues(aKnobs))
        {
            cTexts += oKnob169.cText + " ";
        }

        return cTexts;
    }
}

public class Shape169 : IHbObject
{

    public virtual string Name()
    {
        return "shape";
    }
}

public class Round169 : Shape169
{

    public override string Name()
    {
        return "round";
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        var aKnobs = new List<Knob169> { new Knob169().New("Cancel", 0), new Knob169().New("OK", 1) };
        var aShapes = new List<Shape169> { new Shape169(), new Round169() };
        var oPanel169 = new Panel169();

        AAdd(aKnobs, new Knob169().New("Search", 3));

        foreach (Knob169 oKnob169 in HbEnumValues(aKnobs))
        {
            QOut(oKnob169.cText, oKnob169.iCode);
            oPanel169.Add(oKnob169);
        }

        QOut(CodeTotal169(aKnobs), Len(Yes169()), Yes169()[I(1)].cText);
        QOut(AScan(aKnobs, ((Func<Knob169, bool>)((oKnob169) => oKnob169.iCode == 3))));
        QOut(oPanel169.Texts(), Len(oPanel169.aKnobs));

        for (long i = 1; i <= Len(aShapes); i++)
        {
            QOut(aShapes[I(i)].Name());
        }
    }

    public static long CodeTotal169(List<Knob169> aList = default)
    {
        long iTotal = 0;

        for (long i = 1; i <= Len(aList); i++)
        {
            iTotal += aList[I(i)].iCode;
        }

        return iTotal;
    }

    public static List<Knob169> Yes169()
    {
        return new List<Knob169> { new Knob169().New("Yes", 1) };
    }
}
