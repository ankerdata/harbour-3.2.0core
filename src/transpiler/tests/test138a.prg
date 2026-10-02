// Test 138 (multi-file pair): downcasts across files, and an element's
// subclass member left uncast (plan C11).
//
// A parameter named for its class (`oPrintLine138`) keeps the class when a
// caller passes its parent. The name used to be taken only once the slot
// held the class: here the classes are in test138b.prg, which the scan
// reaches after this file, so on the first pass the name names nothing
// and the slot is a plain object; on the next, FirstTotal138's Line138
// came first and the slot became Line138, which the later subclass callers
// fit under. EasiPOS's GetPLULineColor( oTransaction, oItemTranLine )
// came out TranLine that way. Now a parent given to a name reads as the
// name's class: the slot is PrintLine138 and the parent's call casts.
//
// A member only a subclass declares, read off an element of a declared
// array (`oTill138:aLines[ i ]:nValue`, aLines AS ARRAY OF CLASS Line138),
// is written as it was before the declaration: the element is `dynamic`
// in C# (a List<dynamic> slot), so `((dynamic)...)` added nothing but
// noise, 2,516 times in EasiPOS's sale buffer.
//
// This file has the callers and the named parameter.

// the first caller passes the parent: an element of a declared array
FUNCTION FirstTotal138( oTill138 )
   LOCAL oLine := oTill138:aLines[ 1 ]
   RETURN Amount138( oLine )

// later callers pass subclasses
FUNCTION LaterTotal138( oFcnLine138, oGenLine138 )
   RETURN Amount138( oFcnLine138 ) + Amount138( oGenLine138 )

// named for the middle class, which it keeps
FUNCTION Amount138( oPrintLine138 )
   RETURN oPrintLine138:nValue

// a member only a subclass declares, read off an element: no cast
FUNCTION SumValues138( oTill138 )
   LOCAL nTotal := 0
   LOCAL iPos
   FOR iPos := 1 TO Len( oTill138:aLines )
      IF oTill138:aLines[ iPos ]:nKind != 0
         nTotal += oTill138:aLines[ iPos ]:nValue
      ENDIF
   NEXT
   RETURN nTotal
