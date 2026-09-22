using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

/// <summary>
/// Harbour's core runtime library for transpiled C# code, plus the
/// helpers the emitter itself calls (Eval, StrCmp, HbIn, FuncPtr, ...).
/// Nothing application-specific belongs here: EasiPOS's own C functions
/// live in the application's EasiPosNative project, and a function the
/// program defines in .prg is always the program's (gencsharp.c,
/// hb_csFuncTabPrefix).
///
/// Method names are spelt as Harbour's include/*.hbx lists them
/// (`SubStr`, `hb_HGetDef`): the emitter writes every core call in that
/// canonical spelling whatever casing the source used, and C# is
/// case-sensitive. A name spelt any other way is never called.
///
/// The class is split by category across the files of this folder:
/// HbRuntime.cs holds the core the emitter leans on (codeblocks, Empty(),
/// ValType(), hb_default(), function references), and HbRuntime.<Part>.cs
/// the rest — Numbers, Strings, Transform, Dates, Arrays, Hashes, Files,
/// FileSystem, Process, Console, Set, Constants, Threads, Errors, Objects.
/// </summary>
public static partial class HbRuntime
{
    static readonly CultureInfo INV = CultureInfo.InvariantCulture;

    // ---- Codeblock dispatch ----
    // Harbour codeblocks emit as Func<dynamic[], dynamic>, so Eval just
    // packs the trailing args into the array and invokes. Delegate
    // fallback covers blocks that survived as a plain delegate (e.g.
    // typed fixed-arity lambdas) through reflection (InvokeBlock).
    public static dynamic Eval(dynamic block, params dynamic[] args) =>
        block is Delegate d ? InvokeBlock(d, args ?? System.Array.Empty<dynamic>()) : null;

    // Eval()'s work, also the error launcher's: hidden from ProcName(),
    // like the reflection frames under it.
    [System.Diagnostics.StackTraceHidden]
    internal static dynamic InvokeBlock(Delegate d, dynamic[] args)
    {
        var ps = d.Method.GetParameters();
        object[] fitted;
        // A varargs codeblock (`{|...|}`) is a single dynamic[] param —
        // hand it the whole args array as that one parameter.
        if (ps.Length == 1 && ps[0].ParameterType == typeof(object[]))
            fitted = new object[] { args };
        else
        {
            // Fixed-arity codeblock. A Harbour block tolerates being
            // called with more args than it declares (extras ignored) or
            // fewer (missing become NIL); reflection throws on any
            // mismatch, so fit the count to the real parameter list.
            fitted = new object[ps.Length];
            System.Array.Copy(args, fitted, Math.Min(args.Length, ps.Length));
        }
        // Invoked without reflection's TargetInvocationException wrapper:
        // a BREAK in the block must reach its BEGIN SEQUENCE as HbBreak,
        // and a runtime error reach the error launcher with the block's
        // frames still on the stack. A static method closed over its
        // first argument cannot be invoked that way; DynamicInvoke it.
        if (d.Method.IsStatic && d.Target != null)
            return d.DynamicInvoke(fitted);
        return d.Method.Invoke(d.Target, System.Reflection.BindingFlags.DoNotWrapExceptions,
                               null, fitted, null);
    }

    // ---- Logical functions ----

    public static bool Empty(dynamic val)
    {
        if (val == null) return true;
        if (IsNumeric(val)) return Convert.ToDecimal(val, INV) == 0;
        if (val is string s) return s.Trim().Length == 0;
        // Harbour's empty date (CToD("")) is DateOnly's default here.
        if (val is DateOnly dt) return dt == default;
        if (val is DateTime ts) return ts == default;
        if (val is bool b) return !b;
        if (val is System.Array a) return a.Length == 0;
        if (val is System.Collections.IDictionary h) return h.Count == 0;
        return false;
    }

    public static bool IsDigit(string s) => s.Length > 0 && char.IsDigit(s[0]);
    public static bool IsAlpha(string s) => s.Length > 0 && char.IsLetter(s[0]);
    public static bool IsUpper(string s) => s.Length > 0 && char.IsUpper(s[0]);
    public static bool IsLower(string s) => s.Length > 0 && char.IsLower(s[0]);

