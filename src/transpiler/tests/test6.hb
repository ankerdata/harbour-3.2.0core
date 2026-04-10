#include "astype.ch"
// Test 6: CLASS inheritance, DATA/CLASSDATA, ACCESS/ASSIGN, scope modifiers
#include "hbclass.ch"

CLASS Inherited


   CLASSDATA nVersion AS NUMERIC INIT 1.0

ENDCLASS

CLASS Person INHERIT Inherited


   DATA nAge AS NUMERIC INIT 0
   DATA cName AS STRING INIT ""
   DATA dBirth AS DATE

   CLASSDATA nCount AS NUMERIC INIT 0

   ACCESS FullName
   ASSIGN FullName

   METHOD New()
   METHOD SetAge()

   PROTECTED:
   DATA cSecret AS STRING INIT "hidden" READONLY

   HIDDEN:
   METHOD InternalCalc()

ENDCLASS

METHOD New() AS OBJECT CLASS Person
RETURN Self

METHOD SetAge( nAge AS NUMERIC ) AS OBJECT CLASS Person
   ::nAge := nAge
   QOut("nAge=" + Str(::nAge))
RETURN Self

METHOD FullName() AS STRING CLASS Person
RETURN ::cName

METHOD _FullName( cVal AS STRING ) AS STRING CLASS Person
   ::cName := cVal
   QOut("cName=" + ::cName)
RETURN cVal

METHOD InternalCalc() AS NUMERIC CLASS Person
RETURN ::nAge * 2

FUNCTION Main() AS USUAL

   LOCAL oPerson := Person():New()
   QOut("oPerson created")

   oPerson:SetAge(25)
   QOut("FullName=" + oPerson:FullName)

RETURN oPerson:FullName
