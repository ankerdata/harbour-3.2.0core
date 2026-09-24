using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// Errors: BREAK, the Error object, ErrorNew() and ErrorBlock() (family 9).
// One part of HbRuntime; HbRuntime.cs says what the whole is.
//
// How Harbour's error handling becomes C# (Alex, 2026-09-22). BREAK
// throws HbBreak, carrying its value, and BEGIN SEQUENCE is a try whose
// catch takes only HbBreak, so a runtime error in the body goes on to the
// error block at the entry point, as in Harbour. BEGIN SEQUENCE WITH
// { |e| break(e) }, the one error block the transpiler accepts, catches
// every exception instead, and RECOVER USING gets the Error object
// HbError.From makes of it. An exception cannot resume where it was
// raised, so a handler that returns a substitute value is not honoured.

// BREAK [<value>]: unwinds to the nearest BEGIN SEQUENCE, whose RECOVER
// USING gets the value — NIL for a bare BREAK.
public sealed class HbBreak : Exception
{
    public dynamic Value { get; }

    public HbBreak() : base("BREAK") { }
    public HbBreak(object? xValue) : base("BREAK") { Value = xValue; }

    // RECOVER USING under WITH { || break() }: a BREAK's own value, and
    // NIL for a runtime error
    public static dynamic ValueOf(Exception ex) => ex is HbBreak b ? b.Value : null;
}

// Harbour's Error object (rtl/errapi.c): every member it has, with its
// type. A fresh one reads as Harbour's does, where each member is NIL
// and its getter answers 0, "" or .F. Derives from HbDynamicObject so a
// message in any casing reaches its member, as Harbour's messages are
// case-insensitive: EasiPOS writes oError:description and
// oError:Description, hbtest o:oscode.
public class HbError : HbDynamicObject
{
    public decimal severity      { get; set; }
    public decimal genCode       { get; set; }
    public string  subSystem     { get; set; } = "";
    public decimal subCode       { get; set; }
    public string  description   { get; set; } = "";
    public string  operation     { get; set; } = "";
    public string  fileName      { get; set; } = "";
    public decimal osCode        { get; set; }
    public decimal tries         { get; set; }
    public bool    canRetry      { get; set; }
    public bool    canDefault    { get; set; }
    public bool    canSubstitute { get; set; }
    public dynamic args          { get; set; }
    public dynamic cargo         { get; set; }

    // The .NET exception an Error was made from, for the log: its stack
    // trace is where the error happened, which the handler, running once
    // the stack has unwound, cannot see with ProcName().
    public Exception? exception  { get; set; }

    public string classname() => "ERROR";

    const int ES_ERROR = 2;                         // error.ch
    const int EG_ARG = 1, EG_BOUND = 2, EG_NUMOVERFLOW = 4, EG_ZERODIV = 5,
              EG_MEM = 11, EG_NOMETHOD = 13, EG_OPEN = 21, EG_UNSUPPORTED = 30;

    // The subCode Harbour's VM gives an operator's argument error
    // (vm/hvm.c), by the C# spelling the runtime binder names it in, with
    // the name Harbour gives the operation.
    static readonly Dictionary<string, (int subCode, string operation)> s_operatorError = new()
    {
        ["+"] = (1081, "+"),   ["-"] = (1082, "-"),   ["*"] = (1083, "*"),
        ["/"] = (1084, "/"),   ["%"] = (1085, "%"),
        ["=="] = (1070, "=="), ["!="] = (1072, "<>"),
        ["<"] = (1073, "<"),   ["<="] = (1074, "<="), [">"] = (1075, ">"), [">="] = (1076, ">="),
        ["!"] = (1077, ".NOT."), ["&&"] = (1078, ".AND."), ["||"] = (1079, ".OR."),
    };
    const int SUB_UNARY_MINUS = 1080;
    const int SUB_NOMETHOD = 1004;

    // How the runtime binder says a member or an operator is missing:
    // "'HbError' does not contain a definition for 'Foo'", "Operator '-'
    // cannot be applied to operands of type 'string' and 'decimal'", and
    // "... to operand of type 'string'" for a unary one.
    static readonly Regex s_binderNoMember =
        new(@"does not contain a definition for '(\w+)'", RegexOptions.CultureInvariant);
    static readonly Regex s_binderOperator =
        new(@"^Operator '([^']+)' cannot be applied to (operands?)", RegexOptions.CultureInvariant);

