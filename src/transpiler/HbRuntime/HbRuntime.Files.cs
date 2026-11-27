using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// Files: the handle functions (FOpen() ... FError()), the memo functions
// and temporary files.
// One part of HbRuntime; HbRuntime.cs says what the whole is.

public static partial class HbRuntime
{
    // ---- Files and directories ----
    // Ports of Harbour's file layer as it behaves on Windows: rtl/philes.c
    // (the F* functions), filesys.c (open flags and seek), fserr.c (the
    // error codes), direct.c with common/hbffind.c (Directory), spfiles.c
    // (File), memofile.c, fnsplit.c and hbfilehc.c (the name helpers),
    // hbfile.c, fstemp.c, vfile.c, diskspac.c, dirdrive.c, and the .prg
    // level of hbfilehi.prg and dirscan.prg.
    //
    // What crosses into or out of a file is the string's bytes, as
    // HbBytes has them: UTF-8 for its text, each other byte as it is.

    // Open handles. Harbour hands out the OS handle; these are numbers
    // from 3 up, 0-2 being the console's there. The streams are unbuffered
    // so that, as in Harbour, every FWrite reaches the file at once and two
    // handles on one file see each other's writes.
    static readonly Dictionary<long, FileStream> s_files = new();
    static long s_nextHandle = 3;

    // FError() is per thread, as Harbour's is (it lives on the thread's
    // stack), and every F* function sets it: 0 on success, else the DOS
    // error code of its last operation. t_ioError is hb_fsError(), the
    // error of the last real file operation whoever made it — the
    // functions that only look a name up leave it alone, and the ones
    // that report "the last error" (hb_vfExists(), hb_cwd()) copy it.
    [ThreadStatic] static int t_fError;
    [ThreadStatic] static int t_ioError;

    // An operation's outcome: both FError() and hb_fsError().
    static int FsError(int nError) => t_fError = t_ioError = nError;

    // fileio.ch flags that only this layer uses
    const int HB_FO_READ = 0, HB_FO_WRITE = 1, HB_FO_READWRITE = 2,
              HB_FO_EXCLUSIVE = 0x10, HB_FO_CREAT = 0x100,
              HB_FO_TRUNC = 0x200, HB_FO_EXCL = 0x400;
    const int HB_FA_READONLY = 0x01, HB_FA_HIDDEN = 0x02, HB_FA_SYSTEM = 0x04,
              HB_FA_LABEL = 0x08, HB_FA_DIRECTORY = 0x10, HB_FA_ARCHIVE = 0x20,
              HB_FA_ALL = 0;        // fileio.ch: no attribute is asked for

    // The DOS error code Harbour reports for a failed System.IO call. A
    // Win32 failure carries its code in the HRESULT (0x8007xxxx) the way
    // GetLastError() gives it to hb_fsSetIOError(); the rest are mapped by
    // kind. NotSupportedException is what .NET raises where Windows would
    // fail a read on a write-only handle with ERROR_ACCESS_DENIED.
    // HbError.From gives an Error object the same osCode.
    internal static int OsError(Exception e) => WinToDosError(
        (e.HResult & unchecked((int) 0xFFFF0000)) == unchecked((int) 0x80070000) ? e.HResult & 0xFFFF
        // off Windows a file another FileStream holds is EWOULDBLOCK from
        // .NET's flock(), carried bare: Windows's ERROR_SHARING_VIOLATION
        : e is IOException && !OperatingSystem.IsWindows() && e.HResult == (OperatingSystem.IsMacOS() ? 35 : 11) ? 32
        : e is FileNotFoundException ? 2
        : e is DirectoryNotFoundException ? 3
        : e is UnauthorizedAccessException or NotSupportedException ? 5
        : 31);      // ERROR_GEN_FAILURE

    // hb_WinToDosError() (rtl/fserr.c): all it translates
    static int WinToDosError(int nError) => nError switch
    {
        183 or 1314 => 5,   // ERROR_ALREADY_EXISTS, ERROR_PRIVILEGE_NOT_HELD
        123 => 2,           // ERROR_INVALID_NAME
        _ => nError,
    };

