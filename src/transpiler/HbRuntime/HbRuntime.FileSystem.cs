using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// The file system: File() and Directory(), the file-name helpers,
// paths, and the current directory and drive.
// One part of HbRuntime; HbRuntime.cs says what the whole is.

public static partial class HbRuntime
{
    // ---- Finding files ----

    // One directory entry, as FindFirstFile() reports it.
    readonly record struct FoundFile(string Name, FileAttributes Attr, long Size, DateTime WriteUtc);

    static int LastDelimiter(string cName)
    {
        for (int i = cName.Length - 1; i >= 0; i--)
            if (cName[i] is '\\' or '/' or ':')
                return i;
        return -1;
    }

    // hb_fsFindFirst() / hb_fsFindNext() (common/hbffind.c): the entries a
    // Windows mask matches — "." and ".." among them — less any hidden,
    // system or directory entry the attribute mask did not ask for.
    static List<FoundFile> FindFiles(string cMask, int nAttrMask)
    {
        var aFound = new List<FoundFile>();
        int nPos = LastDelimiter(cMask);
        string cDir = cMask.Substring(0, nPos + 1);
        string cPattern = cMask.Substring(nPos + 1);

        if (cPattern.Length == 0)
            return aFound;          // FindFirstFile() fails on a bare path

        try
        {
            string cExpr = System.IO.Enumeration.FileSystemName.TranslateWin32Expression(cPattern);
            var aEntries = new System.IO.Enumeration.FileSystemEnumerable<FoundFile>(
                cDir.Length == 0 ? "." : cDir,
                (ref System.IO.Enumeration.FileSystemEntry e) =>
                    new FoundFile(e.FileName.ToString(), e.Attributes,
                                  e.IsDirectory ? 0 : e.Length, e.LastWriteTimeUtc.UtcDateTime),
                new EnumerationOptions
                {
                    AttributesToSkip = 0,
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                    ReturnSpecialDirectories = true,
                })
            {
                ShouldIncludePredicate = (ref System.IO.Enumeration.FileSystemEntry e) =>
                    System.IO.Enumeration.FileSystemName.MatchesWin32Expression(cExpr, e.FileName, ignoreCase: true),
            };

            foreach (FoundFile f in aEntries)
            {
                if ((f.Attr & FileAttributes.Hidden) != 0 && (nAttrMask & HB_FA_HIDDEN) == 0)
                    continue;
                if ((f.Attr & FileAttributes.System) != 0 && (nAttrMask & HB_FA_SYSTEM) == 0)
                    continue;
                if ((f.Attr & FileAttributes.Directory) != 0 && (nAttrMask & HB_FA_DIRECTORY) == 0)
                    continue;
                aFound.Add(f);
            }
        }
        catch (Exception e) when (IsFsFailure(e)) { }

        return aFound;
    }

