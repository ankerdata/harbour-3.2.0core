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
    const int EG_ARG = 1, EG_BOUND = 2, EG_NUMOVERFLOW = 4, EG_ZERODIV = 5;

    // HbRuntime's own errors say which Harbour error they are
    static readonly Regex s_ownError =
        new(@"^(Argument error|Bound error) \((\w+)\)$", RegexOptions.CultureInvariant);

    // What RECOVER USING gets under WITH { |e| break(e) }: a BREAK's own
    // value, or the Error object Harbour would have raised. The runtime
    // errors Harbour has a code for get Harbour's genCode, subCode,
    // description and operation (the base subsystem's, errors of
    // rtl/errapi.c); any other exception is subsystem ".NET" with its
    // type and message. osCode is the DOS error a failed file operation
    // gives FError().
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
        }
        else switch (ex)
        {
            case DivideByZeroException:
                (e.genCode, e.subCode, e.description, e.operation) = (EG_ZERODIV, 1340, "Zero divisor", "/");
                break;
            case IndexOutOfRangeException:
            case ArgumentOutOfRangeException:
                (e.genCode, e.subCode, e.description, e.operation) = (EG_BOUND, 1132, "Bound error", "array access");
                break;
            case OverflowException:
                (e.genCode, e.description) = (EG_NUMOVERFLOW, "Numeric overflow");
                break;
            case ArgumentException:
                (e.genCode, e.description) = (EG_ARG, "Argument error");
                break;
            default:
                e.subSystem = ".NET";
                e.description = ex.GetType().Name + ": " + ex.Message;
                break;
        }
        if (ex is IOException or UnauthorizedAccessException)
            e.osCode = HbRuntime.OsError(ex);
        return e;
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
    // SEQUENCE took ends the program quietly, and a runtime error goes
    // to the error block, where EasiPOS's DefError logs it and QUITs.
    // A block that returns cannot resume anything, so the program ends
    // with ErrorLevel() 1 unless it set one. With no error block
    // installed the exception goes on to .NET, which prints it and ends
    // the process.
    [System.Diagnostics.StackTraceHidden]
    public static void RunEntry(Action entry)
    {
        try
        {
            entry();
        }
        catch (HbBreak)
        {
        }
        catch (Exception ex) when (LaunchError(ex))
        {
            if (ErrorLevel() == 0)
                ErrorLevel(1);
        }
    }

    // hb_errLaunch() for an error that reached the top: the error block
    // evaluated with the Error object. It runs as RunEntry's exception
    // filter, before C# unwinds the stack, so the block sees the stack
    // where the error happened, as Harbour's does: DefError's traceback
    // (Stack2Str, ProcName) names the routine that failed. False — the
    // exception going on to .NET — when no block is installed or the
    // block itself fails; a BREAK out of the block ends the program.
    [System.Diagnostics.StackTraceHidden]
    static bool LaunchError(Exception ex)
    {
        Delegate? bBlock = t_errorBlock;
        if (bBlock == null)
            return false;
        try
        {
            InvokeBlock(bBlock, new dynamic[] { HbError.From(ex) });
        }
        catch (HbBreak)
        {
        }
        catch (Exception exBlock)
        {
            Console.Error.WriteLine("The error block failed: " + exBlock);
            return false;
        }
        return true;
    }
}
