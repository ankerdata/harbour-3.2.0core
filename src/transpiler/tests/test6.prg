#include "hbclass.ch"

CLASS Inherited

   CLASSDATA nVersion INIT 1.0

ENDCLASS

CLASS Person INHERIT Inherited

   DATA nAge      AS NUMERIC  INIT 0
   DATA cName     AS STRING   INIT ""
   DATA dBirth    AS DATE

   CLASSDATA nCount AS NUMERIC INIT 0

   ACCESS FullName
   ASSIGN FullName

   EXPORTED:
   METHOD New()
   METHOD SetAge()

   PROTECTED:
   DATA cSecret   AS STRING  INIT "hidden" READONLY

   HIDDEN:
   METHOD InternalCalc()

ENDCLASS

METHOD New() CLASS Person
RETURN Self

METHOD SetAge( nAge ) CLASS Person
   ::nAge := nAge
RETURN Self

METHOD FullName() CLASS Person
RETURN ::cName

METHOD _FullName( cVal ) CLASS Person
   ::cName := cVal
RETURN cVal

METHOD InternalCalc() CLASS Person
RETURN ::nAge * 2

FUNCTION Main()

   LOCAL oPerson := Person():New()

   oPerson:SetAge( 25 )

   ? "nAge=" + Str( oPerson:nAge )
   ? "FullName=" + oPerson:FullName

RETURN oPerson:FullName
