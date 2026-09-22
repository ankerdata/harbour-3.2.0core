using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// Objects: runtime member access for the ORM-style classes
// (HbDynamicObject), the reflection helpers the emitter calls, and
// :className (on every value) / :Super() on every object.
// One part of HbRuntime; HbRuntime.cs says what the whole is.

/// <summary>
/// Base class for Harbour classes that use runtime member access
/// (::&amp;(name) patterns). Backed by a Dictionary so that column
/// names or dynamically-created properties resolve at runtime.
/// Statically-declared properties (from DATA/VAR) take precedence
/// via the reflection path in TryGetMember/TrySetMember.
/// </summary>
public class HbDynamicObject : System.Dynamic.DynamicObject
{
    private readonly Dictionary<string, dynamic> _bag =
        new Dictionary<string, dynamic>(StringComparer.OrdinalIgnoreCase);

    // Exposed so HbRuntime.GETMEMBER/SETMEMBER — the obj:&(name) macro
    // path, which doesn't go through the DLR — can reach the bag too,
    // not only statically reflected members.
    public bool TryBagGet(string name, out dynamic value) =>
        _bag.TryGetValue(name, out value);
    public void BagSet(string name, dynamic value) => _bag[name] = value;

    public override bool TryGetMember(System.Dynamic.GetMemberBinder binder, out object result)
    {
        var prop = GetType().GetProperty(binder.Name, HbRuntime.MemberFlags);
        if (prop != null)
        {
            result = prop.GetValue(this);
            return true;
        }
        // Class DATA members emit as plain fields, not properties — a
        // declared member reached via ((dynamic)this) lands here.
        var field = GetType().GetField(binder.Name, HbRuntime.MemberFlags);
        if (field != null)
        {
            result = field.GetValue(this);
            return true;
        }
        return _bag.TryGetValue(binder.Name, out result);
    }

    public override bool TrySetMember(System.Dynamic.SetMemberBinder binder, object value)
    {
        var prop = GetType().GetProperty(binder.Name, HbRuntime.MemberFlags);
        if (prop != null)
        {
            object coerced = value;
            if (value != null && prop.PropertyType != typeof(object) &&
                prop.PropertyType != value.GetType())
                coerced = Convert.ChangeType(value, prop.PropertyType);
            prop.SetValue(this, coerced);
            return true;
        }
        var field = GetType().GetField(binder.Name, HbRuntime.MemberFlags);
        if (field != null)
        {
            object coerced = value;
            if (value != null && field.FieldType != typeof(object) &&
                field.FieldType != value.GetType())
                coerced = Convert.ChangeType(value, field.FieldType);
            field.SetValue(this, coerced);
            return true;
        }
        _bag[binder.Name] = value;
        return true;
    }

    public override bool TryInvokeMember(System.Dynamic.InvokeMemberBinder binder, object[] args, out object result)
    {
        var method = GetType().GetMethod(binder.Name, HbRuntime.MemberFlags);
        if (method != null)
        {
            result = method.Invoke(this, System.Reflection.BindingFlags.DoNotWrapExceptions,
                                   null, args, null);
            return true;
        }
        result = null;
        return false;
    }
}

public static partial class HbRuntime
{

    public static readonly System.Reflection.BindingFlags MemberFlags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.IgnoreCase;

    public static dynamic GETMEMBER(object obj, string name)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return null;
        var t = obj.GetType();
        var prop = t.GetProperty(name, MemberFlags);
        if (prop != null) return prop.GetValue(obj);
        // Class DATA members now emit as plain fields (so they can be
        // passed by ref). Fall back to a field lookup.
        var field = t.GetField(name, MemberFlags);
        if (field != null) return field.GetValue(obj);
        // Dynamic class: a column held only in the dictionary bag.
        if (obj is HbDynamicObject hdo && hdo.TryBagGet(name, out dynamic bagVal))
            return bagVal;
        return null;
    }

    public static void SETMEMBER(object obj, string name, dynamic value)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return;
        var t = obj.GetType();
        var prop = t.GetProperty(name, MemberFlags);
        if (prop != null)
        {
            object coerced = value;
            if (value != null && prop.PropertyType != typeof(object) &&
                prop.PropertyType != value.GetType())
                coerced = Convert.ChangeType(value, prop.PropertyType);
            prop.SetValue(obj, coerced);
            return;
        }
        var field = t.GetField(name, MemberFlags);
        if (field == null)
        {
            // Dynamic class: store the column in the dictionary bag.
            if (obj is HbDynamicObject hdo) hdo.BagSet(name, value);
            return;
        }
        object coercedf = value;
        if (value != null && field.FieldType != typeof(object) &&
            field.FieldType != value.GetType())
            coercedf = Convert.ChangeType(value, field.FieldType);
        field.SetValue(obj, coercedf);
    }

    public static dynamic SENDMSG(object obj, string name, params dynamic[] args)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return null;
        var method = obj.GetType().GetMethod(name, MemberFlags);
        if (method == null) return null;
        return method.Invoke(obj, System.Reflection.BindingFlags.DoNotWrapExceptions,
                             null, args, null);
    }

    // x:className, which the emitter sends here for every receiver.
    // Harbour answers it for any value (hb_objGetClsName, vm/classes.c):
    // an object with its class, anything else with its type's name, the
    // types as ValType() reads them. `::Super:className` answers with
    // the parent class.
    public static string CLASSNAME(object? x) => x switch
    {
        HbSuperRef s => s.t?.Name ?? "",
        HbError e => e.classname(),
        _ => ValType(x) switch
        {
            "U" => "NIL",
            "C" => "CHARACTER",
            "N" => "NUMERIC",
            "L" => "LOGICAL",
            "D" => "DATE",
            "T" => "TIMESTAMP",
            "A" => "ARRAY",
            "B" => "BLOCK",
            "H" => "HASH",
            _ => x!.GetType().Name,
        },
    };

}

// Wrapper returned by `obj:Super()` — keeps the :className() chain working.
public class HbSuperRef
{
    public Type? t;
    public string className() => t?.Name ?? "";
    public string ClassName() => t?.Name ?? "";
}

// Extension methods so every object supports :className / :Super.
public static class HbObjectExtensions
{
    public static string className(this object obj) => HbRuntime.CLASSNAME(obj);
    public static string ClassName(this object obj) => HbRuntime.CLASSNAME(obj);
    public static HbSuperRef Super(this object obj) =>
        new HbSuperRef { t = obj?.GetType().BaseType };
}
