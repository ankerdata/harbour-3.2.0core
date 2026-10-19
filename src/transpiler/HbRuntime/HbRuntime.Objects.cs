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
/// What ValType() answers "O" for: a Harbour object. Every class the
/// transpiler emits implements it, a root class directly and a subclass
/// through its parent; so do HbDynamicObject, and with it HbError, and
/// HbSuperRef.
/// </summary>
public interface IHbObject { }

/// <summary>
/// Base class for Harbour classes that use runtime member access
/// (::&amp;(name) patterns). Backed by a Dictionary so that column
/// names or dynamically-created properties resolve at runtime.
/// Statically-declared properties (from DATA/VAR) take precedence
/// via the reflection path in TryGetMember/TrySetMember.
/// </summary>
public class HbDynamicObject : System.Dynamic.DynamicObject, IHbObject
{
    private readonly Dictionary<string, dynamic> _bag =
        new Dictionary<string, dynamic>(StringComparer.OrdinalIgnoreCase);

    // Exposed so HbRuntime.GETMEMBER/SETMEMBER — the obj:&(name) macro
    // path, which doesn't go through the DLR — can reach the bag too,
    // not only statically reflected members.
    public bool TryBagGet(string name, out dynamic value) =>
        _bag.TryGetValue(name, out value);
    public void BagSet(string name, dynamic value) => _bag[name] = value;

    public override bool TryGetMember(System.Dynamic.GetMemberBinder binder, out object result) =>
        TryReadMember(binder.Name, out result);

    // A member's value by name: a property, a field — class DATA members
    // emit as plain fields, and a declared member reached through
    // ((dynamic)this) lands here — or a column in the bag.
    bool TryReadMember(string cName, out object result)
    {
        HbMemberSlot oSlot = HbMemberSlot.FindReadable(this, cName, out bool fAmbiguous);
        if (fAmbiguous)
            throw new System.Reflection.AmbiguousMatchException("Ambiguous member " + cName);
        if (oSlot != null && oSlot.TryGet(this, out object xValue))
        {
            result = xValue;
            return true;
        }
        result = null;
        return false;
    }

    public override bool TrySetMember(System.Dynamic.SetMemberBinder binder, object value)
    {
        HbMemberSlot oSlot = HbMemberSlot.Declared(GetType(), binder.Name, true, out bool fAmbiguous);
        if (fAmbiguous)
            throw new System.Reflection.AmbiguousMatchException("Ambiguous member " + binder.Name);
        if (oSlot != null)
        {
            oSlot.Set(this, oSlot.Convert(value));
            return true;
        }
        if (_bag.ContainsKey(binder.Name))
        {
            _bag[binder.Name] = value;
            return true;
        }
        // A name the object has neither declared nor been given in its bag
        // (SETMEMBER, BagSet: an ad-hoc table's fields, set as it is built,
        // as addData() declares them) is Harbour's error 16/1005 on
        // assignment (vm/classes.c), where the bag used to take it.
        throw new MissingMemberException(
            "No exported variable (" + binder.Name.ToUpperInvariant() + ", 1005)");
    }

    // Reached only when C# could not bind the call to a declared method as
    // it stands (the DLR binds a call that fits itself, optional parameters
    // and numbers included): no method of that name, or arguments that do
    // not fit. The method of that name is called with the arguments fitted
    // to it as Harbour passes them (HbRuntime.FitArgs).
    public override bool TryInvokeMember(System.Dynamic.InvokeMemberBinder binder, object[] args, out object result)
    {
        var method = HbRuntime.MostDerivedMethod(GetType(), binder.Name);
        if (method != null)
        {
            result = method.Invoke(this, System.Reflection.BindingFlags.DoNotWrapExceptions,
                                   null, HbRuntime.FitArgs(method, args, binder.Name), null);
            return true;
        }
        // Harbour answers an instance variable sent as a message with
        // parentheses, `oError:subsystem()`, with its value — errorsys.prg's
        // ErrorMessage() does it. With arguments it stays an error.
        if (args.Length == 0)
            return TryReadMember(binder.Name, out result);
        result = null;
        return false;
    }
}

