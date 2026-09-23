using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

// Family 12: the serial ports, as rtl/hbcom.c has them on Windows.
//
// EasiPOS reaches them through one class, shared/hbcom.prg, which the
// customer display, the scale, the card terminals and the serial printers
// use. Harbour's Windows build talks to the Win32 API, so this does too:
// .NET's SerialPort is a package of its own and cannot say DTR and RTS the
// way hbcom.prg asks. The port numbers, the flags and the error codes are
// Harbour's (include/hbcom.ch).
//
// What Windows cannot do, Harbour does not do either, and neither does
// this: hb_comMCR() cannot READ the modem control lines there — it answers
// 0 and fails unless it was asked to set or clear one — and the DELTA bits
// of hb_comMSR() are never set. hbcom.prg's IsRTS() / IsDTR() therefore
// report failure on Windows, in 9.0 as here.
public static partial class HbRuntime
{
    // ---- the port table ----
    //
    // Harbour numbers ports from 1 and gives each a device name, COM<n> by
    // default; hb_comFindPort() answers the number for a name and, asked
    // to, gives an unknown name a number of its own.

    sealed class HbComPort
    {
        public string cDevice = "";
        public bool fNamed;     // given a name by hb_comFindPort(), not the default

        public IntPtr hComm = ComInvalidHandle;
        public int nError;      // HB_COM_ERR_*, what hb_comGetError() answers
        public int nOsError;    // GetLastError(), hb_comGetOSError()
        public bool IsOpen => hComm != ComInvalidHandle;
    }

    static readonly Dictionary<int, HbComPort> s_com = new();
    static readonly object s_comLock = new();

    // hbcom.ch's error codes
    public const decimal HB_COM_ERR_WRONGPORT = 1;
    public const decimal HB_COM_ERR_CLOSED = 2;
    public const decimal HB_COM_ERR_TIMEOUT = 3;
    public const decimal HB_COM_ERR_NOSUPPORT = 4;
    public const decimal HB_COM_ERR_PARAMVALUE = 5;
    public const decimal HB_COM_ERR_BUSY = 6;
    public const decimal HB_COM_ERR_OTHER = 7;
    public const decimal HB_COM_ERR_ALREADYOPEN = 8;

    // hb_comFlush() types, and the modem lines of hbcom.ch
    const int ComIFlush = 1, ComOFlush = 2, ComIOFlush = 3;
    const int ComMcrDtr = 0x01, ComMcrRts = 0x02;
    const int ComMsrCts = 0x10, ComMsrDsr = 0x20, ComMsrRi = 0x40, ComMsrDcd = 0x80;
    const int ComFlowIRtsCts = 0x01, ComFlowORtsCts = 0x02;
    const int ComFlowIDtrDsr = 0x04, ComFlowODtrDsr = 0x08;
    const int ComFlowXoff = 0x20, ComFlowXon = 0x40;

    // The highest port number a name can be given, as Harbour's table has
    // a size (HB_COM_PORT_MAX); past it hb_comFindPort() answers 0.
    const int ComPortMax = 256;

    static HbComPort? ComPort(object? xPort, bool fMustBeOpen)
    {
        int nPort = xPort != null && IsNumeric(xPort) ? (int) Convert.ToInt64(xPort, INV) : 0;
        lock (s_comLock)
        {
            if (nPort < 1 || nPort > ComPortMax)
                return null;
            if (!s_com.TryGetValue(nPort, out HbComPort? pCom))
            {
                // A number that was never named is the default device, as
                // Harbour's pre-filled table has it.
                pCom = new HbComPort { cDevice = "COM" + nPort.ToString(INV) };
                s_com[nPort] = pCom;
            }
            if (fMustBeOpen && !pCom.IsOpen)
            {
                pCom.nError = (int) HB_COM_ERR_CLOSED;
                return null;
            }
            return pCom;
        }
    }

    // A call on a port number that is not one: Harbour sets the error on
    // the port it could not find, and there is none, so the last port
    // asked for keeps it. Here the number itself answers.
    static bool ComWrongPort(object? xPort)
    {
        int nPort = xPort != null && IsNumeric(xPort) ? (int) Convert.ToInt64(xPort, INV) : 0;
        return nPort < 1 || nPort > ComPortMax;
    }

