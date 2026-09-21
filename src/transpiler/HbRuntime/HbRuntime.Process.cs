using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// The process: the command line, the environment, child processes,
// ErrorLevel() and QUIT, and the call stack (ProcName() / ProcLine() /
// ProcFile()).
// One part of HbRuntime; HbRuntime.cs says what the whole is.

public static partial class HbRuntime
{
    // ---- The process: arguments, environment, child processes, exit ----
    // Ports of vm/cmdarg.c, rtl/gete.c, rtl/hbrunfun.c, rtl/hbprocfn.c
    // with hbproces.c, vm/hvm.c (ErrorLevel), vm/initexit.c (__Quit),
    // rtl/idle.c and vm/garbage.c, as they behave on Windows.

    // hb_argc(): how many arguments follow the program's name. Harbour
    // counts its own //switches among them, and so does this.
    public static decimal hb_argc() => Environment.GetCommandLineArgs().Length - 1;

    // hb_argv( [<n>] ): argument n, "" past the last. Argument 0 is the
    // program, as the full path GetModuleFileName() gives — where .NET's
    // own argument 0 is the entry assembly, a .dll.
    public static string hb_argv(decimal n = 0)
    {
        int i = (int) n;
        if (i == 0)
            return Environment.ProcessPath ?? "";
        string[] aArgs = Environment.GetCommandLineArgs();
        return i > 0 && i < aArgs.Length ? aArgs[i] : "";
    }

    // hb_GetEnv( <cName>, [<cDefault>] ): the variable's value; cDefault,
    // or "", when it is not set or the name is empty.
    public static string hb_GetEnv(string cName, string cDefault = null)
    {
        string cValue = string.IsNullOrEmpty(cName) ? null : Environment.GetEnvironmentVariable(cName);
        return cValue ?? cDefault ?? "";
    }

    // hb_run( <cCommand> ): what the C runtime's system() does — the
    // command through %COMSPEC% /c, in this console — and its exit code;
    // -1 when it could not be started.
    public static decimal hb_run(string cCommand)
    {
        if (cCommand == null)
            throw new ArgumentException("Argument error (HB_RUN)");
        var psi = new System.Diagnostics.ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe")
        {
            Arguments = "/c " + cCommand,
            UseShellExecute = false,
        };
        return RunAndWait(psi);
    }

    // hb_processRun( <cCommand> ) (rtl/hbprocfn.c, hb_fsProcessRun()): the
    // command line straight to CreateProcess(), no shell, with this
    // process's standard handles; the exit code, or -1 when it could not be
    // started, with FError() set either way.
    public static decimal hb_processRun(string cCommand)
    {
        if (cCommand == null)
            throw new ArgumentException("Argument error (HB_PROCESSRUN)");
        SplitCommandLine(cCommand, out string cProgram, out string cArgs);
        var psi = new System.Diagnostics.ProcessStartInfo(cProgram)
        {
            Arguments = cArgs,
            UseShellExecute = false,
        };
        return RunAndWait(psi);
    }

    static decimal RunAndWait(System.Diagnostics.ProcessStartInfo psi)
    {
        try
        {
            using var p = System.Diagnostics.Process.Start(psi);
            p.WaitForExit();
            FsError(0);
            return p.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            FsError(WinToDosError(e.NativeErrorCode));
            return -1;
        }
    }

    // How CreateProcess() reads a command line it is given without an
    // application name: the program is the first token — up to the closing
    // quote when it starts with one — and the rest is passed on as it is.
    static void SplitCommandLine(string cCommand, out string cProgram, out string cArgs)
    {
        string c = cCommand.TrimStart();
        int nEnd;
        if (c.StartsWith('"'))
        {
            nEnd = c.IndexOf('"', 1);
            cProgram = nEnd < 0 ? c.Substring(1) : c.Substring(1, nEnd - 1);
            nEnd = nEnd < 0 ? c.Length : nEnd + 1;
        }
        else
        {
            nEnd = c.IndexOfAny(new[] { ' ', '\t' });
            if (nEnd < 0)
                nEnd = c.Length;
            cProgram = c.Substring(0, nEnd);
        }
        cArgs = c.Substring(nEnd).TrimStart();
    }

