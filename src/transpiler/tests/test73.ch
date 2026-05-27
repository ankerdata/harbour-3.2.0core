// Test 73 companion header — #defines whose names appear in CLASS VAR
// INIT positions. These have to resolve via the per-source Const class
// in the inline / INIT translator, not just the AST-level emit path.

#define TEST73_BASE_PANEL    2
#define TEST73_DEFAULT_NAME  "default-name"