    static void ComSetOsError(HbComPort pCom, bool fError)
    {
        pCom.nOsError = fError ? Marshal.GetLastWin32Error() : 0;
        if (!fError)
            pCom.nError = 0;
    }

    // ---- the functions hbcom.prg calls ----

    // hb_comFindPort( <cDevice>, [<lCreate>] ): the port that carries that
    // device name, or 0 — and, asked to create, a port number given the
    // name.
    //
    // A name never becomes its own number on Windows, so "COM1" is not
    // port 1: hb_comGetPortNum() reads the digits with
    // `iPort = iPort * ( 10 + *pszName++ - '0' )`, which multiplies zero by
    // something for ever and answers 0 (an upstream slip in rtl/hbcom.c —
    // upstream takes no patches, and 9.0 runs on that behaviour, so this
    // keeps it). What is left is the table scan: a name matches a port that
    // was given it, and hb_comFindPort( ..., .T. ) takes the highest free
    // port above 16, counting down. EasiPOS hands the number straight back
    // to hb_comOpen(), so only the name matters.
    public static decimal hb_comFindPort(string? cDevice, bool lCreate = false)
    {
        if (string.IsNullOrEmpty(cDevice))
            return 0;
        lock (s_comLock)
        {
            foreach (var kv in s_com)
                if (kv.Value.fNamed &&
                    string.Equals(kv.Value.cDevice, cDevice, StringComparison.OrdinalIgnoreCase))
                    return kv.Key;
            if (!lCreate)
                return 0;
            for (int n = ComPortMax; n > 16; n--)
                if (!s_com.TryGetValue(n, out HbComPort? pFree) || !pFree.fNamed)
                {
                    s_com[n] = new HbComPort { cDevice = cDevice, fNamed = true };
                    return n;
                }
            // Every name taken: Harbour reuses the highest port that is not open.
            for (int n = ComPortMax; n > 0; n--)
                if (!s_com[n].IsOpen)
                {
                    s_com[n] = new HbComPort { cDevice = cDevice, fNamed = true };
                    return n;
                }
            return 0;
        }
    }

    // hb_comOpen( <nPort> ): open it. Exclusive, unbuffered, as Harbour's
    // CreateFile() call is.
    public static bool hb_comOpen(object? xPort)
    {
        HbComPort? pCom = ComPort(xPort, false);
        if (pCom == null)
            return false;
        if (pCom.IsOpen)
        {
            pCom.nError = (int) HB_COM_ERR_ALREADYOPEN;
            return false;
        }
        string cName = pCom.cDevice.StartsWith(@"\\", StringComparison.Ordinal)
            ? pCom.cDevice : @"\\.\" + pCom.cDevice;
        IntPtr h = CreateFileW(cName, GenericRead | GenericWrite, 0, IntPtr.Zero,
                               OpenExisting, FileFlagNoBuffering, IntPtr.Zero);
        if (h == ComInvalidHandle)
        {
            ComSetOsError(pCom, true);
            return false;
        }
        pCom.hComm = h;
        ComSetOsError(pCom, false);
        return true;
    }

    // hb_comClose( <nPort> )
    public static bool hb_comClose(object? xPort)
    {
        HbComPort? pCom = ComPort(xPort, true);
        if (pCom == null)
            return false;
        bool fResult = CloseHandle(pCom.hComm);
        pCom.hComm = ComInvalidHandle;
        ComSetOsError(pCom, !fResult);
        return fResult;
    }

