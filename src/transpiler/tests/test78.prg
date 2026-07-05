// Test 78: INTEGER inference (Pass 2.5 int candidacy).
//
// decimal stays the default for Harbour numerics, but a variable whose
// numeric life is purely index-shaped — array subscripts, FOR loop
// variables, integral initializers/arithmetic (literals, Len(), other
// INTEGER vars) — emits as C# `int`: subscripts drop the (int) cast,
// FOR loops declare int, and int widens to decimal implicitly at every
// consumer boundary. Division is the hard disqualifier (Harbour 5/2 is
// 2.5, C# int/int truncates): a division-fed variable stays decimal
// and, when used as an index, earns W0026 pointing at the division so
// the source can decide (wrap with int() or keep decimal). W0026 is
// expected on nHalf below.

PROCEDURE Main()

    LOCAL aItems := { "alpha", "beta", "gamma", "delta" }
    LOCAL i
    LOCAL nIdx := 1
    LOCAL nLast := Len(aItems)
    LOCAL nHalf := 4 / 2            // division → stays decimal, W0026

    for i := 1 to Len(aItems)
        ? "i=", aItems[i]
    next

    nIdx := nIdx + 2                // integral arithmetic keeps int
    ? "a=", aItems[nIdx]
    ? "b=", aItems[nLast]
    ? "c=", aItems[nHalf]           // decimal index — cast path
    ? "d=", aItems[FirstReal(aItems)]
    ? "e=", Str(nIdx * 1.5, 6, 1)   // int widens into decimal math
                                    // (explicit width: Harbour's
                                    // derived display widths for
                                    // var*literal aren't modelled)

RETURN

// Returns an always-int local: the function's return type resolves to
// INTEGER and callers may chain it straight into subscripts.
function FirstReal(aList)

    local nPos := 1

    do while nPos < Len(aList) .and. Empty(aList[nPos])
        nPos := nPos + 1
    enddo

return nPos
