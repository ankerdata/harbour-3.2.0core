using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// Arrays: AAdd(), ASize(), AScan(), ASort() and the rest, hb_ADel() /
// hb_AIns(), and the array helpers the emitter calls.
// One part of HbRuntime; HbRuntime.cs says what the whole is.

public static partial class HbRuntime
{
    // ---- Array functions ----
    // A Harbour array is a List<dynamic> (IsHbArray, HbRuntime.cs): one
    // object every holder shares, which AAdd(), ASize() and hb_AIns() /
    // hb_ADel() grow and shrink in place, as Harbour's do. They were C#
    // arrays, which cannot: the size-changing functions took the array by
    // ref and handed back a new one, so an array held in an element or by
    // a second variable never saw the change. The functions read any
    // IList, so a typed List<T> can be a Harbour array too.

    // Array( <nElements> [, <nElements>...] ): an array of NIL with one
    // more dimension for each argument — Array( 2, 3 ) is two arrays of
    // three (vm/arrayshb.c, hb_arrayNewRagged). No argument gives NIL; a
    // negative dimension is Harbour's bound error. (Harbour gives NIL for
    // a non-numeric argument too; the signature makes that a compile error.)
    public static List<dynamic> Array(params decimal[] anElements)
    {
        if (anElements.Length == 0)
            return null;
        foreach (decimal n in anElements)
            if (Math.Truncate(n) < 0)
                throw new ArgumentOutOfRangeException(nameof(anElements), n,
                    "Bound error: array dimension (ARRAY)");
        return ArrayRagged(anElements, 0);
    }

    static List<dynamic> ArrayRagged(decimal[] anElements, int iDimension)
    {
        int n = (int)Math.Truncate(anElements[iDimension]);
        var a = new List<dynamic>(n);
        for (int i = 0; i < n; i++)
            a.Add(iDimension + 1 < anElements.Length ? ArrayRagged(anElements, iDimension + 1) : null);
        return a;
    }

    // AAdd( <aArray>, <xValue> ): xValue, added at the end of the array
    // itself. An object is an array of its instance variables in Harbour,
    // which AAdd() grows; a C# object is not an array, so it is left as it
    // is, as ASize() and ACopy() leave it. Anything else is the argument
    // error (1123).
    public static dynamic AAdd(dynamic arr, dynamic val)
    {
        if ((object)arr is IList a && IsHbArray(a))
            a.Add(val);
        else if (ValType((object)arr) != "O")
            throw new ArgumentException("Argument error (AADD, 1123)");
        return val;
    }

    // ASize( <aArray>, <nLen> ): the array itself, grown with NILs or cut
    // to nLen (a size below 0 is 0); not an array, NIL
    public static dynamic ASize(dynamic arr, decimal nLen)
    {
        if ((object)arr is not IList a || !IsHbArray(a))
            return null;
        Resize(a, SizeArg(nLen));
        return arr;
    }

    static void Resize(IList a, int n)
    {
        if (a is List<dynamic> l)
        {
            if (n < l.Count)
                l.RemoveRange(n, l.Count - n);
            else
                while (l.Count < n)
                    l.Add(null);
            return;
        }
        while (a.Count > n)
            a.RemoveAt(a.Count - 1);
        while (a.Count < n)
            a.Add(null);
    }

    static int SizeArg(decimal n) => n <= 0 ? 0 : (int)Math.Min(Math.Truncate(n), int.MaxValue);

    // A start/count argument as hb_arrayScan()/Fill()/Eval() read it: the
    // C holds it unsigned, so a negative one is huge
    static ulong SizeParam(decimal n) => unchecked((ulong)(long)Math.Truncate(n));

    // (first index, count) for a start/count pair over nLen elements, as
    // hb_arrayScan/Fill/Eval work them out: a start of 0 is 1, a count
    // past the end stops at the end
    static (long, long) StartCount(long nLen, decimal? nStart, decimal? nCount)
    {
        ulong uStart = nStart is null ? 0 : SizeParam(nStart.Value);
        ulong uFirst = uStart != 0 ? uStart - 1 : 0;
        if (uFirst >= (ulong)nLen)
            return (0, 0);
        ulong uCount = (ulong)nLen - uFirst;
        if (nCount is not null && SizeParam(nCount.Value) < uCount)
            uCount = SizeParam(nCount.Value);
        return ((long)uFirst, (long)uCount);
    }

    // AClone(): a copy with nested arrays and hashes copied too
    // (CloneNested); objects are shared. Not an array, NIL.
    public static dynamic AClone(dynamic arr) =>
        IsHbArray((object)arr)
            ? CloneNested(arr, new Dictionary<object, object>(ReferenceEqualityComparer.Instance))
            : null;

