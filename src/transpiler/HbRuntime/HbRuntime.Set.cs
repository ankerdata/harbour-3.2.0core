using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// Set(): every SET Harbour has, with its default and its type, and SET
// CENTURY.
// One part of HbRuntime; HbRuntime.cs says what the whole is.

public static partial class HbRuntime
{
    // ---- Set() ----
    // Harbour's Set( _SET_XXX [, newVal] ) reads/writes a setting. Keyed on
    // the numeric _SET_* identifier. Returns the prior value.
    //
    // Every SET Harbour has, with its default (vm/set.c, hb_setInitialize,
    // and the values HB_FUNC( SET ) reports for the few it computes). The
    // default also fixes each SET's type, which is all Set() will take:
    // a logical SET takes .T. / .F. or "ON" / "OFF", a numeric one a
    // number, a string one a string, and anything else leaves the setting
    // as it was (set_logical(), set_number(), set_string()). Harbour keeps
    // them per thread, copied from the parent's when a thread starts; here
    // they are one set for the process until threads come (family 10).

    static readonly Dictionary<int, dynamic> s_sets = new()
    {
        [1]   = false,              // _SET_EXACT
        [2]   = false,              // _SET_FIXED
        [3]   = 2m,                 // _SET_DECIMALS
        [4]   = "mm/dd/yy",         // _SET_DATEFORMAT, century off
        [5]   = 1900m,              // _SET_EPOCH
        [6]   = "",                 // _SET_PATH
        [7]   = "",                 // _SET_DEFAULT
        [8]   = true,               // _SET_EXCLUSIVE
        [9]   = false,              // _SET_SOFTSEEK
        [10]  = false,              // _SET_UNIQUE
        [11]  = false,              // _SET_DELETED
        [12]  = true,               // _SET_CANCEL
        [13]  = false,              // _SET_DEBUG
        [14]  = 50m,                // _SET_TYPEAHEAD
        [15]  = "W/N,N/W,N,N,N/W",  // _SET_COLOR
        [16]  = 1m,                 // _SET_CURSOR, SC_NORMAL
        [17]  = true,               // _SET_CONSOLE
        [18]  = false,              // _SET_ALTERNATE
        [19]  = "",                 // _SET_ALTFILE
        [20]  = "SCREEN",           // _SET_DEVICE
        [21]  = false,              // _SET_EXTRA
        [22]  = "",                 // _SET_EXTRAFILE
        [23]  = false,              // _SET_PRINTER
        [24]  = "PRN",              // _SET_PRINTFILE
        [25]  = 0m,                 // _SET_MARGIN
        [26]  = false,              // _SET_BELL
        [27]  = false,              // _SET_CONFIRM
        [28]  = true,               // _SET_ESCAPE
        [29]  = false,              // _SET_INSERT
        [30]  = false,              // _SET_EXIT
        [31]  = true,               // _SET_INTENSITY
        [32]  = true,               // _SET_SCOREBOARD
        [33]  = false,              // _SET_DELIMITERS
        [34]  = "::",               // _SET_DELIMCHARS
        [35]  = false,              // _SET_WRAP
        [36]  = 0m,                 // _SET_MESSAGE
        [37]  = false,              // _SET_MCENTER
        [38]  = true,               // _SET_SCROLLBREAK
        [39]  = 128m,               // _SET_EVENTMASK, INKEY_KEYBOARD
        [40]  = 0m,                 // _SET_VIDEOMODE
        [41]  = 64m,                // _SET_MBLOCKSIZE
        [42]  = "",                 // _SET_MFILEEXT
        [43]  = false,              // _SET_STRICTREAD
        [44]  = true,               // _SET_OPTIMIZE
        [45]  = true,               // _SET_AUTOPEN
        [46]  = 0m,                 // _SET_AUTORDER
        [47]  = 0m,                 // _SET_AUTOSHARE
        [48]  = false,              // HbRuntime's own: SET CENTURY
        [100] = "EN",               // _SET_LANGUAGE
        [101] = true,               // _SET_IDLEREPEAT
        [102] = 0m,                 // _SET_FILECASE, mixed
        [103] = 0m,                 // _SET_DIRCASE, mixed
        [104] = "\\",               // _SET_DIRSEPARATOR
        [105] = true,               // _SET_EOF
        [106] = true,               // _SET_HARDCOMMIT
        [107] = false,              // _SET_FORCEOPT
        [108] = 0m,                 // _SET_DBFLOCKSCHEME
        [109] = true,               // _SET_DEFEXTENSIONS
        [110] = "\r\n",             // _SET_EOL
        [111] = false,              // _SET_TRIMFILENAME
        [112] = "hb_out.log",       // _SET_HBOUTLOG
        [113] = "",                 // _SET_HBOUTLOGINFO
        [114] = "EN",               // _SET_CODEPAGE
        [115] = "",                 // _SET_OSCODEPAGE
        [116] = "hh:mm:ss.fff",     // _SET_TIMEFORMAT
        [117] = "",                 // _SET_DBCODEPAGE
    };

    public static dynamic Set(decimal nSet, dynamic newVal = null)
    {
        int key = (int)nSet;
        if (!s_sets.TryGetValue(key, out dynamic prev))
            return null;                        // not a SET Harbour has
        if (newVal is not null)
        {
            object xNew = newVal;
            if (prev is bool)
                s_sets[key] = AsOnOff(xNew, (bool) prev);
            else if (prev is string)
            {
                if (xNew is string cNew)
                    s_sets[key] = cNew;
            }
            else if (IsNumeric(xNew))
                s_sets[key] = Convert.ToDecimal(xNew, INV);
            // A date format turns century on when its (first) year has
            // four digits, off otherwise (vm/set.c)
            if (key == (int)_SET_DATEFORMAT && xNew is string cFormat)
            {
                var m = System.Text.RegularExpressions.Regex.Match(cFormat, "[Yy]+");
                s_sets[(int)_SET_CENTURY] = m.Success && m.Length >= 4;
            }
        }
        return prev;
    }

    // set_logical() (vm/set.c): a logical as it is; a string starting "ON"
    // or "OFF", in any case, as .T. / .F. — the #command rules for
    // SET ... ON / OFF hand Set() the keyword as a string — and anything
    // else leaves the old value.
    static bool AsOnOff(dynamic val, bool fallback = false)
    {
        if (val is bool b)
            return b;
        if (val is string s)
        {
            if (s.StartsWith("ON", StringComparison.OrdinalIgnoreCase))
                return true;
            if (s.StartsWith("OFF", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return fallback;
    }

    // SET CENTURY: turning it on or off rewrites the date format's year
    // as YYYY or YY (vm/set.c, hb_setSetCentury — which also upper-cases
    // the format).
    public static bool __SetCentury(dynamic val = null)
    {
        bool prev = s_sets[(int)_SET_CENTURY] is bool pb && pb;
        if (val is null)
            return prev;
        bool fCentury = AsOnOff(val);
        s_sets[(int)_SET_CENTURY] = fCentury;
        if (fCentury != prev)
        {
            string cFormat = SetDateFormat().ToUpperInvariant();
            int iStart = cFormat.IndexOf('Y');
            int iStop = iStart < 0 ? 0 : iStart;
            while (iStart >= 0 && iStop < cFormat.Length && cFormat[iStop] == 'Y')
                iStop++;
            if (iStart < 0)
                iStart = 0;
            s_sets[(int)_SET_DATEFORMAT] = cFormat.Substring(0, iStart) +
                (fCentury ? "YYYY" : "YY") + cFormat.Substring(iStop);
        }
        return prev;
    }
}