    // hb_comInit( <nPort>, <nBaud>, <cParity>, <nSize>, <nStop> ): the line
    // settings, each one left as it was when 0 is given. The DCB is set the
    // way hb_comInit() sets it: binary, no flow control of its own, DTR and
    // RTS enabled.
    public static bool hb_comInit(object? xPort, decimal nBaud = 0, string? cParity = null,
                                  decimal nSize = 0, decimal nStop = 0)
    {
        HbComPort? pCom = ComPort(xPort, true);
        if (pCom == null)
            return false;

        var dcb = new DCB { DCBlength = (uint) Marshal.SizeOf<DCB>() };
        bool fResult = GetCommState(pCom.hComm, ref dcb);
        if (fResult)
        {
            byte bParity = dcb.Parity;
            switch (char.ToUpperInvariant(string.IsNullOrEmpty(cParity) ? '\0' : cParity[0]))
            {
                case 'N': bParity = NoParity; break;
                case 'E': bParity = EvenParity; break;
                case 'O': bParity = OddParity; break;
                case 'S': bParity = SpaceParity; break;
                case 'M': bParity = MarkParity; break;
                case '\0': break;
                default:
                    pCom.nError = (int) HB_COM_ERR_PARAMVALUE;
                    return false;
            }
            byte bSize = dcb.ByteSize;
            if (nSize != 0)
            {
                if (nSize < 5 || nSize > 8)
                {
                    pCom.nError = (int) HB_COM_ERR_PARAMVALUE;
                    return false;
                }
                bSize = (byte) nSize;
            }
            byte bStop = dcb.StopBits;
            if (nStop != 0)
            {
                if (nStop == 1)
                    bStop = OneStopBit;
                else if (nStop == 2)
                    bStop = TwoStopBits;
                else
                {
                    pCom.nError = (int) HB_COM_ERR_PARAMVALUE;
                    return false;
                }
            }

            if (nBaud != 0)
                dcb.BaudRate = (uint) nBaud;
            dcb.ByteSize = bSize;
            dcb.Parity = bParity;
            dcb.StopBits = bStop;
            dcb.ErrorChar = (byte) '?';
            // fBinary, fDtrControl = DTR_CONTROL_ENABLE,
            // fTXContinueOnXoff, fRtsControl = RTS_CONTROL_ENABLE; the rest off
            dcb.Flags = DcbBinary | DcbTxContinueOnXoff |
                        (DtrControlEnable << DcbDtrControlShift) |
                        (RtsControlEnable << DcbRtsControlShift);
            fResult = SetCommState(pCom.hComm, ref dcb);
        }
        ComSetOsError(pCom, !fResult);
        return fResult;
    }

    // hb_comSend( <nPort>, <cBuffer>, [<nLen>], [<nTimeout>] ): the bytes
    // written, or -1. A timeout of its own applies to the whole write, as
    // Harbour's WriteTotalTimeoutConstant does.
    public static decimal hb_comSend(object? xPort, string? cBuffer, decimal? nLen = null,
                                     decimal? nTimeout = null)
    {
        HbComPort? pCom = ComPort(xPort, true);
        if (pCom == null)
            return ComFailed;
        byte[] aData = s_byteCP.GetBytes(cBuffer ?? "");
        int nSend = nLen is decimal n && n >= 0 && (int) n < aData.Length ? (int) n : aData.Length;
        if (!ComSetTimeouts(pCom, nTimeout, false))
            return ComFailed;
        bool fResult = WriteFile(pCom.hComm, aData, (uint) nSend, out uint nWritten, IntPtr.Zero);
        ComSetOsError(pCom, !fResult);
        if (!fResult)
            return ComFailed;
        if (nWritten < nSend)
            pCom.nError = (int) HB_COM_ERR_TIMEOUT;
        return nWritten;
    }

    // hb_comRecv( <nPort>, @<cBuffer>, [<nLen>], [<nTimeout>] ): the bytes
    // read, or -1; they overwrite the start of the buffer, which keeps its
    // length, as hb_socketRecv() does. Nothing within the timeout is 0 with
    // HB_COM_ERR_TIMEOUT.
    public static decimal hb_comRecv(object? xPort, ref string cBuffer, decimal? nLen = null,
                                     decimal? nTimeout = null)
    {
        HbComPort? pCom = ComPort(xPort, true);
        if (pCom == null)
            return ComFailed;
        cBuffer ??= "";
        int nWant = nLen is decimal n && n >= 0 && (int) n < cBuffer.Length ? (int) n : cBuffer.Length;
        if (nWant == 0)
            return 0;
        if (!ComSetTimeouts(pCom, nTimeout, true))
            return ComFailed;
        var aData = new byte[nWant];
        bool fResult = ReadFile(pCom.hComm, aData, (uint) nWant, out uint nRead, IntPtr.Zero);
        ComSetOsError(pCom, !fResult);
        if (!fResult)
            return ComFailed;
        if (nRead == 0)
            pCom.nError = (int) HB_COM_ERR_TIMEOUT;
        else
            cBuffer = s_byteCP.GetString(aData, 0, (int) nRead) + cBuffer.Substring((int) nRead);
        return nRead;
    }

