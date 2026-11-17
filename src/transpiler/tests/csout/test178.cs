// Test 178: Directory()'s entries are a record the runtime declares
// (hbclasses.tab's DirectoryEntry): `aDir[ i ][ F_NAME ]` is the entry's
// name field, a string, in a plain `a` local, a FOR EACH, a codeblock's
// parameter and an untyped parameter of a function the list is passed to.
public static partial class Program
{
    public static void Main(string[] args)
    {
        string cName = "_t178.tmp";

        hb_MemoWrit(cName, "abc");
        List<DirectoryEntry> aDir = Directory(cName);

        QOut(Len(aDir), aDir[I(1)].cName, aDir[I(1)].nSize, aDir[I(1)].dDate == Date(), Len(aDir[I(1)].cTime));
        foreach (DirectoryEntry aFile in HbEnumValues(aDir))
        {
            QOut(Upper(aFile.cName));
        }

        QOut(AScan(aDir, ((Func<DirectoryEntry, bool>)((aEntry) => aEntry.cName == cName))));
        for (long i = 1; i <= Len(aDir); i++)
        {
            QOut(Name178(aDir, i));
        }

        FErase(cName);
    }

    public static string Name178(List<DirectoryEntry> aDir = default, decimal i = default)
    {
        return Left(aDir[I(i)].cName, 5);
    }
}
