using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

// Sockets (family 11): ports of rtl/hbsockhb.c (the hb_socket*() functions)
// and rtl/hbsocket.c (what they do on Windows), and hb_inetInit() /
// hb_inetCleanup() of rtl/hbinet.c.
// One part of HbRuntime; HbRuntime.cs says what the whole is.
//
// hb_socketGetError() is Harbour's own code, HB_SOCKET_ERR_* of
// hbsocket.ch, per thread, as Harbour keeps it on the thread's stack; every
// function sets it. Winsock's own error, which .NET's SocketError also
// uses, becomes it through hbsocket.c's Windows table. A timeout is in
// milliseconds, and none waits for ever; a wait with a timeout is made in
// slices, so a quit request ends the thread, as Harbour's select loop gives
// up on hb_vmRequestQuery(). A handle that is not a live socket — never one,
// or closed — is an argument error, as hb_socketParam() raises. Bytes cross
// as Latin-1, as a file's do.

// A socket's handle: what hb_socketOpen() and hb_socketAccept() return.
public sealed class HbSocket
{
    internal HbSocket(Socket sd) { this.sd = sd; }

    internal Socket? sd;            // null once closed
    internal bool lShutDown;        // connected: shut down before it is closed

    public override string ToString() => "HbSocket";
}

public static partial class HbRuntime
{
    // hbsocket.ch: Harbour's error codes, the index of s_socketErrors
    const int HB_SOCKET_ERR_NONE = 0, HB_SOCKET_ERR_PIPE = 1, HB_SOCKET_ERR_TIMEOUT = 2,
              HB_SOCKET_ERR_WRONGADDR = 3, HB_SOCKET_ERR_AFNOSUPPORT = 4,
              HB_SOCKET_ERR_PFNOSUPPORT = 5, HB_SOCKET_ERR_PROTONOSUPPORT = 6,
              HB_SOCKET_ERR_PARAMVALUE = 7, HB_SOCKET_ERR_NOSUPPORT = 8,
              HB_SOCKET_ERR_NORESOURCE = 9, HB_SOCKET_ERR_ACCESS = 10,
              HB_SOCKET_ERR_ADDRINUSE = 11, HB_SOCKET_ERR_INTERRUPT = 12,
              HB_SOCKET_ERR_ALREADYCONNECTED = 13, HB_SOCKET_ERR_CONNREFUSED = 14,
              HB_SOCKET_ERR_CONNABORTED = 15, HB_SOCKET_ERR_CONNRESET = 16,
              HB_SOCKET_ERR_NETUNREACH = 17, HB_SOCKET_ERR_NETDOWN = 18,
              HB_SOCKET_ERR_NETRESET = 19, HB_SOCKET_ERR_INPROGRESS = 20,
              HB_SOCKET_ERR_ALREADY = 21, HB_SOCKET_ERR_ADDRNOTAVAIL = 22,
              HB_SOCKET_ERR_READONLY = 23, HB_SOCKET_ERR_AGAIN = 24,
              HB_SOCKET_ERR_INVALIDHANDLE = 25, HB_SOCKET_ERR_INVAL = 26,
              HB_SOCKET_ERR_PROTO = 27, HB_SOCKET_ERR_PROTOTYPE = 28,
              HB_SOCKET_ERR_NOFILE = 29, HB_SOCKET_ERR_NOBUFS = 30,
              HB_SOCKET_ERR_NOMEM = 31, HB_SOCKET_ERR_FAULT = 32,
              HB_SOCKET_ERR_NAMETOOLONG = 33, HB_SOCKET_ERR_NOENT = 34,
              HB_SOCKET_ERR_NOTDIR = 35, HB_SOCKET_ERR_LOOP = 36,
              HB_SOCKET_ERR_MSGSIZE = 37, HB_SOCKET_ERR_DESTADDRREQ = 38,
              HB_SOCKET_ERR_NOPROTOOPT = 39, HB_SOCKET_ERR_NOTCONN = 40,
              HB_SOCKET_ERR_SHUTDOWN = 41, HB_SOCKET_ERR_TOOMANYREFS = 42,
              HB_SOCKET_ERR_RESTARTSYS = 43, HB_SOCKET_ERR_NOSR = 44,
              HB_SOCKET_ERR_HOSTDOWN = 45, HB_SOCKET_ERR_HOSTUNREACH = 46,
              HB_SOCKET_ERR_NOTEMPTY = 47, HB_SOCKET_ERR_USERS = 48,
              HB_SOCKET_ERR_DQUOT = 49, HB_SOCKET_ERR_STALE = 50,
              HB_SOCKET_ERR_REMOTE = 51, HB_SOCKET_ERR_PROCLIM = 52,
              HB_SOCKET_ERR_DISCON = 53, HB_SOCKET_ERR_NOMORE = 54,
              HB_SOCKET_ERR_CANCELLED = 55, HB_SOCKET_ERR_INVALIDPROCTABLE = 56,
              HB_SOCKET_ERR_INVALIDPROVIDER = 57, HB_SOCKET_ERR_PROVIDERFAILEDINIT = 58,
              HB_SOCKET_ERR_REFUSED = 59, HB_SOCKET_ERR_SYSNOTREADY = 60,
              HB_SOCKET_ERR_VERNOTSUPPORTED = 61, HB_SOCKET_ERR_NOTINITIALISED = 62,
              HB_SOCKET_ERR_TRYAGAIN = 63, HB_SOCKET_ERR_HOSTNOTFOUND = 64,
              HB_SOCKET_ERR_NORECOVERY = 65, HB_SOCKET_ERR_NODATA = 66,
              HB_SOCKET_ERR_SYSCALLFAILURE = 67, HB_SOCKET_ERR_SERVICENOTFOUND = 68,
              HB_SOCKET_ERR_TYPENOTFOUND = 69, HB_SOCKET_ERR_OTHER = 70;

