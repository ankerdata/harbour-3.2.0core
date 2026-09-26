using System;
using static HbRuntime;
using static Program;

// Test 110: a codeblock with several expressions runs every one of them.
//
// `{|| a, b, c }` evaluates a, b and c in order and yields c. The
// block's body is a chain of expressions, and the emitter wrote only
// the first: the C# of EasiPOS's Z reset
// `{ || oFixedTtl:ReadAll(), oFixedTtl:nDayValue := 0, ... }` read the
// row and never zeroed it (xzauto.prg, 14 tables), a Locate condition
// was `ReadAll()`'s result, and AADDs never ran. A block with more
// than one expression is now a statement lambda: each expression but
// the last is a statement — a call, an assignment or a step as it
// stands, a value with no effect dropped, an IIF as if/else, any other
// value discarded — and the last is returned. A `...` block runs them
// all before its `return null`. -GT writes the whole list back.
// #include "hbclass.ch"
public class BlockTally
{
    public decimal nPunches = 0;
    public decimal nDayTotal = 5;

    public virtual BlockTally Punch()
    {
        nPunches++;
        return this;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        dynamic[] aSeen = new dynamic[] {  };
        decimal nSum = 0;
        string cTrail = "";
        dynamic[] aNums = new dynamic[] { 3, 1, 2 };
        decimal nCalls = 0;
        Func<dynamic, dynamic> bPair = ((Func<dynamic, dynamic>)((x) => { HbRuntime.AAdd(ref aSeen, x); return x * 10; }));
        Func<dynamic> bThree = ((Func<dynamic>)(() => { nSum += 1; nSum += 10; return nSum; }));
        BlockTally oTally = new BlockTally();

        HbRuntime.QOut(HbRuntime.Eval(bPair, 4));
        HbRuntime.QOut(HbRuntime.Len(aSeen));
        HbRuntime.QOut(HbRuntime.Eval(bThree));

        HbRuntime.AEval(aNums, ((Func<dynamic, dynamic>)((n) => { nSum += n; return cTrail += HbRuntime.Str(n, 1); })));
        HbRuntime.QOut(nSum);
        HbRuntime.QOut(cTrail);

        HbRuntime.ASort(aNums, null, null, ((Func<dynamic, dynamic, dynamic>)((a, b) => { nCalls++; return a < b; })));
        HbRuntime.QOut(aNums[0], aNums[1], aNums[2]);
        HbRuntime.QOut(nCalls > 0);

        // a value that is only discarded; a value with no effect at all
        // (`nSum`, `nSum > 0`) is Harbour's own W0027, which -es2 rejects
        HbRuntime.QOut(HbRuntime.Eval(((Func<dynamic>)(() => { _ = nSum > 0 && BlockSay("and"); return "kept"; }))));

        // an IIF in the middle, a procedure, a member assigned through a send
        HbRuntime.AEval(aNums, ((Func<dynamic, dynamic>)((n) => { if (n > 1) { HbRuntime.AAdd(ref aSeen, n); } else { } BlockNote("n"); return n; })));
        HbRuntime.QOut(HbRuntime.Len(aSeen));
        HbRuntime.Eval(((Func<dynamic>)(() => { oTally.Punch(); return oTally.nDayTotal = 0; })));
        HbRuntime.QOut(oTally.nPunches, oTally.nDayTotal);

        // a FOR-style condition: the block's value is its last expression
        HbRuntime.QOut(BlockCount(aNums, ((Func<dynamic, dynamic>)((n) => { nCalls = 0; return n >= 2; }))));
        HbRuntime.QOut(nCalls);

        // a `...` block runs every expression too
        HbRuntime.Eval(((Func<dynamic[], dynamic>)((dynamic[] hbva) => { nSum += 100; BlockNote("v"); return null; })), 1, 2);
        HbRuntime.QOut(nSum);

        return;
    }

    public static void BlockNote(string cWhat = default)
    {
        HbRuntime.QOut("note " + cWhat);
        return;
    }

    public static bool BlockSay(string cWhat = default)
    {
        HbRuntime.QOut("say " + cWhat);
        return true;
    }

    public static decimal BlockCount(dynamic[] aItems = default, dynamic bFor = default)
    {
        decimal nHits = 0;
        decimal nItem = default;

        foreach (dynamic __hb_fe_nItem in aItems)
        {
            nItem = __hb_fe_nItem;
            if (HbRuntime.Eval(bFor, nItem))
            {
                nHits++;
            }
        }

        return nHits;
    }
}