    // String ordering: the emitter turns `<`, `<=`, `>`, `>=` on strings
    // into `StrCmp(a, b) <op> 0` (`==` stays C# `==`; the transpiler
    // refuses `=`, E0100). Exact and ordinal, by decision (Alex,
    // 2026-09-19): Harbour's SET EXACT semantics — an equal prefix
    // compares equal when EXACT is OFF, trailing blanks are ignored when
    // it is ON — are not reproduced; EasiPOS writes `==` and runs with
    // SET EXACT ON. "Lisbon" > "Lis".
    public static int StrCmp(string a, string b) =>
        Math.Sign(string.CompareOrdinal(a ?? "", b ?? ""));

    // FOR ... STEP <n> whose step is not a constant: the emitter's loop
    // condition, which counts down when the step is below zero and up
    // otherwise — vm/hvm.c hb_vmForTest, evaluated on every pass as the
    // step can change. The end comes before the step, Harbour's order.
    public static bool ForTest(decimal nCounter, decimal nEnd, decimal nStep) =>
        nStep < 0 ? nCounter >= nEnd : nCounter <= nEnd;

    // ---- Type inspection ----

    public static string ValType(dynamic x)
    {
        if (x is null) return "U";
        if (x is string) return "C";
        if (IsNumeric(x)) return "N";
        if (x is bool) return "L";
        if (x is DateOnly) return "D";
        if (x is DateTime) return "T";      // a timestamp
        if (x is System.Array) return "A";
        if (x is Delegate) return "B";
        if (x is System.Collections.IDictionary) return "H";
        // Harbour's pointer items: a thread, a mutex, a Windows handle
        if (x is HbThread or HbMutex or System.Threading.WaitHandle) return "P";
        return "O";
    }

    public static decimal PCount() => 0;  // varargs path uses hbva.Length directly

    // ---- Defaults helper (hb_default) ----
    // Harbour's hb_default( @var, val ) sets var to val if NIL. Without
    // by-ref pass-through we return the non-null one of the two.

    /* Harbour's hb_default( @var, val ) mutates `var` in place when it
       is NIL, leaving a non-NIL value untouched. The emitter emits
       `ref var` at every call site (because the PRG uses `@var`).

       Generic `ref T` is required because C# `ref` is invariant — a
       `ref decimal` won't bind to `ref dynamic`, and most emitted
       call sites have concrete types for the by-ref parameter. The
       null check only fires for reference / nullable types; for
       non-nullable value types it's trivially false and the call is
       a no-op. That's a behaviour regression against Harbour (where
       an omitted NIL parameter would pick up the default) but the
       emitter already initialises such parameters with `= default`,
       so the common case still compiles and runs. A fuller fix
       would inline the default at the call site based on the param's
       nilable flag — leaving that for when it actually bites. */
    public static void hb_default<T>(ref T val, T defVal)
    {
        if (val is null) val = defVal;
    }

    // ---- Type predicates ----

    public static bool ISNIL(dynamic x) => x is null;
    public static bool ISCHARACTER(dynamic x) => x is string;
    public static bool ISNUMBER(dynamic x) => IsNumeric(x);
    public static bool ISLOGICAL(dynamic x) => x is bool;
    public static bool ISDATE(dynamic x) => x is DateOnly or DateTime;
    public static bool ISARRAY(dynamic x) => x is System.Array;
    public static bool ISHASH(dynamic x) => x is System.Collections.IDictionary;
    public static bool ISOBJECT(dynamic x) =>
        x is not null && !(x is string or bool or DateOnly or DateTime or System.Array or Delegate
                          or System.Collections.IDictionary || IsNumeric(x));
    public static bool ISBLOCK(dynamic x) => x is Delegate;

    // ---- Misc ----

    /// <summary>
    /// Placeholder the transpiler emits in place of unsupported Harbour
    /// constructs (macros `&name`, workarea ALIAS expressions, comma-
    /// operator). Typed `dynamic` so it works on both sides of an
    /// assignment — C#'s `default` can't be an LHS, which would break
    /// `&cMemvar := expr` → `default = expr`. Executing the path that
    /// uses it silently discards the value; the transpiler emits a
    /// `warning W0016` at generation time listing every occurrence.
    /// </summary>
    public static dynamic MacroStub;

    public static decimal RecNo() => 0;

    // ---- Function-reference resolution (Harbour's @FunName() operator) ----

