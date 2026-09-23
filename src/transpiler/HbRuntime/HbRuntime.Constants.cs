using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// The Harbour header constants the emitted code refers to (fileio.ch,
// directry.ch, set.ch, common.ch).
// One part of HbRuntime; HbRuntime.cs says what the whole is.

public static partial class HbRuntime
{
    // ---- Harbour header constants ----
    // fileio.ch
    public const decimal FC_NORMAL = 0;
    public const decimal FC_READONLY = 1;
    public const decimal FC_HIDDEN = 2;
    public const decimal FC_SYSTEM = 4;
    public const decimal FO_READ = 0;
    public const decimal FO_WRITE = 1;
    public const decimal FO_READWRITE = 2;
    public const decimal FO_COMPAT = 0;
    public const decimal FO_EXCLUSIVE = 16;
    public const decimal FO_DENYWRITE = 32;
    public const decimal FO_DENYREAD = 48;
    public const decimal FO_DENYNONE = 64;
    public const decimal FO_SHARED = 64;
    public const decimal FS_SET = 0;
    public const decimal FS_RELATIVE = 1;
    public const decimal FS_END = 2;
    // directry.ch
    public const decimal F_NAME = 1;
    public const decimal F_SIZE = 2;
    public const decimal F_DATE = 3;
    public const decimal F_TIME = 4;
    public const decimal F_ATTR = 5;
    public const decimal F_LEN = 5;
    public const decimal F_ERROR = -1;
    // set.ch — only the ones actually used so far
    public const decimal _SET_EXACT = 1;
    public const decimal _SET_FIXED = 2;
    public const decimal _SET_DECIMALS = 3;
    public const decimal _SET_DATEFORMAT = 4;
    public const decimal _SET_EPOCH = 5;
    public const decimal _SET_PATH = 6;
    public const decimal _SET_DEFAULT = 7;
    public const decimal _SET_CENTURY = 48;
    public const decimal _SET_EOL = 110;
    public const decimal _SET_TIMEFORMAT = 116;
    // common.ch
    public const string CRLF = "\r\n";
}