    // The errors a System.IO call raises for a file it cannot use, as
    // opposed to a bug here.
    static bool IsFsFailure(Exception e) =>
        e is IOException or UnauthorizedAccessException or ArgumentException or
             NotSupportedException or System.Security.SecurityException;

    // What Windows reports for a name that is not there: 3 when its
    // directory is missing too, 2 otherwise.
    static int MissingError(string cFile)
    {
        try
        {
            string cDir = Path.GetDirectoryName(Path.GetFullPath(cFile));
            return cDir == null || System.IO.Directory.Exists(cDir) ? 2 : 3;
        }
        catch (Exception e) when (IsFsFailure(e)) { return 3; }
    }

    static bool TryGetFile(decimal nHandle, out FileStream fs)
    {
        lock (s_files)
            return s_files.TryGetValue((long) nHandle, out fs);
    }

    static FileAttributes AttrOf(int nAttr) =>
        FileAttributes.Archive |
        ((nAttr & HB_FA_READONLY) != 0 ? FileAttributes.ReadOnly : 0) |
        ((nAttr & HB_FA_HIDDEN) != 0 ? FileAttributes.Hidden : 0) |
        ((nAttr & HB_FA_SYSTEM) != 0 ? FileAttributes.System : 0);

    // hb_fsOpenEx() with convert_open_flags() (rtl/filesys.c, the Windows
    // branch): the create disposition comes from FO_CREAT / FO_TRUNC /
    // FO_EXCL, the access from the low two bits, and the share mode from
    // FO_EXCLUSIVE / FO_DENYWRITE / FO_DENYREAD — anything else (FO_COMPAT,
    // FO_DENYNONE, FO_SHARED) shares both.
    static long FsOpen(string cFile, int nFlags, int nAttr)
    {
        FileMode mode =
            (nFlags & HB_FO_CREAT) != 0
               ? (nFlags & HB_FO_EXCL) != 0 ? FileMode.CreateNew
               : (nFlags & HB_FO_TRUNC) != 0 ? FileMode.Create
               : FileMode.OpenOrCreate
            : (nFlags & HB_FO_TRUNC) != 0 ? FileMode.Truncate
            : FileMode.Open;
        FileAccess access = (nFlags & 3) switch
        {
            HB_FO_WRITE => FileAccess.Write,
            HB_FO_READWRITE => FileAccess.ReadWrite,
            _ => FileAccess.Read,
        };
        FileShare share = (nFlags & 0x70) switch
        {
            HB_FO_EXCLUSIVE => FileShare.None,
            0x20 => FileShare.Read,     // FO_DENYWRITE
            0x30 => FileShare.Write,    // FO_DENYREAD
            _ => FileShare.ReadWrite,
        };

        try
        {
            var fs = new FileStream(cFile, mode, access, share, bufferSize: 1);
            if (nAttr != 0 && mode is FileMode.Create or FileMode.CreateNew)
                System.IO.File.SetAttributes(cFile, AttrOf(nAttr));
            FsError(0);
            lock (s_files)
            {
                s_files[s_nextHandle] = fs;
                return s_nextHandle++;
            }
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            FsError(OsError(e));
            return F_ERROR;
        }
    }

    // FOpen( <cFile>, [<nMode>] ): the handle, or -1. No file name is the
    // undocumented Clipper argument error.
    public static long FOpen(string cFile, decimal nMode = 0)
    {
        if (cFile == null)
        {
            FsError(0);
            throw new ArgumentException("Argument error (FOPEN)");
        }
        return FsOpen(cFile, (int) nMode, 0);
    }

    // FCreate( <cFile>, [<nAttr>] ): create or truncate, read-write and
    // exclusive (hb_fsCreate()).
    public static long FCreate(string cFile, decimal nAttr = 0)
    {
        if (cFile == null) { FsError(0); return F_ERROR; }
        return FsOpen(cFile, HB_FO_READWRITE | HB_FO_CREAT | HB_FO_TRUNC | HB_FO_EXCLUSIVE, (int) nAttr);
    }