    // hb_fsFile(): is there a file of this name? It searches with
    // HB_FA_ALL, which is 0 — so hb_fsFindNext() drops every hidden,
    // system and directory entry, and only a plain file answers. A name
    // with wildcards has to be searched for; a plain one is asked about
    // directly, which is what FindFirstFile() amounts to. A name that is
    // only a path ("C:\DIR\") matches nothing.
    static bool FindAny(string cMask)
    {
        string cName = cMask.Substring(LastDelimiter(cMask) + 1);
        if (cName.Length == 0)
            return false;
        if (cName.IndexOf('*') < 0 && cName.IndexOf('?') < 0)
        {
            try
            {
                var fi = new FileInfo(cMask);
                return fi.Exists && (fi.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0;
            }
            catch (Exception e) when (IsFsFailure(e)) { return false; }
        }
        return FindFiles(cMask, HB_FA_ALL).Count > 0;
    }

    static string SetString(decimal nSet) =>
        s_sets.TryGetValue((int) nSet, out dynamic v) && v is string s ? s : "";

    // File( <cFile> ) (rtl/spfiles.c, hb_spFile): the name may hold
    // wildcards. With no path of its own it is looked for under SET
    // DEFAULT first and then along each SET PATH entry.
    public static bool File(string cFile)
    {
        if (cFile == null)
            return false;

        var fn = FNameSplit(cFile);
        if (fn.Path != null)
            return FindAny(FNameMerge(fn.Path, fn.Name, fn.Ext));

        if (FindAny(FNameMerge(SetString(_SET_DEFAULT), fn.Name, fn.Ext)))
            return true;

        foreach (string cDir in SetString(_SET_PATH).Split(';'))
            if (FindAny(FNameMerge(cDir, fn.Name, fn.Ext)))
                return true;

        return false;
    }

    public static bool hb_FileExists(string cFile)
    {
        try { return cFile != null && System.IO.File.Exists(cFile); }
        catch (Exception e) when (IsFsFailure(e)) { return false; }
    }

    public static bool hb_DirExists(string cDir)
    {
        try { return cDir != null && System.IO.Directory.Exists(cDir); }
        catch (Exception e) when (IsFsFailure(e)) { return false; }
    }

    // hb_vfExists() / hb_vfErase() / hb_vfSize(): the virtual file system's
    // names for the three, on a real file. They set FError() as the F*
    // functions do.
    public static bool hb_vfExists(string cFile)
    {
        if (cFile == null) { t_fError = 2; return false; }
        bool lFound = hb_FileExists(cFile);
        t_fError = t_ioError;
        return lFound;
    }

    public static decimal hb_vfErase(string cFile) => FErase(cFile);

    public static decimal hb_vfSize(string cFile, bool lUseDirEntry = true)
    {
        if (cFile == null)
            return 0;
        try
        {
            var fi = new FileInfo(cFile);
            if (fi.Exists)
            {
                FsError(0);
                return fi.Length;
            }
            FsError(System.IO.Directory.Exists(cFile) ? 0 : MissingError(cFile));
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            FsError(OsError(e));
        }
        return 0;
    }

    // ---- Directory() ----

    // hb_fsAttrEncode() (common/hbffind.c)
    static int AttrEncode(string cAttr)
    {
        int nAttr = 0;
        foreach (char c in cAttr ?? "")
            nAttr |= char.ToUpperInvariant(c) switch
            {
                'R' => HB_FA_READONLY,
                'H' => HB_FA_HIDDEN,
                'S' => HB_FA_SYSTEM,
                'A' => HB_FA_ARCHIVE,
                'D' => HB_FA_DIRECTORY,
                'V' => HB_FA_LABEL,
                _ => 0,
            };
        return nAttr;
    }

    // hb_fsAttrDecode(), in Clipper's order. A Windows entry never carries
    // the volume label or link bits Harbour would write as V and L.
    static string AttrDecode(FileAttributes nAttr)
    {
        var cAttr = new System.Text.StringBuilder(5);
        if ((nAttr & FileAttributes.ReadOnly) != 0) cAttr.Append('R');
        if ((nAttr & FileAttributes.Hidden) != 0) cAttr.Append('H');
        if ((nAttr & FileAttributes.System) != 0) cAttr.Append('S');
        if ((nAttr & FileAttributes.Archive) != 0) cAttr.Append('A');
        if ((nAttr & FileAttributes.Directory) != 0) cAttr.Append('D');
        return cAttr.ToString();
    }

    // Directory( [<cDirSpec>], [<cAttr>] ) (rtl/direct.c): one
    // { name, size, date, "hh:mm:ss", attributes } per entry. Normal and
    // read-only files always; hidden, system and directories only when
    // <cAttr> asks for them, and "D" brings "." and ".." with it. A spec
    // ending in a path or drive separator gets "*.*" added, as Clipper's
    // did.
    public static dynamic[] Directory(string cDirSpec = null, string cAttr = null)
    {
        int nMask = HB_FA_ARCHIVE | HB_FA_READONLY | AttrEncode(cAttr);

        if (string.IsNullOrEmpty(cDirSpec))
            cDirSpec = "*.*";
        else if (cDirSpec[^1] is '\\' or '/' or ':')
            cDirSpec += "*.*";

        var aDir = new List<dynamic>();
        foreach (FoundFile f in FindFiles(cDirSpec, nMask))
        {
            DateTime tWrite = f.WriteUtc.ToLocalTime();
            aDir.Add(new dynamic[]
            {
                f.Name,
                (decimal) f.Size,
                DateOnly.FromDateTime(tWrite),
                tWrite.ToString("HH:mm:ss", INV),
                AttrDecode(f.Attr),
            });
        }
        return aDir.ToArray();
    }

    // hb_strMatchWildRaw() (common/strwild.c) as hb_strMatchFile() calls it
    // on Windows: the whole name, case-insensitive, and a pattern ending in
    // "." or ".*" also matches a name with no extension.
    static bool FileMatch(string cString, string cPattern)
    {
        bool fMatch = true, fAny = false;
        var anyP = new List<int>();
        var anyV = new List<int>();
        int nPosP = 0, nPosV = 0;
        int nLen = cString.Length, nSize = cPattern.Length;

        while (nPosP < nSize || (!fAny && nPosV < nLen))
        {
            if (nPosP < nSize && cPattern[nPosP] == '*')
            {
                fAny = true;
                nPosP++;
            }
            else if (nPosV < nLen && nPosP < nSize &&
                     (cPattern[nPosP] == '?' || AsciiUpper(cPattern[nPosP]) == AsciiUpper(cString[nPosV])))
            {
                if (fAny)
                {
                    anyP.Add(nPosP);
                    anyV.Add(nPosV);
                    fAny = false;
                }
                nPosV++;
                nPosP++;
            }
            else if (nPosV == nLen && nPosP < nSize && cPattern[nPosP] == '.' &&
                     (nPosP + 1 == nSize || (nPosP + 2 == nSize && cPattern[nPosP + 1] == '*')))
                break;
            else if (fAny && nPosV < nLen)
                nPosV++;
            else if (anyP.Count > 0)
            {
                int n = anyP.Count - 1;
                nPosP = anyP[n];
                nPosV = anyV[n] + 1;
                anyP.RemoveAt(n);
                anyV.RemoveAt(n);
                fAny = true;
            }
            else
            {
                fMatch = false;
                break;
            }
        }
        return fMatch;
    }

    static char AsciiUpper(char c) => c >= 'a' && c <= 'z' ? (char) (c - 32) : c;

    public static bool hb_FileMatch(string cFile, string cMask) =>
        cFile != null && cMask != null && FileMatch(cFile, cMask);

    // hb_DirScan( [<cPath>], [<cFileMask>], [<cAttr>] ) (rtl/dirscan.prg):
    // Directory()'s entries for the whole tree, each name carrying the path
    // below <cPath> it was found at.
    public static dynamic[] hb_DirScan(string cPath = null, string cFileMask = null, string cAttr = null) =>
        DirScan(hb_DirSepAdd(cPath ?? ""), cFileMask ?? "*.*", cAttr ?? "").ToArray();

    static List<dynamic> DirScan(string cPath, string cMask, string cAttr)
    {
        var aResult = new List<dynamic>();

        foreach (dynamic[] aFile in Directory(cPath + "*.*", cAttr + "D"))
        {
            string cName = (string) aFile[0];
            bool lMatch = FileMatch(cName, cMask);

            if (((string) aFile[4]).Contains('D'))
            {
                if (lMatch && cAttr.Contains('D'))
                    aResult.Add(aFile);
                if (cName != "." && cName != ".." && cName != "")
                    foreach (dynamic[] aSub in DirScan(cPath + cName + "\\", cMask, cAttr))
                    {
                        aSub[0] = cName + "\\" + (string) aSub[0];
                        aResult.Add(aSub);
                    }
            }
            else if (lMatch)
                aResult.Add(aFile);
        }
        return aResult;
    }

    // ---- File names ----

    // What hb_fsFNameSplit() takes a name apart into. A part Harbour leaves
    // NULL is null here, which is what tells hb_spFile() that a name has no
    // path of its own; the Harbour-level functions store "" for it.
    readonly record struct FName(string Path, string Name, string Ext, string Drive);

    // hb_fsFNameSplit() (common/hbfsapi.c): the path is everything up to
    // and including the last "\", "/" or ":"; the extension starts at the
    // last "." of what is left, unless that dot is its first character.
    static FName FNameSplit(string cFileName)
    {
        if (cFileName == null)
            return new FName(null, null, null, null);

        string cPath = null, cDrive = null;
        int nPos = LastDelimiter(cFileName);
        if (nPos >= 0)
        {
            cPath = cFileName.Substring(0, nPos + 1);
            cFileName = cFileName.Substring(nPos + 1);
            int nColon = cPath.IndexOf(':');
            if (nColon >= 0)
                cDrive = cPath.Substring(0, nColon);
        }

        string cName = cFileName, cExt = null;
        int nDot = cFileName.LastIndexOf('.');
        if (nDot > 0)
        {
            cExt = cFileName.Substring(nDot);
            cName = cFileName.Substring(0, nDot);
        }
        if (cName.Length == 0)
            cName = null;

        return new FName(cPath, cName, cExt, cDrive);
    }

    // hb_fsFNameMerge(): path, then name, then extension, with a path
    // separator put in if the path lacks one and a dot if the extension
    // lacks one. A leading separator on the name is dropped.
    static string FNameMerge(string cPath, string cName, string cExt)
    {
        var cFileName = new System.Text.StringBuilder();

        if (cName != null && cName.Length > 0 && (cName[0] is '\\' or '/' or ':'))
            cName = cName.Substring(1);

        if (cPath != null)
            cFileName.Append(cPath);

        if (cFileName.Length > 0 && (cName != null || cExt != null) &&
            !(cFileName[^1] is '\\' or '/' or ':'))
            cFileName.Append('\\');

        if (cName != null)
            cFileName.Append(cName);

        if (cExt != null)
        {
            if (cExt.Length > 0 && cExt[0] != '.')
                cFileName.Append('.');
            cFileName.Append(cExt);
        }

        return cFileName.ToString();
    }

    // hb_FNameSplit( <cFileName>, @<cPath>, @<cName>, @<cExt>, @<cDrive> ):
    // each part that is not there comes back as "". The gaps EasiPOS leaves
    // are the overload with plain arguments — Harbour only writes back
    // what was passed by reference.
    public static void hb_FNameSplit(string cFileName, ref string cPath, ref string cName, ref string cExt)
    {
        var fn = FNameSplit(cFileName ?? "");
        cPath = fn.Path ?? "";
        cName = fn.Name ?? "";
        cExt = fn.Ext ?? "";
    }

    public static void hb_FNameSplit(string cFileName, ref string cPath, ref string cName, ref string cExt, ref string cDrive)
    {
        var fn = FNameSplit(cFileName ?? "");
        cPath = fn.Path ?? "";
        cName = fn.Name ?? "";
        cExt = fn.Ext ?? "";
        cDrive = fn.Drive ?? "";
    }

    public static void hb_FNameSplit(string cFileName, object _1, object _2, ref string cExt)
    {
        cExt = FNameSplit(cFileName ?? "").Ext ?? "";
    }

    public static string hb_FNameMerge(string cPath = null, string cName = null, string cExt = null) =>
        FNameMerge(cPath, cName, cExt);

    public static string hb_FNameDir(string cFileName) => FNameSplit(cFileName ?? "").Path ?? "";

    public static string hb_FNameName(string cFileName) => FNameSplit(cFileName ?? "").Name ?? "";

    public static string hb_FNameExt(string cFileName) => FNameSplit(cFileName ?? "").Ext ?? "";

    public static string hb_FNameNameExt(string cFileName)
    {
        var fn = FNameSplit(cFileName ?? "");
        return FNameMerge(null, fn.Name, fn.Ext);
    }

    public static string hb_FNameExtSet(string cFileName, string cExt = null)
    {
        var fn = FNameSplit(cFileName ?? "");
        return FNameMerge(fn.Path, fn.Name, cExt);
    }

    public static string hb_FNameExtSetDef(string cFileName, string cExt = null)
    {
        var fn = FNameSplit(cFileName ?? "");
        return FNameMerge(fn.Path, fn.Name, fn.Ext ?? cExt);
    }

    // ---- Paths and directories ----

    public static string hb_ps() => "\\";

    public static string hb_osDriveSeparator() => ":";

    public static string hb_osFileMask() => "*.*";

    static bool IsDriveSpec(string cDir) => cDir.EndsWith(':');

    // hb_DirSepAdd() (rtl/hbfilehi.prg)
    public static string hb_DirSepAdd(string cDir)
    {
        if (cDir == null)
            return "";
        if (!Empty(cDir) && !IsDriveSpec(cDir) && !cDir.EndsWith('\\'))
            cDir += "\\";
        return cDir;
    }

    // hb_DirSepDel()
    public static string hb_DirSepDel(string cDir)
    {
        if (cDir == null)
            return "";
        while (cDir.Length > 1 && cDir.EndsWith('\\') && cDir != "\\\\" && !cDir.EndsWith(":\\"))
            cDir = cDir.Substring(0, cDir.Length - 1);
        return cDir;
    }

    // hb_PathNormalize(): takes out every "." and empty step, and every
    // step followed by "..", leaving a path that means the same thing.
    public static string hb_PathNormalize(string cPath)
    {
        if (cPath == null)
            return "";
        if (Empty(cPath))
            return cPath;

        var aDir = new List<string>(cPath.Split('\\'));

        for (int i = aDir.Count - 1; i >= 0; i--)
        {
            string cDir = aDir[i];
            bool lLast = i == aDir.Count - 1;

            if (cDir == "." ||
                (Empty(cDir) && !lLast && (i + 1 > 2 || (i + 1 == 2 && !Empty(aDir[0])))))
                aDir.RemoveAt(i);
            else if (cDir != ".." && !Empty(cDir) && !IsDriveSpec(cDir))
            {
                if (!lLast && aDir[i + 1] == "..")
                {
                    aDir.RemoveAt(i + 1);
                    aDir.RemoveAt(i);
                }
            }
        }

        cPath = string.Join("\\", aDir);
        return cPath.Length == 0 ? ".\\" : cPath;
    }

    // hb_DirBuild(): create every directory the path names that is not
    // there yet. .F. as soon as one cannot be created, or a file stands
    // where a directory should.
    public static bool hb_DirBuild(string cDir)
    {
        if (cDir == null)
            return false;

        cDir = hb_PathNormalize(cDir);

        if (!hb_DirExists(cDir))
        {
            cDir = hb_DirSepAdd(cDir);

            string cDirTemp;
            int nPos = cDir.IndexOf(':');
            if (nPos >= 0)
            {
                cDirTemp = cDir.Substring(0, nPos + 1);
                cDir = cDir.Substring(nPos + 1);
            }
            else if (cDir.StartsWith("\\\\"))    // UNC path, network share
            {
                int nShare = cDir.IndexOf('\\', 2);
                cDirTemp = nShare < 0 ? "" : cDir.Substring(0, nShare + 1);
                cDir = cDir.Substring(cDirTemp.Length);
            }
            else if (cDir.StartsWith("\\"))
            {
                cDirTemp = "\\";
                cDir = cDir.Substring(1);
            }
            else
                cDirTemp = "";

            foreach (string cDirItem in cDir.Split('\\'))
            {
                if (!cDirTemp.EndsWith('\\') && cDirTemp != "")
                    cDirTemp += "\\";
                if (cDirItem != "")      // skip the root path, if any
                {
                    cDirTemp += cDirItem;
                    if (hb_FileExists(cDirTemp))
                        return false;
                    if (!hb_DirExists(cDirTemp) && MakeDir(cDirTemp) != 0)
                        return false;
                }
            }
        }

        return true;
    }

    // MakeDir() / hb_DirCreate(): 0, or the OS error. CreateDirectory()
    // fails on a name that is taken (ERROR_ALREADY_EXISTS, which Harbour
    // reports as 5) and on a missing parent, where .NET would build the
    // whole chain instead.
    public static decimal MakeDir(string cDir)
    {
        if (cDir == null)
            return F_ERROR;
        if (cDir.Length == 0)
            return 3;
        try
        {
            if (System.IO.File.Exists(cDir) || System.IO.Directory.Exists(cDir))
                return t_ioError = 5;
            string cParent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(cDir)));
            if (cParent != null && !System.IO.Directory.Exists(cParent))
                return t_ioError = 3;
            System.IO.Directory.CreateDirectory(cDir);
            return t_ioError = 0;
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            return t_ioError = OsError(e);
        }
    }

    // DirRemove() / hb_DirDelete()
    public static decimal DirRemove(string cDir)
    {
        if (cDir == null)
            return F_ERROR;
        try
        {
            if (!System.IO.Directory.Exists(cDir))
                return t_ioError = MissingError(cDir);
            System.IO.Directory.Delete(cDir);
            return t_ioError = 0;
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            return t_ioError = OsError(e);
        }
    }

    // DirChange(): 0, or the OS error.
    public static decimal DirChange(string cDir)
    {
        if (cDir == null)
            return F_ERROR;
        try
        {
            System.IO.Directory.SetCurrentDirectory(cDir);
            return t_ioError = 0;
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            // .NET reports both missing steps as DirectoryNotFound, where
            // Windows says 2 for the last one and 3 for any before it.
            return t_ioError = e is DirectoryNotFoundException or FileNotFoundException
                             ? MissingError(cDir) : OsError(e);
        }
    }

    // CurDir( [<cDrive>] ): the current directory of that drive (the
    // current one by default), without the drive, without the leading
    // separator and without the trailing one.
    public static string CurDir(string cDrive = null)
    {
        string cDir;
        try
        {
            cDir = cDrive != null && cDrive.Length > 0 && char.IsLetter(cDrive[0])
                 ? Path.GetFullPath(cDrive.Substring(0, 1) + ":")
                 : System.IO.Directory.GetCurrentDirectory();
        }
        catch (Exception e) when (IsFsFailure(e)) { return ""; }

        if (cDir.Length > 1 && cDir[1] == ':')
            cDir = cDir.Substring(2);
        if (cDir.Length > 0 && (cDir[0] is '\\' or '/'))
            cDir = cDir.Substring(1);
        if (cDir.Length > 0 && (cDir[^1] is '\\' or '/'))
            cDir = cDir.Substring(0, cDir.Length - 1);
        return cDir;
    }

    // hb_cwd( [<cNewDir>] ): the working directory, with its trailing
    // separator, and changes it when given one.
    public static string hb_cwd(string cNewDir = null)
    {
        string cDir;
        try { cDir = System.IO.Directory.GetCurrentDirectory(); }
        catch (Exception e) when (IsFsFailure(e)) { cDir = ""; }

        if (cDir.Length > 0 && !(cDir[^1] is '\\' or '/' or ':'))
            cDir += "\\";

        if (cNewDir != null)
            DirChange(cNewDir);
        t_fError = t_ioError;
        return cDir;
    }

    // hb_CurDrive(): the current drive's letter, without the colon.
    public static string hb_CurDrive()
    {
        try
        {
            string cDir = System.IO.Directory.GetCurrentDirectory();
            return cDir.Length > 1 && cDir[1] == ':' ? cDir.Substring(0, 1).ToUpperInvariant() : "";
        }
        catch (Exception e) when (IsFsFailure(e)) { return ""; }
    }

    // hb_DirBase(): the directory the running program is in, with its
    // trailing separator (hb_fsBaseDirBuff(), the path of argv[ 0 ]).
    public static string hb_DirBase()
    {
        string cExe = Environment.ProcessPath;
        if (cExe == null)
            return AppContext.BaseDirectory;
        var fn = FNameSplit(cExe);
        return fn.Path ?? "";
    }

    // DiskSpace( [<nDrive>] ): the free space the caller may use, in
    // bytes. 0 for a drive that is not there (where Harbour raises).
    public static decimal DiskSpace(decimal nDrive = 0)
    {
        if (nDrive < 0)
            return 0;
        try
        {
            string cRoot = nDrive == 0
                ? Path.GetPathRoot(System.IO.Directory.GetCurrentDirectory())
                : (char) ('A' + (int) nDrive - 1) + ":\\";
            return new DriveInfo(cRoot).AvailableFreeSpace;
        }
        catch (Exception e) when (IsFsFailure(e) || e is System.Runtime.InteropServices.COMException) { return 0; }
    }
}