    // Harbour's timeouts: no timeout reads what is there and returns at
    // once; a positive one waits that long in total for the first byte.
    static bool ComSetTimeouts(HbComPort pCom, decimal? nTimeout, bool fRead)
    {
        long nMs = nTimeout is decimal n ? (long) n : 0;
        var t = new COMMTIMEOUTS
        {
            ReadIntervalTimeout = MaxDword,
            ReadTotalTimeoutMultiplier = nMs > 0 ? MaxDword : 0,
            ReadTotalTimeoutConstant = nMs > 0 ? (uint) nMs : 0,
            WriteTotalTimeoutMultiplier = 0,
            WriteTotalTimeoutConstant = (uint) Math.Max(fRead ? 1 : nMs, 1)
        };
        bool fResult = SetCommTimeouts(pCom.hComm, ref t);
        ComSetOsError(pCom, !fResult);
        return fResult;
    }

    // hb_comFlush( <nPort>, [<nType>] ): throw away what is queued
    public static bool hb_comFlush(object? xPort, decimal? nType = null)
    {
        HbComPort? pCom = ComPort(xPort, true);
        if (pCom == null)
            return false;
        int nWhat = nType is decimal n ? (int) n : ComIOFlush;
        uint nPurge = nWhat switch
        {
            ComIFlush => PurgeRxClear,
            ComOFlush => PurgeTxClear,
            ComIOFlush => PurgeRxClear | PurgeTxClear,
            _ => 0
        };
        if (nPurge == 0)
        {
            pCom.nError = (int) HB_COM_ERR_PARAMVALUE;
            return false;
        }
        bool fResult = PurgeComm(pCom.hComm, nPurge);
        ComSetOsError(pCom, !fResult);
        return fResult;
    }

    // hb_comInputCount( <nPort> ) / hb_comOutputCount( <nPort> ): what is
    // waiting to be read, and what is waiting to go out
    public static decimal hb_comInputCount(object? xPort) => ComQueue(xPort, true);
    public static decimal hb_comOutputCount(object? xPort) => ComQueue(xPort, false);

    static decimal ComQueue(object? xPort, bool fInput)
    {
        HbComPort? pCom = ComPort(xPort, true);
        if (pCom == null)
            return ComFailed;
        bool fResult = ClearCommError(pCom.hComm, out uint _, out COMSTAT stat);
        ComSetOsError(pCom, !fResult);
        return fResult ? (fInput ? stat.cbInQue : stat.cbOutQue) : ComFailed;
    }

    // hb_comMCR( <nPort>, @<nValue>, <nClear>, <nSet> ): set or clear DTR
    // and RTS. Windows cannot read them back, so the value is 0 and a call
    // that asks for neither fails — Harbour answers exactly this.
    public static bool hb_comMCR(object? xPort, ref decimal nValue, decimal? nClear = null,
                                 decimal? nSet = null)
    {
        nValue = 0;
        HbComPort? pCom = ComPort(xPort, true);
        if (pCom == null)
            return false;
        int nClr = nClear is decimal c ? (int) c : 0;
        int nOn = nSet is decimal s ? (int) s : 0;
        bool fResult = false;
        if ((nOn & ComMcrDtr) != 0)
            fResult = EscapeCommFunction(pCom.hComm, SetDtr);
        else if ((nClr & ComMcrDtr) != 0)
            fResult = EscapeCommFunction(pCom.hComm, ClrDtr);
        if ((nOn & ComMcrRts) != 0)
            fResult = EscapeCommFunction(pCom.hComm, SetRts);
        else if ((nClr & ComMcrRts) != 0)
            fResult = EscapeCommFunction(pCom.hComm, ClrRts);
        ComSetOsError(pCom, !fResult);
        return fResult;
    }