    // hbsocket.ch: the rest of what these functions take
    const int HB_SOCKET_AF_INET = 2;
    const int HB_SOCKET_PT_STREAM = 1;
    const int HB_SOCKET_SHUT_RD = 0, HB_SOCKET_SHUT_WR = 1, HB_SOCKET_SHUT_RDWR = 2;
    const int HB_SOCKET_MSG_OOB = 0x01, HB_SOCKET_MSG_PEEK = 0x02,
              HB_SOCKET_MSG_DONTROUTE = 0x04, HB_SOCKET_MSG_WAITALL = 0x08;

    // Winsock's errors (winerror.h), the ones hbsocket.c's table knows
    const int WSA_NOT_ENOUGH_MEMORY = 8, WSAEINTR = 10004, WSAEBADF = 10009,
              WSAEACCES = 10013, WSAEFAULT = 10014, WSAEINVAL = 10022,
              WSAEMFILE = 10024, WSAEWOULDBLOCK = 10035, WSAEINPROGRESS = 10036,
              WSAEALREADY = 10037, WSAENOTSOCK = 10038, WSAEDESTADDRREQ = 10039,
              WSAEMSGSIZE = 10040, WSAEPROTOTYPE = 10041, WSAENOPROTOOPT = 10042,
              WSAEPROTONOSUPPORT = 10043, WSAESOCKTNOSUPPORT = 10044,
              WSAEOPNOTSUPP = 10045, WSAEPFNOSUPPORT = 10046, WSAEAFNOSUPPORT = 10047,
              WSAEADDRINUSE = 10048, WSAEADDRNOTAVAIL = 10049, WSAENETDOWN = 10050,
              WSAENETUNREACH = 10051, WSAENETRESET = 10052, WSAECONNABORTED = 10053,
              WSAECONNRESET = 10054, WSAENOBUFS = 10055, WSAEISCONN = 10056,
              WSAENOTCONN = 10057, WSAESHUTDOWN = 10058, WSAETOOMANYREFS = 10059,
              WSAETIMEDOUT = 10060, WSAECONNREFUSED = 10061, WSAELOOP = 10062,
              WSAENAMETOOLONG = 10063, WSAEHOSTDOWN = 10064, WSAEHOSTUNREACH = 10065,
              WSAENOTEMPTY = 10066, WSAEPROCLIM = 10067, WSAEUSERS = 10068,
              WSAEDQUOT = 10069, WSAESTALE = 10070, WSAEREMOTE = 10071,
              WSASYSNOTREADY = 10091, WSAVERNOTSUPPORTED = 10092,
              WSANOTINITIALISED = 10093, WSAEDISCON = 10101, WSAENOMORE = 10102,
              WSAECANCELLED = 10103, WSAEINVALIDPROCTABLE = 10104,
              WSAEINVALIDPROVIDER = 10105, WSAEPROVIDERFAILEDINIT = 10106,
              WSASYSCALLFAILURE = 10107, WSASERVICE_NOT_FOUND = 10108,
              WSATYPE_NOT_FOUND = 10109, WSA_E_NO_MORE = 10110,
              WSA_E_CANCELLED = 10111, WSAEREFUSED = 10112,
              WSAHOST_NOT_FOUND = 11001, WSATRY_AGAIN = 11002,
              WSANO_RECOVERY = 11003, WSANO_DATA = 11004;

