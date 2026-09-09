#include "astype.ch"
// Test 99: sends on a DYNAMIC receiver; ACCESS / ASSIGN INLINE bodies.
//
// A receiver the transpiler cannot type is `dynamic` in C#, and the
// DLR reads `d.Name` as a field or property: a method sent bare
// (`xBin:Heft`, legal Harbour) throws at runtime, and so does `d.Len()`
// on a property (a parameterless MESSAGE alias, test97). When the
// receiver's class is unknown the emitter asks the reftab whether the
// name is a method on every class declaring it, or a member on every
// one, and adds or drops the parentheses accordingly; a name that is
// both somewhere is left as written.
// Second: an ACCESS / ASSIGN INLINE body is now the accessor's body —
// it used to be dropped into an auto-property reading default. The
// body goes through the inline text translator (as an INLINE method's
// does), which now maps a single-colon send (`::oProxy:member`); it
// does not rebase a 1-based subscript, so those stay in a method.
#include "hbclass.ch"

CLASS Bin

   PROTECTED:
   DATA aItems AS ARRAY INIT {}
   EXPORTED:
   ACCESS Peak INIT ::Last()
   ASSIGN Peak INIT ( ::Pop(), ::Push( xNew ) )
   METHOD Len() INLINE ::Heft()
   METHOD Push( xItem )
   METHOD Pop()
   METHOD Last()
   METHOD Heft()

ENDCLASS

METHOD Push( xItem AS USUAL ) AS USUAL CLASS Bin
   AAdd(::aItems, xItem)
RETURN xItem

METHOD Pop() AS USUAL CLASS Bin
   LOCAL xLast := ::Last() AS USUAL
   ASize(::aItems, Len(::aItems) - 1)
RETURN xLast

METHOD Last() CLASS Bin
RETURN ::aItems[Len(::aItems)]

METHOD Heft() AS NUMERIC CLASS Bin
RETURN Len(::aItems)

PROCEDURE Main()
   // x: the receiver is dynamic
   LOCAL xBin := Bin():New() AS OBJECT
   xBin:Push("jar")
   xBin:Push("tin")
   QOut("heft=" + LTrim(Str(xBin:Heft)) + " len=" + LTrim(Str(xBin:Len())))
   QOut("top=" + xBin:Peak)
   xBin:Peak := "box"
   QOut("top=" + xBin:Peak + " heft=" + LTrim(Str(xBin:Heft())))
RETURN