/// <summary>
/// A member as GETMEMBER() and SETMEMBER() reach it, found once per class
/// and name and used for every object of the class: a public property or a
/// public field (either case-insensitive), else the bag of an
/// HbDynamicObject. EasiPosNative's row loader keeps one per column.
/// </summary>
public sealed class HbMemberSlot
{
    const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase;

    static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type, string, bool), (HbMemberSlot? oSlot, bool fAmbiguous)>
        s_declared = new();

    readonly System.Reflection.PropertyInfo? oProperty;
    readonly System.Reflection.FieldInfo? oField;
    readonly Type oType;

    public string Name { get; }

    HbMemberSlot(string cName, Type oType, System.Reflection.PropertyInfo? oProperty, System.Reflection.FieldInfo? oField)
    {
        Name = cName;
        this.oType = oType;
        this.oProperty = oProperty;
        this.oField = oField;
    }

    // Where a value is written: a declared member, else the bag of an
    // HbDynamicObject, which takes any name
    public static HbMemberSlot? FindWritable(Type oClass, string cName) => FindWritable(oClass, cName, out _);

    public static HbMemberSlot? FindWritable(Type oClass, string cName, out bool fAmbiguous)
    {
        HbMemberSlot? oSlot = Declared(oClass, cName, true, out fAmbiguous);
        if (oSlot != null || fAmbiguous)
            return oSlot;
        return typeof(HbDynamicObject).IsAssignableFrom(oClass) ? new HbMemberSlot(cName, typeof(object), null, null) : null;
    }

    // Where a value is read: a declared member, else a name the object's bag holds
    public static HbMemberSlot? FindReadable(object oObject, string cName) => FindReadable(oObject, cName, out _);

    public static HbMemberSlot? FindReadable(object oObject, string cName, out bool fAmbiguous)
    {
        HbMemberSlot? oSlot = Declared(oObject.GetType(), cName, false, out fAmbiguous);
        if (oSlot != null || fAmbiguous)
            return oSlot;
        return oObject is HbDynamicObject oBag && oBag.TryBagGet(cName, out _)
            ? new HbMemberSlot(cName, typeof(object), null, null) : null;
    }

    // A declared property or field, readable or writable as fWrite asks; two
    // that differ only in case are no slot (fAmbiguous)
    public static HbMemberSlot? Declared(Type oClass, string cName, bool fWrite, out bool fAmbiguous)
    {
        var oKey = (oClass, cName.ToUpperInvariant(), fWrite);
        if (!s_declared.TryGetValue(oKey, out var oFound))
            oFound = s_declared.GetOrAdd(oKey, Find(oClass, cName, fWrite));
        fAmbiguous = oFound.fAmbiguous;
        return oFound.oSlot;
    }

    static (HbMemberSlot? oSlot, bool fAmbiguous) Find(Type oClass, string cName, bool fWrite)
    {
        try
        {
            var oProperty = oClass.GetProperty(cName, Flags);
            if (oProperty != null && oProperty.GetIndexParameters().Length == 0 &&
                (fWrite ? oProperty.GetSetMethod(true) : oProperty.GetGetMethod(true)) != null)
                return (new HbMemberSlot(oProperty.Name, oProperty.PropertyType, oProperty, null), false);
            var oField = oClass.GetField(cName, Flags);
            if (oField != null && !oField.IsLiteral && !(fWrite && oField.IsInitOnly))
                return (new HbMemberSlot(oField.Name, oField.FieldType, null, oField), false);
            return (null, false);
        }
        catch (System.Reflection.AmbiguousMatchException)
        {
            return (null, true);
        }
    }

    public bool SameMember(HbMemberSlot oOther) =>
        oProperty != null ? oProperty == oOther.oProperty :
        oField != null ? oField == oOther.oField :
        oOther.oProperty == null && oOther.oField == null &&
        string.Equals(Name, oOther.Name, StringComparison.OrdinalIgnoreCase);

    public bool TryGet(object oObject, out object? xValue)
    {
        if (oProperty != null)
        {
            xValue = oProperty.GetValue(oObject);
            return true;
        }
        if (oField != null)
        {
            xValue = oField.GetValue(oObject);
            return true;
        }
        if (oObject is HbDynamicObject oBag && oBag.TryBagGet(Name, out dynamic xBagValue))
        {
            xValue = xBagValue;
            return true;
        }
        xValue = null;
        return false;
    }

    // The value in the member's type
    public object? Convert(object? xValue)
    {
        if (xValue == null || oType == typeof(object) || oType.IsInstanceOfType(xValue))
            return xValue;
        return System.Convert.ChangeType(xValue, Nullable.GetUnderlyingType(oType) ?? oType,
                                         CultureInfo.InvariantCulture);
    }

    public void Set(object oObject, object? xValue)
    {
        if (oProperty != null)
            oProperty.SetValue(oObject, xValue);
        else if (oField != null)
            oField.SetValue(oObject, xValue);
        else
            ((HbDynamicObject)oObject).BagSet(Name, xValue);
    }
}

