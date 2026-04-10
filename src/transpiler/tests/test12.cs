using System;

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
        Func<dynamic, dynamic, dynamic> bAdd = ((a, b) => a + b);

        // Compound assignment operators
        nX += 5;
        HbRuntime.QOut("nX += 5: " + HbRuntime.Str(nX, 10, 2));
        nX -= 3;
        HbRuntime.QOut("nX -= 3: " + HbRuntime.Str(nX, 10, 2));
        nX *= 2;
        HbRuntime.QOut("nX *= 2: " + HbRuntime.Str(nX, 10, 2));
        nX /= 4;
        HbRuntime.QOut("nX /= 4: " + HbRuntime.Str(nX, 10, 2));

        // Logical operators
        if (lFlag && !lOther)
        {
            cResult = "both true";
            HbRuntime.QOut("cResult=" + cResult);
        }

        if (lFlag || lOther)
        {
            cResult = "at least one";
            HbRuntime.QOut("cResult=" + cResult);
        }

        // IIF expression
        cResult = (nX > 10 ? "big" : "small");
        HbRuntime.QOut("cResult=" + cResult);

        // Nested IIF
        nVal = (nX > 100 ? 3 : (nX > 10 ? 2 : 1));
        HbRuntime.QOut("nVal=" + HbRuntime.Str(nVal, 10, 2));

        // Pre/post increment
        nX++;
        HbRuntime.QOut("nX++: " + HbRuntime.Str(nX, 10, 2));
        ++nY;
        HbRuntime.QOut("++nY: " + HbRuntime.Str(nY, 10, 2));
        nX--;
        HbRuntime.QOut("nX--: " + HbRuntime.Str(nX, 10, 2));
        --nY;
        HbRuntime.QOut("--nY: " + HbRuntime.Str(nY, 10, 2));

        // Negation
        nVal = -nX;
        HbRuntime.QOut("nVal=-nX: " + HbRuntime.Str(nVal, 10, 2));

        // NOT operator
        lFlag = !lOther;
        HbRuntime.QOut("lFlag=" + (lFlag ? ".T." : ".F."));

        // Comparison operators
        if (nX == nY)
        {
            cResult = "equal";
            HbRuntime.QOut("cResult=" + cResult);
        }

        if (nX != nY)
        {
            cResult = "not equal";
            HbRuntime.QOut("cResult=" + cResult);
        }

        if (nX < nY && nX >= 0)
        {
            cResult = "in range";
            HbRuntime.QOut("cResult=" + cResult);
        }

        return;
    }
}
