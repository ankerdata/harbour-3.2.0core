// W0041 for a typed list given to a plain array: C# holds a List<dynamic>
// there, which takes no List<string> (the build fails, or through dynamic
// the run). A dynamic holds one as it is. The lines marked "quiet" must
// not warn.

STATIC saKept41s := {}

PROCEDURE Keep41s( acLines )
   LOCAL xHeld
   saKept41s := AClone( acLines )                   // fires W0041: a List<string> into a plain array
   xHeld := acLines                                 // quiet: a dynamic holds it as it is
   saKept41s := iif( xHeld == NIL, {}, AClone( acLines ) )   // fires W0041: through iif() too
   ? xHeld
RETURN

PROCEDURE Give41s( /*@*/aLines )
   aLines := AClone( saKept41s )
RETURN