    /// <summary>
    /// Cache of name → callable delegate so dispatch-table patterns like
    /// <c>{ TABLEFILE =&gt; @TableDetailDef() }</c> don't reflect on every
    /// invocation. Keyed case-insensitively (Harbour convention).
    /// </summary>
    private static readonly Dictionary<string, Func<dynamic[], dynamic>> s_funcPtrCache =
        new Dictionary<string, Func<dynamic[], dynamic>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Return a callable <c>Func&lt;dynamic[], dynamic&gt;</c> for the
    /// named static method on the merged <c>Program</c> partial class.
    /// The transpiler emits <c>@FuncName()</c> as a call to this helper
    /// so that method groups — which C# won't convert to <c>dynamic</c>
    /// directly — become first-class values suitable for hash entries,
    /// array elements, and <c>LOCAL pFunc := @Foo()</c> assignments.
    /// Missing methods return a sentinel that throws when invoked —
    /// matches Harbour's runtime-failure semantics for a bad @ref.
    /// </summary>
    public static Func<dynamic[], dynamic> FuncPtr(string methodName)
    {
        if (methodName == null) return _ => null;
        if (s_funcPtrCache.TryGetValue(methodName, out var cached)) return cached;

        // Look up on Program. We assume the transpiled code lives in a
        // single `public static partial class Program` merged across
        // every .prg file's partial contribution. If someone builds a
        // multi-program executable, the lookup here will miss and
        // callers get the throwing sentinel.
        var programType = System.Type.GetType("Program")
            ?? System.Reflection.Assembly.GetExecutingAssembly().GetType("Program")
            ?? System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Program"))
                .FirstOrDefault(t => t != null);

        System.Reflection.MethodInfo method = null;
        if (programType != null)
        {
            method = programType.GetMethod(methodName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.IgnoreCase);
        }

        Func<dynamic[], dynamic> result;
        if (method == null)
        {
            result = _ => throw new MissingMethodException(
                $"HbRuntime.FuncPtr: no static method '{methodName}' on Program");
        }
        else
        {
            var parms = method.GetParameters();
            result = args =>
            {
                args ??= System.Array.Empty<dynamic>();
                var invokeArgs = new object[parms.Length];
                for (int i = 0; i < parms.Length; i++)
                {
                    if (i < args.Length)
                    {
                        var val = args[i];
                        // Convert.ChangeType handles the common
                        // numeric/string/bool coercions; null passes
                        // through unchanged.
                        if (val != null && !parms[i].ParameterType.IsAssignableFrom(val.GetType()))
                        {
                            try { val = Convert.ChangeType(val, parms[i].ParameterType, INV); }
                            catch { /* leave as-is; Invoke may still accept via boxing */ }
                        }
                        invokeArgs[i] = val;
                    }
                    else if (parms[i].HasDefaultValue)
                        invokeArgs[i] = parms[i].DefaultValue;
                    else
                        invokeArgs[i] = parms[i].ParameterType.IsValueType
                            ? Activator.CreateInstance(parms[i].ParameterType)
                            : null;
                }
                return method.Invoke(null, System.Reflection.BindingFlags.DoNotWrapExceptions,
                                     null, invokeArgs, null);
            };
        }

        s_funcPtrCache[methodName] = result;
        return result;
    }
}

// Harbour passes parameters by value; `@` opts into by-reference.
// C# `ref` is per-signature, so a param that ANY caller @-passes emits
// `ref` for ALL callers. This holder reconciles the callers who did
// NOT write `@` — they must still satisfy the `ref`, but Harbour
// semantics say their variable is untouched:
//
//   * omitted slot (Foo(a, , c)) or literal -> `ref HbDiscard<T>.Value`
//     — no input to preserve; the write-back is discarded.
//   * bare variable  (Foo(x) into a by-ref param) -> the callee still
//     sees x's VALUE as input, but the write-back must NOT reach the
//     caller's x. `ref HbDiscard<T>.Seed(x)` seeds the throwaway with
//     x, hands out a ref to it, and drops the write — exactly Harbour's
//     by-value semantics for an un-@'d argument (in-out and out-only
//     both correct).
//
// [ThreadStatic] so concurrent calls (the app builds with
// -DMULTITHREAD) don't race on the shared slot; Seed always assigns
// before the ref is read within the same call expression.
public static class HbDiscard<T>
{
   [System.ThreadStatic] public static T Value;
   public static ref T Seed(T v) { Value = v; return ref Value; }
}
