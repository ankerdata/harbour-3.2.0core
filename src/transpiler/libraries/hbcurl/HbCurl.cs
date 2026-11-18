// HbCurl — the hbcurl contrib library (contrib/hbcurl/hbcurl.hbx) for
// transpiled Harbour code. The emitter writes `HbCurl.<name>(...)` for
// every name that .hbx lists. Real implementations go here; HbCurl.Stubs.cs
// holds NotImplementedException stubs for the rest the application calls
// (scripts/gen_library_stubs.py in easipos-transpiled).
//
// The easy interface over HTTP and HTTPS is System.Net.Http's HttpClient:
// an easy handle keeps the options it is given, and curl_easy_perform()
// makes the one request they describe. A transfer libcurl would make
// another way (a WebSocket's CONNECT_ONLY, SMTP, an upload) is not
// implemented and says so.

using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

// An easy handle: the options set on it and what the last transfer left
public sealed class HbCurlEasy
{
    internal readonly Dictionary<int, object?> Options = new();
    internal long ResponseCode;
    internal string EffectiveUrl = "";
    internal byte[] Body = Array.Empty<byte>();
}

public static partial class HbCurl
{
    // hbcurl.ch
    const int HB_CURLOPT_URL = 2;
    const int HB_CURLOPT_TIMEOUT = 13;
    const int HB_CURLOPT_POSTFIELDS = 15;
    const int HB_CURLOPT_HTTPHEADER = 23;
    const int HB_CURLOPT_FAILONERROR = 45;
    const int HB_CURLOPT_UPLOAD = 46;
    const int HB_CURLOPT_POST = 47;
    const int HB_CURLOPT_FOLLOWLOCATION = 52;
    const int HB_CURLOPT_CONNECT_ONLY = 141;
    const int HB_CURLOPT_TIMEOUT_MS = 155;
    const int HB_CURLOPT_NOPROXY = 177;
    const int HB_CURLOPT_DL_FILE_SETUP = 1005;
    const int HB_CURLINFO_EFFECTIVE_URL = 1;
    const int HB_CURLINFO_RESPONSE_CODE = 2;
    const long HB_CURLE_OK = 0;
    const long HB_CURLE_UNSUPPORTED_PROTOCOL = 1;
    const long HB_CURLE_URL_MALFORMAT = 3;
    const long HB_CURLE_COULDNT_RESOLVE_HOST = 6;
    const long HB_CURLE_COULDNT_CONNECT = 7;
    const long HB_CURLE_HTTP_RETURNED_ERROR = 22;
    const long HB_CURLE_OPERATION_TIMEDOUT = 28;
    const long HB_CURLE_SEND_ERROR = 55;

    static HbCurlEasy Easy(object? pCurl, string cFunc) =>
        pCurl as HbCurlEasy ?? throw new ArgumentException(cFunc + ": not an easy handle");

    static int Int(object? x) => Convert.ToInt32(x, CultureInfo.InvariantCulture);

    static bool Truth(object? x) => x switch
    {
        null => false,
        bool l => l,
        string => true,
        _ => Convert.ToDecimal(x, CultureInfo.InvariantCulture) != 0,
    };

    public static long curl_global_init(params object?[] args) => HB_CURLE_OK;

    public static void curl_global_cleanup()
    {
    }

    public static HbCurlEasy curl_easy_init() => new HbCurlEasy();

    public static void curl_easy_cleanup(object? pCurl)
    {
    }

    public static void curl_easy_reset(object? pCurl)
    {
        HbCurlEasy oEasy = Easy(pCurl, "CURL_EASY_RESET");
        oEasy.Options.Clear();
        oEasy.ResponseCode = 0;
        oEasy.EffectiveUrl = "";
        oEasy.Body = Array.Empty<byte>();
    }

    // curl_easy_setopt( <pCurl>, <nOption>, [<xValue>] ): the option kept
    // for curl_easy_perform(); an option set without a value is on
    public static long curl_easy_setopt(object? pCurl, object? nOption, object? xValue = null)
    {
        Easy(pCurl, "CURL_EASY_SETOPT").Options[Int(nOption)] = xValue ?? true;
        return HB_CURLE_OK;
    }

