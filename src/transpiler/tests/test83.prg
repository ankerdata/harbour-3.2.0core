// Test 83: ORM def-class field typing (--fieldtypes / fieldtypes.tsv).
//
// `ConstructORMTable(TestDeptDef())` names its def class lexically; with
// tests/orm/fieldtypes.tsv loaded the receiver types as the generated
// model class (tests/orm/OrmFixture.cs) and emits `new TestDeptDef(...)`,
// so every field access below is a typed C# member: nNo int, nClerkNo
// long (len >= 10 overflows Int32), nRate decimal, cName string,
// lDisable bool, dSince DateOnly.
//
// Guarded two ways. Mistyping is a COMPILE error in buildcs.sh: a wrong
// field type in the model (or a wrongly-typed local inferred from a
// field read) will not convert. And the RUNTIME output must match the
// Harbour (.prg) run, where ConstructORMTable is the plain dynamic
// object below — catching wrong-but-compilable typing.
//
// The Harbour-side stand-in class is deliberately NOT named
// TestDeptDef: in the C# build that name belongs to the generated
// model; in the Harbour run the def name only appears as the factory
// function, exactly like the real orm.prg flow.

#include "hbclass.ch"

CLASS TestOrmRec
   VAR nNo
   VAR nClerkNo
   VAR cName
   VAR lDisable
   VAR nRate
   VAR dSince
   METHOD New() CONSTRUCTOR
ENDCLASS

METHOD New() CLASS TestOrmRec
RETURN Self

FUNCTION TestDeptDef( cPath )
RETURN { { "Dept", "DEPT" }, iif( cPath != nil, cPath, "" ), {}, {}, { => }, 1 }

FUNCTION ConstructORMTable( aFileDefinition, lReadOnly, lShared )
RETURN TestOrmRec():New()

PROCEDURE Main()
   LOCAL oDept := ConstructORMTable( TestDeptDef() )
   LOCAL nNext

   oDept:nNo      := 5
   oDept:nClerkNo := 9999999999      // 10 digits — needs long in C#
   oDept:cName    := "Fred"
   oDept:lDisable := .F.
   oDept:nRate    := 12.34
   oDept:dSince   := 0d20260315

   nNext := oDept:nNo + 1

   ? "no="    + LTrim( Str( oDept:nNo ) )
   ? "next="  + LTrim( Str( nNext ) )
   ? "clerk=" + LTrim( Str( oDept:nClerkNo ) )
   ? "name="  + oDept:cName
   ? "dis="   + iif( oDept:lDisable, "T", "F" )
   ? "rate="  + LTrim( Str( oDept:nRate, 6, 2 ) )
   ? "since=" + DToS( oDept:dSince )
RETURN
