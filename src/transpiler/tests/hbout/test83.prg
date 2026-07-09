#include "astype.ch"
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

   DATA nNo AS NUMERIC
   DATA nClerkNo AS NUMERIC
   DATA cName AS STRING
   DATA lDisable AS LOGICAL
   DATA nRate AS NUMERIC
   DATA dSince AS DATE
   METHOD New()

ENDCLASS

METHOD New() AS OBJECT CLASS TestOrmRec
RETURN Self

FUNCTION TestDeptDef( cPath AS STRING ) AS ARRAY
RETURN {{"Dept", "DEPT"}, IIF(cPath != NIL, cPath, ""), {}, {}, {=>}, 1}

FUNCTION ConstructORMTable( aFileDefinition AS ARRAY, lReadOnly AS LOGICAL, lShared AS LOGICAL ) AS OBJECT
RETURN TestOrmRec():New()

PROCEDURE Main()
   LOCAL oDept := ConstructORMTable(TestDeptDef()) AS OBJECT
   LOCAL nNext AS NUMERIC

   oDept:nNo := 5
   // 10 digits — needs long in C#
   oDept:nClerkNo := 9999999999
   oDept:cName := "Fred"
   oDept:lDisable := .F.
   oDept:nRate := 12.34
   oDept:dSince := 0d20260315

   nNext := oDept:nNo + 1

   QOut("no=" + LTrim(Str(oDept:nNo)))
   QOut("next=" + LTrim(Str(nNext)))
   QOut("clerk=" + LTrim(Str(oDept:nClerkNo)))
   QOut("name=" + oDept:cName)
   QOut("dis=" + IIF(oDept:lDisable, "T", "F"))
   QOut("rate=" + LTrim(Str(oDept:nRate, 6, 2)))
   QOut("since=" + DToS(oDept:dSince))
RETURN