    // ErrorLevel( [<nNew>] ): the exit code the program will end with,
    // which a new value replaces. One for the whole process, as in Harbour;
    // Main returns it (easipos-transpiled host/).
    static int s_errorLevel;

    public static decimal ErrorLevel() => s_errorLevel;

    public static decimal ErrorLevel(decimal nNew)
    {
        int nPrev = s_errorLevel;
        s_errorLevel = (int) nNew;
        return nPrev;
    }

    // __Quit() — QUIT: ends the program with ErrorLevel() as its exit code.
    // Harbour runs its EXIT procedures on the way out; the corpus has none.
    public static void __Quit() => Environment.Exit(s_errorLevel);

    // hb_idleSleep( <nSeconds> ): sleep. Harbour runs its idle tasks (the
    // GC) while it waits; .NET does that on threads of its own.
    public static void hb_idleSleep(decimal nSeconds = 0) =>
        System.Threading.Thread.Sleep(nSeconds <= 0 ? 0 : (int) Math.Min(nSeconds * 1000m, int.MaxValue));

    // hb_gcAll( [<lForce>] ): a full collection.
    public static void hb_gcAll(bool lForce = true) => GC.Collect();

    // Version(): what the program runs on. Once the port is the product the
    // Harbour code is gone (Alex, 2026-09-21), so this is .NET's own
    // description, ".NET 10.0.x".
    public static string Version() => System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

    // ---- The call stack ----
    // ProcName( [<n>] ), ProcLine( [<n>] ), ProcFile( [<n>] ) (vm/proc.c):
    // the routine n calls up the stack — 0 is the one calling ProcName() —
    // its source file and its line in it. Once the port is the product the
    // C# is the source (Alex, 2026-09-21), so they name the C# method, its
    // .cs file and its line. File and line come from the debug symbols —
    // "" and 0 without them — and an optimised build may inline a small
    // routine out of the stack altogether. The runtime's own frames
    // (reflection, the call sites behind `dynamic`) are not routines, and
    // are not counted.

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static string ProcName(decimal nLevel = 0) => RoutineName(CallerFrame(nLevel));

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static decimal ProcLine(decimal nLevel = 0) => CallerFrame(nLevel)?.GetFileLineNumber() ?? 0;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static string ProcFile(decimal nLevel = 0) => Path.GetFileName(CallerFrame(nLevel)?.GetFileName() ?? "");

    // The routine nLevel above the caller of ProcName() / ProcLine() /
    // ProcFile(): the trace starts past this method and the ProcXxx() that
    // called it.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static System.Diagnostics.StackFrame CallerFrame(decimal nLevel)
    {
        int n = (int) nLevel;
        if (n < 0)
            return null;
        foreach (var f in new System.Diagnostics.StackTrace(2, true).GetFrames())
        {
            var m = f.GetMethod();
            string ns = m?.DeclaringType?.Namespace ?? "";
            if (m?.DeclaringType == null || ns == "System" || ns.StartsWith("System.") || ns.StartsWith("Microsoft."))
                continue;
            if (n-- == 0)
                return f;
        }
        return null;
    }

    // A routine as C# names it: one of Program's functions by its own name,
    // a method with its class ("Transaction.SetAllValues"), and a codeblock —
    // compiled to "<Outer>b__3_0" in a closure class nested in the routine's
    // own — as Harbour writes one, "(b)" and the routine it sits in.
    static string RoutineName(System.Diagnostics.StackFrame f)
    {
        var m = f?.GetMethod();
        if (m == null)
            return "";
        string cName = m.Name;
        Type t = m.DeclaringType;
        int nEnd = cName.IndexOf('>');
        bool lBlock = cName.StartsWith('<') && nEnd > 1;
        if (lBlock)
        {
            cName = cName.Substring(1, nEnd - 1);
            while (t?.DeclaringType != null && t.Name.StartsWith('<'))
                t = t.DeclaringType;
        }
        string cRoutine = t == null || t.Name == "Program" ? cName : t.Name + "." + cName;
        return lBlock ? "(b)" + cRoutine : cRoutine;
    }
}
