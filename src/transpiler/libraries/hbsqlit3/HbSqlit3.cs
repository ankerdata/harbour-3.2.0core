// HbSqlit3 — the hbsqlit3 contrib library (contrib/hbsqlit3/hbsqlit3.hbx) for
// transpiled Harbour code. The emitter writes `HbSqlit3.<name>(...)` for
// every name that .hbx lists. Real implementations go here; HbSqlit3.Stubs.cs
// holds NotImplementedException stubs for the rest the application calls
// (scripts/gen_library_stubs.py in easipos-transpiled).
//
// The functions are ports of contrib/hbsqlit3/core.c over the SQLite that
// Harbour's hbsqlit3 links: SQLite3 Multiple Ciphers, built from the same
// amalgamation with the same flags as native\<arch>\sqlite3mc.dll
// (native\build.bat), so an encrypted EasiPOS database opens as it does
// under 9.0. As in core.c:
//   - a database is a handle the collector closes (sqlite3_close) when the
//     last reference to it goes; a statement is finalized by its owner
//   - an index is 1-based, as Harbour counts columns
//   - text crosses as UTF-8 through the Harbour string, a byte per char
//     (the Latin-1 convention HbRuntime's hb_StrToUTF8() / hb_UTF8ToStr()
//     keep): a character the string cannot hold comes back as '?'
//   - a dead or missing handle is the argument error (EG_ARG, subCode 0)
//   - a failed open or prepare is NIL, where core.c returns an empty
//     pointer: the ruling HbRuntime's sockets follow (EasiPOS asks Empty()
//     or == NIL, which both answer alike)

using System.Runtime.InteropServices;
using System.Text;

public static partial class HbSqlit3
{
    // ---- the handles ----------------------------------------------------

    // sqlite3 *: the collector closes it, as core.c's hb_sqlite3_destructor
    // does, with sqlite3_close(): a connection with a statement not yet
    // finalized stays open (SQLITE_BUSY), there as here.
    public sealed class Sqlite3Db : SafeHandle
    {
        internal Sqlite3Db(IntPtr pDb) : base(IntPtr.Zero, true) => SetHandle(pDb);
        public override bool IsInvalid => handle == IntPtr.Zero;
        protected override bool ReleaseHandle()
        {
            Native.sqlite3_close(handle);
            return true;
        }
    }

    // sqlite3_stmt *: its owner finalizes it (sqlite3_finalize()), after
    // which it is dead; one nothing finalized is finalized when collected.
    public sealed class Sqlite3Stmt : SafeHandle
    {
        internal Sqlite3Stmt(IntPtr pStmt) : base(IntPtr.Zero, true) => SetHandle(pStmt);
        public override bool IsInvalid => handle == IntPtr.Zero;
        protected override bool ReleaseHandle()
        {
            Native.sqlite3_finalize(handle);
            return true;
        }
        internal int Finalize()
        {
            int rc = Native.sqlite3_finalize(handle);
            SetHandleAsInvalid();
            return rc;
        }
    }

    const int SQLITE_OK = 0;
    static readonly IntPtr SQLITE_TRANSIENT = new(-1);

    // ---- the functions --------------------------------------------------

    // sqlite3_open( <cFile>, [<lCreate>] ): a file that does not exist is
    // opened only when lCreate asks for it (":memory:" never exists).
    public static Sqlite3Db? sqlite3_open(object? cFile, object? lCreate = null)
    {
        string cName = cFile as string ?? "";
        if (!File.Exists(cName) && lCreate is not true)
            return null;
        int rc = Native.sqlite3_open(ToUtf8(cName), out IntPtr pDb);
        if (rc != SQLITE_OK)
        {
            Native.sqlite3_close(pDb);
            return null;
        }
        return new Sqlite3Db(pDb);
    }