    const long SockForever = -1;            // a timeout that never ends (hb_parnintdef( n, -1 ))
    const int SockFailed = -1;              // hb_socketSend() / hb_socketRecv(): an error or the timeout
    const int SockDefaultBacklog = 10;      // hb_socketListen()'s queue, hbsockhb.c
    const int SockArgSubCode = 3012;        // the subCode of hbsockhb.c's argument errors

    // hbsocket.c s_socketErrors, by HB_SOCKET_ERR_* code
    static readonly string[] s_socketErrors =
    {
        "OK", "EPIPE", "ETIMEOUT", "EWRONGADDR", "EAFNOSUPPORT", "EPFNOSUPPORT",
        "EPROTONOSUPPORT", "EPARAMVALUE", "ENOSUPPORT", "ENORESOURCE", "EACCESS",
        "EADDRINUSE", "EINTERRUPT", "EALREADYCONNECTED", "ECONNREFUSED",
        "ECONNABORTED", "ECONNRESET", "ENETUNREACH", "ENETDOWN", "ENETRESET",
        "EINPROGRESS", "EALREADY", "EADDRNOTAVAIL", "EREADONLY", "EAGAIN",
        "EINVALIDHANDLE", "EINVAL", "EPROTO", "EPROTOTYPE", "ENOFILE", "ENOBUFS",
        "ENOMEM", "EFAULT", "ENAMETOOLONG", "ENOENT", "ENOTDIR", "ELOOP",
        "EMSGSIZE", "EDESTADDRREQ", "ENOPROTOOPT", "ENOTCONN", "ESHUTDOWN",
        "ETOOMANYREFS", "ERESTARTSYS", "ENOSR", "EHOSTDOWN", "EHOSTUNREACH",
        "ENOTEMPTY", "EUSERS", "EDQUOT", "ESTALE", "EREMOTE", "EPROCLIM",
        "EDISCON", "ENOMORE", "ECANCELLED", "EINVALIDPROCTABLE",
        "EINVALIDPROVIDER", "EPROVIDERFAILEDINIT", "EREFUSED", "ESYSNOTREADY",
        "EVERNOTSUPPORTED", "ENOTINITIALISED", "TRYAGAIN", "HOSTNOTFOUND",
        "NORECOVERY", "NODATA", "ESYSCALLFAILURE", "ESERVICENOTFOUND",
        "ETYPENOTFOUND", "EOTHER",
    };

    // hb_socketGetError() and hb_socketGetOsError(), per thread
    [ThreadStatic] static int t_sockError;
    [ThreadStatic] static int t_sockOsError;

