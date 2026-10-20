// Test 6: CLASS inheritance, DATA/CLASSDATA, ACCESS/ASSIGN, scope modifiers
//
// Also exercises two regression paths:
//
//  - `EXPORT <name>` and `PROTECT <name>` shorthand from hbclass.ch.
//    The parser used to mis-read bare `EXPORT <name>` as the EXPORTED
//    section header (which swallows the line), so every EXPORT-prefixed
//    data member went missing. The fix distinguishes the header form
//    (followed by `:` or EOL) from the shorthand (followed by an
//    identifier) — see hbclsparse.c.
//
//  - INIT expressions that contain Harbour built-in function calls.
//    `INIT Space(3)` and `INIT CToD("")` are raw PP text, so they
//    bypass the normal HB_ET_FUNCALL remap. hb_csTranslateInit now
//    falls through to the INLINE translator, which walks identifiers
//    and prefixes built-ins from hbfuncs.tab using canonical casing
//    (→ `HbRuntime.Space`, `HbRuntime.CToD`). Without the canonical-
//    casing path the emit was `HbRuntime.SPACE` / `HbRuntime.CTOD`
//    which only compiled thanks to NIE stubs — throwing at runtime.
public class Inherited : IHbObject
{
    public static decimal nVersion = 1.0m;

}

public class Person : Inherited
{
    public decimal nAge = 0;
    public string cName = "";
    public DateOnly dBirth = CToD( "" );
    public string cInitials = Space( 3 );
    public static decimal nCount = 0;
    public dynamic FullName { get; set; }
        public string cPublicNotes;
    protected dynamic oContext;
    protected string cSecret = "hidden";

    public virtual Person New()
    {
        return this;
    }

    public virtual Person SetAge(decimal nAge = default)
    {
        this.nAge = nAge;
        QOut("nAge=" + Str(this.nAge));
        return this;
    }

    public virtual decimal InternalCalc()
    {
        return nAge * 2;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Person oPerson = new Person().New();
        QOut("oPerson created");

        oPerson.SetAge(25);
        QOut("FullName=" + oPerson.FullName);

        // Touch the EXPORT-shorthand member so it's visibly present at
        // compile AND runtime (the PROTECT counterpart `oContext` is
        // deliberately only accessed from inside the class).
        oPerson.cPublicNotes = "hello";
        QOut("cPublicNotes=" + oPerson.cPublicNotes);
        QOut("cInitials=[" + oPerson.cInitials + "]");

        return;
    }
}
