// Test 43: multiple classes in one .prg file.
public class Foo : IHbObject
{
    public decimal nA = 1;

}

public class Bar : IHbObject
{
    public decimal nB = 2;

}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Foo oF = new Foo();
        Bar oB = new Bar();

        QOut("Foo:nA = " + Str(oF.nA, 4));
        QOut("Bar:nB = " + Str(oB.nB, 4));

        return;
    }
}