    // sqlite3_exec( <pDb>, <cSQL> ): the result code; a callback is not
    // supported (EasiPOS passes none), and a failed statement's message is
    // sqlite3_errmsg()'s to give, as core.c frees its own.
    public static long sqlite3_exec(object? pDb, object? cSQL, object? xCallback = null)
    {
        IntPtr db = Db(pDb, "SQLITE3_EXEC");
        if (xCallback != null)
            throw new NotSupportedException("sqlite3_exec(): a callback is not supported");
        int rc = Native.sqlite3_exec(db, ToUtf8(cSQL as string ?? ""), IntPtr.Zero, IntPtr.Zero, out IntPtr pErr);
        if (pErr != IntPtr.Zero)
            Native.sqlite3_free(pErr);
        return rc;
    }

    // sqlite3_get_table( <pDb>, <cSQL> ): { { <column names> }, { <row> }, ... },
    // every value text ("" for NULL); {} when the statement fails.
    public static object?[] sqlite3_get_table(object? pDb, object? cSQL)
    {
        IntPtr db = Db(pDb, "SQLITE3_GET_TABLE");
        var aResult = new List<object?>();
        int rc = Native.sqlite3_get_table(db, ToUtf8(cSQL as string ?? ""), out IntPtr pResult,
                                          out int nRow, out int nCol, out IntPtr pErr);
        if (rc == SQLITE_OK)
        {
            int k = 0;
            for (int i = 0; i < nRow + 1; i++)
            {
                var aRow = new object?[nCol];
                for (int j = 0; j < nCol; j++, k++)
                    aRow[j] = FromUtf8(Marshal.ReadIntPtr(pResult, k * IntPtr.Size), -1);
                aResult.Add(aRow);
            }
        }
        else if (pErr != IntPtr.Zero)
            Native.sqlite3_free(pErr);
        Native.sqlite3_free_table(pResult);
        return aResult.ToArray();
    }

    // sqlite3_errmsg( <pDb> ): the last error's text
    public static string sqlite3_errmsg(object? pDb) =>
        FromUtf8(Native.sqlite3_errmsg(Db(pDb, "SQLITE3_ERRMSG")), -1);

    // sqlite3_prepare( <pDb>, <cSQL>, [<nPrepFlags>] ): the statement, NIL
    // when it does not compile
    public static Sqlite3Stmt? sqlite3_prepare(object? pDb, object? cSQL, object? nPrepFlags = null)
    {
        IntPtr db = Db(pDb, "SQLITE3_PREPARE");
        byte[] aSQL = ToUtf8(cSQL as string ?? "");
        int rc = Native.sqlite3_prepare_v3(db, aSQL, aSQL.Length, (uint) Int(nPrepFlags),
                                           out IntPtr pStmt, out _);
        if (rc != SQLITE_OK)
        {
            Native.sqlite3_finalize(pStmt);
            return null;
        }
        return new Sqlite3Stmt(pStmt);
    }

    public static long sqlite3_step(object? pStmt) => Native.sqlite3_step(Stmt(pStmt, "SQLITE3_STEP"));

    public static long sqlite3_reset(object? pStmt) => Native.sqlite3_reset(Stmt(pStmt, "SQLITE3_RESET"));

    public static long sqlite3_clear_bindings(object? pStmt) =>
        Native.sqlite3_clear_bindings(Stmt(pStmt, "SQLITE3_CLEAR_BINDINGS"));

    // sqlite3_finalize( <pStmt> ): the result code; the statement is dead
    public static long sqlite3_finalize(object? pStmt)
    {
        Stmt(pStmt, "SQLITE3_FINALIZE");
        return ((Sqlite3Stmt) pStmt!).Finalize();
    }

    // sqlite3_bind_*( <pStmt>, <nParam>, <xValue> ): the result code
    public static long sqlite3_bind_double(object? pStmt, object? nParam, object? nValue) =>
        Native.sqlite3_bind_double(Stmt(pStmt, "SQLITE3_BIND_DOUBLE"), Int(nParam), Double(nValue));

    public static long sqlite3_bind_int(object? pStmt, object? nParam, object? nValue) =>
        Native.sqlite3_bind_int(Stmt(pStmt, "SQLITE3_BIND_INT"), Int(nParam), Int(nValue));

