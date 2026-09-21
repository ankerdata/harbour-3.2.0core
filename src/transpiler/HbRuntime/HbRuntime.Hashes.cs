using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// Hashes: the hb_H*() functions, over .NET's OrderedDictionary.
// One part of HbRuntime; HbRuntime.cs says what the whole is.

public static partial class HbRuntime
{
    // ---- Hashes ----
    // A hash is an OrderedDictionary<string, dynamic> — <long, dynamic>
    // for a numeric-keyed one, <dynamic, dynamic> where the emitter knows
    // no key type (gencsharp.c): Harbour's default hash keeps its keys in
    // the order they were added, deletes included (HB_HASH_KEEPORDER),
    // and compares string keys exactly (HB_HASH_BINARY). Anything that
    // implements IDictionary is taken as a hash.

    static System.Collections.IDictionary AsDict(dynamic h) => h as System.Collections.IDictionary;
    // The (object) casts below are load-bearing. AsDict(h) with a
    // dynamic argument is itself dynamically bound, which makes `d`
    // dynamic too — and then d.Contains(...) resolves against the
    // hash's RUNTIME type (e.g. OrderedDictionary<string, dynamic>, which
    // has no public Contains): RuntimeBinderException. AsDict((object)h)
    // binds statically, `d` is IDictionary, and the key casts keep the
    // member calls bound to the non-generic IDictionary surface.

    // A key as the hash is keyed. Through the non-generic IDictionary a
    // key of another CLR type is simply not there — an int literal 5 is
    // not a long 5 nor a decimal 5 — so a number is converted to the
    // hash's key type: long for a numeric-keyed hash (which must then be
    // an integer: EasiPOS keys by ids, and 1.5 would silently become 1),
    // decimal where the key type is open, as Harbour compares numbers by
    // value. Anything else is used as given.
    static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Type> s_hashKeyTypes = new();

    static Type HashKeyType(Type t)
    {
        foreach (Type i in t.GetInterfaces())
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                return i.GetGenericArguments()[0];
        return typeof(object);
    }

    static object HashKey(System.Collections.IDictionary d, object key)
    {
        if (key is null || !IsNumeric(key))
            return key;
        Type t = s_hashKeyTypes.GetOrAdd(d.GetType(), HashKeyType);
        decimal n = Convert.ToDecimal(key, INV);
        if (t == typeof(long))
        {
            if (n != Math.Truncate(n))
                throw new ArgumentException("A numeric-keyed hash is keyed by integers: " + n.ToString(INV));
            return (long)n;
        }
        if (t == typeof(decimal) || t == typeof(object))
            return n;
        return key;
    }

    // hb_HGetDef( <h>, <key>[, <xDefault>] ): the value, or xDefault
    public static dynamic hb_HGetDef(dynamic h, dynamic key, dynamic def = null)
    {
        var d = AsDict((object)h);
        if (d == null)
            return def;
        object k = HashKey(d, (object)key);
        return d.Contains(k) ? d[k] : def;
    }

    public static bool hb_HHasKey(dynamic h, dynamic key)
    {
        var d = AsDict((object)h);
        return d != null && d.Contains(HashKey(d, (object)key));
    }

    // hb_HGet( <h>, <key> ): the value; a key that is not there is a
    // bound error (EG_BOUND 1132)
    public static dynamic hb_HGet(dynamic h, dynamic key)
    {
        var d = AsDict((object)h);
        object k = d == null ? null : HashKey(d, (object)key);
        if (d == null || !d.Contains(k))
            throw new KeyNotFoundException("Bound error: array access (HB_HGET)");
        return d[k];
    }

    // hb_HSet( <h>, <key>, <xValue> ): add or replace — a replaced key
    // keeps its place; returns the hash
    public static dynamic hb_HSet(dynamic h, dynamic key, dynamic value)
    {
        var d = AsDict((object)h);
        if (d != null)
            d[HashKey(d, (object)key)] = (object)value;
        return h;
    }

