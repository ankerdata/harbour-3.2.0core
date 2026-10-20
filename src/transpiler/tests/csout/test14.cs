using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 14: Two classes with inheritance, constructor params, method chaining
public class Animal : IHbObject
{
    public string cName = "";
    public decimal nLegs = 4;
    public string cSound = "";

    public virtual Animal New(string cName = default, string cSound = default)
    {
        this.cName = cName;
        QOut("cName=" + this.cName);
        this.cSound = cSound;
        QOut("cSound=" + this.cSound);
        return this;
    }

    public virtual string Speak()
    {
        return cName + " says: " + cSound;
    }

    public virtual string Describe()
    {
        return cName + " has " + Str(nLegs) + " legs";
    }
}

public class Dog : Animal
{
    public string cBreed = "";

    public virtual Dog Init(string cName = default, string cBreed = default)
    {
        this.cName = cName;
        QOut("cName=" + this.cName);
        this.cBreed = cBreed;
        QOut("cBreed=" + this.cBreed);
        return this;
    }

    public virtual string DescribeFull()
    {
        return Describe() + " (" + cBreed + ")";
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Animal oAnimal = new Animal().New("Cat", "Meow");
        Dog oDog = new Dog().Init("Rex", "Labrador");

        QOut("Speak=" + oAnimal.Speak());
        QOut("Describe=" + oAnimal.Describe());
        QOut("Speak=" + oDog.Speak());
        QOut("DescribeFull=" + oDog.DescribeFull());

        return;
    }
}