    public static long sqlite3_bind_int64(object? pStmt, object? nParam, object? nValue) =>
        Native.sqlite3_bind_int64(Stmt(pStmt, "SQLITE3_BIND_INT64"), Int(nParam), Int64(nValue));

    public static long sqlite3_bind_text(object? pStmt, object? nParam, object? cText)
    {
        IntPtr stmt = Stmt(pStmt, "SQLITE3_BIND_TEXT");
        byte[] aText = ToUtf8(cText as string ?? "");
            // the length without ToUtf8()'s terminating NUL, which is not the value's
        return Native.sqlite3_bind_text(stmt, Int(nParam), aText, aText.Length - 1, SQLITE_TRANSIENT);
    }

    // sqlite3_column_*( <pStmt>, <nColumn> ): the current row's value
    public static decimal sqlite3_column_double(object? pStmt, object? nColumn) =>
        (decimal) Native.sqlite3_column_double(Stmt(pStmt, "SQLITE3_COLUMN_DOUBLE"), Int(nColumn) - 1);

    public static long sqlite3_column_int(object? pStmt, object? nColumn) =>
        Native.sqlite3_column_int(Stmt(pStmt, "SQLITE3_COLUMN_INT"), Int(nColumn) - 1);

    public static long sqlite3_column_int64(object? pStmt, object? nColumn) =>
        Native.sqlite3_column_int64(Stmt(pStmt, "SQLITE3_COLUMN_INT64"), Int(nColumn) - 1);

    public static string sqlite3_column_text(object? pStmt, object? nColumn)
    {
        IntPtr stmt = Stmt(pStmt, "SQLITE3_COLUMN_TEXT");
        int iColumn = Int(nColumn) - 1;
        IntPtr pText = Native.sqlite3_column_text(stmt, iColumn);
        return FromUtf8(pText, Native.sqlite3_column_bytes(stmt, iColumn));
    }

    public static string sqlite3_column_name(object? pStmt, object? nColumn) =>
        FromUtf8(Native.sqlite3_column_name(Stmt(pStmt, "SQLITE3_COLUMN_NAME"), Int(nColumn) - 1), -1);

    // ---- arguments ------------------------------------------------------

    // hb_sqlite3_param() / hb_parptr(): a live handle, else the argument
    // error HbError.From() reads as Harbour's (EG_ARG)
    static IntPtr Db(object? pDb, string cFunc) =>
        pDb is Sqlite3Db { IsInvalid: false, IsClosed: false } db
            ? db.DangerousGetHandle()
            : throw new ArgumentException("Argument error (" + cFunc + ")");

    static IntPtr Stmt(object? pStmt, string cFunc) =>
        pStmt is Sqlite3Stmt { IsInvalid: false, IsClosed: false } stmt
            ? stmt.DangerousGetHandle()
            : throw new ArgumentException("Argument error (" + cFunc + ")");

    // hb_parni(): a number truncated to an int, 0 for anything else
    static int Int(object? n) => n switch
    {
        null or bool or string => 0,
        IConvertible c when IsNumber(n) => (int) Math.Truncate(c.ToDecimal(null)),
        _ => 0,
    };

    // hb_parnint(): the same, 64-bit
    static long Int64(object? n) => n switch
    {
        long l => l,
        IConvertible c when IsNumber(n) => (long) Math.Truncate(c.ToDecimal(null)),
        _ => 0,
    };

    // hb_parnd(): the number as a double, 0 for anything else
    static double Double(object? n) => n is IConvertible c && IsNumber(n) ? c.ToDouble(null) : 0;

    static bool IsNumber(object? n) =>
        n is decimal or long or int or double or float or short or byte or sbyte or uint or ulong or ushort;

    // ---- text ------------------------------------------------------------

    // A Harbour string (a byte per char) as UTF-8, NUL-terminated for the C side
    static byte[] ToUtf8(string cText)
    {
        byte[] aUtf8 = Encoding.UTF8.GetBytes(cText);
        Array.Resize(ref aUtf8, aUtf8.Length + 1);
        return aUtf8;
    }

