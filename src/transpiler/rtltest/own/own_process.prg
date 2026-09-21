/*
 * Our tests for the process, environment and console functions (family
 * 8), which hbtest barely touches: the command line EasiPOS parses at
 * startup, the environment, the exit code, child processes, the few
 * keyboard and screen calls the test program makes, DiskChange(), and
 * Set()'s rule that a SET only takes a value of its own type.
 *
 * Written as hbtest writes its assertions; Harbour runs them first, so
 * every expected value here is Harbour's own. The harness starts each
 * program with no arguments and no keys waiting.
 *
 * Version() and the call stack name what is running, and the C# names
 * itself rather than the Harbour it came from (Alex, 2026-09-21: once the
 * port is the product the Harbour code is gone), so here they are held
 * to their shape only.
 */

#include "rt_main.ch"
#include "set.ch"

PROCEDURE Main_PROCESS()

   /* The command line */
   HBTEST hb_argc()                                   IS 0
   HBTEST hb_argv( 1 )                                IS ""
   HBTEST hb_argv( -1 )                               IS ""
   HBTEST hb_argv( 0 ) == hb_argv()                   IS .T.
   HBTEST hb_argv( 0 ) == ""                          IS .F.
   HBTEST Lower( Right( hb_argv( 0 ), 4 ) )           IS ".exe"

   /* The environment. Windows names ignore case. */
   HBTEST hb_GetEnv( "RTL_NOT_SET_7Q" )               IS ""
   HBTEST hb_GetEnv( "RTL_NOT_SET_7Q", "dflt" )       IS "dflt"
   HBTEST hb_GetEnv( "" )                             IS ""
   HBTEST hb_GetEnv( "", "dflt" )                     IS "dflt"
   HBTEST hb_GetEnv( "PATH" ) == ""                   IS .F.
   HBTEST hb_GetEnv( "path" ) == hb_GetEnv( "PATH" )  IS .T.

   /* ErrorLevel(): the old value back, the new one kept; left at 0 */
   HBTEST ErrorLevel()                                IS 0
   HBTEST ErrorLevel( 3 )                             IS 0
   HBTEST ErrorLevel()                                IS 3
   HBTEST ErrorLevel( 0 )                             IS 3

   /* Child processes: hb_run() through the shell, hb_processRun() without
      one; the exit code either way, -1 when it cannot start */
   HBTEST hb_run( "exit 3" )                          IS 3
   HBTEST hb_run( "exit 0" )                          IS 0
   HBTEST hb_processRun( "cmd /c exit 5" )            IS 5
   HBTEST hb_processRun( "_rtl_no_such_program_.exe" ) IS -1
   HBTEST FError()                                    IS 2

   /* These return nothing; they only have to run */
   hb_idleSleep( 0.01 )
   hb_gcAll()
   hb_gcAll( .F. )
   OutStd()
   OutErr()

   /* The keyboard, which only the test program reads (Alex: Inkey() in
      EasiPOS itself would be a code smell). With no console to read,
      Harbour's console driver reports 13 here, so only the type is held. */
   HBTEST ValType( Inkey() )                          IS "N"
   HBTEST hb_keyStd( 27 )                             IS 27
   HBTEST hb_keyStd( 65 )                             IS 65

   /* DiskChange(): the current drive is always there */
   HBTEST DiskChange( hb_CurDrive() )                 IS .T.
   HBTEST DiskChange( "" )                            IS .F.
   HBTEST DiskChange( "1" )                           IS .F.

   /* What is running, and where */
   HBTEST Version() == ""                             IS .F.
   HBTEST ProcName() == ""                            IS .F.
   HBTEST ProcLine() > 0                              IS .T.
   HBTEST ProcFile() == ""                            IS .F.
   HBTEST ProcName( 1000 )                            IS ""
   HBTEST ProcLine( 1000 )                            IS 0
   HBTEST ProcFile( 1000 )                            IS ""

   /* Set(): a SET takes a value of its own type; a logical one takes
      "ON" / "OFF" too, by their first letters */
   HBTEST Set( _SET_CONFIRM )                         IS .F.
   HBTEST Set( _SET_CONFIRM, "ON" )                   IS .F.
   HBTEST Set( _SET_CONFIRM )                         IS .T.
   HBTEST Set( _SET_CONFIRM, "offline" )              IS .T.
   HBTEST Set( _SET_CONFIRM, "maybe" )                IS .F.
   HBTEST Set( _SET_CONFIRM, 1 )                      IS .F.
   HBTEST Set( _SET_CONFIRM )                         IS .F.
   HBTEST Set( _SET_MARGIN, 5 )                       IS 0
   HBTEST Set( _SET_MARGIN, "x" )                     IS 5
   HBTEST Set( _SET_MARGIN, 0 )                       IS 5
   HBTEST Set( _SET_DELIMCHARS, 3 )                   IS "::"
   HBTEST Set( _SET_DELIMCHARS )                      IS "::"
   HBTEST Set( 999 )                                  IS NIL

   RETURN
