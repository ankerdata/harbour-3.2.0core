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
   VAR nNo
   METHOD New() CONSTRUCTOR
ENDCLASS

METHOD New() CLASS TestOrmRec84
RETURN Self

FUNCTION TestDeptDef( cPath )
RETURN { { "Dept", "DEPT" }, iif( cPath != nil, cPath, "" ), {}, {}, { => }, 1 }

FUNCTION ConstructORMTable( aFileDefinition, lReadOnly, lShared )
RETURN TestOrmRec84():New()

PROCEDURE Main()
   LOCAL aList := { 10, 20, 30 }
   LOCAL i := 3
   LOCAL oDept := ConstructORMTable( TestDeptDef() )

   oDept:nNo := 5

   // Explicit width/decimals — Harbour's SET DECIMALS default renders
   // division results as 1.50 where C# Str gives 1.5; the semantics
   // under test are the VALUES, so pin the format on both sides.
   ? LTrim( Str( aList[ i ], 4, 0 ) )         // 30 — index use makes i an int
   ? LTrim( Str( i / 2, 8, 2 ) )              // 1.50  int local / int literal
   ? LTrim( Str( SEVEN / 2, 8, 2 ) )          // 3.50  int define / int literal
   ? LTrim( Str( ( i + 1 ) / 2, 8, 2 ) )      // 2.00  int arithmetic operand
   ? LTrim( Str( oDept:nNo / 2, 8, 2 ) )      // 2.50  ORM int field / literal
   ? LTrim( Str( SEVEN / oDept:nNo, 8, 2 ) )  // 1.40  define / field
   ? LTrim( Str( i % 2, 4, 0 ) )              // 1     mod is exact on ints
   ? LTrim( Str( i / 2.0, 8, 2 ) )            // 1.50  decimal side, no cast needed
RETURN