    // hbsocket.c hb_socketSetOsError: a Winsock error and Harbour's code for it
    static void SetSockOsError(int nOsError)
    {
        t_sockOsError = nOsError;
        t_sockError = nOsError switch
        {
            0 => HB_SOCKET_ERR_NONE,
            WSAEINTR => HB_SOCKET_ERR_INTERRUPT,
            WSAEBADF or WSAENOTSOCK => HB_SOCKET_ERR_INVALIDHANDLE,
            WSAEACCES => HB_SOCKET_ERR_ACCESS,
            WSAEFAULT => HB_SOCKET_ERR_FAULT,
            WSAEINVAL => HB_SOCKET_ERR_INVAL,
            WSAEMFILE => HB_SOCKET_ERR_NOFILE,
            WSAEWOULDBLOCK => HB_SOCKET_ERR_AGAIN,
            WSAEINPROGRESS => HB_SOCKET_ERR_INPROGRESS,
            WSAEALREADY => HB_SOCKET_ERR_ALREADY,
            WSAEDESTADDRREQ => HB_SOCKET_ERR_DESTADDRREQ,
            WSAEMSGSIZE => HB_SOCKET_ERR_MSGSIZE,
            WSAEPROTOTYPE => HB_SOCKET_ERR_PROTOTYPE,
            WSAENOPROTOOPT => HB_SOCKET_ERR_NOPROTOOPT,
            WSAEPROTONOSUPPORT => HB_SOCKET_ERR_PROTONOSUPPORT,
            WSAEOPNOTSUPP or WSAESOCKTNOSUPPORT => HB_SOCKET_ERR_NOSUPPORT,
            WSAEPFNOSUPPORT => HB_SOCKET_ERR_PFNOSUPPORT,
            WSAEAFNOSUPPORT => HB_SOCKET_ERR_AFNOSUPPORT,
            WSAEADDRINUSE => HB_SOCKET_ERR_ADDRINUSE,
            WSAEADDRNOTAVAIL => HB_SOCKET_ERR_ADDRNOTAVAIL,
            WSAENETDOWN => HB_SOCKET_ERR_NETDOWN,
            WSAENETUNREACH => HB_SOCKET_ERR_NETUNREACH,
            WSAENETRESET => HB_SOCKET_ERR_NETRESET,
            WSAECONNREFUSED => HB_SOCKET_ERR_CONNREFUSED,
            WSAECONNABORTED => HB_SOCKET_ERR_CONNABORTED,
            WSAECONNRESET => HB_SOCKET_ERR_CONNRESET,
            WSAENOBUFS => HB_SOCKET_ERR_NOBUFS,
            WSAEISCONN => HB_SOCKET_ERR_ALREADYCONNECTED,
            WSAENOTCONN => HB_SOCKET_ERR_NOTCONN,
            WSAESHUTDOWN => HB_SOCKET_ERR_SHUTDOWN,
            WSAETOOMANYREFS => HB_SOCKET_ERR_TOOMANYREFS,
            WSAETIMEDOUT => HB_SOCKET_ERR_TIMEOUT,
            WSAELOOP => HB_SOCKET_ERR_LOOP,
            WSAENAMETOOLONG => HB_SOCKET_ERR_NAMETOOLONG,
            WSAEHOSTDOWN => HB_SOCKET_ERR_HOSTDOWN,
            WSAEHOSTUNREACH => HB_SOCKET_ERR_HOSTUNREACH,
            WSAENOTEMPTY => HB_SOCKET_ERR_NOTEMPTY,
            WSAEUSERS => HB_SOCKET_ERR_USERS,
            WSAEDQUOT => HB_SOCKET_ERR_DQUOT,
            WSAESTALE => HB_SOCKET_ERR_STALE,
            WSAEREMOTE => HB_SOCKET_ERR_REMOTE,
            WSAEPROCLIM => HB_SOCKET_ERR_PROCLIM,
            WSAEDISCON => HB_SOCKET_ERR_DISCON,
            WSAENOMORE or WSA_E_NO_MORE => HB_SOCKET_ERR_NOMORE,
            WSAECANCELLED or WSA_E_CANCELLED => HB_SOCKET_ERR_CANCELLED,
            WSAEINVALIDPROCTABLE => HB_SOCKET_ERR_INVALIDPROCTABLE,
            WSAEINVALIDPROVIDER => HB_SOCKET_ERR_INVALIDPROVIDER,
            WSAEPROVIDERFAILEDINIT => HB_SOCKET_ERR_PROVIDERFAILEDINIT,
            WSAEREFUSED => HB_SOCKET_ERR_REFUSED,
            WSATRY_AGAIN => HB_SOCKET_ERR_TRYAGAIN,
            WSASYSNOTREADY => HB_SOCKET_ERR_SYSNOTREADY,
            WSAVERNOTSUPPORTED => HB_SOCKET_ERR_VERNOTSUPPORTED,
            WSANOTINITIALISED => HB_SOCKET_ERR_NOTINITIALISED,
            WSAHOST_NOT_FOUND => HB_SOCKET_ERR_HOSTNOTFOUND,
            WSANO_RECOVERY => HB_SOCKET_ERR_NORECOVERY,
            WSANO_DATA => HB_SOCKET_ERR_NODATA,
            WSASYSCALLFAILURE => HB_SOCKET_ERR_SYSCALLFAILURE,
            WSASERVICE_NOT_FOUND => HB_SOCKET_ERR_SERVICENOTFOUND,
            WSATYPE_NOT_FOUND => HB_SOCKET_ERR_TYPENOTFOUND,
            WSA_NOT_ENOUGH_MEMORY => HB_SOCKET_ERR_NOMEM,
            _ => HB_SOCKET_ERR_OTHER,
        };
    }