    // hb_comMSR( <nPort>, @<nValue> ): the modem status lines. The DELTA
    // bits are not supported on Windows, in Harbour or here.
    public static bool hb_comMSR(object? xPort, ref decimal nValue)
    {
        nValue = 0;
        HbComPort? pCom = ComPort(xPort, true);
        if (pCom == null)
            return false;
        bool fResult = GetCommModemStatus(pCom.hComm, out uint nStat);
        if (fResult)
        {
            int nOut = 0;
            if ((nStat & MsCtsOn) != 0) nOut |= ComMsrCts;
            if ((nStat & MsDsrOn) != 0) nOut |= ComMsrDsr;
            if ((nStat & MsRingOn) != 0) nOut |= ComMsrRi;
            if ((nStat & MsRlsdOn) != 0) nOut |= ComMsrDcd;
            nValue = nOut;
        }
        ComSetOsError(pCom, !fResult);
        return fResult;
    }

    // hb_comFlowControl( <nPort>, @<nOld>, [<nNew>] ): read the flow
    // control, and set it when a value is given (-1 means only read)
    public static bool hb_comFlowControl(object? xPort, ref decimal nOld, decimal? nNew = null)
    {
        nOld = 0;
        HbComPort? pCom = ComPort(xPort, true);
        if (pCom == null)
            return false;
        var dcb = new DCB { DCBlength = (uint) Marshal.SizeOf<DCB>() };
        bool fResult = GetCommState(pCom.hComm, ref dcb);
        if (fResult)
        {
            int nValue = 0;
            if (((dcb.Flags >> DcbRtsControlShift) & 3) == RtsControlHandshake)
                nValue |= ComFlowIRtsCts;
            if ((dcb.Flags & DcbOutxCtsFlow) != 0)
                nValue |= ComFlowORtsCts;
            if (((dcb.Flags >> DcbDtrControlShift) & 3) == DtrControlHandshake)
                nValue |= ComFlowIDtrDsr;
            if ((dcb.Flags & DcbOutxDsrFlow) != 0)
                nValue |= ComFlowODtrDsr;
            if ((dcb.Flags & DcbInX) != 0)
                nValue |= ComFlowXoff;
            if ((dcb.Flags & DcbOutX) != 0)
                nValue |= ComFlowXon;
            nOld = nValue;

            int nSet = nNew is decimal n ? (int) n : -1;
            if (nSet >= 0)
            {
                uint nFlags = dcb.Flags;
                nFlags &= ~(uint) ((3 << DcbRtsControlShift) | (3 << DcbDtrControlShift) |
                                   DcbOutxCtsFlow | DcbOutxDsrFlow | DcbInX | DcbOutX);
                nFlags |= (uint) (((nSet & ComFlowIRtsCts) != 0
                                     ? RtsControlHandshake : RtsControlEnable) << DcbRtsControlShift);
                nFlags |= (uint) (((nSet & ComFlowIDtrDsr) != 0
                                     ? DtrControlHandshake : DtrControlEnable) << DcbDtrControlShift);
                if ((nSet & ComFlowORtsCts) != 0) nFlags |= DcbOutxCtsFlow;
                if ((nSet & ComFlowODtrDsr) != 0) nFlags |= DcbOutxDsrFlow;
                if ((nSet & ComFlowXoff) != 0) nFlags |= DcbInX;
                if ((nSet & ComFlowXon) != 0) nFlags |= DcbOutX;
                dcb.Flags = nFlags;
                fResult = SetCommState(pCom.hComm, ref dcb);
            }
        }
        ComSetOsError(pCom, !fResult);
        return fResult;
    }

    // hb_comFlowSet( <nPort>, <nFlow> ): start or stop the flow by hand.
    // Windows has EscapeCommFunction for the XON/XOFF pair; the RTS/CTS and
    // DTR/DSR forms are the lines themselves.
    public static bool hb_comFlowSet(object? xPort, decimal nFlow)
    {
        HbComPort? pCom = ComPort(xPort, true);
        if (pCom == null)
            return false;
        int nSet = (int) nFlow;
        bool fResult;
        if ((nSet & ComFlSoft) != 0)
            fResult = EscapeCommFunction(pCom.hComm,
                (nSet & ComFlOoff) != 0 ? SetXoff : SetXon);
        else if ((nSet & ComFlRtsCts) != 0)
            fResult = EscapeCommFunction(pCom.hComm,
                (nSet & ComFlIoff) != 0 ? ClrRts : SetRts);
        else if ((nSet & ComFlDtrDsr) != 0)
            fResult = EscapeCommFunction(pCom.hComm,
                (nSet & ComFlIoff) != 0 ? ClrDtr : SetDtr);
        else
        {
            pCom.nError = (int) HB_COM_ERR_NOSUPPORT;
            return false;
        }
        ComSetOsError(pCom, !fResult);
        return fResult;
    }

