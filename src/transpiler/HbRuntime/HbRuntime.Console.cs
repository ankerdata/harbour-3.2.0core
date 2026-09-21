using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// The console: ? and ?? (QOut() / QQOut()), OutStd() / OutErr(), and the
// keyboard and screen functions only the test program calls.
// One part of HbRuntime; HbRuntime.cs says what the whole is.

public static partial class HbRuntime
{
    // ---- Console output (? and ??) ----

    // Harbour's `?` / `??` commands separate comma-delimited args
    // with a single space in the output. "hi", n prints as "hi 3"
    // (space between label and value), not "hi3".
    public static void QOut(params dynamic[] args)
    {
        Console.WriteLine();
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) Console.Write(' ');
            Console.Write(Fmt(args[i]));
        }
    }

    public static void QQOut(params dynamic[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) Console.Write(' ');
            Console.Write(Fmt(args[i]));
        }
    }

    static string Fmt(dynamic a) =>
        a is decimal d ? Str(d) :
        // Harbour renders logicals as .T. / .F., and pads all numerics
        // to Str()'s default width. Integral values reach here when a
        // literal int crossed a dynamic boundary (the emitter only
        // m-suffixes floating literals), so give them the same padding
        // a decimal would get.
        a is bool l ? (l ? ".T." : ".F.") :
        IsNumeric(a) ? Str(Convert.ToDecimal(a, INV)) :
        Convert.ToString(a, INV);

    // A Harbour number in any of the CLR's numeric types: a decimal as a
    // rule, but an int or long where an integer literal crossed a
    // dynamic boundary, a uint for a literal past int's range
    // (2147483648), a double from a library
    static bool IsNumeric(object x) =>
        x is decimal or int or long or double or float or short or byte
          or sbyte or ushort or uint or ulong;

    // ---- Terminal ----

    public static void SetColor(string cColor) { }

    // ---- The console ----
    // rtl/console.c, inkey.c, gx.c, accept.c. The test program is the only
    // caller of the keyboard and screen ones.

    // OutStd( ... ) / OutErr( ... ): each value as ? would write it,
    // separated by single spaces, with no newline, to stdout / stderr.
    public static void OutStd(params dynamic[] aValues) => WriteValues(Console.Out, aValues);

    public static void OutErr(params dynamic[] aValues) => WriteValues(Console.Error, aValues);

    static void WriteValues(TextWriter w, dynamic[] aValues)
    {
        for (int i = 0; i < aValues.Length; i++)
        {
            if (i > 0)
                w.Write(' ');
            w.Write(Fmt(aValues[i]));
        }
        w.Flush();
    }

    // Inkey( [<nSeconds>] ): the next key's code, 0 when there is none.
    // With no argument it does not wait; 0 waits until a key comes. With no
    // console to read (input redirected) there are no keys.
    public static decimal Inkey() => ReadKey(false, 0);

    public static decimal Inkey(decimal nSeconds) => ReadKey(true, nSeconds);

    static decimal ReadKey(bool lWait, decimal nSeconds)
    {
        if (Console.IsInputRedirected)
        {
            if (lWait && nSeconds > 0)
                hb_idleSleep(nSeconds);
            return 0;
        }
        DateTime tEnd = DateTime.Now.AddSeconds((double) nSeconds);
        try
        {
            while (!Console.KeyAvailable)
            {
                if (!lWait || (nSeconds > 0 && DateTime.Now >= tEnd))
                    return 0;
                System.Threading.Thread.Sleep(10);
            }
            return KeyCode(Console.ReadKey(true));
        }
        catch (Exception e) when (e is InvalidOperationException or IOException)
        {
            return 0;       // no console to read keys from
        }
    }

    // The inkey.ch code of a key: its character for a plain one, the K_*
    // value for the keys that have one.
    static decimal KeyCode(ConsoleKeyInfo k) => k.Key switch
    {
        ConsoleKey.UpArrow => 5,        // K_UP
        ConsoleKey.DownArrow => 24,     // K_DOWN
        ConsoleKey.LeftArrow => 19,     // K_LEFT
        ConsoleKey.RightArrow => 4,     // K_RIGHT
        ConsoleKey.Home => 1,           // K_HOME
        ConsoleKey.End => 6,            // K_END
        ConsoleKey.PageUp => 18,        // K_PGUP
        ConsoleKey.PageDown => 3,       // K_PGDN
        ConsoleKey.Insert => 22,        // K_INS
        ConsoleKey.Delete => 7,         // K_DEL
        ConsoleKey.F1 => 28,            // K_F1
        >= ConsoleKey.F2 and <= ConsoleKey.F10 => -(k.Key - ConsoleKey.F2 + 1),    // K_F2 .. K_F10
        _ => k.KeyChar,
    };

    // hb_keyStd( <nKey> ): the standard code of a key. Inkey() gives
    // standard codes already.
    public static decimal hb_keyStd(decimal nKey) => nKey;

    // SetMode( <nRows>, <nCols> ): size the screen — here, the console's
    // buffer — and say whether that worked. There is no screen to size when
    // output is redirected.
    public static bool SetMode(decimal nRows, decimal nCols)
    {
        if (Console.IsOutputRedirected || !OperatingSystem.IsWindows())
            return false;
        try
        {
            Console.SetBufferSize((int) nCols, (int) nRows);
            return true;
        }
        catch (Exception e) when (e is IOException or ArgumentOutOfRangeException) { return false; }
    }

    // __Accept( <cPrompt> ) — ACCEPT: the prompt as ? writes it, then a line
    // typed in.
    public static string __Accept(dynamic cPrompt)
    {
        QOut(cPrompt);
        return Console.ReadLine() ?? "";
    }

    // DiskChange( <cDrive> ): make that drive current (rtl/dirdrive.c). It
    // keeps its own current directory, as the drive's does in Windows.
    public static bool DiskChange(string cDrive)
    {
        if (string.IsNullOrEmpty(cDrive) || !char.IsAsciiLetter(cDrive[0]))
            return false;
        try
        {
            System.IO.Directory.SetCurrentDirectory(Path.GetFullPath(char.ToUpperInvariant(cDrive[0]) + ":"));
            return true;
        }
        catch (Exception e) when (IsFsFailure(e)) { return false; }
    }
}
