// Test 171: a method declares its return type on its own line, before its
// class: METHOD M() AS <type> CLASS X. The CLASS of `AS CLASS Y` belongs to
// the type, not to the method, and the type is the method's as one declared
// in the class body is. Each method here returns an element of a plain
// array, which nothing else could type.
public class Coin171 : IHbObject
{
    public string cName = "";
    public long iCents = 0;

    public virtual Coin171 New(string cName = default, long iCents = default)
    {
        this.cName = cName;
        this.iCents = iCents;

        return this;
    }
}

public class Purse171 : IHbObject
{
    public List<dynamic> aThings = new List<dynamic>();

    public virtual Purse171 Fill()
    {
        aThings = new List<dynamic> { new Coin171().New("dime", 10), 2, "purse" };

        return this;
    }

    public virtual Coin171 FirstCoin()
    {
        return aThings[I(1)];
    }

    public virtual long CoinCount()
    {
        return (long)(aThings[I(2)]);
    }

    public virtual string Label()
    {
        return aThings[I(3)];
    }

    public virtual string Describe171()
    {
        return Upper(Label()) + ": " + FirstCoin().cName + " " + hb_ntos(CoinCount() + FirstCoin().iCents);
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Purse171 oPurse171 = new Purse171().Fill();

        QOut(oPurse171.FirstCoin().cName);
        QOut(oPurse171.CoinCount() + 1);
        QOut(Len(oPurse171.Label()));
        QOut(oPurse171.Describe171());
    }
}