    // curl_easy_perform( <pCurl> ): the request the handle's options
    // describe, a CURLE code
    public static long curl_easy_perform(object? pCurl)
    {
        HbCurlEasy oEasy = Easy(pCurl, "CURL_EASY_PERFORM");
        var hOpt = oEasy.Options;
        oEasy.ResponseCode = 0;
        oEasy.Body = Array.Empty<byte>();

        if (!hOpt.TryGetValue(HB_CURLOPT_URL, out object? xUrl) || xUrl is not string cUrl ||
            !Uri.TryCreate(cUrl, UriKind.Absolute, out Uri? oUri))
            return HB_CURLE_URL_MALFORMAT;
        if (oUri.Scheme != Uri.UriSchemeHttp && oUri.Scheme != Uri.UriSchemeHttps)
            throw new NotImplementedException("curl_easy_perform(): " + oUri.Scheme + " is not implemented");
        if (hOpt.TryGetValue(HB_CURLOPT_CONNECT_ONLY, out object? xConnect) && Truth(xConnect))
            throw new NotImplementedException("curl_easy_perform(): HB_CURLOPT_CONNECT_ONLY is not implemented");
        if (hOpt.TryGetValue(HB_CURLOPT_UPLOAD, out object? xUpload) && Truth(xUpload))
            throw new NotImplementedException("curl_easy_perform(): HB_CURLOPT_UPLOAD is not implemented");

        TimeSpan tTimeout = Timeout.InfiniteTimeSpan;
        if (hOpt.TryGetValue(HB_CURLOPT_TIMEOUT_MS, out object? xMs) && Int(xMs) > 0)
            tTimeout = TimeSpan.FromMilliseconds(Int(xMs));
        else if (hOpt.TryGetValue(HB_CURLOPT_TIMEOUT, out object? xSec) && Int(xSec) > 0)
            tTimeout = TimeSpan.FromSeconds(Int(xSec));

        using var oHandler = new HttpClientHandler
        {
            AllowAutoRedirect = hOpt.TryGetValue(HB_CURLOPT_FOLLOWLOCATION, out object? xFollow) && Truth(xFollow),
        };
        if (hOpt.TryGetValue(HB_CURLOPT_NOPROXY, out object? xNoProxy) && xNoProxy is string cNoProxy && cNoProxy == "*")
            oHandler.UseProxy = false;
        using var oClient = new HttpClient(oHandler) { Timeout = tTimeout };

        bool lPost = hOpt.ContainsKey(HB_CURLOPT_POSTFIELDS) ||
                     (hOpt.TryGetValue(HB_CURLOPT_POST, out object? xPost) && Truth(xPost));
        using var oRequest = new HttpRequestMessage(lPost ? HttpMethod.Post : HttpMethod.Get, oUri);
        if (lPost)
            oRequest.Content = new ByteArrayContent(
                HbBytes.Of(hOpt.TryGetValue(HB_CURLOPT_POSTFIELDS, out object? xBody) ? xBody as string : ""));
        if (hOpt.TryGetValue(HB_CURLOPT_HTTPHEADER, out object? xHeaders) && xHeaders is System.Collections.IEnumerable aHeaders
            && xHeaders is not string)
        {
            foreach (object? xHeader in aHeaders)
            {
                if (xHeader is not string cHeader)
                    continue;
                int iColon = cHeader.IndexOf(':');
                if (iColon <= 0)
                    continue;
                string cName = cHeader.Substring(0, iColon).Trim();
                string cValue = cHeader.Substring(iColon + 1).Trim();
                if (!oRequest.Headers.TryAddWithoutValidation(cName, cValue))
                    oRequest.Content?.Headers.TryAddWithoutValidation(cName, cValue);
            }
        }
        if (lPost && oRequest.Content!.Headers.ContentType is null)
            oRequest.Content.Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");

        try
        {
            using HttpResponseMessage oResponse = oClient.Send(oRequest, HttpCompletionOption.ResponseContentRead);
            oEasy.ResponseCode = (long)oResponse.StatusCode;
            oEasy.EffectiveUrl = oResponse.RequestMessage?.RequestUri?.ToString() ?? cUrl;
            using (var oStream = new MemoryStream())
            {
                oResponse.Content.ReadAsStream().CopyTo(oStream);
                oEasy.Body = oStream.ToArray();
            }
            if (hOpt.TryGetValue(HB_CURLOPT_FAILONERROR, out object? xFail) && Truth(xFail) && oEasy.ResponseCode >= 400)
                return HB_CURLE_HTTP_RETURNED_ERROR;
            if (hOpt.TryGetValue(HB_CURLOPT_DL_FILE_SETUP, out object? xFile) && xFile is string cFile)
                File.WriteAllBytes(cFile, oEasy.Body);
            return HB_CURLE_OK;
        }
        catch (TaskCanceledException)
        {
            return HB_CURLE_OPERATION_TIMEDOUT;
        }
        catch (HttpRequestException e)
        {
            if (e.InnerException is SocketException oSocket)
                return oSocket.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain
                    ? HB_CURLE_COULDNT_RESOLVE_HOST
                    : HB_CURLE_COULDNT_CONNECT;
            return e.InnerException is IOException ? HB_CURLE_SEND_ERROR : HB_CURLE_COULDNT_CONNECT;
        }
    }

    // curl_easy_strerror( <nCode> ): libcurl's text for a CURLE code
    public static string curl_easy_strerror(object? nCode) => Convert.ToInt64(nCode, CultureInfo.InvariantCulture) switch
    {
        HB_CURLE_OK => "No error",
        HB_CURLE_UNSUPPORTED_PROTOCOL => "Unsupported protocol",
        HB_CURLE_URL_MALFORMAT => "URL using bad/illegal format or missing URL",
        HB_CURLE_COULDNT_RESOLVE_HOST => "Couldn't resolve host name",
        HB_CURLE_COULDNT_CONNECT => "Couldn't connect to server",
        HB_CURLE_HTTP_RETURNED_ERROR => "HTTP response code said error",
        HB_CURLE_OPERATION_TIMEDOUT => "Timeout was reached",
        HB_CURLE_SEND_ERROR => "Failed sending data to the peer",
        long n => "Unknown error " + n.ToString(CultureInfo.InvariantCulture),
    };

    // curl_easy_getinfo( <pCurl>, <nInfo>, [@<nError>] ): what the last
    // transfer left; the third argument takes HB_CURLE_OK
    public static dynamic curl_easy_getinfo(object? pCurl, object? nInfo)
    {
        HbCurlEasy oEasy = Easy(pCurl, "CURL_EASY_GETINFO");
        return Int(nInfo) switch
        {
            HB_CURLINFO_RESPONSE_CODE => (decimal)oEasy.ResponseCode,
            HB_CURLINFO_EFFECTIVE_URL => oEasy.EffectiveUrl,
            _ => 0m,
        };
    }

    public static dynamic curl_easy_getinfo<T2>(object? pCurl, object? nInfo, ref T2 nError)
    {
        dynamic xInfo = curl_easy_getinfo(pCurl, nInfo);
        if (typeof(T2) == typeof(object))
            nError = (T2)(object)0m;
        else if (typeof(T2) != typeof(string))
            nError = (T2)Convert.ChangeType(0, typeof(T2), CultureInfo.InvariantCulture);
        return xInfo;
    }
}
