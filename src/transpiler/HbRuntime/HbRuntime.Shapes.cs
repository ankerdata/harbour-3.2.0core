using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

// Shapes: the base of every class a `SHAPE X( Key AS …, … )` declares.
// One part of HbRuntime; HbRuntime.cs says what the whole is.
//
// A shape is a hash to Harbour, whose keys and their types are declared,
// and a class of public fields to C#, one per key, in the declaration's
// order. Typed code reads a field (`hcFlags.FullScreen`). Code that holds
// one as `dynamic`, and every hb_H*() function, hb_jsonEncode(), ValType()
// and Empty(), reads it through IDictionary, as any hash: the keys are the
// fields' names, all of them there always, and none can be added or taken
// away.
public abstract class HbShape : IDictionary
{
    static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, FieldInfo[]> s_fields = new();

    FieldInfo[] ShapeFields() => s_fields.GetOrAdd(GetType(), static oType =>
    {
        var aFields = oType.GetFields(BindingFlags.Public | BindingFlags.Instance);
        Array.Sort(aFields, static (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
        return aFields;
    });

    FieldInfo ShapeField(object key)
    {
        if (key is string cKey)
            foreach (var oField in ShapeFields())
                if (oField.Name == cKey)
                    return oField;
        return null;
    }

    // The value under a key: a key the shape has not is a bound error, as
    // a hash's is. A value of another type than the field's is converted
    // where Harbour's numbers agree (a whole decimal into a long).
    public dynamic this[object key]
    {
        get
        {
            var oField = ShapeField(key)
                ?? throw new KeyNotFoundException("Bound error: array access (" + GetType().Name + " has no key " + key + ")");
            return oField.GetValue(this);
        }
        set
        {
            var oField = ShapeField(key)
                ?? throw new KeyNotFoundException("Bound error: array assign (" + GetType().Name + " has no key " + key + ")");
            oField.SetValue(this, ShapeValue(oField.FieldType, value));
        }
    }

    static object ShapeValue(Type oType, object value)
    {
        if (value == null || oType.IsInstanceOfType(value))
            return value;
        Type oTarget = Nullable.GetUnderlyingType(oType) ?? oType;
        if (oTarget == typeof(long) && value is decimal n && n != Math.Truncate(n))
            throw new ArgumentException("A whole number's key takes no fraction: " + n);
        if (oTarget == typeof(long) || oTarget == typeof(decimal))
            return Convert.ChangeType(value, oTarget, System.Globalization.CultureInfo.InvariantCulture);
        return value;
    }

    public bool Contains(object key) => ShapeField(key) != null;
    public int Count => ShapeFields().Length;
    public bool IsFixedSize => true;
    public bool IsReadOnly => false;
    public bool IsSynchronized => false;
    public object SyncRoot => this;

    public ICollection Keys
    {
        get
        {
            var aKeys = new List<object>();
            foreach (var oField in ShapeFields())
                aKeys.Add(oField.Name);
            return aKeys;
        }
    }

    public ICollection Values
    {
        get
        {
            var aValues = new List<object>();
            foreach (var oField in ShapeFields())
                aValues.Add(oField.GetValue(this));
            return aValues;
        }
    }

    public void Add(object key, object value) =>
        throw new NotSupportedException(GetType().Name + " is a shape: its keys are declared");
    public void Remove(object key) =>
        throw new NotSupportedException(GetType().Name + " is a shape: its keys are declared");
    public void Clear() =>
        throw new NotSupportedException(GetType().Name + " is a shape: its keys are declared");

    public void CopyTo(Array array, int index)
    {
        foreach (DictionaryEntry oEntry in this)
            array.SetValue(oEntry, index++);
    }

    public IDictionaryEnumerator GetEnumerator()
    {
        var aEntries = new List<DictionaryEntry>();
        foreach (var oField in ShapeFields())
            aEntries.Add(new DictionaryEntry(oField.Name, oField.GetValue(this)));
        return new ShapeEnumerator(aEntries);
    }

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
