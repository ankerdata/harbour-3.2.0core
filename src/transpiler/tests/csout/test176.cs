// Test 176: handles. A `p` name that ends in a kind of handle is the
// runtime's class of it (`pSocket`, `pListenSocket` an HbSocket,
// `pWorkThread` an HbThread, `pMemDB` and `pCountStmt` hbsqlit3's
// connection and statement), and a function says what it returns with
// `AS HANDLE OF SOCKET` (DATABASE, STATEMENT, THREAD). A `p` name of no
// kind stays opaque.
public static partial class Program
{
    public static void Main(string[] args)
    {
        HbSocket pSocket = Open176();
        HbThread pWorkThread = Start176(7);
        HbSqlit3.Sqlite3Db pMemDB = NoDB176();
        HbSqlit3.Sqlite3Stmt pCountStmt = NoStmt176();
        decimal pOther = 3;

        hb_threadWait(pWorkThread, 5);
        QOut("open:", pSocket != null, Closed176(pSocket));
        HbSocket pListenSocket = Open176();
        QOut("listen:", hb_socketClose(pListenSocket));
        QOut("none:", pMemDB == null, pCountStmt == null, pOther);
    }

    public static HbSocket Open176()
    {
        return hb_socketOpen();
    }

    public static bool Closed176(HbSocket pSocket = default)
    {
        return hb_socketClose(pSocket);
    }

    public static HbThread Start176(long iValue = default)
    {
        return hb_threadStart(FuncPtr("Work176"), iValue);
    }

    public static void Work176(long iValue = default)
    {
        QOut("thread:", iValue);
    }

    public static HbSqlit3.Sqlite3Db NoDB176()
    {
        return null;
    }

    public static HbSqlit3.Sqlite3Stmt NoStmt176()
    {
        return null;
    }
}
