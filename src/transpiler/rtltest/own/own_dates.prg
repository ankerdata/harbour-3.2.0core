/*
 * Our tests for the date functions hbtest does not cover. hbtest tests
 * hb_SToD at length (rt_misc.prg) but never SToD, which is the same
 * function under its Clipper-compatible name and the one EasiPOS calls
 * (15 sites; hb_SToD 2). Written as hbtest writes its assertions; Harbour
 * runs them first, so every expected value here is Harbour's own.
 */

#include "rt_main.ch"

PROCEDURE Main_DATES()

   /* SToD: YYYYMMDD, anything else the empty date */
   HBTEST DToS( SToD( "19840325" ) )           IS "19840325"
   HBTEST SToD( "19840325" ) == hb_SToD( "19840325" ) IS .T.
   HBTEST Empty( SToD( "" ) )                   IS .T.
   HBTEST Empty( SToD() )                       IS .T.
   HBTEST Empty( SToD( NIL ) )                  IS .T.
   HBTEST DToS( SToD( "20240229" ) )            IS "20240229"
   HBTEST Empty( SToD( "20230229" ) )           IS .T.
   HBTEST Empty( SToD( "19000229" ) )           IS .T.
   HBTEST DToS( SToD( "20000229" ) )            IS "20000229"
   HBTEST DToS( SToD( "21001231" ) )            IS "21001231"
   HBTEST DToS( SToD( "00010101" ) )            IS "00010101"
   HBTEST DToS( SToD( "99991231" ) )            IS "99991231"
   HBTEST Empty( SToD( "2024013" ) )            IS .T.
   HBTEST DToS( SToD( "2024013199" ) )          IS "20240131"
   HBTEST Empty( SToD( "2024-01-31" ) )         IS .T.
   HBTEST Empty( SToD( "20241301" ) )           IS .T.
   HBTEST Empty( SToD( "20240100" ) )           IS .T.
   HBTEST Year( SToD( "20040101" ) )            IS 2004
   HBTEST SToD( "20040102" ) - SToD( "20040101" ) IS 1

   RETURN