    // hb_socketSetError: one of Harbour's own codes, no Winsock error behind it
    static void SetSockError(int nError)
    {
        t_sockError = nError;
        t_sockOsError = 0;
    }

    static void SetSockOsError(SocketException e) => SetSockOsError((int) e.SocketErrorCode);

    // The argument error hbsockhb.c raises (EG_ARG, SockArgSubCode)
    static ArgumentException SockArgError(string cFunc) =>
        new("Argument error (" + cFunc + ", " + SockArgSubCode + ")");

    // hb_socketParam(): the live socket a handle holds, else the argument error
    static HbSocket SockParam(object? pSocket, string cFunc) =>
        pSocket is HbSocket { sd: not null } s ? s : throw SockArgError(cFunc);

    // hb_parnintdef( n, -1 ): a timeout in milliseconds; none, or any
    // negative one, waits for ever
    static long SockTimeout(decimal? nTimeout)
    {
        long nMs = nTimeout is decimal n ? (long) Math.Truncate(n) : SockForever;
        return nMs < 0 ? SockForever : nMs;
    }

    // hbsocket.c hb_socketTransFlags: Harbour's HB_SOCKET_MSG_* are
    // Winsock's MSG_* on Windows, so they pass as they are
    static SocketFlags SockFlags(decimal? nFlags) =>
        nFlags is decimal n
            ? (SocketFlags) ((long) Math.Truncate(n) & (HB_SOCKET_MSG_OOB | HB_SOCKET_MSG_PEEK |
                                                       HB_SOCKET_MSG_DONTROUTE | HB_SOCKET_MSG_WAITALL))
            : SocketFlags.None;

    // What a wait ended in: hbsocket.c's select functions return 1, 0, -1
    enum SockWait { Failed, TimedOut, Ready }

    static int Microseconds(long nMs) => (int) nMs * 1000;

    // hbsocket.c hb_socketSelectRD / hb_socketSelectWR: wait until the
    // socket can be read (or accepted from) or written
    static SockWait SockSelect(Socket sd, SelectMode mode, long nTimeout)
    {
        long nEnd = nTimeout == SockForever ? long.MaxValue : Environment.TickCount64 + nTimeout;
        try
        {
            for (;;)
            {
                long nLeft = nEnd - Environment.TickCount64;
                bool lReady = sd.Poll(Microseconds(Math.Clamp(nLeft, 0, QuitSliceMs)), mode);
                SetSockOsError(0);
                if (lReady)
                    return SockWait.Ready;
                if (nLeft <= 0)
                    return SockWait.TimedOut;
                QuitCheck();
            }
        }
        catch (SocketException e) { SetSockOsError(e); }
        catch (ObjectDisposedException) { SetSockOsError(WSAENOTSOCK); }
        return SockWait.Failed;
    }

