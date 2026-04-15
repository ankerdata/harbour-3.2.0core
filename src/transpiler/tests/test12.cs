using System;
using static HbRuntime;

// Test 12: Expressions — IIF, codeblocks, compound assignment, logical operators
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nX = 10;
        decimal nY = 20;
        string cResult = default;
        bool lFlag = true;
        bool lOther = false;
        decimal nVal = default;
        Func<dynamic, dynamic, dynamic> bAdd = ((a, b) => a + b);

        // Compound assignment operators
        nX += 5;
        HbRuntime.QOUT("nX += 5: " + HbRuntime.STR(nX, 10, 2));
        nX -= 3;
        HbRuntime.QOUT("nX -= 3: " + HbRuntime.STR(nX, 10, 2));
        nX *= 2;
        HbRuntime.QOUT("nX *= 2: " + HbRuntime.STR(nX, 10, 2));
        nX /= 4;
        HbRuntime.QOUT("nX /= 4: " + HbRuntime.STR(nX, 10, 2));

        // Logical operators
        if (lFlag && !lOther)
        {
            cResult = "both true";
            HbRuntime.QOUT("cResult=" + cResult);
        }

        if (lFlag || lOther)
        {
            cResult = "at least one";
            HbRuntime.QOUT("cResult=" + cResult);
        }

        // IIF expression
        cResult = (nX > 10 ? "big" : "small");
        HbRuntime.QOUT("cResult=" + cResult);

        // Nested IIF
        nVal = (nX > 100 ? 3 : (nX > 10 ? 2 : 1));
        HbRuntime.QOUT("nVal=" + HbRuntime.STR(nVal, 10, 2));

        // Pre/post increment
        nX++;
        HbRuntime.QOUT("nX++: " + HbRuntime.STR(nX, 10, 2));
        ++nY;
        HbRuntime.QOUT("++nY: " + HbRuntime.STR(nY, 10, 2));
        nX--;
        HbRuntime.QOUT("nX--: " + HbRuntime.STR(nX, 10, 2));
        --nY;
        HbRuntime.QOUT("--nY: " + HbRuntime.STR(nY, 10, 2));

        // Negation
        nVal = -nX;
        HbRuntime.QOUT("nVal=-nX: " + HbRuntime.STR(nVal, 10, 2));

        // NOT operator
        lFlag = !lOther;
        HbRuntime.QOUT("lFlag=" + (lFlag ? ".T." : ".F."));

        // Comparison operators
        if (nX == nY)
        {
            cResult = "equal";
            HbRuntime.QOUT("cResult=" + cResult);
        }

        if (nX != nY)
        {
            cResult = "not equal";
            HbRuntime.QOUT("cResult=" + cResult);
        }

        if (nX < nY && nX >= 0)
        {
            cResult = "in range";
            HbRuntime.QOUT("cResult=" + cResult);
        }

        return;
    }
}