    // ATail(): the last element, NIL for an empty array or none
    public static dynamic ATail(dynamic arr) =>
        (object)arr is IList a && IsHbArray(a) && a.Count > 0 ? a[a.Count - 1] : null;

    // AScan( <a>, <xValue>|<bBlock>[, <nStart>[, <nCount>]] ) and
    // hb_AScan( ..., <lExact> ) (vm/arrays.c, hb_arrayScan). A block is
    // evaluated with each element and its index, and matches when it
    // returns .T.; a value matches an element of its own type — strings
    // exactly (by decision: no SET EXACT prefix rules), numbers and
    // logicals by value, dates by day (and time too with lExact), NIL
    // NIL; an array, hash or object only itself, and only with lExact.
    public static decimal AScan(dynamic arr, dynamic xValue, decimal? nStart = null, decimal? nCount = null) =>
        ArrayScan(arr, xValue, nStart, nCount, false);

    public static decimal hb_AScan(dynamic arr, dynamic xValue, decimal? nStart = null,
                                   decimal? nCount = null, bool lExact = false) =>
        ArrayScan(arr, xValue, nStart, nCount, lExact);

    static decimal ArrayScan(object arr, object xValue, decimal? nStart, decimal? nCount, bool fExact)
    {
        if (arr is not IList a || !IsHbArray(a))
            return 0;
        (long i, long n) = StartCount(a.Count, nStart, nCount);
        if (xValue is Delegate)
        {
            for (; n > 0 && i < a.Count; n--)
            {
                i++;
                if (Eval(xValue, a[(int)i - 1], (decimal)i) is bool l && l)
                    return i;
            }
            return 0;
        }
        for (; n > 0; n--, i++)
            if (ScanMatch(a[(int)i], xValue, fExact))
                return i + 1;
        return 0;
    }

    static bool ScanMatch(object x, object v, bool fExact)
    {
        switch (v)
        {
            case null: return x is null;
            case string s: return x is string xs && xs == s;
            case bool l: return x is bool xl && xl == l;
            case DateOnly d: return fExact ? x is DateOnly xd && xd == d : DayOf(x) is DateOnly xday && xday == d;
            case DateTime t: return fExact ? x is DateTime xt && xt == t : DayOf(x) is DateOnly tday && tday == DateOnly.FromDateTime(t);
        }
        if (IsNumeric(v))
            return IsNumeric(x) && Convert.ToDecimal(x, INV) == Convert.ToDecimal(v, INV);
        return fExact && ReferenceEquals(x, v);
    }

    static DateOnly? DayOf(object x) => x switch
    {
        DateOnly d => d,
        DateTime t => DateOnly.FromDateTime(t),
        _ => null,
    };

    // AEval( <a>, <bBlock>[, <nStart>[, <nCount>]] ): the block with each
    // element and its index; stops early if the block shrinks the array
    public static dynamic AEval(dynamic arr, dynamic block, decimal? nStart = null, decimal? nCount = null)
    {
        if ((object)arr is IList a && IsHbArray(a) && block is Delegate)
        {
            (long i, long n) = StartCount(a.Count, nStart, nCount);
            for (; n > 0 && i < a.Count; n--, i++)
                Eval(block, a[(int)i], (decimal)(i + 1));
        }
        return arr;
    }

    // ADel()/AIns(): shift the elements after (from) nPos, leaving a NIL
    // at the end (at nPos); the array keeps its length. A position of 0
    // is 1; out of range, nothing happens.
    public static dynamic ADel(dynamic arr, decimal nPos)
    {
        if ((object)arr is IList a && IsHbArray(a))
            ArrayDel(a, nPos);
        return arr;
    }

    public static dynamic AIns(dynamic arr, decimal nPos)
    {
        if ((object)arr is IList a && IsHbArray(a))
            ArrayIns(a, nPos);
        return arr;
    }

    static bool ArrayDel(IList a, decimal nPos)
    {
        long i = (long)Math.Truncate(nPos);
        if (i == 0)
            i = 1;
        if (i < 1 || i > a.Count)
            return false;
        a.RemoveAt((int)i - 1);
        a.Add(null);
        return true;
    }

    static bool ArrayIns(IList a, decimal nPos)
    {
        long i = (long)Math.Truncate(nPos);
        if (i == 0)
            i = 1;
        if (i < 1 || i > a.Count)
            return false;
        a.RemoveAt(a.Count - 1);
        a.Insert((int)i - 1, null);
        return true;
    }