    // hbsocket.c hb_socketSelectWRE on Windows: wait for a connect under way
    // to finish; when it failed, the error is the socket's own (SO_ERROR)
    static SockWait SockSelectConnect(Socket sd, long nTimeout)
    {
        long nEnd = Environment.TickCount64 + nTimeout;
        for (;;)
        {
            long nLeft = nEnd - Environment.TickCount64;
            var aWrite = new List<Socket> { sd };
            var aError = new List<Socket> { sd };
            Socket.Select(null, aWrite, aError, Microseconds(Math.Clamp(nLeft, 0, QuitSliceMs)));
            SetSockOsError(0);
            if (aError.Count > 0)
            {
                SetSockOsError((int) sd.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error)!);
                return SockWait.Failed;
            }
            if (aWrite.Count > 0)
                return SockWait.Ready;
            if (nLeft <= 0)
                return SockWait.TimedOut;
            QuitCheck();
        }
    }

    // The wait before a send, receive or accept with a timeout: Ready to go
    // on, else the reason it cannot, the error set (a timeout is
    // HB_SOCKET_ERR_TIMEOUT)
    static SockWait SockWaitFor(Socket sd, SelectMode mode, long nTimeout)
    {
        SockWait wait = SockSelect(sd, mode, nTimeout);
        if (wait == SockWait.TimedOut)
            SetSockError(HB_SOCKET_ERR_TIMEOUT);
        return wait;
    }

    // s_socketaddrParam(): { HB_SOCKET_AF_INET, <cAddress>, <nPort> } as an
    // end point; an empty address is any. The address must be dotted
    // decimal, as Windows' inet_pton() takes it — no name is looked up.
    // Anything else is the argument error Harbour raises. AF_INET is the one
    // family built: the only one EasiPOS uses.
    static readonly Regex s_dottedQuad = new(@"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$", RegexOptions.CultureInvariant);

    static IPEndPoint SockAddrParam(object? aAddr, string cFunc)
    {
        if (aAddr is object?[] { Length: >= 2 } a && a[0] is object xFamily && IsNumeric(xFamily)
            && Convert.ToInt64(xFamily, INV) == HB_SOCKET_AF_INET)
        {
            string cAddress = a[1] as string ?? "";
            long nPort = a.Length > 2 && a[2] is object xPort && IsNumeric(xPort)
                ? (long) Math.Truncate(Convert.ToDecimal(xPort, INV)) : 0;
            IPAddress? ip = cAddress.Length == 0 ? IPAddress.Any
                : s_dottedQuad.IsMatch(cAddress) && IPAddress.TryParse(cAddress, out var parsed) ? parsed
                : null;
            if (ip != null)
            {
                SetSockError(HB_SOCKET_ERR_NONE);
                return new IPEndPoint(ip, (ushort) nPort);      // htons( ( HB_U16 ) iPort )
            }
        }
        SetSockError(HB_SOCKET_ERR_AFNOSUPPORT);
        throw SockArgError(cFunc);
    }

    // ---- The functions ----

    // hb_inetInit(): .T. when the socket layer started, which .NET has
    // done; hb_inetCleanup() has nothing to undo
    public static bool hb_inetInit() => true;

    public static object? hb_inetCleanup() => null;

    public static decimal hb_socketGetError() => t_sockError;

    // hb_socketErrorString( [<nError>], [<pSocket>] ): the text of an error
    // code, of the last error by default; a handle, in either place, only
    // has to be a live socket, and anything else is ignored
    public static string hb_socketErrorString() => SockErrorText(t_sockError);

    public static string hb_socketErrorString(object? x1, object? x2 = null)
    {
        object? xPointer = IsPointer(x1) ? x1 : IsPointer(x2) ? x2 : null;
        if (xPointer != null)
            SockParam(xPointer, "HB_SOCKETERRORSTRING");
        object? xError = x1 != null && IsNumeric(x1) ? x1 : x2 != null && IsNumeric(x2) ? x2 : null;
        return SockErrorText(xError == null ? t_sockError : (int) Convert.ToInt64(xError, INV));
    }

    static string SockErrorText(int nError) =>
        nError >= HB_SOCKET_ERR_NONE && nError <= HB_SOCKET_ERR_OTHER ? s_socketErrors[nError] : "";

    // hb_socketOpen( [<nDomain>], [<nType>], [<nProtocol>] ): a socket, an
    // AF_INET stream by default; NIL when it cannot be made, where Harbour
    // gives an empty pointer (ValType "P", not NIL) — EasiPOS asks Empty(),
    // which both answer .T., as hb_socketAccept()'s callers do. The numbers go
    // to Winsock as they are, as Harbour's Windows build passes them, and
    // .NET's AddressFamily, SocketType and ProtocolType are Winsock's. No
    // protocol is the type's own.
    public static HbSocket? hb_socketOpen(decimal? nDomain = null, decimal? nType = null, decimal? nProtocol = null)
    {
        try
        {
            var sd = new Socket((AddressFamily) (int) (nDomain ?? HB_SOCKET_AF_INET),
                                (SocketType) (int) (nType ?? HB_SOCKET_PT_STREAM),
                                nProtocol is decimal n ? (ProtocolType) (int) n : ProtocolType.Unspecified);
            SetSockOsError(0);
            return new HbSocket(sd);
        }
        catch (SocketException e)
        {
            SetSockOsError(e);
            return null;
        }
    }

    // hb_socketClose( <pSocket> ): shut a connected socket down, then close
    // it; the handle is dead after, as Harbour clears it
    public static bool hb_socketClose(object? pSocket)
    {
        HbSocket s = SockParam(pSocket, "HB_SOCKETCLOSE");
        Socket sd = s.sd!;
        s.sd = null;
        if (s.lShutDown)
        {
            try { sd.Shutdown(SocketShutdown.Both); }
            catch (SocketException) { }
        }
        sd.Close();
        SetSockOsError(0);
        return true;
    }

    // hb_socketShutdown( <pSocket>, [<nMode>] ): HB_SOCKET_SHUT_RD, _WR or
    // _RDWR (the default)
    public static bool hb_socketShutdown(object? pSocket, decimal? nMode = null)
    {
        Socket sd = SockParam(pSocket, "HB_SOCKETSHUTDOWN").sd!;
        SocketShutdown? how = (nMode ?? HB_SOCKET_SHUT_RDWR) switch
        {
            HB_SOCKET_SHUT_RD => SocketShutdown.Receive,
            HB_SOCKET_SHUT_WR => SocketShutdown.Send,
            HB_SOCKET_SHUT_RDWR => SocketShutdown.Both,
            _ => null,
        };
        if (how == null)
        {
            SetSockError(HB_SOCKET_ERR_PARAMVALUE);
            return false;
        }
        try
        {
            sd.Shutdown(how.Value);
            SetSockOsError(0);
            return true;
        }
        catch (SocketException e)
        {
            SetSockOsError(e);
            return false;
        }
    }

    public static bool hb_socketBind(object? pSocket, object? aAddr)
    {
        Socket sd = SockParam(pSocket, "HB_SOCKETBIND").sd!;
        IPEndPoint ep = SockAddrParam(aAddr, "HB_SOCKETBIND");
        try
        {
            sd.Bind(ep);
            SetSockOsError(0);
            return true;
        }
        catch (SocketException e)
        {
            SetSockOsError(e);
            return false;
        }
    }

    public static bool hb_socketListen(object? pSocket, decimal? nBacklog = null)
    {
        Socket sd = SockParam(pSocket, "HB_SOCKETLISTEN").sd!;
        try
        {
            sd.Listen((int) (nBacklog ?? SockDefaultBacklog));
            SetSockOsError(0);
            return true;
        }
        catch (SocketException e)
        {
            SetSockOsError(e);
            return false;
        }
    }

    // hb_socketAccept( <pSocket>, [@<aAddr>], [<nTimeout>] ): the next
    // connection, NIL on the timeout (HB_SOCKET_ERR_TIMEOUT) or a failure.
    // The listening socket is non-blocking while it accepts, so a connection
    // another thread took first fails with EAGAIN instead of hanging; the
    // accepted one is blocking. The address is not returned: EasiPOS never
    // asks.
    public static HbSocket? hb_socketAccept(object? pSocket, object? xAddr = null, decimal? nTimeout = null)
    {
        Socket sd = SockParam(pSocket, "HB_SOCKETACCEPT").sd!;
        long nTime = SockTimeout(nTimeout);
        if (SockWaitFor(sd, SelectMode.SelectRead, nTime) != SockWait.Ready)
            return null;
        bool lTimed = nTime != SockForever;
        try
        {
            if (lTimed)
                sd.Blocking = false;
            Socket sdNew = sd.Accept();
            sdNew.Blocking = true;
            SetSockOsError(0);
            return new HbSocket(sdNew) { lShutDown = true };
        }
        catch (SocketException e)
        {
            SetSockOsError(e);
            return null;
        }
        finally
        {
            if (lTimed)
                sd.Blocking = true;
        }
    }

    // hb_socketConnect( <pSocket>, <aAddr>, [<nTimeout>] ): .T. when
    // connected. With a timeout the connect is made non-blocking and waited
    // for, as Harbour does it; running out of time is HB_SOCKET_ERR_TIMEOUT,
    // a refusal HB_SOCKET_ERR_CONNREFUSED.
    public static bool hb_socketConnect(object? pSocket, object? aAddr, decimal? nTimeout = null)
    {
        HbSocket s = SockParam(pSocket, "HB_SOCKETCONNECT");
        Socket sd = s.sd!;
        IPEndPoint ep = SockAddrParam(aAddr, "HB_SOCKETCONNECT");
        long nTime = SockTimeout(nTimeout);
        bool lTimed = nTime != SockForever;
        bool lConnected;
        try
        {
            if (lTimed)
                sd.Blocking = false;
            sd.Connect(ep);
            SetSockOsError(0);
            lConnected = true;
        }
        catch (SocketException e) when (lTimed && (int) e.SocketErrorCode == WSAEWOULDBLOCK)
        {
            SockWait wait = SockSelectConnect(sd, nTime);
            if (wait == SockWait.TimedOut)
                SetSockError(HB_SOCKET_ERR_TIMEOUT);
            else if (wait == SockWait.Ready)
                SetSockError(HB_SOCKET_ERR_NONE);
            lConnected = wait == SockWait.Ready;
        }
        catch (SocketException e)
        {
            SetSockOsError(e);
            lConnected = false;
        }
        finally
        {
            if (lTimed)
                sd.Blocking = true;
        }
        if (lConnected)
            s.lShutDown = true;
        return lConnected;
    }

    // hb_socketSend( <pSocket>, <cBuffer>, [<nLen>], [<nFlags>], [<nTimeout>] ):
    // how many bytes went; -1 on an error or the timeout (HB_SOCKET_ERR_TIMEOUT)
    public static decimal hb_socketSend(object? pSocket, string cBuffer, decimal? nLen = null,
                                        decimal? nFlags = null, decimal? nTimeout = null)
    {
        Socket sd = SockParam(pSocket, "HB_SOCKETSEND").sd!;
        byte[] aData = s_byteCP.GetBytes(cBuffer);
        int nSend = aData.Length;
        if (nLen is decimal n && n >= 0 && n < nSend)
            nSend = (int) n;
        long nTime = SockTimeout(nTimeout);
        if (nTime != SockForever && SockWaitFor(sd, SelectMode.SelectWrite, nTime) != SockWait.Ready)
            return SockFailed;
        try
        {
            int nSent = sd.Send(aData, 0, nSend, SockFlags(nFlags), out SocketError err);
            SetSockOsError((int) err);
            return err == SocketError.Success ? nSent : SockFailed;
        }
        catch (ObjectDisposedException)
        {
            SetSockOsError(WSAENOTSOCK);
            return SockFailed;
        }
    }

    // hb_socketRecv( <pSocket>, @<cBuffer>, [<nLen>], [<nFlags>], [<nTimeout>] ):
    // read at most Len( cBuffer ) (or nLen, when less) bytes over the start
    // of cBuffer, which keeps its length. How many came; 0 when the other
    // end closed; -1 on an error or the timeout (HB_SOCKET_ERR_TIMEOUT).
    public static decimal hb_socketRecv(object? pSocket, ref string cBuffer, decimal? nLen = null,
                                        decimal? nFlags = null, decimal? nTimeout = null)
    {
        Socket sd = SockParam(pSocket, "HB_SOCKETRECV").sd!;
        int nRead = cBuffer.Length;
        if (nLen is decimal n && n >= 0 && n < nRead)
            nRead = (int) n;
        long nTime = SockTimeout(nTimeout);
        if (nTime != SockForever && SockWaitFor(sd, SelectMode.SelectRead, nTime) != SockWait.Ready)
            return SockFailed;
        byte[] aData = new byte[nRead];
        int nGot;
        try
        {
            nGot = sd.Receive(aData, 0, nRead, SockFlags(nFlags), out SocketError err);
            SetSockOsError((int) err);
            if (err != SocketError.Success)
                return SockFailed;
        }
        catch (ObjectDisposedException)
        {
            SetSockOsError(WSAENOTSOCK);
            return SockFailed;
        }
        if (nGot > 0)
        {
            char[] aBuffer = cBuffer.ToCharArray();
            for (int i = 0; i < nGot; i++)
                aBuffer[i] = (char) aData[i];
            cBuffer = new string(aBuffer);
        }
        return nGot;
    }
}
