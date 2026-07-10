#include "astype.ch"
// Test 84: Harbour float-division semantics over C#-integral operands.
//
// Harbour `/` is ALWAYS float: 7/2 == 3.5. Every int source the C#
// emitter produces — INTEGER locals (Pass 2.5 index-shaped candidacy),
// int #defines, ORM def-class int fields (tests/orm/fieldtypes.tsv) —
// would truncate under C# int/int division. gencsharp forces decimal
// division by casting the left operand ((decimal)(a) / b) when BOTH
// operands are statically C#-integral; a decimal on either side stays
// cast-free.
//
// Guarded by runtime parity: the Harbour run computes with plain
// numerics, so any C#-side truncation diverges the output. `%` needs
// no cast (int%int is exact) and is pinned to prove we don't over-cast.

#include "hbclass.ch"

#define SEVEN 7

CLASS TestOrmRec84

   DATA nNo AS NUMERIC
   METHOD New()

ENDCLASS

METHOD New() AS OBJECT CLASS TestOrmRec84
RETURN Self

FUNCTION TestDeptDef( cPath AS STRING ) AS ARRAY
RETURN {{"Dept", "DEPT"}, IIF(cPath != NIL, cPath, ""), {}, {}, {=>}, 1}

FUNCTION ConstructORMTable( aFileDefinition AS ARRAY, lReadOnly AS LOGICAL, lShared AS LOGICAL ) AS OBJECT
RETURN TestOrmRec84():New()

PROCEDURE Main()
   LOCAL aList := {10, 20, 30} AS ARRAY
   LOCAL i := 3 AS NUMERIC
   LOCAL oDept := ConstructORMTable(TestDeptDef()) AS OBJECT

   oDept:nNo := 5

   // Explicit width/decimals — Harbour's SET DECIMALS default renders
   // division results as 1.50 where C# Str gives 1.5; the semantics
   // under test are the VALUES, so pin the format on both sides.
   // 30 — index use makes i an int
   QOut(LTrim(Str(aList[i], 4, 0)))
   // 1.50  int local / int literal
   QOut(LTrim(Str(i / 2, 8, 2)))
   // 3.50  int define / int literal
   QOut(LTrim(Str(SEVEN / 2, 8, 2)))
   // 2.00  int arithmetic operand
   QOut(LTrim(Str((i + 1) / 2, 8, 2)))
   // 2.50  ORM int field / literal
   QOut(LTrim(Str(oDept:nNo / 2, 8, 2)))
   // 1.40  define / field
   QOut(LTrim(Str(SEVEN / oDept:nNo, 8, 2)))
   // 1     mod is exact on ints
   QOut(LTrim(Str(i % 2, 4, 0)))
   // 1.50  decimal side, no cast needed
   QOut(LTrim(Str(i / 2.0, 8, 2)))
RETURN
