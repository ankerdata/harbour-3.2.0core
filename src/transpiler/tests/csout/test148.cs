// Test 148: a whole-number parameter of a method whose receiver's class
// is unknown at the call.
//
// An `i` parameter is a C# long, and C# will not narrow a decimal to it on
// its own. A call to a function or through a known class has the argument
// cast already. A receiver the reftab cannot place (an `x` variable here; in
// EasiPOS a table model, `oTableIndexTable:GoTo( nRecNo )`) emitted the
// argument plain: through a typed receiver that is CS1503, through a
// dynamic one the binder throws at run time. The method row every class
// declaring the method shares now types the slot, as it names a gap.
public class RowCursor : IHbObject
{
    public decimal nLastRow = 7;

    public virtual long SeekRow(long iRow = default, bool lQuiet = false)
    {
        if (iRow > nLastRow)
        {
            iRow = (long)(nLastRow);
        }

        if (!lQuiet)
        {
            QOut("row", iRow);
        }

        return iRow;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        RowCursor xRowCursor = new RowCursor();
        decimal nWanted = 3;
        xRowCursor.SeekRow((long)(nWanted));
        QOut(xRowCursor.SeekRow((long)(nWanted + 9), true));
        return;
    }
}
