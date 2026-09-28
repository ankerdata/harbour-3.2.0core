// Test 131 (multi-file pair): the subclass, in a file of its own, sends its
// parent's member and methods through Self (test131a.prg says why).

#include "hbclass.ch"

CLASS Child131 INHERIT Base131
   METHOD Run131()
ENDCLASS

METHOD Run131() CLASS Child131
   ::Bump131()
   ::Bump131( 2 )
   ::nCount131 += 10
   RETURN ::Label131()

PROCEDURE Main()
   ? Child131():New():Run131()
   RETURN
