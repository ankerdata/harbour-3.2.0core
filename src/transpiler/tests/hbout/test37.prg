#include "astype.ch"
// Test 37: class DATA INIT with trailing line comment.
//
// Copied from sharedx/transaction.prg style: a VAR with INIT value
// followed by a `//` comment on the same line. Before the fix, the
// emitter pasted the raw INIT text — comment and all — into
// `= <szInit>;`, producing `= 0 // Owner;` which made the `;` part
// of the line comment and broke C#. Same path prevented `.F.`/`.T.`/
// `NIL` from matching the canonical conversions because the trailing
// whitespace + comment meant the exact-string compare failed.

#include "hbclass.ch"

CLASS Widget

   DATA nCount AS NUMERIC INIT 0 // Owner
   DATA lEnabled AS LOGICAL INIT .T. // default on
   DATA lHidden AS LOGICAL INIT .F. // hide at boot
   DATA cName AS STRING INIT "" // caller sets

ENDCLASS

PROCEDURE Main()
   LOCAL oW := Widget():New() AS OBJECT

   QOut("count: " + Str(oW:nCount, 4))
   QOut("name:  " + "[" + oW:cName + "]")
   IF oW:lEnabled
      QOut("enabled: yes")
   ELSE
      QOut("enabled: no")
   ENDIF

   IF oW:lHidden
      QOut("hidden: yes")
   ELSE
      QOut("hidden: no")
   ENDIF

RETURN
