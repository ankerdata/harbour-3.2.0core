// Test 176: handles. A `p` name that ends in a kind of handle is the
// runtime's class of it (`pSocket`, `pListenSocket` an HbSocket,
// `pWorkThread` an HbThread, `pMemDBConnection` and `pCountDBStatement`
// hbsqlit3's connection and statement), and a function says what it returns with
// `AS HANDLE OF SOCKET` (DBCONNECTION, DBSTATEMENT, THREAD, MUTEX). A mutex's
// name ends in Mutex or Lock (`pQueueMutex`, `pStateLock`). A `p` name of
// no kind stays opaque.
public static partial class Program
{
    public static void Main(string[] args)
    {
        HbSocket pSocket = Open176();
        HbThread pWorkThread = Start176(7);
        HbSqlit3.Sqlite3Db pMemDBConnection = NoDB176();
        HbSqlit3.Sqlite3Stmt pCountDBStatement = NoStmt176();
        decimal pOther = 3;
        HbMutex pQueueMutex = Mutex176();
        HbMutex pStateLock = hb_mutexCreate();

        hb_threadWait(pWorkThread, 5);
        QOut("open:", pSocket != null, Closed176(pSocket));
        HbSocket pListenSocket = Open176();
        QOut("listen:", hb_socketClose(pListenSocket));
        QOut("none:", pMemDBConnection == null, pCountDBStatement == null, pOther);
        QOut("mutex:", Locked176(pQueueMutex), Locked176(pStateLock));
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
        return hb_threadStart(FuncPtr(nameof(Work176)), iValue);
    }

    public static void Work176(long iValue = default)
    {
        QOut("thread:", iValue);
    }

    public static HbMutex Mutex176()
    {
        return hb_mutexCreate();
    }

    public static bool Locked176(HbMutex pLock = default)
    {
        bool lLocked = hb_mutexLock(pLock);

        if (lLocked)
        {
            hb_mutexUnlock(pLock);
        }

        return lLocked;
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
