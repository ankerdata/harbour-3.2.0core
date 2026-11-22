// Test 186: a shape, a hash whose keys are declared. `SHAPE X( Key AS ..., ... )`
// names the keys and their types; a hash to Harbour, a class of a field per
// key to C#. A literal under the declaration is the class with its fields
// set, nested as deep as the declarations go; a subscript by a string
// literal is the field, read or written; `{ => }` is the shape with nothing
// set, built key by key. `AS SHAPE` with no name holds any shape. Held as
// an untyped value a shape is still a hash: ValType(), hb_HHasKey(), a
// subscript by a variable, hb_jsonEncode().
public sealed class Lamp186 : HbShape
{
    public string Name;
    public long Watts;
    public decimal Price;
    public bool Lit;
}

public sealed class Room186 : HbShape
{
    public string Title;
    public Lamp186 Lamp;
    public Lamp186 Spare;
    public OrderedDictionary<string, dynamic> Tags;
    public List<Lamp186> Lamps;
    public string uuid;
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        var hcLamp = new Lamp186
        {
            Name = "desk",
            Watts = 40,
            Price = 12.50m,
            Lit = true
        };

        var hcRoom = new Room186
        {
            Title = "study",
            Lamp = new Lamp186
            {
                Name = "ceiling",
                Watts = 60,
                Price = 30.25m,
                Lit = false
            },
            Spare = null,
            Tags = new OrderedDictionary<string, dynamic> { { "floor", "first" } },
            Lamps = new List<Lamp186> { hcLamp },
            uuid = "a-1"
        };
        var hcBuilt = new Lamp186();
        string cKey = "Title";

        QOut(hcLamp.Name, hcLamp.Watts + 1, hcLamp.Price * 2, hcLamp.Lit);
        hcLamp.Watts = 25;
        hcLamp.Lit = !hcLamp.Lit;
        QOut(hcLamp.Watts, hcLamp.Lit);

        QOut(hcRoom.Lamp.Name, hcRoom.Spare == null, hcRoom.Tags["floor"]);
        hcRoom.Spare = new Lamp186
        {
            Name = "spare",
            Watts = 5,
            Price = 1.00m,
            Lit = false
        };
        QOut(hcRoom.Spare.Watts, Len(hcRoom.Lamps), hcRoom.Lamps[I(1)].Name);
        AAdd(hcRoom.Lamps, MakeLamp186("floor", 15));
        QOut(Len(hcRoom.Lamps), hcRoom.Lamps[I(2)].Watts, hcRoom.uuid);

        hcBuilt.Name = "built";
        hcBuilt.Watts = 7;
        hcBuilt.Price = 2.75m;
        hcBuilt.Lit = true;
        QOut(Describe186(hcBuilt), Describe186(MakeLamp186("made", 9)));

        QOut(hb_jsonEncode(hcLamp));
        QOut(hb_jsonEncode(hcRoom));

        HbShape hcAny = hcLamp;
        QOut(hb_jsonEncode(hcAny));
        hcAny = hcBuilt;
        QOut(Wire186(hcAny));

        dynamic xHeld = hcRoom;
        QOut(ValType(xHeld), hb_HHasKey(xHeld, "Title"), hb_HHasKey(xHeld, "Colour"), xHeld[cKey]);
        QOut(Len(xHeld), hb_HGetDef(xHeld, "uuid", "none"), hb_HGetDef(xHeld, "Colour", "none"));
        QOut(hb_jsonEncode(new OrderedDictionary<string, dynamic>
        {
            { "Type", "Room" },
            { "Payload", hcRoom }
        }));
    }

    public static Lamp186 MakeLamp186(string cName = default, long iWatts = default)
    {
        return new Lamp186
        {
            Name = cName,
            Watts = iWatts,
            Price = 1.25m,
            Lit = true
        };
    }

    public static string Describe186(Lamp186 hcLamp = default)
    {
        return hcLamp.Name + " " + hb_ntos(hcLamp.Watts) + (hcLamp.Lit ? " lit" : " dark");
    }

    public static string Wire186(HbShape hcAny = default)
    {
        return hb_jsonEncode(hcAny);
    }
}
