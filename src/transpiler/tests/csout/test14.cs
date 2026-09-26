using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 14: Two classes with inheritance, constructor params, method chaining

// #include "hbclass.ch"
public class Animal
{
    public string cName = "";
    public decimal nLegs = 4;
    public string cSound = "";

    public virtual Animal New(string cName = default, string cSound = default)
    {
        this.cName = cName;
        HbRuntime.QOut("cName=" + this.cName);
        this.cSound = cSound;
        HbRuntime.QOut("cSound=" + this.cSound);
        return this;
    }

    public virtual string Speak()
    {
        return cName + " says: " + cSound;
    }

    public virtual string Describe()
    {
        return cName + " has " + HbRuntime.Str(nLegs) + " legs";
    }
}

public class Dog : Animal
{
    public string cBreed = "";

    public virtual Dog Init(string cName = default, string cBreed = default)
    {
        this.cName = cName;
        HbRuntime.QOut("cName=" + this.cName);
        this.cBreed = cBreed;
        HbRuntime.QOut("cBreed=" + this.cBreed);
        return this;
    }

    public virtual dynamic DescribeFull()
    {
        return Describe() + " (" + cBreed + ")";
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Animal oAnimal = (Animal)new Animal().New("Cat", "Meow");
        Dog oDog = (Dog)new Dog().Init("Rex", "Labrador");

        HbRuntime.QOut("Speak=" + oAnimal.Speak());
        HbRuntime.QOut("Describe=" + oAnimal.Describe());
        HbRuntime.QOut("Speak=" + oDog.Speak());
        HbRuntime.QOut("DescribeFull=" + oDog.DescribeFull());

        return;
    }
}
