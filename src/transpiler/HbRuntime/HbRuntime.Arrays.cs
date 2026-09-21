using System;
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
    // Harbour arrays map to C# dynamic[]. Size-changing mutators (ASize,
    // AAdd) take `ref dynamic[]` so the reallocated array propagates back;
    // the transpiler inserts `ref` at the call site. Size-stable mutators
    // (AIns, ADel, AFill, ASort) shift contents in place.

    // Array( <nElements> [, <nElements>...] ): an array of NIL with one
    // more dimension for each argument — Array( 2, 3 ) is two arrays of
    // three (vm/arrayshb.c, hb_arrayNewRagged). No argument gives NIL; a
    // negative dimension is Harbour's bound error. (Harbour gives NIL for
    // a non-numeric argument too; the signature makes that a compile error.)
    public static dynamic[] Array(params decimal[] anElements)
    {
        if (anElements.Length == 0)
            return null;
        foreach (decimal n in anElements)
            if (Math.Truncate(n) < 0)
                throw new ArgumentOutOfRangeException(nameof(anElements), n,
                    "Bound error: array dimension (ARRAY)");
        return ArrayRagged(anElements, 0);
    }

    static dynamic[] ArrayRagged(decimal[] anElements, int iDimension)
    {
        var a = new dynamic[(int)anElements[iDimension]];
        if (iDimension + 1 < anElements.Length)
            for (int i = 0; i < a.Length; i++)
                a[i] = ArrayRagged(anElements, iDimension + 1);
        return a;
    }

    public static dynamic AAdd(ref dynamic[] arr, dynamic val)
    {
        int n = arr?.Length ?? 0;
        System.Array.Resize(ref arr, n + 1);
        arr[n] = val;
        return val;
    }

    // `ref dynamic` overload — used when the lvalue's static type is
    // `dynamic` (a class DATA field without a Hungarian array prefix,
    // e.g. `protected dynamic array;`). C# `ref` is invariant so a
    // `ref dynamic` arg won't bind to a `ref dynamic[]` parameter even
    // though the runtime value is an array. Build a new array, assign
    // it back through the ref.
    public static dynamic AAdd(ref dynamic arr, dynamic val)
    {
        if (arr is object[] objArr)
        {
            int n = objArr.Length;
            var tmp = new dynamic[n + 1];
            System.Array.Copy(objArr, tmp, n);
            tmp[n] = val;
            arr = tmp;
        }
        else if (arr == null)
        {
            arr = new dynamic[] { val };
        }
        return val;
    }

    // Non-ref fallback for call sites the emitter can't pin to an lvalue
    // (GETMEMBER results, array indices, method-call results). Returns
    // val to preserve Harbour's AAdd return semantic; the underlying
    // array isn't actually grown — same silent-failure mode as the
    // pre-ref-overload runtime. Real fix requires the source to be
    // restructured to assign through.
    public static dynamic AAdd(dynamic arr, dynamic val) => val;

    // ASize() (vm/arrayshb.c): a size below 0 is 0; not an array, NIL
    public static dynamic[] ASize(ref dynamic[] arr, decimal nLen)
    {
        int n = SizeArg(nLen);
        if (arr == null)
            arr = new dynamic[n];
        else
            System.Array.Resize(ref arr, n);
        return arr;
    }

    public static dynamic[] ASize(ref dynamic arr, decimal nLen)
    {
        if (arr is not object[] objArr)
            return null;
        var tmp = new dynamic[SizeArg(nLen)];
        System.Array.Copy(objArr, tmp, Math.Min(objArr.Length, tmp.Length));
        arr = tmp;
        return tmp;
    }

    // Non-ref fallback. Returns a new resized array but can't update
    // the caller's variable — caller code that does `aArr := ASize(...)`
    // still works; `ASize(GETMEMBER(...), n)` as a statement is a
    // silent no-op for the caller, matching the pre-ref behavior.
    public static dynamic[] ASize(dynamic arr, decimal nLen)
    {
        if (arr is not object[] objArr)
            return null;
        var copy = new dynamic[SizeArg(nLen)];
        System.Array.Copy(objArr, copy, Math.Min(objArr.Length, copy.Length));
        return copy;
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
        (object)arr is object[] a
            ? CloneNested(a, new Dictionary<object, object>(ReferenceEqualityComparer.Instance))
            : null;

    // ATail(): the last element, NIL for an empty array or none
    public static dynamic ATail(dynamic arr) =>
        arr is object[] a && a.Length > 0 ? a[a.Length - 1] : null;

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
        if (arr is not object[] a)
            return 0;
        (long i, long n) = StartCount(a.Length, nStart, nCount);
        if (xValue is Delegate)
        {
            for (; n > 0 && i < a.Length; n--)
            {
                i++;
                if (Eval(xValue, a[i - 1], (decimal)i) is bool l && l)
                    return i;
            }
            return 0;
        }
        for (; n > 0; n--, i++)
            if (ScanMatch(a[i], xValue, fExact))
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
        if (arr is object[] a && block is Delegate)
        {
            (long i, long n) = StartCount(a.Length, nStart, nCount);
            for (; n > 0 && i < a.Length; n--, i++)
                Eval(block, a[i], (decimal)(i + 1));
        }
        return arr;
    }

    // ADel()/AIns(): shift the elements after (from) nPos, leaving a NIL
    // at the end (at nPos); the array keeps its length. A position of 0
    // is 1; out of range, nothing happens.
    public static dynamic ADel(dynamic arr, decimal nPos)
    {
        if (arr is object[] a)
            ArrayDel(a, nPos);
        return arr;
    }

    public static dynamic AIns(dynamic arr, decimal nPos)
    {
        if (arr is object[] a)
            ArrayIns(a, nPos);
        return arr;
    }

    static bool ArrayDel(object[] a, decimal nPos)
    {
        long i = (long)Math.Truncate(nPos);
        if (i == 0)
            i = 1;
        if (i < 1 || i > a.Length)
            return false;
        System.Array.Copy(a, (int)i, a, (int)i - 1, a.Length - (int)i);
        a[^1] = null;
        return true;
    }

    static bool ArrayIns(object[] a, decimal nPos)
    {
        long i = (long)Math.Truncate(nPos);
        if (i == 0)
            i = 1;
        if (i < 1 || i > a.Length)
            return false;
        System.Array.Copy(a, (int)i - 1, a, (int)i, a.Length - (int)i);
        a[i - 1] = null;
        return true;
    }

    // hb_ADel( <a>, <nPos>[, <lAutoSize>] ) and hb_AIns( <a>, <nPos>[,
    // <xValue>[, <lAutoSize>]] ) (vm/arrayshb.c): ADel()/AIns() that can
    // also shrink or grow the array (lAutoSize), and set the inserted
    // element; they return the array, NIL for anything else. A C# array
    // cannot change length in place, so the emitter passes the array by
    // ref, as for AAdd() and ASize(): `ref dynamic[]`, or `ref dynamic` for
    // an lvalue typed dynamic (C# ref is invariant). An argument that is
    // not an lvalue (a literal, an element, a method's result) gets the
    // plain overload: the array returned is resized, but the caller's
    // array cannot follow — in Harbour it would, being the same array.
    public static dynamic[] hb_ADel(ref dynamic[] arr, decimal nPos, bool lAutoSize = false)
    {
        if (arr is not null && ArrayDel(arr, nPos) && lAutoSize)
            System.Array.Resize(ref arr, arr.Length - 1);
        return arr;
    }

    public static dynamic hb_ADel(ref dynamic arr, decimal nPos, bool lAutoSize = false)
    {
        if (arr is not object[] a)
            return null;
        dynamic[] d = a;
        return arr = hb_ADel(ref d, nPos, lAutoSize);
    }

    public static dynamic hb_ADel(dynamic arr, decimal nPos, bool lAutoSize = false)
    {
        if (arr is not object[] a)
            return null;
        dynamic[] d = a;
        return hb_ADel(ref d, nPos, lAutoSize);
    }

    public static dynamic[] hb_AIns(ref dynamic[] arr, decimal nPos, dynamic xValue = null, bool lAutoSize = false)
    {
        if (arr is null)
            return null;
        long i = (long)Math.Truncate(nPos);
        if (i == 0)
            i = 1;
        if (lAutoSize && i >= 1 && i <= arr.Length + 1)
            System.Array.Resize(ref arr, arr.Length + 1);
        if (ArrayIns(arr, i) && xValue is not null)
            arr[i - 1] = xValue;
        return arr;
    }

    public static dynamic hb_AIns(ref dynamic arr, decimal nPos, dynamic xValue = null, bool lAutoSize = false)
    {
        if (arr is not object[] a)
            return null;
        dynamic[] d = a;
        return arr = hb_AIns(ref d, nPos, xValue, lAutoSize);
    }

    public static dynamic hb_AIns(dynamic arr, decimal nPos, dynamic xValue = null, bool lAutoSize = false)
    {
        if (arr is not object[] a)
            return null;
        dynamic[] d = a;
        return hb_AIns(ref d, nPos, xValue, lAutoSize);
    }

    // ACopy( <aSource>, <aTarget>[, <nStart>[, <nCount>[, <nTargetPos>]]] )
    // (vm/arrays.c, hb_arrayCopy, as Harbour is built: with
    // HB_COMPAT_C53): elements copied into aTarget, which keeps its
    // length; a start past the end copies nothing. Returns aTarget; NIL
    // unless both are arrays.
    public static dynamic ACopy(dynamic aSource, dynamic aTarget, decimal? nStart = null,
                                decimal? nCount = null, decimal? nTargetPos = null)
    {
        if (aSource is not object[] src || aTarget is not object[] dst)
            return null;
        ulong uSrcLen = (ulong)src.Length, uDstLen = (ulong)dst.Length;
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
            dst[uTarget - 1 + k] = src[uStart - 1 + k];
        return aTarget;
    }

    // ---- Array extras ----

    // AFill( <a>, <xValue>[, <nStart>[, <nCount>]] ) (vm/arrayshb.c): a
    // count of 0 fills nothing; a negative start fills nothing, a start of
    // 0 is 1; a negative count means "to the end" from start 1 and
    // nothing from anywhere else
    public static dynamic AFill(dynamic arr, dynamic xValue, decimal? nStart = null, decimal? nCount = null)
    {
        if (arr is not object[] a)
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
        (long i, long n) = StartCount(a.Length, nStart is null ? null : lStart, dCount is null || dCount == 0 ? null : dCount);
        for (; n > 0; n--)
            a[i++] = xValue;
        return arr;
    }

    // ASort( <a>[, <nStart>[, <nCount>[, <bOrder>]]] ) (vm/asort.c): a
    // stable merge sort, in place, of nCount elements from nStart. The
    // block gets two elements and says whether the first goes first; a
    // result that is not a logical or a number counts as .T. Without a
    // block like types compare natively (strings exactly, by decision)
    // and unlike ones by Clipper's weights. Not an array, NIL.
    public static dynamic ASort(dynamic arr, decimal? nStart = null, decimal? nCount = null, dynamic bOrder = null)
    {
        if (arr is not object[] a)
            return null;
        long nLen = a.Length;
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
            ? (x, y) => Eval(bOrder, a[x], a[y]) switch
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
        var sorted = new object[n];
        for (int k = 0; k < n; k++)
            sorted[k] = a[mem[iDest + k]];
        System.Array.Copy(sorted, 0, a, iStart, n);
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
