#include "astype.ch"
// Test 106: a contrib library's function is routed to that library's
// class; a core Harbour function stays on HbRuntime.
//
// Every name a harbour-core .hbx lists is emitted qualified. A core name
// (include/*.hbx) goes to HbRuntime: `HbRuntime.Upper(...)`. A name a
// contrib library's .hbx lists goes to that library's class, named after
// its directory: wapi_Sleep is in contrib/hbwin/hbwin.hbx, so the call is
// `HbWin.wapi_Sleep(...)`. Each library is its own C# project under
// src/transpiler/libraries/<lib>/ and an application references it only
// once the library is taken on — until then every call to it fails the
// build at its call site, instead of compiling against a stub that
// throws. The suite compiles every library into its runtime assembly.
// The suite's Harbour build does not link hbwin, so the Harbour side
// gets a stand-in under #ifndef __HB_TRANSPILER__.

PROCEDURE Main()
   QOut("sleep=" + LTrim(Str(wapi_Sleep(1))))
   QOut("upper=" + Upper("hbwin"))
RETURN