    // UTF-8 from the C side as a Harbour string; nBytes < 0: NUL-terminated.
    // A character the string cannot hold is '?', as hb_UTF8ToStr() gives.
    static string FromUtf8(IntPtr pText, int nBytes)
    {
        if (pText == IntPtr.Zero)
            return "";
        string cUtf16 = nBytes < 0 ? Marshal.PtrToStringUTF8(pText) ?? ""
                                   : Marshal.PtrToStringUTF8(pText, nBytes);
        return Encoding.Latin1.GetString(Encoding.Latin1.GetBytes(cUtf16));
    }

    // ---- the native library ------------------------------------------------

    static class Native
    {
        const string Lib = "sqlite3mc";

        // native\<arch>\sqlite3mc.dll beside the assembly, the architecture
        // the process runs as (native\build.bat builds all three)
        static Native()
        {
            NativeLibrary.SetDllImportResolver(typeof(Native).Assembly, (cName, oAssembly, _) =>
            {
                if (cName != Lib)
                    return IntPtr.Zero;
                string cArch = RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X86 => "x86",
                    Architecture.X64 => "x64",
                    Architecture.Arm64 => "arm64",
                    var a => throw new PlatformNotSupportedException("sqlite3mc: no build for " + a),
                };
                string cPath = Path.Combine(AppContext.BaseDirectory, "native", cArch, Lib + ".dll");
                return NativeLibrary.Load(cPath);
            });
        }

        [DllImport(Lib)] internal static extern int sqlite3_open(byte[] zFilename, out IntPtr ppDb);
        [DllImport(Lib)] internal static extern int sqlite3_close(IntPtr db);
        [DllImport(Lib)] internal static extern IntPtr sqlite3_errmsg(IntPtr db);
        [DllImport(Lib)] internal static extern int sqlite3_exec(IntPtr db, byte[] zSql, IntPtr callback, IntPtr arg, out IntPtr errmsg);
        [DllImport(Lib)] internal static extern int sqlite3_get_table(IntPtr db, byte[] zSql, out IntPtr pazResult, out int pnRow, out int pnColumn, out IntPtr pzErrmsg);
        [DllImport(Lib)] internal static extern void sqlite3_free_table(IntPtr result);
        [DllImport(Lib)] internal static extern void sqlite3_free(IntPtr p);
        [DllImport(Lib)] internal static extern int sqlite3_prepare_v3(IntPtr db, byte[] zSql, int nByte, uint prepFlags, out IntPtr ppStmt, out IntPtr pzTail);
        [DllImport(Lib)] internal static extern int sqlite3_step(IntPtr stmt);
        [DllImport(Lib)] internal static extern int sqlite3_reset(IntPtr stmt);
        [DllImport(Lib)] internal static extern int sqlite3_clear_bindings(IntPtr stmt);
        [DllImport(Lib)] internal static extern int sqlite3_finalize(IntPtr stmt);
        [DllImport(Lib)] internal static extern int sqlite3_bind_double(IntPtr stmt, int i, double value);
        [DllImport(Lib)] internal static extern int sqlite3_bind_int(IntPtr stmt, int i, int value);
        [DllImport(Lib)] internal static extern int sqlite3_bind_int64(IntPtr stmt, int i, long value);
        [DllImport(Lib)] internal static extern int sqlite3_bind_text(IntPtr stmt, int i, byte[] value, int nByte, IntPtr destructor);
        [DllImport(Lib)] internal static extern double sqlite3_column_double(IntPtr stmt, int iCol);
        [DllImport(Lib)] internal static extern int sqlite3_column_int(IntPtr stmt, int iCol);
        [DllImport(Lib)] internal static extern long sqlite3_column_int64(IntPtr stmt, int iCol);
        [DllImport(Lib)] internal static extern IntPtr sqlite3_column_text(IntPtr stmt, int iCol);
        [DllImport(Lib)] internal static extern int sqlite3_column_bytes(IntPtr stmt, int iCol);
        [DllImport(Lib)] internal static extern IntPtr sqlite3_column_name(IntPtr stmt, int iCol);
    }
}