public static partial class HbRuntime
{
    // X():New( ... ) for a class that declares Init() and no New():
    // HBObject's New() (rtl/tobject.prg) runs Init( ... ) and answers the
    // object itself, whatever Init() returned. Transaction's and
    // POSStatus's Init() return NIL, and a device class's Init() returns
    // NIL to say it could not connect; New() gives the object all the same.
    [System.Diagnostics.StackTraceHidden]
    public static T Initialised<T>(T self, Action<T> init)
    {
        init(self);
        return self;
    }

    public static readonly System.Reflection.BindingFlags MemberFlags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.IgnoreCase;

    // A member by name, as `o:&( cName )` reads it: a property, a field, else
    // a column in an HbDynamicObject's bag; NIL when there is none
    public static dynamic GETMEMBER(object obj, string name)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return null;
        HbMemberSlot oSlot = HbMemberSlot.FindReadable(obj, name, out bool fAmbiguous);
        if (fAmbiguous)
            throw new System.Reflection.AmbiguousMatchException("Ambiguous member " + name);
        return oSlot != null && oSlot.TryGet(obj, out object xValue) ? xValue : null;
    }

    // `o:&( cName ) := x`: the value in the member's type; an HbDynamicObject's
    // bag takes a name it does not declare, any other object ignores it
    public static void SETMEMBER(object obj, string name, dynamic value)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return;
        HbMemberSlot oSlot = HbMemberSlot.FindWritable(obj.GetType(), name, out bool fAmbiguous);
        if (fAmbiguous)
            throw new System.Reflection.AmbiguousMatchException("Ambiguous member " + name);
        oSlot?.Set(obj, oSlot.Convert((object)value));
    }

    public static dynamic SENDMSG(object obj, string name, params dynamic[] args)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return null;
        var method = MostDerivedMethod(obj.GetType(), name);
        if (method == null) return null;
        return method.Invoke(obj, System.Reflection.BindingFlags.DoNotWrapExceptions,
                             null, FitArgs(method, args, name), null);
    }

    // The method a message names on a type: the most derived of that name,
    // as Harbour's method of that name overrides its parent's (a class's
    // New() is a C# overload beside its parent's when the parameters
    // differ, and GetMethod() refuses to choose between them)
    internal static System.Reflection.MethodInfo? MostDerivedMethod(System.Type t, string cName)
    {
        System.Reflection.MethodInfo? best = null;
        int iBest = -1;
        foreach (var m in t.GetMethods(MemberFlags))
        {
            if (!string.Equals(m.Name, cName, StringComparison.OrdinalIgnoreCase))
                continue;
            int iDepth = 0;
            for (var d = m.DeclaringType; d != null; d = d.BaseType)
                iDepth++;
            if (iDepth > iBest)
                (best, iBest) = (m, iDepth);
        }
        return best;
    }

    // The arguments of a message sent through reflection, fitted to the
    // method as Harbour passes them, which reflection does not: those past
    // its parameters dropped, one left out its declared default (NIL, a
    // value type's zero, when it has none), a number converted to a decimal
    // or long parameter (FitBlockArg). A value the parameter cannot hold is
    // Harbour's argument error on the message; reflection used to report
    // "Parameter count mismatch" for this and for an extra argument alike.
    internal static object?[] FitArgs(System.Reflection.MethodInfo method, object?[] args, string cMsg)
    {
        var aParams = method.GetParameters();
        var aFitted = new object?[aParams.Length];
        for (int i = 0; i < aParams.Length; i++)
        {
            var t = aParams[i].ParameterType;
            if (t.IsByRef)
                t = t.GetElementType()!;
            var tValue = Nullable.GetUnderlyingType(t) ?? t;
            object? v = i < args.Length ? FitBlockArg(args[i], tValue)
                      : aParams[i].HasDefaultValue ? aParams[i].DefaultValue : null;
            if (v != null && tValue != typeof(object) && !tValue.IsInstanceOfType(v))
                throw new ArgumentException("Argument error (" + cMsg.ToUpperInvariant() + ")");
            aFitted[i] = v;
        }
        return aFitted;
    }

    // Type( <cExpr> ): the type of an expression given as text, as
    // Harbour's macro compiler finds it (vm/macro.c hb_macroGetType). What
    // EasiPOS asks is whether a PUBLIC exists yet — Type( "aHWFlag" ) —
    // and whether a function is linked — Type( "GetJnlOn()" ). A name is a
    // PUBLIC, a static field of Program: its ValType(), "U" when there is
    // none or it is NIL. name() with no arguments is a function of the
    // program: "UI" when it is there, as Harbour cannot know a user
    // function's type without calling it, and "U" when it is not linked.
    // Any other expression would need the macro compiler: an argument
    // error, rather than a guess.
    static readonly System.Text.RegularExpressions.Regex s_typeName =
        new(@"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*(\(\s*\))?\s*$");

    public static string Type(string cExpr)
    {
        var m = s_typeName.Match(cExpr ?? "");
        if (!m.Success)
            throw new ArgumentException("Argument error (TYPE)");
        string cName = m.Groups[1].Value;
        System.Type? program = ProgramType;
        if (program == null)
            return "U";
        const System.Reflection.BindingFlags Statics =
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static;
        if (m.Groups[2].Success)
            return program.GetMethods(Statics)
                          .Any(f => string.Equals(f.Name, cName, StringComparison.OrdinalIgnoreCase))
                   ? "UI" : "U";
        var field = program.GetField(cName, Statics | System.Reflection.BindingFlags.IgnoreCase);
        if (field != null)
            return ValType(field.GetValue(null));
        var prop = program.GetProperty(cName, Statics | System.Reflection.BindingFlags.IgnoreCase);
        return prop != null ? ValType(prop.GetValue(null)) : "U";
    }

    // hb_IsFunction( <cName> ): is a function of that name linked — one of
    // the program's, or Harbour's own (HbRuntime's), as the symbol table
    // has both. EasiPOS asks it of optional hooks: messagebox.prg's
    // SilentBox() calls BESILENT() when a program defines one.
    public static bool hb_IsFunction(string cName)
    {
        if (string.IsNullOrEmpty(cName))
            return false;
        const System.Reflection.BindingFlags Statics =
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static;
        bool Has(System.Type? t) => t != null && t.GetMethods(Statics)
            .Any(f => string.Equals(f.Name, cName, StringComparison.OrdinalIgnoreCase));
        return Has(ProgramType) || Has(typeof(HbRuntime));
    }

    // HB_ISARRAY( <x> ): an array, and not an object — Harbour's objects
    // are arrays there (hb_extIsArray, vm/extend.c), where ours are real
    // classes, so the question is only whether it is an array.
    public static bool HB_ISARRAY(object? x) => IsHbArray(x);

    // hb_defaultValue( <xValue>, <xDefault> ): the value, or the default
    // when the two are not of the same type (rtl/hbdef.c compares the
    // basic types, so NIL takes the default, and so does a value of
    // another type). A second parameter that is itself NIL defaults
    // nothing: the value comes back as it is.
    public static dynamic? hb_defaultValue(object? xValue, object? xDefault = null) =>
        xDefault != null && ValType(xValue) != ValType(xDefault) ? xDefault : xValue;

    // The same, for a call that passes its value by reference
    // (internalerror.prg's hb_DefaultValue( @lSilent, .F. )); nothing is
    // written back, as Harbour writes nothing.
    public static dynamic? hb_defaultValue<T>(ref T xValue, object? xDefault = null) =>
        hb_defaultValue((object?) xValue, xDefault);

    // hb_ExecFromArray(): call a function, a block or a method with the
    // parameters given, and return its result — vm/eval.c reads it as
    //   ( { <func>, <params,...> } ) or ( { <oObject>, <cMessage>, <params,...> } )
    //   ( <func> )  ( <func>, <aParams> )  ( <oObject>, <cMessage>, [<aParams>] )
    // where <func> is a name, @Func() or a block. messagebox.prg calls
    // hb_ExecFromArray( { "BESILENT" } ). One overload per count, not
    // `params`: an array passed alone would be spread into the arguments.
    public static dynamic hb_ExecFromArray(object? x1) => ExecFromArray(new[] { x1 });
    public static dynamic hb_ExecFromArray(object? x1, object? x2) => ExecFromArray(new[] { x1, x2 });
    public static dynamic hb_ExecFromArray(object? x1, object? x2, object? x3) => ExecFromArray(new[] { x1, x2, x3 });

    // a Harbour array of arguments as the call's own
    static object?[] ArgsOf(object? x) =>
        IsHbArray(x) ? ((System.Collections.IList)x!).Cast<object?>().ToArray()
                     : throw new ArgumentException("Argument error (HB_EXECFROMARRAY)");

    static dynamic ExecFromArray(object?[] args)
    {
        object? xSelf = null, xFunc = null;
        object?[] aParams = System.Array.Empty<object?>();
        if (args.Length == 1)
        {
            if (IsHbArray(args[0]))
            {
                object?[] aExec = ((System.Collections.IList)args[0]!).Cast<object?>().ToArray();
                int nFunc = aExec.Length > 0 && ValType(aExec[0]) == "O" ? 1 : 0;
                if (nFunc == 1)
                    xSelf = aExec[0];
                xFunc = aExec.Length > nFunc ? aExec[nFunc] : null;
                aParams = aExec.Length > nFunc + 1 ? aExec[(nFunc + 1)..] : aParams;
            }
            else
                xFunc = args[0];
        }
        else if (args.Length is 2 or 3 && ValType(args[0]) == "O")
        {
            xSelf = args[0];
            xFunc = args[1];
            if (args.Length == 3)
                aParams = ArgsOf(args[2]);
        }
        else if (args.Length == 2)
        {
            xFunc = args[0];
            if (args[1] != null)
                aParams = ArgsOf(args[1]);
        }

        if (xSelf != null)
            return xFunc is string cMessage
                ? SENDMSG(xSelf, cMessage, aParams!)
                : throw new ArgumentException("Argument error (HB_EXECFROMARRAY)");
        return xFunc switch
        {
            Delegate d => InvokeBlock(d, aParams!),
            string cFunc => InvokeBlock(FuncPtr(cFunc), aParams!),
            _ => throw new ArgumentException("Argument error (HB_EXECFROMARRAY)"),
        };
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
            "P" => "POINTER",
            _ => x!.GetType().Name,
        },
    };

}

// Wrapper returned by `obj:Super()` — keeps the :className() chain working.
public class HbSuperRef : IHbObject
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