    // hb_ADel( <a>, <nPos>[, <lAutoSize>] ) and hb_AIns( <a>, <nPos>[,
    // <xValue>[, <lAutoSize>]] ) (vm/arrayshb.c): ADel()/AIns() that can
    // also shrink or grow the array (lAutoSize), and set the inserted
    // element; they return the array, NIL for anything else.
    public static dynamic hb_ADel(dynamic arr, decimal nPos, bool lAutoSize = false)
    {
        if ((object)arr is not IList a || !IsHbArray(a))
            return null;
        if (ArrayDel(a, nPos) && lAutoSize)
            a.RemoveAt(a.Count - 1);
        return arr;
    }

    public static dynamic hb_AIns(dynamic arr, decimal nPos, dynamic xValue = null, bool lAutoSize = false)
    {
        if ((object)arr is not IList a || !IsHbArray(a))
            return null;
        long i = (long)Math.Truncate(nPos);
        if (i == 0)
            i = 1;
        if (lAutoSize && i >= 1 && i <= a.Count + 1)
            a.Add(null);
        if (ArrayIns(a, i) && xValue is not null)
            a[(int)i - 1] = xValue;
        return arr;
    }

    // ACopy( <aSource>, <aTarget>[, <nStart>[, <nCount>[, <nTargetPos>]]] )
    // (vm/arrays.c, hb_arrayCopy, as Harbour is built: with
    // HB_COMPAT_C53): elements copied into aTarget, which keeps its
    // length; a start past the end copies nothing. Returns aTarget; NIL
    // unless both are arrays.
    public static dynamic ACopy(dynamic aSource, dynamic aTarget, decimal? nStart = null,
                                decimal? nCount = null, decimal? nTargetPos = null)
    {
        if ((object)aSource is not IList src || !IsHbArray(src) ||
            (object)aTarget is not IList dst || !IsHbArray(dst))
            return null;
        ulong uSrcLen = (ulong)src.Count, uDstLen = (ulong)dst.Count;
        ulong uStart = nStart is not null && SizeParam(nStart.Value) >= 1 ? SizeParam(nStart.Value) : 1;
        ulong uTarget = nTargetPos is not null && SizeParam(nTargetPos.Value) >= 1 ? SizeParam(nTargetPos.Value) : 1;
        if (uStart > uSrcLen)
            return aTarget;
        ulong uCount = nCount is not null && SizeParam(nCount.Value) <= uSrcLen - uStart
            ? SizeParam(nCount.Value) : uSrcLen - uStart + 1;
        if (uDstLen == 0)
            return aTarget;
        if (uTarget > uDstLen)
            uTarget = uDstLen;
        if (uCount > uDstLen - uTarget)
            uCount = uDstLen - uTarget + 1;
        for (ulong k = 0; k < uCount; k++)
            dst[(int)(uTarget - 1 + k)] = src[(int)(uStart - 1 + k)];
        return aTarget;
    }

    // ---- Array extras ----

    // AFill( <a>, <xValue>[, <nStart>[, <nCount>]] ) (vm/arrayshb.c): a
    // count of 0 fills nothing; a negative start fills nothing, a start of
    // 0 is 1; a negative count means "to the end" from start 1 and
    // nothing from anywhere else
    public static dynamic AFill(dynamic arr, dynamic xValue, decimal? nStart = null, decimal? nCount = null)
    {
        if ((object)arr is not IList a || !IsHbArray(a))
            return arr;
        long lStart = nStart is null ? 0 : (long)Math.Truncate(nStart.Value);
        long lCount = nCount is null ? 0 : (long)Math.Truncate(nCount.Value);
        if ((nCount is not null && lCount == 0) || lStart < 0)
            return arr;
        if (lStart == 0)
            lStart = 1;
        decimal? dCount = nCount;
        if (lCount < 0)
        {
            if (lStart != 1)
                return arr;
            dCount = 0;
        }
        (long i, long n) = StartCount(a.Count, nStart is null ? null : lStart, dCount is null || dCount == 0 ? null : dCount);
        for (; n > 0; n--)
            a[(int)i++] = xValue;
        return arr;
    }

