using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

// Shapes: the base of every class a `SHAPE X( Key AS …, … )` declares.
// One part of HbRuntime; HbRuntime.cs says what the whole is.
//
// A shape is a hash to Harbour, whose keys and their types are declared,
// and a class to C#, a member per key in the declaration's order. Typed
// code reads a member (`hcFlags.FullScreen`). Code that holds one as
// `dynamic`, and every hb_H*() function, hb_jsonEncode(), ValType() and
// Empty(), reads it through IDictionary, as any hash: the keys are the
// members' names, and none can be added or taken away.
//
// A key is there always, unless it is declared OPTIONAL: that one is there
// once it has a value, as a key of Harbour's hash is there once it is set.
// The emitter writes an optional reference as a field marked
// [HbOptionalKey], absent while null, and an optional value (a number, a
// logical, a date) as a property over a nullable field `_Key`, absent
// while that is null.
[AttributeUsage(AttributeTargets.Field)]
public sealed class HbOptionalKeyAttribute : Attribute { }

public abstract class HbShape : IDictionary
{
    sealed class ShapeKey
    {
        public string Name;
        public FieldInfo Field;
        public bool Optional;
        public Type Type;
    }

    static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, ShapeKey[]> s_keys = new();

    // The keys, in the declaration's order: the fields the class declares,
    // which the compiler keeps in theirs. A private `_Key` is the store of
    // an optional value.
    ShapeKey[] ShapeKeys() => s_keys.GetOrAdd(GetType(), static oType =>
    {
        var aFields = oType.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                      BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Array.Sort(aFields, static (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
        var aKeys = new List<ShapeKey>();
        foreach (var oField in aFields)
        {
            bool lStore = !oField.IsPublic && oField.Name.StartsWith('_');
            if (!oField.IsPublic && !lStore)
                continue;
            aKeys.Add(new ShapeKey
            {
                Name = lStore ? oField.Name.Substring(1) : oField.Name,
                Field = oField,
                Optional = lStore || oField.IsDefined(typeof(HbOptionalKeyAttribute), false),
                Type = Nullable.GetUnderlyingType(oField.FieldType) ?? oField.FieldType
            });
        }
        return aKeys.ToArray();
    });

    ShapeKey FindKey(object key)
    {
        if (key is string cKey)
            foreach (var oKey in ShapeKeys())
                if (oKey.Name == cKey)
                    return oKey;
        return null;
    }

    bool IsThere(ShapeKey oKey) => !oKey.Optional || oKey.Field.GetValue(this) != null;

    // An optional value read by typed code: unset, it is the bound error
    // the key's absence is in Harbour
    protected T Set<T>(T? xValue, [CallerMemberName] string cKey = null) where T : struct =>
        xValue ?? throw new KeyNotFoundException("Bound error: array access (" + GetType().Name + " has no " + cKey + " yet)");

    // The value under a key: a key that is not there is a bound error, as
    // a hash's is. A value of another type than the key's is converted
    // where Harbour's numbers agree (a whole decimal into a long).
    public dynamic this[object key]
    {
        get
        {
            var oKey = FindKey(key);
            if (oKey == null || !IsThere(oKey))
                throw new KeyNotFoundException("Bound error: array access (" + GetType().Name + " has no key " + key + ")");
            return oKey.Field.GetValue(this);
        }
        set
        {
            var oKey = FindKey(key)
                ?? throw new KeyNotFoundException("Bound error: array assign (" + GetType().Name + " has no key " + key + ")");
            oKey.Field.SetValue(this, ShapeValue(oKey.Type, (object)value));
        }
    }

    static object ShapeValue(Type oType, object value)
    {
        if (value == null || oType.IsInstanceOfType(value))
            return value;
        if (oType == typeof(long) && value is decimal n && n != Math.Truncate(n))
            throw new ArgumentException("A whole number's key takes no fraction: " + n);
        if (oType == typeof(long) || oType == typeof(decimal))
            return Convert.ChangeType(value, oType, System.Globalization.CultureInfo.InvariantCulture);
        return value;
    }

    public bool Contains(object key) => FindKey(key) is { } oKey && IsThere(oKey);
    public int Count
    {
        get
        {
            int n = 0;
            foreach (var oKey in ShapeKeys())
                if (IsThere(oKey))
                    n++;
            return n;
        }
    }
    public bool IsFixedSize => true;
    public bool IsReadOnly => false;
    public bool IsSynchronized => false;
    public object SyncRoot => this;

    List<DictionaryEntry> Entries()
    {
        var aEntries = new List<DictionaryEntry>();
        foreach (var oKey in ShapeKeys())
            if (IsThere(oKey))
                aEntries.Add(new DictionaryEntry(oKey.Name, oKey.Field.GetValue(this)));
        return aEntries;
    }

    public ICollection Keys
    {
        get
        {
            var aKeys = new List<object>();
            foreach (var oEntry in Entries())
                aKeys.Add(oEntry.Key);
            return aKeys;
        }
    }

    public ICollection Values
    {
        get
        {
            var aValues = new List<object>();
            foreach (var oEntry in Entries())
                aValues.Add(oEntry.Value);
            return aValues;
        }
    }

    public void Add(object key, object value) =>
        throw new NotSupportedException(GetType().Name + " is a shape: its keys are declared");

    // hb_HDel() of an optional key takes its value away; any other key stays
    public void Remove(object key)
    {
        if (FindKey(key) is { Optional: true } oKey)
            oKey.Field.SetValue(this, null);
        else
            throw new NotSupportedException(GetType().Name + " is a shape: its keys are declared");
    }

    public void Clear() =>
        throw new NotSupportedException(GetType().Name + " is a shape: its keys are declared");

    public void CopyTo(Array array, int index)
    {
        foreach (var oEntry in Entries())
            array.SetValue(oEntry, index++);
    }

    public IDictionaryEnumerator GetEnumerator() => new ShapeEnumerator(Entries());

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    sealed class ShapeEnumerator : IDictionaryEnumerator
    {
        readonly List<DictionaryEntry> _aEntries;
        int _i = -1;
        public ShapeEnumerator(List<DictionaryEntry> aEntries) => _aEntries = aEntries;
        public DictionaryEntry Entry => _aEntries[_i];
        public object Key => Entry.Key;
        public object Value => Entry.Value;
        public object Current => Entry;
        public bool MoveNext() => ++_i < _aEntries.Count;
        public void Reset() => _i = -1;
    }
}