    // HbRuntime's own errors say which Harbour error they are, and where
    // Harbour gives one its subCode: "Argument error (HB_SOCKETCLOSE, 3012)"
    static readonly Regex s_ownError =
        new(@"^(Argument error|Bound error) \((\w+)(?:, (\d+))?\)$", RegexOptions.CultureInvariant);

    // What RECOVER USING gets under WITH { |e| break(e) }, and what the
    // error block at the entry point is handed: a BREAK's own value, or
    // the Error object Harbour would have raised. The runtime errors
    // Harbour has a code for get Harbour's genCode, subCode, description
    // and operation (the base subsystem's, errors of rtl/errapi.c): a
    // zero divisor, a bound error, a hash key that is not there, a
    // message the object does not answer, an operator given the wrong
    // types. Any other exception is subsystem ".NET" with its type and
    // message, and a genCode for the kind of error it is where there is
    // one, so the handler's "genCode:" line names it rather than reading
    // "Unknown or reserved". osCode is the OS error a failed file
    // operation gives FError().
    public static dynamic From(Exception ex)
    {
        if (ex is HbBreak b)
            return b.Value;

        var e = new HbError { severity = ES_ERROR, subSystem = "BASE", exception = ex };
        var own = s_ownError.Match(ex.Message);
        if (own.Success)
        {
            e.genCode = own.Groups[1].Value == "Argument error" ? EG_ARG : EG_BOUND;
            e.description = own.Groups[1].Value;
            e.operation = own.Groups[2].Value;
            if (own.Groups[3].Success)
                e.subCode = int.Parse(own.Groups[3].Value);
        }
        else switch (ex)
        {
            case DivideByZeroException:
                (e.genCode, e.subCode, e.description, e.operation) = (EG_ZERODIV, 1340, "Zero divisor", "/");
                break;
            // A hash subscript whose key is not there is Harbour's bound
            // error too (hb_vmArrayPush)
            case IndexOutOfRangeException:
            case ArgumentOutOfRangeException:
            case KeyNotFoundException:
                (e.genCode, e.subCode, e.description, e.operation) = (EG_BOUND, 1132, "Bound error", "array access");
                break;
            case OverflowException:
                (e.genCode, e.description) = (EG_NUMOVERFLOW, "Numeric overflow");
                break;
            case ArgumentException:
                (e.genCode, e.description) = (EG_ARG, "Argument error");
                break;
            case Microsoft.CSharp.RuntimeBinder.RuntimeBinderException:
                FromBinder(e, ex);
                break;
            // Neither can say which Harbour error it was: a message sent
            // to NIL is "No exported method" in Harbour, NIL subscripted
            // an argument error, and both reach C# as the same exception
            case NullReferenceException:
            case InvalidCastException:
            case FormatException:
                AsDotNet(e, ex, EG_ARG);
                break;
            case FileNotFoundException:
            case DirectoryNotFoundException:
            case UnauthorizedAccessException:
                AsDotNet(e, ex, EG_OPEN);
                break;
            // What a routine guarded out of C# throws
            case NotImplementedException:
            case NotSupportedException:
                AsDotNet(e, ex, EG_UNSUPPORTED);
                break;
            case OutOfMemoryException:
                AsDotNet(e, ex, EG_MEM);
                break;
            default:
                AsDotNet(e, ex, 0);
                break;
        }
        if (ex is IOException or UnauthorizedAccessException)
            e.osCode = HbRuntime.OsError(ex);
        return e;
    }

    // A dynamic send or operator the runtime binder could not bind: a
    // message the object does not answer is Harbour's "No exported
    // method", an operator given types it does not take its argument
    // error, both named as Harbour names them. Anything else it refuses
    // (a conversion, an overload, NIL as the receiver) is an argument
    // error in .NET's own words.
    static void FromBinder(HbError e, Exception ex)
    {
        var member = s_binderNoMember.Match(ex.Message);
        var op = s_binderOperator.Match(ex.Message);
        if (member.Success)
            (e.genCode, e.subCode, e.description, e.operation) =
                (EG_NOMETHOD, SUB_NOMETHOD, "No exported method", member.Groups[1].Value.ToUpperInvariant());
        else if (op.Success && op.Groups[1].Value == "-" && op.Groups[2].Value == "operand")
            (e.genCode, e.subCode, e.description, e.operation) = (EG_ARG, SUB_UNARY_MINUS, "Argument error", "-");
        else if (op.Success && s_operatorError.TryGetValue(op.Groups[1].Value, out var harbour))
            (e.genCode, e.subCode, e.description, e.operation) = (EG_ARG, harbour.subCode, "Argument error", harbour.operation);
        else
            AsDotNet(e, ex, EG_ARG);
    }

