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
    public DateTime dBirth { get; set; }
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
        HbRuntime.QOut("nAge=" + HbRuntime.Str(this.nAge));
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
        HbRuntime.QOut("oPerson created");

        oPerson.SetAge(25);
        HbRuntime.QOut("FullName=" + oPerson.FullName);

        return;
    }
}
