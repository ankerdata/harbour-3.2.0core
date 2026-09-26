// Xhb — the xhb contrib library (contrib/xhb/xhb.hbx) for transpiled
// Harbour code. The emitter writes `Xhb.<name>(...)` for every name that
// .hbx lists. Real implementations go here; Xhb.Stubs.cs holds
// NotImplementedException stubs for the rest the application calls
// (scripts/gen_library_stubs.py in easipos-transpiled).
//
// Seeded 2026-09-16 with TOleAuto, moved unchanged from HbRuntime.cs.

public static partial class Xhb
{
    // Harbour's `TOleAuto():New("ADODB.Recordset")` reaches C# as
    // Xhb.TOleAuto().New(...): the class function is this factory and
    // New() on the result is the COM creation. global:: because the
    // method shares the type's name.
    public static global::TOleAuto TOleAuto() => new global::TOleAuto();

    // hb_regexReplace( <cRegex>, <cString>, <cReplace>, [<lCaseSensitive>],
    //                  [<lNewLine>], [<nMaxMatches>], [<nGetMatch>] ):
    // contrib/xhb/regexrpl.prg. Every match hb_regexAll() finds, left to
    // right, is replaced by cReplace as it stands: no $1-style references,
    // it is Stuff()ed in. lCaseSensitive defaults to .T., lNewLine makes ^
    // and $ match at each line, nMaxMatches 0 (the default) takes them all.
    // nGetMatch, a sub-match to replace, is not used by EasiPOS and not
    // supported; a compiled pattern (hb_regexComp()) neither.
    public static string hb_regexReplace(object? cRegex, object? cString, object? cReplace,
                                         object? lCaseSensitive = null, object? lNewLine = null,
                                         object? nMaxMatches = null, object? nGetMatch = null)
    {
        if (cRegex is not string cPattern || cString is not string cText)
            throw new ArgumentException("Argument error (HB_REGEXREPLACE)");
        if (nGetMatch is IConvertible g && g.ToInt64(null) != 0)
            throw new NotSupportedException("hb_regexReplace(): nGetMatch is not supported");
        var eOptions = System.Text.RegularExpressions.RegexOptions.CultureInvariant;
        if (lCaseSensitive is false)
            eOptions |= System.Text.RegularExpressions.RegexOptions.IgnoreCase;
        if (lNewLine is true)
            eOptions |= System.Text.RegularExpressions.RegexOptions.Multiline;
        long nMax = nMaxMatches is IConvertible m ? m.ToInt64(null) : 0;
        string cWith = cReplace as string ?? "";
        var oRegex = new System.Text.RegularExpressions.Regex(cPattern, eOptions);
        return nMax > 0 ? oRegex.Replace(cText, _ => cWith, (int) nMax)
                        : oRegex.Replace(cText, _ => cWith);
    }
}

/// <summary>
/// Harbour's COM automation wrapper. Source calls look like
/// <c>TOleAuto():New("ADODB.Recordset")</c> — compile-time shape: a class
/// with a <c>New(string progId)</c> method returning a dynamic COM proxy.
/// On Windows this delegates to <c>Type.GetTypeFromProgID</c> +
/// <c>Activator.CreateInstance</c>; on other platforms there is no COM,
/// so New throws at call time.
/// </summary>
public class TOleAuto
{
    private object? _com;
    public TOleAuto() { }
    public dynamic New(string progId)
    {
        if (OperatingSystem.IsWindows())
        {
            var t = Type.GetTypeFromProgID(progId);
            if (t == null)
                throw new PlatformNotSupportedException(
                    $"ProgID '{progId}' not registered");
            _com = Activator.CreateInstance(t);
            return _com!;
        }
        throw new PlatformNotSupportedException(
            "TOleAuto (COM automation) is only supported on Windows");
    }
}
