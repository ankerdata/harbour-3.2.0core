#include "astype.ch"
#include "hbclass.ch"

CLASS Person INHERIT HBObject


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
RETURN Self


METHOD FullName() AS STRING CLASS Person
RETURN ::cName


METHOD _FullName( cVal AS STRING ) AS STRING CLASS Person
   ::cName := cVal
RETURN cVal


METHOD InternalCalc() AS NUMERIC CLASS Person
RETURN ::nAge * 2


FUNCTION Main() AS USUAL

   LOCAL oPerson := Person():New() AS OBJECT

   oPerson:SetAge(25)

RETURN oPerson:FullName

