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
