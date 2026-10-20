// Test 49: Super / className handling.
//
// Covers all four forms the transpiler maps:
//   1. obj:className()        → HbRuntime.CLASSNAME(obj)
//   2. obj:Super():className() → HbRuntime.CLASSNAME(obj.Super())  (the HbSuperRef: parent class)
//   3. ::Super:className()    → HbRuntime.CLASSNAME(this.Super()) (same as 2, colon form)
//   4. ::Super:Method(args)   → base.Method(args)          (inheritance call)
//
// The last form is the important one — Harbour's idiomatic "call the
// parent class's implementation" that C# spells as `base.Method(...)`.
public class Animal : IHbObject
{

    public dynamic Kind() => "animal";
    public virtual Animal Speak()
    {
        QOut("Animal::Speak");
        return this;
    }
}

public class Dog : Animal
{

    public override Dog Speak()
    {
        // form 4: → base.Speak()
        base.Speak();
        QOut("Dog::Speak");
        return this;
    }

    public virtual Dog Identify()
    {
        // form 1
        QOut("className=" + Upper(CLASSNAME(this)));
        // form 3
        QOut("Super-className=" + Upper(CLASSNAME(this.Super())));
        // form 4 (inline parent method)
        QOut("Kind=" + base.Kind());
        return this;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Dog oDog = new Dog();
        Animal oAnimal = new Animal();

        // form 1
        QOut("oAnimal:className()=" + Upper(CLASSNAME(oAnimal)));
        // form 1
        QOut("oDog:className()=" + Upper(CLASSNAME(oDog)));
        // form 2
        QOut("oDog:Super():className()=" + Upper(CLASSNAME(oDog.Super())));

        oDog.Speak();
        oDog.Identify();
    }
}
