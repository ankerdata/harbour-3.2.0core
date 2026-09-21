/*
 * Our tests for the timestamp functions and the date forms hbtest does not
 * cover. Timestamps are recent in Harbour and hbtest has none; EasiPOS uses
 * them mostly for communications — receipts, fiscal interfaces, JSON
 * payloads — so the text and numbers must come out exactly: hb_TToC in the
 * formats EasiPOS sends, hb_StrToTS on the "YYYY-MM-DD hh:mm" it builds,
 * hb_TToD( t, @cTime, "HH:MM:SS" ), hb_TToMSec( hb_TSToUTC( ... ) ), and
 * the ORM's hb_CToD / hb_DToC ("DD/MM/YYYY"). Written as hbtest writes its
 * assertions; Harbour runs them first, so every expected value here is
 * Harbour's own. The harness runs under SET DATE "yyyy-mm-dd", SET EPOCH
 * 1900 and the default SET TIME FORMAT "hh:mm:ss.fff".
 */

#include "rt_main.ch"

PROCEDURE Main_TIMES()

   LOCAL tStamp := hb_DateTime( 2024, 1, 31, 13, 5, 7, 250 )
   LOCAL tMorning := hb_DateTime( 2024, 1, 31, 9, 4, 0, 5 )

   /* hb_TToC: the default formats, and the ones EasiPOS sends */
   HBTEST hb_TToC( tStamp )                                  IS "2024-01-31 13:05:07.250"
   HBTEST hb_TToC( tStamp, "YYYY-MM-DD", "HH:MM:SS" )        IS "2024-01-31 13:05:07"
   HBTEST hb_TToC( tStamp, "YYYYMMDD", "HHMMSS" )            IS "20240131 130507"
   HBTEST hb_TToC( tStamp, "YYYY/MM/DD", "HH:MM:SS" )        IS "2024/01/31 13:05:07"
   HBTEST hb_TToC( tStamp, "DD/MM/YYYY", "hh:mm" )           IS "31/01/2024 13:05"
   HBTEST hb_TToC( tStamp, "", "HH:MM" )                     IS "13:05"
   HBTEST hb_TToC( tMorning, "YYYY-MM-DD", "hh:mm pp" )      IS "2024-01-31  9:04 AM"
   HBTEST hb_TToC( tStamp, "YYYY-MM-DD", "hh:mm pp" )        IS "2024-01-31  1:05 PM"
   HBTEST hb_TToC( tStamp, "YYYY-MM-DD", "hh:mm:ss.ff" )     IS "2024-01-31 13:05:07.25"
   HBTEST hb_TToC( tMorning, "YYYY-MM-DD", "hh:mm:ss.f" )    IS "2024-01-31 09:04:00.0"
   HBTEST hb_TToC( tStamp, "YYYY-MM-DD", "hh:mm:ss.ffff" )   IS "2024-01-31 13:05:07.2500"

   /* hb_StrToTS: EasiPOS builds "YYYY-MM-DD hh:mm[:ss]" */
   HBTEST hb_TToC( hb_StrToTS( "2024-01-31 13:05" ) )         IS "2024-01-31 13:05:00.000"
   HBTEST hb_TToC( hb_StrToTS( "2024-01-31 13:05:07" ) )      IS "2024-01-31 13:05:07.000"
   HBTEST hb_TToC( hb_StrToTS( "2024-01-31T13:05:07" ) )      IS "2024-01-31 13:05:07.000"
   HBTEST hb_TToC( hb_StrToTS( "2024-01-31 13:05:07.5" ) )    IS "2024-01-31 13:05:07.500"
   HBTEST hb_TToC( hb_StrToTS( "2024-01-31" ) )               IS "2024-01-31 00:00:00.000"
   HBTEST hb_TToC( hb_StrToTS( " 2024-01-31 1:05 pm" ) )      IS "2024-01-31 13:05:00.000"
   HBTEST hb_TToC( hb_StrToTS( "2024-01-31 13:05Z" ) )        IS "2024-01-31 13:05:00.000"
   HBTEST hb_TToC( hb_StrToTS( "2024-01-31 13:05+02:00" ) )   IS "2024-01-31 11:05:00.000"
   HBTEST hb_TToC( hb_StrToTS( "2024-01-31 01:05+02:00" ) )   IS "2024-01-30 23:05:00.000"
   HBTEST hb_TToC( hb_StrToTS( "2024-02-30 13:05" ) )         IS "    -  -   00:00:00.000"
   HBTEST hb_TToC( hb_StrToTS( "13:05" ) )                    IS "    -  -   13:05:00.000"
   HBTEST Empty( hb_StrToTS( "not a time" ) )                 IS .T.

   /* hb_TToD, with the time as text or as seconds */
   HBTEST DToS( hb_TToD( tStamp ) )                          IS "20240131"
   HBTEST TimeOf( tStamp )                                   IS "13:05:07"
   HBTEST SecondsOf( tStamp )                                IS 47107.25

   /* hb_TToMSec: the Julian day times 86400000, plus the time */
   HBTEST hb_TToMSec( tStamp )                               IS 212573509507250     /* Julian 2460341 * 86400000 + 47107250 */
   HBTEST hb_TToMSec( hb_DateTime( 1970, 1, 1, 0, 0, 0, 0 ) ) IS 210866803200000      /* Julian 2440588 * 86400000 */
   HBTEST ValType( hb_TSToUTC( tStamp ) )                    IS "T"

   /* hb_DateTime: an impossible date or time is 0 on its own side */
   HBTEST hb_TToC( hb_DateTime( 2024, 2, 30, 10, 0, 0, 0 ), "YYYY-MM-DD", "hh:mm" ) IS "    -  -   10:00"
   HBTEST hb_TToC( hb_DateTime( 2024, 2, 29, 24, 0, 0, 0 ), "YYYY-MM-DD", "hh:mm" ) IS "2024-02-29 00:00"
   HBTEST Year( tStamp )                                     IS 2024
   HBTEST DoW( tStamp )                                      IS 4

   /* hb_SToT: YYYYMMDDhhmmss[fff] */
   HBTEST hb_TToC( hb_SToT( "20240131130507250" ) )          IS "2024-01-31 13:05:07.250"
   HBTEST hb_TToC( hb_SToT( "20240131" ) )                   IS "2024-01-31 00:00:00.000"
   HBTEST hb_TToC( hb_SToT( "2024013113" ) )                 IS "2024-01-31 13:00:00.000"

   /* hb_CToD / hb_DToC, the ORM's "DD/MM/YYYY" among them */
   HBTEST DToS( hb_CToD( "31/01/2024", "DD/MM/YYYY" ) )      IS "20240131"
   HBTEST hb_DToC( hb_SToD( "20240131" ), "DD/MM/YYYY" )     IS "31/01/2024"
   HBTEST DToS( hb_CToD( "", "DD/MM/YYYY" ) )                IS "        "
   HBTEST hb_DToC( hb_SToD( "" ), "DD/MM/YYYY" )             IS "  /  /    "
   HBTEST DToS( hb_CToD( "31/01/24", "DD/MM/YY" ) )          IS "19240131"
   HBTEST DToS( hb_CToD( "30/02/2024", "DD/MM/YYYY" ) )      IS "        "
   HBTEST DToS( hb_CToD( " 2024-1-5 extra", "YYYY-MM-DD" ) ) IS "20240105"
   HBTEST DToS( CToD( "2024-01-31" ) )                       IS "20240131"
   HBTEST hb_DToC( tStamp, "YYYY.MM.DD" )                    IS "2024.01.31"

   /* hb_Date */
   HBTEST DToS( hb_Date( 2024, 1, 31 ) )                     IS "20240131"
   HBTEST DToS( hb_Date( 2024, 2, 30 ) )                     IS "        "
   HBTEST hb_Date() == Date()                                IS .T.

   /* the clocks, by shape */
   HBTEST ValType( hb_DateTime() )                           IS "T"
   HBTEST hb_MilliSeconds() > 212544000000000             IS .T.
   HBTEST Seconds() >= 0 .AND. Seconds() < 86400             IS .T.

   RETURN

STATIC FUNCTION TimeOf( tStamp )

   LOCAL cTime := ""

   hb_TToD( tStamp, @cTime, "HH:MM:SS" )

   RETURN cTime

STATIC FUNCTION SecondsOf( tStamp )

   LOCAL nSeconds := 0

   hb_TToD( tStamp, @nSeconds )

   RETURN nSeconds
