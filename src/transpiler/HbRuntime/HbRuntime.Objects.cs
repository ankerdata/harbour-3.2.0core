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

    public override bool TryGetMember(System.Dynamic.GetMemberBinder binder, out object result) =>
        TryReadMember(binder.Name, out result);

    // A member's value by name: a property, a field — class DATA members
    // emit as plain fields, and a declared member reached through
    // ((dynamic)this) lands here — or a column in the bag.
    bool TryReadMember(string cName, out object result)
    {
        var prop = GetType().GetProperty(cName, HbRuntime.MemberFlags);
        if (prop != null)
        {
            result = prop.GetValue(this);
            return true;
        }
        var field = GetType().GetField(cName, HbRuntime.MemberFlags);
        if (field != null)
        {
            result = field.GetValue(this);
            return true;
        }
        return _bag.TryGetValue(cName, out result);
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
        // Harbour answers an instance variable sent as a message with
        // parentheses, `oError:subsystem()`, with its value — errorsys.prg's
        // ErrorMessage() does it. With arguments it stays an error.
        if (args.Length == 0)
            return TryReadMember(binder.Name, out result);
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

    static dynamic ExecFromArray(object?[] args)
    {
        object? xSelf = null, xFunc = null;
        object?[] aParams = System.Array.Empty<object?>();
        if (args.Length == 1)
        {
            if (args[0] is object[] aExec)
            {
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
                aParams = args[2] as object[] ?? throw new ArgumentException("Argument error (HB_EXECFROMARRAY)");
        }
        else if (args.Length == 2)
        {
            xFunc = args[0];
            if (args[1] != null)
                aParams = args[1] as object[] ?? throw new ArgumentException("Argument error (HB_EXECFROMARRAY)");
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
