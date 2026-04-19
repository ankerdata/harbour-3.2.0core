using System;
using static HbRuntime;
using static Program;

// Test 22b: companion to test22a — second class also has an `Add`
// method, but with a STRING parameter instead of NUMERIC. Plus the
// Main that exercises both. See test22a.prg for the rationale.
// #include "hbclass.ch"
public class Names
{
    public string cAll { get; set; } = "";

    public dynamic New()
    {
        this.cAll = "";
        return this;
    }

    public dynamic Add(string cValue = default)
    {
        this.cAll = this.cAll + cValue + " ";
        return this;
    }

    public dynamic Show()
    {
        HbRuntime.QOUT("all=" + this.cAll);
        return this;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Numbers oNums = new Numbers();
        Names oNames = new Names();

        oNums.Add(10);
        oNums.Add(20);
        oNums.Add(30);
        oNums.Show();

        oNames.Add("alpha");
        oNames.Add("beta");
        oNames.Add("gamma");
        oNames.Show();

        return;
    }
}