    // ASort( <a>[, <nStart>[, <nCount>[, <bOrder>]]] ) (vm/asort.c): a
    // stable merge sort, in place, of nCount elements from nStart. The
    // block gets two elements and says whether the first goes first; a
    // result that is not a logical or a number counts as .T. Without a
    // block like types compare natively (strings exactly, by decision)
    // and unlike ones by Clipper's weights. A block that shrinks the
    // array is survived as Harbour survives it: a pair no longer in the
    // array compares .F., and only the elements still there are placed.
    // Not an array, NIL.
    public static dynamic ASort(dynamic arr, decimal? nStart = null, decimal? nCount = null, dynamic bOrder = null)
    {
        if ((object)arr is not IList a || !IsHbArray(a))
            return null;
        long nLen = a.Count;
        ulong uStart = nStart is not null && SizeParam(nStart.Value) >= 1 ? SizeParam(nStart.Value) : 1;
        if (uStart > (ulong)nLen)
            return arr;
        ulong uCount = nCount is not null && SizeParam(nCount.Value) >= 1 && SizeParam(nCount.Value) <= (ulong)nLen - uStart
            ? SizeParam(nCount.Value) : (ulong)nLen - uStart + 1;
        if (uStart + uCount > (ulong)nLen)
            uCount = (ulong)nLen - uStart + 1;
        if (uCount <= 1)
            return arr;
        int iStart = (int)uStart - 1, n = (int)uCount;
        Func<int, int, bool> isLess = bOrder is Delegate
            ? (x, y) => x < a.Count && y < a.Count && Eval(bOrder, a[x], a[y]) switch
            {
                bool l => l,
                object v when IsNumeric(v) => Convert.ToDecimal(v, INV) != 0,
                _ => true,
            }
            : (x, y) => SortLess(a[x], a[y]);
        int[] mem = new int[2 * n];
        for (int k = 0; k < n; k++)
            mem[k] = iStart + k;
        int iDest = SortMerge(isLess, mem, 0, n, n) ? 0 : n;
        if (iStart + n > a.Count)
        {
            int nTo = 0;
            for (int k = 0; k < n; k++)
                if (mem[iDest + k] < a.Count)
                    mem[iDest + nTo++] = mem[iDest + k];
            n = nTo;
        }
        var sorted = new object[n];
        for (int k = 0; k < n; k++)
            sorted[k] = a[mem[iDest + k]];
        for (int k = 0; k < n; k++)
            a[iStart + k] = sorted[k];
        return arr;
    }

    // hb_arraySortDO: merge sort of the indices mem[src..src+n), using
    // mem[buf..buf+n) as the other buffer; true when the result is in src
    static bool SortMerge(Func<int, int, bool> isLess, int[] mem, int src, int buf, int nCount)
    {
        if (nCount <= 1)
            return true;
        int nCnt1 = nCount >> 1, nCnt2 = nCount - nCnt1;
        int p1 = src, p2 = src + nCnt1;
        bool fBuf1 = SortMerge(isLess, mem, p1, buf, nCnt1);
        bool fBuf2 = SortMerge(isLess, mem, p2, buf + nCnt1, nCnt2);
        int pDst;
        if (fBuf1)
            pDst = buf;
        else
        {
            pDst = src;
            p1 = buf;
        }
        if (!fBuf2)
            p2 = buf + nCnt1;
        while (nCnt1 > 0 && nCnt2 > 0)
        {
            if (isLess(mem[p2], mem[p1]))
            {
                mem[pDst++] = mem[p2++];
                nCnt2--;
            }
            else
            {
                mem[pDst++] = mem[p1++];
                nCnt1--;
            }
        }
        if (nCnt1 > 0)
            while (nCnt1-- > 0)
                mem[pDst++] = mem[p1++];
        else if (nCnt2 > 0 && fBuf1 == fBuf2)
            while (nCnt2-- > 0)
                mem[pDst++] = mem[p2++];
        return !fBuf1;
    }

    // hb_itemIsLess without a block
    static bool SortLess(object x, object y)
    {
        if (x is string sx && y is string sy)
            return string.CompareOrdinal(sx, sy) < 0;
        if (IsNumeric(x) && IsNumeric(y))
            return Convert.ToDecimal(x, INV) < Convert.ToDecimal(y, INV);
        if (x is DateTime tx && y is DateTime ty)
            return tx < ty;
        if (DayOf(x) is DateOnly dx && DayOf(y) is DateOnly dy)
            return dx < dy;
        if (x is bool lx && y is bool ly)
            return !lx && ly;
        return SortWeight(x) < SortWeight(y);
    }

    // Clipper's order across types: array/object, block, string, logical,
    // date, number, NIL (and a hash)
    static int SortWeight(object x) => x switch
    {
        null => 7,
        System.Collections.IDictionary => 7,
        Delegate => 2,
        string => 3,
        bool => 4,
        DateOnly or DateTime => 5,
        _ when IsNumeric(x) => 6,
        _ => 1,
    };
}
