// W0037: Self sends a message neither its class nor an ancestor declares.
// Harbour raises "No exported method" when the line runs; C# routes it
// through ((dynamic)this) and throws there. easipos easizvt.prg's
// ReadCard(), ActivateCardReader() and AbortCommand() sent to ::oEasiZVT,
// a member the class calls oEasiZVTProxy. The lines marked "quiet" must
// not warn.

#include "hbclass.ch"

CLASS Device37
   VAR oProxy37
   METHOD Read37()
   METHOD Ready37()
ENDCLASS

METHOD Read37() CLASS Device37
   RETURN ::oDevice37:Read()             // fires: the member is oProxy37

METHOD Ready37() CLASS Device37
   ::Init()                              // quiet: every object answers Init
   RETURN ::oProxy37:Ready()             // quiet: declared here

CLASS Reader37 INHERIT Device37
   METHOD Use37()
ENDCLASS

METHOD Use37() CLASS Reader37
   ::Ready37()                           // quiet: inherited
   RETURN ::oProxy37                     // quiet: inherited

// a class that keeps members in its bag (::&( cName )) says nothing
CLASS Bag37
   METHOD Get37( cName )
ENDCLASS

METHOD Get37( cName ) CLASS Bag37
   ? ::nAnything37                       // quiet: a dynamic class
   RETURN ::&( cName )