    const int ComFlOoff = 0x01, ComFlIoff = 0x04, ComFlSoft = 0x10;
    const int ComFlRtsCts = 0x20, ComFlDtrDsr = 0x40;

    // hb_comGetError( <nPort> ) / hb_comGetOSError( <nPort> )
    public static decimal hb_comGetError(object? xPort)
    {
        if (ComWrongPort(xPort))
            return HB_COM_ERR_WRONGPORT;
        HbComPort? pCom = ComPort(xPort, false);
        return pCom == null ? HB_COM_ERR_WRONGPORT : pCom.nError;
    }

    public static decimal hb_comGetOSError(object? xPort)
    {
        HbComPort? pCom = ComPort(xPort, false);
        return pCom == null ? 0 : pCom.nOsError;
    }

    // ---- Win32 ----

    const decimal ComFailed = -1;
    static readonly IntPtr ComInvalidHandle = new(-1);
    const uint GenericRead = 0x80000000, GenericWrite = 0x40000000;
    const uint OpenExisting = 3, FileFlagNoBuffering = 0x20000000;
    const uint PurgeTxClear = 0x0004, PurgeRxClear = 0x0008;
    const uint MaxDword = 0xFFFFFFFF;
    const uint SetXoff = 1, SetXon = 2, SetRts = 3, ClrRts = 4, SetDtr = 5, ClrDtr = 6;
    const uint MsCtsOn = 0x0010, MsDsrOn = 0x0020, MsRingOn = 0x0040, MsRlsdOn = 0x0080;
    const byte NoParity = 0, OddParity = 1, EvenParity = 2, MarkParity = 3, SpaceParity = 4;
    const byte OneStopBit = 0, TwoStopBits = 2;
    // The DCB's bit field, as wincon has it: fBinary is bit 0, then
    // fParity, fOutxCtsFlow, fOutxDsrFlow, fDtrControl (two bits), ...
    const uint DcbBinary = 0x0001, DcbOutxCtsFlow = 0x0004, DcbOutxDsrFlow = 0x0008;
    const int DcbDtrControlShift = 4;
    const uint DcbTxContinueOnXoff = 0x0080;
    const uint DcbOutX = 0x0100, DcbInX = 0x0200;
    const int DcbRtsControlShift = 12;
    const int DtrControlEnable = 1, DtrControlHandshake = 2;
    const int RtsControlEnable = 1, RtsControlHandshake = 2;

    [StructLayout(LayoutKind.Sequential)]
    struct DCB
    {
        public uint DCBlength;
        public uint BaudRate;
        public uint Flags;
        public ushort wReserved;
        public ushort XonLim;
        public ushort XoffLim;
        public byte ByteSize;
        public byte Parity;
        public byte StopBits;
        public sbyte XonChar;
        public sbyte XoffChar;
        public byte ErrorChar;
        public sbyte EofChar;
        public sbyte EvtChar;
        public ushort wReserved1;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct COMMTIMEOUTS
    {
        public uint ReadIntervalTimeout;
        public uint ReadTotalTimeoutMultiplier;
        public uint ReadTotalTimeoutConstant;
        public uint WriteTotalTimeoutMultiplier;
        public uint WriteTotalTimeoutConstant;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct COMSTAT
    {
        public uint Flags;
        public uint cbInQue;
        public uint cbOutQue;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
                                     IntPtr lpSecurityAttributes, uint dwCreationDisposition,
                                     uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetCommState(IntPtr hFile, ref DCB lpDCB);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetCommState(IntPtr hFile, ref DCB lpDCB);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetCommTimeouts(IntPtr hFile, ref COMMTIMEOUTS lpCommTimeouts);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead,
                                out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite,
                                 out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool PurgeComm(IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ClearCommError(IntPtr hFile, out uint lpErrors, out COMSTAT lpStat);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool EscapeCommFunction(IntPtr hFile, uint dwFunc);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetCommModemStatus(IntPtr hFile, out uint lpModemStat);
}
