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
   VAR nCount   AS NUMERIC INIT 0              // Owner
   VAR lEnabled AS LOGICAL INIT .T.            // default on
   VAR lHidden  AS LOGICAL INIT .F.            // hide at boot
   VAR cName    AS STRING  INIT ""             // caller sets
ENDCLASS

PROCEDURE Main()
   LOCAL oW := Widget():New()

   ? "count: " + Str( oW:nCount, 4 )
   ? "name:  " + "[" + oW:cName + "]"
   IF oW:lEnabled
      ? "enabled: yes"
   ELSE
      ? "enabled: no"
   ENDIF
   IF oW:lHidden
      ? "hidden: yes"
   ELSE
      ? "hidden: no"
   ENDIF

RETURN
