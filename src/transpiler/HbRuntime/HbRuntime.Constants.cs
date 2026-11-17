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
    public const long F_ERROR = -1;
    // set.ch: every SET (_SET_CENTURY is HbRuntime's own number for SET CENTURY)
    public const decimal _SET_EXACT = 1;
    public const decimal _SET_FIXED = 2;
    public const decimal _SET_DECIMALS = 3;
    public const decimal _SET_DATEFORMAT = 4;
    public const decimal _SET_EPOCH = 5;
    public const decimal _SET_PATH = 6;
    public const decimal _SET_DEFAULT = 7;
    public const decimal _SET_EXCLUSIVE = 8;
    public const decimal _SET_SOFTSEEK = 9;
    public const decimal _SET_UNIQUE = 10;
    public const decimal _SET_DELETED = 11;
    public const decimal _SET_CANCEL = 12;
    public const decimal _SET_DEBUG = 13;
    public const decimal _SET_TYPEAHEAD = 14;
    public const decimal _SET_COLOR = 15;
    public const decimal _SET_CURSOR = 16;
    public const decimal _SET_CONSOLE = 17;
    public const decimal _SET_ALTERNATE = 18;
    public const decimal _SET_ALTFILE = 19;
    public const decimal _SET_DEVICE = 20;
    public const decimal _SET_EXTRA = 21;
    public const decimal _SET_EXTRAFILE = 22;
    public const decimal _SET_PRINTER = 23;
    public const decimal _SET_PRINTFILE = 24;
    public const decimal _SET_MARGIN = 25;
    public const decimal _SET_BELL = 26;
    public const decimal _SET_CONFIRM = 27;
    public const decimal _SET_ESCAPE = 28;
    public const decimal _SET_INSERT = 29;
    public const decimal _SET_EXIT = 30;
    public const decimal _SET_INTENSITY = 31;
    public const decimal _SET_SCOREBOARD = 32;
    public const decimal _SET_DELIMITERS = 33;
    public const decimal _SET_DELIMCHARS = 34;
    public const decimal _SET_WRAP = 35;
    public const decimal _SET_MESSAGE = 36;
    public const decimal _SET_MCENTER = 37;
    public const decimal _SET_SCROLLBREAK = 38;
    public const decimal _SET_EVENTMASK = 39;
    public const decimal _SET_VIDEOMODE = 40;
    public const decimal _SET_MBLOCKSIZE = 41;
    public const decimal _SET_MFILEEXT = 42;
    public const decimal _SET_STRICTREAD = 43;
    public const decimal _SET_OPTIMIZE = 44;
    public const decimal _SET_AUTOPEN = 45;
    public const decimal _SET_AUTORDER = 46;
    public const decimal _SET_AUTOSHARE = 47;
    public const decimal _SET_CENTURY = 48;
    public const decimal _SET_LANGUAGE = 100;
    public const decimal _SET_IDLEREPEAT = 101;
    public const decimal _SET_FILECASE = 102;
    public const decimal _SET_DIRCASE = 103;
    public const decimal _SET_DIRSEPARATOR = 104;
    public const decimal _SET_EOF = 105;
    public const decimal _SET_HARDCOMMIT = 106;
    public const decimal _SET_FORCEOPT = 107;
    public const decimal _SET_DBFLOCKSCHEME = 108;
    public const decimal _SET_DEFEXTENSIONS = 109;
    public const decimal _SET_EOL = 110;
    public const decimal _SET_TRIMFILENAME = 111;
    public const decimal _SET_HBOUTLOG = 112;
    public const decimal _SET_HBOUTLOGINFO = 113;
    public const decimal _SET_CODEPAGE = 114;
    public const decimal _SET_OSCODEPAGE = 115;
    public const decimal _SET_TIMEFORMAT = 116;
    public const decimal _SET_DBCODEPAGE = 117;
    // common.ch
    public const string CRLF = "\r\n";
}
