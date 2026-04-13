using System;
using System.Collections.Generic;

// Test 6: CLASS inheritance, DATA/CLASSDATA, ACCESS/ASSIGN, scope modifiers
// #include "hbclass.ch"
public class Inherited
{
    public static decimal nVersion = 1.0m;

}

public class Person : Inherited
{
    public decimal nAge { get; set; } = 0;
    public string cName { get; set; } = "";
    public DateOnly dBirth { get; set; }
    public static decimal nCount = 0;
    public dynamic FullName { get; set; }
        protected string cSecret { get; set; } = "hidden";

    public object New()
    {
        return this;
    }

    public object SetAge(decimal nAge = default)
    {
        this.nAge = nAge;
        HbRuntime.QOUT("nAge=" + HbRuntime.STR(this.nAge));
        return this;
    }

    public decimal InternalCalc()
    {
        return this.nAge * 2;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Person oPerson = new Person();
        HbRuntime.QOUT("oPerson created");

        oPerson.SetAge(25);
        HbRuntime.QOUT("FullName=" + oPerson.FullName);

        return;
    }
}
