// Test 131 (multi-file pair): a member inherited from a class declared in
// another file is an ordinary C# member access.
//
// A Self send to a member the class does not declare itself went through
// `((dynamic)this)` unless the ancestor declaring it was in the same file:
// the emitter looked only at the file's own classes. easipos's test suites
// inherit TestSuite (testsuite.prg), so ormtestsuite.cs sent
// ((dynamic)this).oTestAssert 505 times, and every dialog reached Dialog's
// SendDialogData() and oPOSStatus the same way. The reftab has the
// ancestor's member and method rows; the send is now `oTally131...` and
// `Bump131( 2 )`, checked by the C# compiler (test131b.prg).
//
// This file declares the parent.

#include "hbclass.ch"

CLASS Base131
   VAR nCount131 INIT 0
   METHOD Bump131( nBy )
   METHOD Label131()
ENDCLASS

METHOD Bump131( nBy ) CLASS Base131
   hb_default( @nBy, 1 )
   ::nCount131 += nBy
   RETURN Self

METHOD Label131() CLASS Base131
   RETURN "count " + hb_ntos( ::nCount131 )
