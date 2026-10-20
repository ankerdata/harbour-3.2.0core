// Test 12: Expressions — IIF, codeblocks, compound assignment, logical operators
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nX = 10;
        decimal nY = 20;
        string cResult;
        bool lFlag = true;
        bool lOther = false;
        decimal nVal;
        Func<dynamic, dynamic, dynamic> bAdd = ((Func<dynamic, dynamic, dynamic>)((a, b) => a + b));

        // Compound assignment operators
        nX += 5;
        QOut("nX += 5: " + Str(nX, 10, 2));
        nX -= 3;
        QOut("nX -= 3: " + Str(nX, 10, 2));
        nX *= 2;
        QOut("nX *= 2: " + Str(nX, 10, 2));
        nX /= 4;
        QOut("nX /= 4: " + Str(nX, 10, 2));

        // Logical operators
        if (lFlag && !lOther)
        {
            cResult = "both true";
            QOut("cResult=" + cResult);
        }

        if (lFlag || lOther)
        {
            cResult = "at least one";
            QOut("cResult=" + cResult);
        }

        // IIF expression
        cResult = (nX > 10 ? "big" : "small");
        QOut("cResult=" + cResult);

        // Codeblock — declared above (`bAdd`) and exercised here so the
        // (Func<dynamic, dynamic, dynamic>) cast we now emit on every
        // non-variadic block actually runs through both Harbour baseline
        // (Eval-based) and the C# target (DLR-invoked Func).
        QOut("bAdd(3, 4)=" + Str(Eval(bAdd, 3, 4), 10, 2));

        // Nested IIF
        nVal = (nX > 100 ? 3 : (nX > 10 ? 2 : 1));
        QOut("nVal=" + Str(nVal, 10, 2));

        // Pre/post increment
        nX++;
        QOut("nX++: " + Str(nX, 10, 2));
        ++nY;
        QOut("++nY: " + Str(nY, 10, 2));
        nX--;
        QOut("nX--: " + Str(nX, 10, 2));
        --nY;
        QOut("--nY: " + Str(nY, 10, 2));

        // Negation
        nVal = -nX;
        QOut("nVal=-nX: " + Str(nVal, 10, 2));

        // NOT operator
        lFlag = !lOther;
        QOut("lFlag=" + (lFlag ? ".T." : ".F."));

        // Comparison operators
        if (nX == nY)
        {
            cResult = "equal";
            QOut("cResult=" + cResult);
        }

        if (nX != nY)
        {
            cResult = "not equal";
            QOut("cResult=" + cResult);
        }

        if (nX < nY && nX >= 0)
        {
            cResult = "in range";
            QOut("cResult=" + cResult);
        }
    }
}
