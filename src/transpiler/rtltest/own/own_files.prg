/*
 * Our tests for the file functions hbtest does not cover. hbtest's
 * rt_file tests FErase only on a file that is not there; EasiPOS calls it
 * 61 times, mostly on files it has just written, and several callers test
 * the result (`FErase( cFile ) != -1`). Written as hbtest writes its
 * assertions; Harbour runs them first, so every expected value here is
 * Harbour's own. The program runs in a scratch folder of its own.
 */

#include "rt_main.ch"

PROCEDURE Main_FILES()

   LOCAL cFile := "_rtl_ferase.tmp"

   /* FErase: 0, or -1 with FError() set to the OS error */
   FClose( FCreate( cFile ) )
   HBTEST File( cFile )                         IS .T.
   HBTEST FErase( cFile )                       IS 0
   HBTEST FError()                              IS 0
   HBTEST File( cFile )                         IS .F.
   HBTEST FErase( cFile )                       IS -1
   HBTEST FError()                              IS 2
   HBTEST FErase( "_rtl_nodir\x.tmp" )          IS -1
   HBTEST FError()                              IS 3
   HBTEST FErase( "." )                         IS -1
   HBTEST FError()                              IS 5
   HBTEST FErase( "" )                          IS -1
   HBTEST FError()                              IS 3

   RETURN
