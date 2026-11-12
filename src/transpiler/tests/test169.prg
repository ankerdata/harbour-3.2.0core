// Test 169: a list of a class that stands alone is typed storage. A class
// that inherits nothing and that nothing inherits is as closed as a record,
// so AS ARRAY OF CLASS X of it is a C# List<X>: on a local, a member, a
// parameter and a function's result, its literal and AAdd() checked by the
// C#. A list of a class with a family stays a List<dynamic>, since a
// List<Child> is no List<Parent>.

#include "hbclass.ch"
#include "../include/astype.ch"

CLASS Knob169

   VAR cText AS STRING INIT ""
   VAR iCode AS INTEGER INIT 0

   METHOD New( cText, iCode ) CONSTRUCTOR

ENDCLASS

METHOD New( cText, iCode ) CLASS Knob169

   ::cText := cText
   ::iCode := iCode

RETURN Self

CLASS Panel169

   VAR aKnobs AS ARRAY OF CLASS Knob169 INIT {}

   METHOD Add( oKnob169 )
   METHOD Texts()

ENDCLASS

METHOD Add( oKnob169 ) CLASS Panel169

   AAdd( ::aKnobs, oKnob169 )

RETURN Self

METHOD Texts() CLASS Panel169

   LOCAL oKnob169
   LOCAL cTexts := ""

   FOR EACH oKnob169 IN ::aKnobs
      cTexts += oKnob169:cText + " "
   NEXT

RETURN cTexts

CLASS Shape169

   METHOD Name()

ENDCLASS

METHOD Name() CLASS Shape169

RETURN "shape"

CLASS Round169 INHERIT Shape169

   METHOD Name()

ENDCLASS

METHOD Name() CLASS Round169

RETURN "round"

PROCEDURE Main()

   LOCAL aKnobs AS ARRAY OF CLASS Knob169 := { Knob169():New( "Cancel", 0 ), Knob169():New( "OK", 1 ) }
   LOCAL aShapes AS ARRAY OF CLASS Shape169 := { Shape169():New(), Round169():New() }
   LOCAL oPanel169 := Panel169():New()
   LOCAL oKnob169
   LOCAL i

   AAdd( aKnobs, Knob169():New( "Search", 3 ) )

   FOR EACH oKnob169 IN aKnobs
      ? oKnob169:cText, oKnob169:iCode
      oPanel169:Add( oKnob169 )
   NEXT

   ? CodeTotal169( aKnobs ), Len( Yes169() ), Yes169()[ 1 ]:cText
   ? AScan( aKnobs, { | oKnob169 | oKnob169:iCode == 3 } )
   ? oPanel169:Texts(), Len( oPanel169:aKnobs )

   FOR i := 1 TO Len( aShapes )
      ? aShapes[ i ]:Name()
   NEXT

RETURN

FUNCTION CodeTotal169( aList AS ARRAY OF CLASS Knob169 )

   LOCAL i
   LOCAL iTotal := 0

   FOR i := 1 TO Len( aList )
      iTotal += aList[ i ]:iCode
   NEXT

RETURN iTotal

FUNCTION Yes169() AS ARRAY OF CLASS Knob169

RETURN { Knob169():New( "Yes", 1 ) }
