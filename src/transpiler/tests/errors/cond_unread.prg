// W0042: a conditional asks about a name a header might define, and the
// transpiler cannot read the header (it is not on the include path), so
// it cannot answer as Harbour would.

#include "cond_unread_missing.ch"

PROCEDURE CondUnread()

#ifdef COND_UNREAD_FLAG
   ? "flag"
#endif

RETURN
