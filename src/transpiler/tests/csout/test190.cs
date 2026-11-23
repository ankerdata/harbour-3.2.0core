// Test 190: a wrapper's method calls the method of the same name on what
// it wraps, an object whose class nothing here declares (a connector's
// proxy). Till190:Ring190() takes a whole number first and text second;
// what it wraps takes text first and an amount second. The arguments of
// the inner call are the inner method's, so they are passed as they are
// and not cast after the till's own parameters. Nothing is wrapped in
// this run, so the till answers itself.
public class Till190 : IHbObject
{
    public dynamic oInside;

    public virtual Till190 New()
    {
        return this;
    }

    public virtual dynamic Ring190(long iTimes = default, string cWhat = default)
    {
        if (oInside != null)
        {
            return oInside.Ring190(cWhat, iTimes * 1.25m);
        }

        return cWhat + " x" + LTrim(Str(iTimes));
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Till190 oTill190 = new Till190().New();

        QOut(oTill190.Ring190(7, "tea"));
        QOut(oTill190.Ring190(2, "milk"));
    }
}
