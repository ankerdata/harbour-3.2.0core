// W0047: a member only a subclass declares is not read off an element of
// a list of its parent, a C# List<Base47> whose elements have no such
// member. A member the list's class or an ancestor declares is read as it
// stands, and so is the subclass's member through a variable named for
// the subclass. The lines marked "quiet" must not warn.

#include "hbclass.ch"

CLASS Base47
   VAR iKind INIT 0
ENDCLASS

CLASS Wide47 INHERIT Base47
   VAR iWidth INIT 3
   METHOD Area47()
ENDCLASS

METHOD Area47() CLASS Wide47
   RETURN ::iWidth * 2

FUNCTION Use47( aLines AS ARRAY OF CLASS Base47, iPos )
   LOCAL oWide47
   LOCAL iTotal := 0
   IF aLines[ iPos ]:iKind == 1                     // quiet: Base47's own
      iTotal += aLines[ iPos ]:iWidth               // fires W0047: a member of Wide47
      iTotal += aLines[ iPos ]:Area47()             // fires W0047: a method of Wide47
      oWide47 := aLines[ iPos ]
      iTotal += oWide47:iWidth + oWide47:Area47()   // quiet: through the subclass's variable
   ENDIF
   RETURN iTotal