    static void AsDotNet(HbError e, Exception ex, int genCode)
    {
        e.subSystem = ".NET";
        e.genCode = genCode;
        e.description = ex.GetType().Name + ": " + ex.Message;
    }
}

public static partial class HbRuntime
{
    public static dynamic ErrorNew() => new HbError();

    // ErrorBlock() is per thread, as Harbour's is (rtl/errapi.c keeps it
    // in thread-specific data); ErrorBlock( b ) sets it when b is a block
    // and returns the one it replaced either way.
    [ThreadStatic] static Delegate? t_errorBlock;

    public static Delegate? ErrorBlock() => t_errorBlock;

    public static Delegate? ErrorBlock(Delegate? bNewBlock)
    {
        Delegate? bOld = t_errorBlock;
        if (bNewBlock != null)
            t_errorBlock = bNewBlock;
        return bOld;
    }

    // Break( [<value>] ): the function form of BREAK, for a codeblock
    // (the statement form emits `throw new HbBreak(...)` directly)
    public static dynamic Break() => throw new HbBreak();
    public static dynamic Break(dynamic xValue) => throw new HbBreak(xValue);

    // The body of an entry point — a program's Main, and each thread's
    // start — handled as Harbour's VM handles the top: a BREAK no BEGIN
    // SEQUENCE took, or a QUIT, ends it quietly, and a runtime error goes
    // to the error block, where EasiPOS's DefError logs it and QUITs.
    // A block that returns cannot resume anything, so the program ends
    // with ErrorLevel() 1 unless it set one. With no error block
    // installed the exception goes on to .NET, which prints it and ends
    // the process; on a thread hb_threadStart() started it is reported
    // and ends that thread, as Harbour's default handler would.
    [System.Diagnostics.StackTraceHidden]
    public static void RunEntry(Action entry) => RunEntry(entry, false);

    [System.Diagnostics.StackTraceHidden]
    internal static void RunEntry(Action entry, bool lThread)
    {
        _ = Self;                       // the main thread is numbered 1 before anything runs
        bool lWasInEntry = t_inEntry;
        t_inEntry = true;
        try
        {
            entry();
        }
        catch (HbBreak)
        {
        }
        catch (HbQuit)
        {
        }
        catch (Exception ex) when (LaunchError(ex, lThread, out bool lQuit))
        {
            if (!lQuit && ErrorLevel() == 0)
                ErrorLevel(1);
        }
        finally
        {
            t_inEntry = lWasInEntry;
        }
    }

    // The thread runs under RunEntry, which a QUIT unwinds to
    [ThreadStatic] static bool t_inEntry;

    // hb_errLaunch() for an error that reached the top: the error block
    // evaluated with the Error object. It runs as RunEntry's exception
    // filter, before C# unwinds the stack, so the block sees the stack
    // where the error happened, as Harbour's does: DefError's traceback
    // (Stack2Str, ProcName) names the routine that failed. False — the
    // exception going on to .NET — when no block is installed (on the
    // main thread) or the block itself fails; a BREAK or QUIT out of the
    // block ends the thread, lQuit saying which.
    [System.Diagnostics.StackTraceHidden]
    static bool LaunchError(Exception ex, bool lThread, out bool lQuit)
    {
        lQuit = false;
        Delegate? bBlock = t_errorBlock;
        if (bBlock == null)
        {
            if (!lThread)
                return false;
            Console.Error.WriteLine("Error in thread " + Self.nId + ": " + ex);
            return true;
        }
        try
        {
            InvokeBlock(bBlock, new dynamic[] { HbError.From(ex) });
        }
        catch (HbBreak)
        {
        }
        catch (HbQuit)
        {
            lQuit = true;
        }
        catch (Exception exBlock)
        {
            Console.Error.WriteLine("The error block failed: " + exBlock);
            return false;
        }
        return true;
    }
}