    // hb_HDel( <h>, <key> ): returns the hash
    public static dynamic hb_HDel(dynamic h, dynamic key)
    {
        var d = AsDict((object)h);
        if (d != null)
            d.Remove(HashKey(d, (object)key));
        return h;
    }

    // hb_HPos( <h>, <key> ): the key's position, 1-based; 0 if absent
    public static decimal hb_HPos(dynamic h, dynamic key)
    {
        var d = AsDict((object)h);
        if (d == null)
            return 0;
        object k = HashKey(d, (object)key);
        int i = 0;
        foreach (object each in d.Keys)
        {
            i++;
            if (Equals(each, k))
                return i;
        }
        return 0;
    }

    // hb_HKeyAt( <h>, <nPos> ): the key at a position, 1-based; a position
    // outside the hash is a bound error (EG_BOUND 1187)
    public static dynamic hb_HKeyAt(dynamic h, decimal nPos)
    {
        var d = AsDict((object)h);
        long n = (long)Math.Truncate(nPos);
        if (d != null && n >= 1 && n <= d.Count)
        {
            long i = 0;
            foreach (object each in d.Keys)
                if (++i == n)
                    return each;
        }
        throw new ArgumentOutOfRangeException(nameof(nPos), "Bound error (HB_HKEYAT)");
    }

    // hb_HKeepOrder( <h>[, <lKeepOrder>] ): whether the hash keeps its
    // keys in the order they were added. A hash here always does; Harbour
    // would sort one told .F. — EasiPOS only ever says .T.
    public static bool hb_HKeepOrder(dynamic h, bool? lKeepOrder = null) =>
        AsDict((object)h) != null;

    // hb_HClone( <h> ): a copy of the hash, its nested arrays and hashes
    // copied too (CloneNested); NIL for anything else
    public static dynamic hb_HClone(dynamic h) =>
        (object)h is System.Collections.IDictionary d
            ? CloneNested(d, new Dictionary<object, object>(ReferenceEqualityComparer.Instance))
            : null;

    // AClone() and hb_HClone() (vm/arrays.c, hb_nestedCloneDo): nested
    // arrays and hashes are cloned as well, each once — an array reached
    // twice is one clone reached twice, a cycle stays a cycle. A hash is
    // cloned as its own C# type. An object is shared, where Harbour would
    // clone its instance variables.
    static object CloneNested(object x, Dictionary<object, object> done)
    {
        if (x is object[] a)
        {
            if (done.TryGetValue(a, out object c))
                return c;
            var copy = new object[a.Length];
            done[a] = copy;
            for (int i = 0; i < a.Length; i++)
                copy[i] = CloneNested(a[i], done);
            return copy;
        }
        if (x is System.Collections.IDictionary d)
        {
            if (done.TryGetValue(d, out object c))
                return c;
            var copy = (System.Collections.IDictionary)Activator.CreateInstance(d.GetType());
            done[d] = copy;
            foreach (System.Collections.DictionaryEntry e in d)
                copy.Add(e.Key, CloneNested(e.Value, done));
            return copy;
        }
        return x;
    }

    // Harbour `$` operator: `a $ b` is substring containment when b is
    // a string — an empty a is in nothing (hb_strAt) — and key containment
    // when b is a hash
    public static bool HbIn(dynamic needle, dynamic haystack)
    {
        if (haystack is string s)
            return needle is string n && n.Length > 0 && s.Contains(n, StringComparison.Ordinal);
        if (haystack is System.Collections.IDictionary d)
            return d.Contains(HashKey(d, (object)needle));
        return false;
    }

    public static dynamic hb_HKeys(dynamic h)
    {
        var d = AsDict((object)h);
        if (d == null) return System.Array.Empty<dynamic>();
        var r = new dynamic[d.Count];
        int i = 0;
        foreach (var k in d.Keys) r[i++] = k;
        return r;
    }

    public static dynamic hb_HValues(dynamic h)
    {
        var d = AsDict((object)h);
        if (d == null) return System.Array.Empty<dynamic>();
        var r = new dynamic[d.Count];
        int i = 0;
        foreach (var v in d.Values) r[i++] = v;
        return r;
    }
}
