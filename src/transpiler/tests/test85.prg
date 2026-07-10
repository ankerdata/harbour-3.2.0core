// Test 85: def-class FAMILY BASES — polymorphic params widen to a
// typed base instead of dynamic USUAL.
//
// TestDeptDef and TestBranchDef share their core fields (the
// shared-static pattern: filedefinitions factories drawing rows from
// one `static saXxxFields` array), so tests/orm/fieldtypes.tsv carries
// a TestFamBase with the shared members and `=inherit` edges. The
// reftab class walk (hb_refTabClassParent) consults those edges, so
// `Stamp(oTable, ...)` — called with BOTH classes — converges its
// param slot to TestFamBase rather than conflict-freezing to USUAL:
// inside the helper, oTable:nNo / oTable:cName are typed members of
// the generated family base (C# long / string), not dynamic.
//
// Mirrors easipos SetTable/SetTableHeader(oORMTable), which receive
// TableIndexDef, CheckIndexDef, CSaleIndexDef and ORHeaderIndexDef —
// four classes with one shared field set.

#include "hbclass.ch"

CLASS TestOrmRec85
   VAR nNo
   VAR cName
   VAR nClerkNo
   VAR nRegion
   METHOD New() CONSTRUCTOR
ENDCLASS

METHOD New() CLASS TestOrmRec85
RETURN Self

FUNCTION TestDeptDef( cPath )
RETURN { { "Dept", "DEPT" }, iif( cPath != nil, cPath, "" ), {}, {}, { => }, 1 }

FUNCTION TestBranchDef( cPath )
RETURN { { "Branch", "BRNCH" }, iif( cPath != nil, cPath, "" ), {}, {}, { => }, 1 }

FUNCTION ConstructORMTable( aFileDefinition, lReadOnly, lShared )
RETURN TestOrmRec85():New()

// Receives both family members in BOTH slots — each param widens to
// TestFamBase and the shared-field copy stays compile-checked, typed
// member to typed member (long := long, string := string).
PROCEDURE Stamp( oTable, oFrom )
   oTable:nNo   := oFrom:nNo
   oTable:cName := oFrom:cName
RETURN

PROCEDURE Main()
   LOCAL oDept   := ConstructORMTable( TestDeptDef() )
   LOCAL oBranch := ConstructORMTable( TestBranchDef() )

   oDept:nNo   := 5
   oDept:cName := "Alpha"
   Stamp( oBranch, oDept )        // through-base copy: branch takes 5/Alpha
   oBranch:nNo := oBranch:nNo + 4 // then diverges: 9
   oBranch:cName := "Beta"
   oDept:nClerkNo  := 9999999999
   oBranch:nRegion := 42

   ? LTrim( Str( oDept:nNo, 4, 0 ) ) + oDept:cName
   ? LTrim( Str( oBranch:nNo, 4, 0 ) ) + oBranch:cName
   ? LTrim( Str( oDept:nClerkNo, 12, 0 ) )
   ? LTrim( Str( oBranch:nRegion, 4, 0 ) )
   ? LTrim( Str( oDept:nNo / 2, 8, 2 ) )    // family-base long field divides floatly
RETURN
