#include "astype.ch"
// Test 141: typed codeblock parameters and results (plan C9).
//
// A codeblock is a C# lambda cast to a Func. Its parameters were all
// `dynamic`; now one takes what its Hungarian name says (`nValue` a
// decimal, `cWord` a string, `oParcel141` its class), and one whose name
// says nothing takes the element type of the declared array an AScan,
// AEval or ASort hands it. The result is typed where the body's is
// certain: a comparison is bool, `Upper( cWord ) + "!"` a string. A local
// initialised with a block is declared with the lambda's own signature.
// HbRuntime calls a block through reflection, which converts no number
// between long and decimal, so InvokeBlock does: an `n` parameter takes a
// number held as long (an element added from an `i` name), an `i`
// parameter one held as decimal (Val()'s). A string parameter left out is
// NIL, as in Harbour.

#include "../include/astype.ch"
#include "hbclass.ch"

CLASS Parcel141

   DATA cLabel AS STRING INIT ""
   DATA nWeight AS NUMERIC INIT 0
   METHOD New( cLabel, nWeight )

ENDCLASS

METHOD New( cLabel AS STRING, nWeight AS NUMERIC ) AS OBJECT CLASS Parcel141
   ::cLabel := cLabel
   ::nWeight := nWeight
RETURN Self

PROCEDURE Main()
   LOCAL aParcels := {} AS ARRAY OF CLASS Parcel141
   LOCAL aLoose := {Parcel141():New("Crate", 3)} AS ARRAY
   LOCAL aCounts := {} AS ARRAY
   LOCAL iCount AS NUMERIC
   LOCAL nTotal := 0 AS NUMERIC
   LOCAL iSum := 0 AS NUMERIC
   LOCAL bShout := {|cWord| Upper(cWord) + "!"} AS BLOCK
   LOCAL nPos AS NUMERIC

   AAdd(aParcels, Parcel141():New("Books", 4))
   AAdd(aParcels, Parcel141():New("Lamp", 2))
   AAdd(aParcels, Parcel141():New("Rug", 7))

   // an element of the declared array: x is a Parcel141, the result bool
   nPos := AScan(aParcels, {|x| x:nWeight > 5})
   QOut(nPos, aParcels[nPos]:cLabel)

   // ASort's two parameters, both elements
   ASort(aParcels, , , {|x, y| x:nWeight < y:nWeight})
   AEval(aParcels, {|x| QOut(x:cLabel)})

   // an o<Class> name types its parameter over an undeclared array
   QOut(AScan(aLoose, {|oParcel141| oParcel141:nWeight == 3}))

   // numbers held as long reach an n parameter
   FOR iCount := 1 TO 3
      AAdd(aCounts, iCount * 10)
   NEXT

   AEval(aCounts, {|nValue| nTotal += nValue})
   QOut(nTotal)

   // numbers held as decimal reach an i parameter
   AEval({Val("3"), Val("4")}, {|iWhole| iSum += iWhole})
   QOut(iSum)

   // a string parameter, a local's block, a parameter left out
   QOut(Eval(bShout, "hello"))
   QOut(Eval({|cFirst, cSecond| cSecond == NIL}, "only"))
RETURN