    // hb_FCreate( <cFile>, [<nAttr>], [<nFlags>] ): as FCreate(), but the
    // share mode is the caller's (hb_fsCreateEx() drops the access bits).
    public static long hb_FCreate(string cFile, decimal nAttr = 0, decimal nFlags = 0)
    {
        if (cFile == null) { FsError(0); return F_ERROR; }
        return FsOpen(cFile, ((int) nFlags & ~3) | HB_FO_READWRITE | HB_FO_CREAT | HB_FO_TRUNC, (int) nAttr);
    }

    // hb_FLock( <nHandle>, <nStart>, <nLength> ): an exclusive lock on a
    // byte range of an open file, refused at once when another handle
    // holds any of it (ERROR_LOCK_VIOLATION), and released when the handle
    // is closed. The range may lie beyond the file's end. Harbour's fourth
    // parameter, HB_FLX_SHARED or HB_FLX_WAIT, has no FileStream form and
    // is not taken.
    //
    // Windows and Linux lock through FileStream; on Linux the lock is the
    // process's, not the handle's (a second handle of the same process is
    // not refused). macOS, where .NET has no FileStream.Lock(), locks the
    // whole file (MacLockFile()).
    public static bool hb_FLock(decimal nHandle, decimal nStart, decimal nLength)
    {
        if (!TryGetFile(nHandle, out FileStream fs))
        {
            FsError(6);
            return false;
        }
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                int nError = MacLockFile(fs);
                FsError(nError);
                return nError == 0;
            }
#pragma warning disable CA1416     // macOS went the other way just above
            fs.Lock((long) nStart, (long) nLength);
#pragma warning restore CA1416
            FsError(0);
            return true;
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            // Linux refuses a held range with EAGAIN, which OsError() reads
            // as a file that is held (32): here it is the range, 33
            int nError = OsError(e);
            FsError(nError == 32 && !OperatingSystem.IsWindows() ? 33 : nError);
            return false;
        }
    }

    // macOS has no byte-range lock a .NET program can use on a file it
    // has open: FileStream holds a shared flock() on every file it opens
    // (how .NET gives FileShare a meaning between processes, which
    // FO_EXCLUSIVE relies on), and there a range lock (fcntl(), lockf())
    // is refused against the process's own flock(), and held, makes every
    // other FileStream's open fail. So the lock is that flock() made
    // exclusive, i.e., the whole file, whatever the range. Another process 
    // then fails to open the file at all (a sharing violation, 32) rather 
    // than to lock it. Returns the DOS error: 0, 33 when it is held, else 5.
    static int MacLockFile(FileStream fs)
    {
        const int LOCK_EX = 2, LOCK_NB = 4, EWOULDBLOCK = 35;

        int fd = (int) fs.SafeFileHandle.DangerousGetHandle();
        if (MacLibc.flock(fd, LOCK_EX | LOCK_NB) == 0)
            return 0;
        return System.Runtime.InteropServices.Marshal.GetLastPInvokeError() == EWOULDBLOCK ? 33 : 5;
    }

    static class MacLibc
    {
        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int flock(int fd, int operation);
    }

    // FClose( <nHandle> ): .T. when it closed. An unknown handle is
    // ERROR_INVALID_HANDLE, as closing a closed handle is in Harbour.
    public static bool FClose(decimal nHandle)
    {
        FileStream fs;
        lock (s_files)
        {
            if (!s_files.Remove((long) nHandle, out fs))
            {
                FsError(6);
                return false;
            }
        }
        try
        {
            fs.Dispose();
            FsError(0);
            return true;
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            FsError(OsError(e));
            return false;
        }
    }

    // FRead( <nHandle>, @<cBuffer>, <nBytes> ): reads into the buffer in
    // place, so it keeps its length. Clipper sized the buffer with
    // _parcsiz(), so nBytes may be one more than Len( cBuffer ) — that byte
    // lands on the terminating zero and is lost — and anything longer reads
    // nothing at all and returns 0.
    public static long FRead(decimal nHandle, ref string cBuf, decimal nBytes)
    {
        int nError = 0;
        int nRead = 0;

        int nSize = HbBytes.Count(cBuf);

        if (cBuf != null && nBytes >= 0 && nBytes <= nSize + 1)
        {
            if (TryGetFile(nHandle, out var fs))
            {
                try
                {
                    byte[] b = new byte[(int) nBytes];
                    nRead = fs.Read(b, 0, b.Length);
                    if (nRead > 0)
                        cBuf = HbBytes.Text(b, 0, Math.Min(nRead, nSize)) + HbBytes.Slice(cBuf, nRead, nSize);
                }
                catch (Exception e) when (IsFsFailure(e))
                {
                    nError = OsError(e);
                    nRead = 0;
                }
            }
            else
                nError = 6;
        }

        FsError(nError);
        return nRead;
    }

    // FWrite( <nHandle>, <cBuffer>, [<nBytes>] ): writes the buffer's
    // bytes, at most nBytes of them. Writing nothing truncates the file at
    // the current position, as hb_fsWriteLarge()'s SetEndOfFile() does.
    public static long FWrite(decimal nHandle, string cBuf, decimal nBytes = -1)
    {
        int nError = 0;
        long nWritten = 0;

        if (cBuf != null)
        {
            byte[] aBuf = HbBytes.Of(cBuf);
            int nLen = aBuf.Length;
            if (nBytes >= 0 && nBytes < nLen)
                nLen = (int) nBytes;

            if (TryGetFile(nHandle, out var fs))
            {
                try
                {
                    if (nLen == 0)
                        fs.SetLength(fs.Position);
                    else
                    {
                        fs.Write(aBuf, 0, nLen);
                        nWritten = nLen;
                    }
                }
                catch (Exception e) when (IsFsFailure(e))
                {
                    nError = OsError(e);
                    nWritten = 0;
                }
            }
            else
                nError = 6;
        }

        FsError(nError);
        return nWritten;
    }

    // FSeek( <nHandle>, <nOffset>, [<nOrigin>] ): the new position, or the
    // current one when the seek failed. hb_fsSeekLarge() refuses a negative
    // FS_SET offset itself, with error 25; a relative seek before the start
    // is Windows' ERROR_NEGATIVE_SEEK. Seeking past the end is allowed.
    public static long FSeek(decimal nHandle, decimal nOffset, decimal nOrigin = 0)
    {
        if (!TryGetFile(nHandle, out var fs))
        {
            FsError(6);
            return 0;
        }

        try
        {
            // convert_seek_flags(): FS_END beats FS_RELATIVE, and anything
            // else — FS_SET among it — counts from the start.
            int nOrg = (int) nOrigin;
            bool lFromStart = (nOrg & ((int) FS_RELATIVE | (int) FS_END)) == 0;
            if (lFromStart && nOffset < 0)
            {
                FsError(25);            // 'Seek Error'
                return fs.Position;
            }
            long nPos = (nOrg & (int) FS_END) != 0 ? fs.Length
                      : (nOrg & (int) FS_RELATIVE) != 0 ? fs.Position
                      : 0;
            nPos += (long) nOffset;
            if (nPos < 0)
            {
                FsError(131);           // ERROR_NEGATIVE_SEEK
                return fs.Position;
            }
            fs.Position = nPos;
            FsError(0);
            return nPos;
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            FsError(OsError(e));
            return 0;
        }
    }

    public static long FError() => t_fError;

    // DosError( [<nError>] ): the OS error code the last raised error
    // carried, or what a caller stored here. Harbour keeps it on the
    // thread's stack and sets it from the error object.
    [ThreadStatic] static int t_dosError;

    public static long DosError(decimal nError = -1)
    {
        int nPrev = t_dosError;
        if (nError != -1)
            t_dosError = (int) nError;
        return nPrev;
    }

    // FErase( <cFile> ): 0, or -1 (F_ERROR) with FError() set to the OS
    // error hb_fsDelete() leaves: 2 for a file that is not there, 3 for a
    // directory that is not, 5 for a directory or a read-only file. .NET's
    // Delete succeeds on a missing file, so those cases are looked for
    // first. No name is 3.
    public static long FErase(string cFile)
    {
        if (string.IsNullOrEmpty(cFile))
        {
            FsError(3);
            return F_ERROR;
        }
        try
        {
            if (System.IO.Directory.Exists(cFile))
                FsError(5);
            else if (!System.IO.File.Exists(cFile))
                FsError(MissingError(cFile));
            else
            {
                System.IO.File.Delete(cFile);
                FsError(0);
                return 0;
            }
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            FsError(OsError(e));
        }
        return F_ERROR;
    }

    // FRename( <cOldName>, <cNewName> ): 0, or -1 with FError() set.
    // MoveFile() renames a directory too, which .NET splits in two.
    public static long FRename(string cOld, string cNew)
    {
        if (cOld == null || cNew == null)
        {
            FsError(2);
            return F_ERROR;
        }
        try
        {
            if (System.IO.Directory.Exists(cOld))
                System.IO.Directory.Move(cOld, cNew);
            else if (!System.IO.File.Exists(cOld))
            {
                FsError(MissingError(cOld));
                return F_ERROR;
            }
            else
                System.IO.File.Move(cOld, cNew);
            FsError(0);
            return 0;
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            FsError(OsError(e));
            return F_ERROR;
        }
    }

    // ---- Memo files ----

    // hb_fileLoad(): the whole file as bytes, or "" when it cannot be read.
    static string FileLoad(string cFile)
    {
        if (cFile == null)
            return "";
        try
        {
            using var fs = new FileStream(cFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            return HbBytes.Text(ms.GetBuffer(), 0, (int) ms.Length);
        }
        catch (Exception e) when (IsFsFailure(e)) { return ""; }
    }

    // MemoRead() drops a trailing EOF character (Chr( 26 )), the Clipper
    // memo terminator; hb_MemoRead() reads the file as it stands.
    public static string MemoRead(string cFile)
    {
        string cData = FileLoad(cFile);
        return cData.Length > 0 && cData[^1] == (char) 26 ? cData.Substring(0, cData.Length - 1) : cData;
    }

    public static string hb_MemoRead(string cFile) => FileLoad(cFile);

    // MemoWrit() / hb_MemoWrit(): create or truncate, exclusive, write it
    // all. MemoWrit() adds the EOF character when the write succeeded.
    static bool MemoWrite(string cFile, string cData, bool lWriteEof)
    {
        if (cFile == null || cData == null)
            return false;
        try
        {
            using var fs = new FileStream(cFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            fs.Write(HbBytes.Of(cData));
            if (lWriteEof)
                fs.WriteByte(26);
            return true;
        }
        catch (Exception e) when (IsFsFailure(e)) { return false; }
    }

    public static bool MemoWrit(string cFile, string cData) => MemoWrite(cFile, cData, true);

    public static bool hb_MemoWrit(string cFile, string cData) => MemoWrite(cFile, cData, false);

    // ---- Temporary files ----

    // hb_DirTemp(): the temporary directory, with its trailing separator.
    public static string hb_DirTemp()
    {
        try { return Path.GetTempPath(); }
        catch (Exception e) when (IsFsFailure(e)) { return ""; }
    }

    // hb_FTempCreateEx( @<cName>, [<cDir>], [<cPrefix>], [<cExt>],
    // [<nAttr>] ): creates a file nobody else holds, named
    // <cDir><cPrefix>xxxxxx<cExt> with six random base-36 characters, and
    // hands back its handle and its name (hb_fsCreateTempEx()).
    public static long hb_FTempCreateEx(ref string cName, string cDir = null, string cPrefix = null,
                                        string cExt = null, decimal nAttr = 0)
    {
        for (int nAttempt = 0; nAttempt < 99; nAttempt++)
        {
            string cFile = string.IsNullOrEmpty(cDir) ? hb_DirTemp() : hb_DirSepAdd(cDir);
            cFile += cPrefix ?? "";
            for (int i = 0; i < 6; i++)
            {
                int n = Random.Shared.Next(36);
                cFile += (char) (n + (n > 9 ? 'a' - 10 : '0'));
            }
            cFile += cExt ?? "";

            cName = cFile;
            long nHandle = FsOpen(cFile, HB_FO_READWRITE | HB_FO_CREAT | HB_FO_TRUNC |
                                         HB_FO_EXCLUSIVE | HB_FO_EXCL, (int) nAttr);
            if (nHandle != F_ERROR)
                return nHandle;
        }
        return F_ERROR;
    }
}
