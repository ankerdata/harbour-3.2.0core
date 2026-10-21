// Test 2: FUNCTION, FOR/NEXT, IF/ELSEIF/ELSE, DO WHILE, EXIT
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nSum = 0;
        string cResult;

        for (long i = 1; i <= 10; i++)
        {
            nSum = nSum + i;
        }

        QOut("nSum=" + Str(nSum));

        if (nSum > 50)
        {
            cResult = "big";
            QOut("cResult=" + cResult);
        }
        else if (nSum > 20)
        {
            cResult = "medium";
            QOut("cResult=" + cResult);
        }
        else
        {
            cResult = "small";
            QOut("cResult=" + cResult);
        }

        while (nSum > 0)
        {
            nSum = nSum - 10;
            if (nSum < 0)
            {
                break;
            }
        }

        QOut("nSum=" + Str(nSum));
    }
}
