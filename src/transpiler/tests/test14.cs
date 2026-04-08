using System;
using System.Collections.Generic;

// Test 14: Two classes with inheritance, constructor params, method chaining

// #include "hbclass.ch"
public class Animal
{
    public string cName { get; set; } = "";
    public decimal nLegs { get; set; } = 4;
    public string cSound { get; set; } = "";

    public object New(string cName, string cSound)
    {
        this.cName = cName;
        HbRuntime.QOut("cName=" + this.cName);
        this.cSound = cSound;
        HbRuntime.QOut("cSound=" + this.cSound);
        return this;
    }

    public string Speak()
    {
        return this.cName + " says: " + this.cSound;
    }

    public string Describe()
    {
        return this.cName + " has " + HbRuntime.Str(this.nLegs) + " legs";
    }
}

public class Dog : Animal
{
    public string cBreed { get; set; } = "";

    public object Init(string cName, string cBreed)
    {
        this.cName = cName;
        HbRuntime.QOut("cName=" + this.cName);
        this.cBreed = cBreed;
        HbRuntime.QOut("cBreed=" + this.cBreed);
        return this;
    }

    public dynamic DescribeFull()
    {
        return this.Describe() + " (" + this.cBreed + ")";
    }
}

public static class Program
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
