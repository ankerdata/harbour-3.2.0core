/*
 * Our tests for the file and directory functions. hbtest's rt_file takes
 * the handle functions through one file and stops there: nothing covers
 * Directory(), File()'s wildcards and SET PATH search, the memo
 * functions' EOF character, the name and path helpers, or the error codes
 * EasiPOS reads (FError() is called 172 times, and adtio.prg retries on 5
 * and 32). Byte fidelity matters as much: a Harbour string is bytes, and
 * what a file carries is Latin-1 here.
 *
 * Written as hbtest writes its assertions; Harbour runs them first, so
 * every expected value here is Harbour's own. The program runs in a
 * scratch folder of its own and leaves it re-runnable: nothing it makes
 * stops a second run.
 *
 * Where an assertion is only a comparison of a value read back through a
 * reference argument, it is wrapped in AllTrim() so that the assertion
 * still has a library function as its subject.
 */

#include "rt_main.ch"
#include "fileio.ch"
#include "directry.ch"

PROCEDURE Main_FILES()

   LOCAL cFile := "_rtl_ferase.tmp"
   LOCAL cBuf := Space( 10 )
   LOCAL cName := "", cPath := "", cExt := ""
   LOCAL nHandle, nShared

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

   /* Share modes. FCreate() opens exclusively, hb_FCreate() the way the
      caller asks — the trace and audit files EasiPOS keeps open. */
   nHandle := FCreate( "_excl.tmp" )
   HBTEST FOpen( "_excl.tmp", FO_READ )         IS -1
   HBTEST FError()                              IS 32
   HBTEST FClose( nHandle )                     IS .T.
   HBTEST hb_FCreate( "_share.tmp", FC_NORMAL, FO_READWRITE + FO_SHARED ) > 0   IS .T.
   HBTEST FError()                              IS 0
   nShared := FOpen( "_share.tmp", FO_READ )
   HBTEST FError()                              IS 0
   HBTEST FClose( nShared )                     IS .T.
   HBTEST FOpen( "_nothere.tmp" )               IS -1
   HBTEST FError()                              IS 2
   HBTEST FCreate( "_nodir\x.tmp" )             IS -1
   HBTEST FError()                              IS 3

   /* A string is bytes: what goes out comes back, Chr( 0 ) and the high
      half included */
   nHandle := FCreate( "_bytes.bin" )
   HBTEST FWrite( nHandle, Chr( 200 ) + Chr( 0 ) + "Z" )   IS 3
   HBTEST FSeek( nHandle, 0 )                              IS 0
   HBTEST FRead( nHandle, @cBuf, 3 )                       IS 3
   HBTEST Left( cBuf, 3 )                                  IS Chr( 200 ) + Chr( 0 ) + "Z"
   HBTEST Len( cBuf )                                      IS 10
   HBTEST FSeek( nHandle, 0, FS_END )                      IS 3
   HBTEST FClose( nHandle )                                IS .T.

   /* FRename */
   FErase( "_ren_b.txt" )
   FClose( FCreate( "_ren_a.txt" ) )
   HBTEST FRename( "_ren_a.txt", "_ren_b.txt" )            IS 0
   HBTEST FError()                                         IS 0
   HBTEST File( "_ren_b.txt" )                             IS .T.
   HBTEST FRename( "_ren_a.txt", "_ren_c.txt" )            IS -1
   HBTEST FError()                                         IS 2
   HBTEST FRename( "_ren_b.txt", "_nodir\x.txt" )          IS -1
   HBTEST FError()                                         IS 3

   /* Directory(): normal and read-only files always, hidden and system
      and directories only when asked, and "*.*" added to a spec that ends
      in a separator — which is how "." and ".." come back. */
   FClose( FCreate( "_dir_a.001" ) )
   FClose( FCreate( "_dir_b.002" ) )
   FErase( "_dir_h.003" )
   FClose( FCreate( "_dir_h.003", FC_HIDDEN ) )
   hb_DirBuild( "_dir_sub" )
   FErase( "_dir_sub\_inpath.txt" )     /* left by an earlier run */
   HBTEST Directory( "_dir_a.001" )[ 1 ][ F_NAME ]         IS "_dir_a.001"
   HBTEST Directory( "_dir_a.001" )[ 1 ][ F_SIZE ]         IS 0
   HBTEST Directory( "_dir_a.001" )[ 1 ][ F_ATTR ]         IS "A"
   HBTEST Directory( "_dir_a.001" )[ 1 ][ F_DATE ]         IS Date()
   HBTEST Len( Directory( "_dir_a.001" )[ 1 ][ F_TIME ] )  IS 8
   HBTEST Len( Directory( "_dir_*.*" ) )                   IS 2
   HBTEST Len( Directory( "_dir_*.*", "H" ) )              IS 3
   HBTEST Directory( "_dir_h.003", "H" )[ 1 ][ F_ATTR ]    IS "HA"
   HBTEST Len( Directory( "_dir_*.*", "D" ) )              IS 3
   HBTEST Directory( "_dir_sub", "D" )[ 1 ][ F_ATTR ]      IS "D"
   HBTEST Len( Directory( "_dir_sub\", "D" ) )             IS 2
   HBTEST Len( Directory( "_dir_sub\" ) )                  IS 0
   HBTEST Len( Directory( "_nodir\*.*" ) )                 IS 0
   HBTEST Directory( "_dir_b.002" )[ 1 ][ F_NAME ]         IS "_dir_b.002"

   /* File(): wildcards, and only a plain file answers — it searches with
      HB_FA_ALL, which is 0, so a directory and a hidden file are both
      filtered out. A name with no path of its own is looked for along
      SET PATH. */
   HBTEST File( "_dir_*.001" )                             IS .T.
   HBTEST File( "_dir_*.999" )                             IS .F.
   HBTEST File( "_dir_sub" )                               IS .F.
   HBTEST File( "_dir_h.003" )                             IS .F.
   HBTEST File( "_dir_sub\" )                              IS .F.
   HBTEST File( "" )                                       IS .F.
   FClose( FCreate( "_dir_sub\_inpath.txt" ) )
   HBTEST File( "_inpath.txt" )                            IS .F.
   Set( _SET_PATH, "_dir_sub" )
   HBTEST File( "_inpath.txt" )                            IS .T.
   Set( _SET_PATH, "" )
   HBTEST File( "_inpath.txt" )                            IS .F.

   /* hb_FileExists() / hb_DirExists() / the hb_vf* trio */
   HBTEST hb_FileExists( "_dir_a.001" )                    IS .T.
   HBTEST hb_FileExists( "_dir_sub" )                      IS .F.
   HBTEST hb_DirExists( "_dir_sub" )                       IS .T.
   HBTEST hb_DirExists( "_dir_a.001" )                     IS .F.
   HBTEST hb_vfExists( "_dir_a.001" )                      IS .T.
   HBTEST hb_vfExists( "_nothere.tmp" )                    IS .F.
   /* hb_fsFileExists() only reads the attributes and records no failure,
      so hb_vfExists() leaves FError() showing the last real operation's */
   HBTEST FError()                                         IS 0
   HBTEST hb_vfSize( "_bytes.bin" )                        IS 3
   HBTEST hb_vfSize( "_nothere.tmp" )                      IS 0
   HBTEST FError()                                         IS 2
   HBTEST hb_vfErase( "_nothere.tmp" )                     IS -1
   HBTEST FError()                                         IS 2

   /* The memo functions. MemoWrit() ends the file with Chr( 26 ) and
      MemoRead() drops it again; the hb_ pair leave the bytes alone. */
   HBTEST MemoWrit( "_memo.txt", "line1" )                 IS .T.
   HBTEST hb_vfSize( "_memo.txt" )                         IS 6
   HBTEST MemoRead( "_memo.txt" )                          IS "line1"
   HBTEST hb_MemoRead( "_memo.txt" )                       IS "line1" + Chr( 26 )
   HBTEST hb_MemoWrit( "_memo.txt", Chr( 0 ) + Chr( 128 ) + Chr( 255 ) )  IS .T.
   HBTEST hb_MemoRead( "_memo.txt" )                       IS Chr( 0 ) + Chr( 128 ) + Chr( 255 )
   HBTEST Len( hb_MemoRead( "_memo.txt" ) )                IS 3
   HBTEST MemoRead( "_nothere.tmp" )                       IS ""
   HBTEST hb_MemoWrit( "_dir_sub", "x" )                   IS .F.

   /* Directories: hb_DirBuild() makes the whole chain and says .T. for
      one that is already there; MakeDir() takes only one step. */
   HBTEST hb_DirBuild( "_b1\_b2\_b3" )                     IS .T.
   HBTEST hb_DirExists( "_b1\_b2\_b3" )                    IS .T.
   HBTEST hb_DirBuild( "_b1\_b2\_b3" )                     IS .T.
   HBTEST hb_DirBuild( "_dir_a.001\x" )                    IS .F.
   HBTEST MakeDir( "_b1" )                                 IS 5
   HBTEST MakeDir( "_nodir\_x" )                           IS 3
   HBTEST DirChange( "_b1\_b2" )                           IS 0
   HBTEST Right( CurDir(), 3 )                             IS "_b2"
   HBTEST Right( hb_cwd(), 4 )                             IS "_b2\"
   HBTEST CurDir() $ hb_cwd()                              IS .T.
   HBTEST hb_cwd() == hb_DirSepAdd( hb_cwd() )             IS .T.
   HBTEST hb_CurDrive() + ":" $ hb_cwd()                   IS .T.
   HBTEST DirChange( "..\.." )                             IS 0
   HBTEST DirChange( "_nothere" )                          IS 2
   HBTEST Len( hb_CurDrive() )                             IS 1

   /* hb_DirScan(): the tree under a path, each name carrying the steps
      below it */
   HBTEST Len( hb_DirScan( "_b1", "*.*" ) )                IS 0
   HBTEST hb_DirScan( "_b1", "_b3", "D" )[ 1 ][ F_NAME ]   IS "_b2\_b3"
   HBTEST Len( hb_DirScan( "_b1", "_b3", "D" ) )           IS 1

   /* The name helpers (common/hbfsapi.c). The path is everything up to
      the last separator, and a dot that starts the name is not an
      extension. */
   HBTEST hb_FNameDir( "C:\dir\sub\file.ext" )             IS "C:\dir\sub\"
   HBTEST hb_FNameDir( "file.ext" )                        IS ""
   HBTEST hb_FNameExt( "C:\dir\sub\file.ext" )             IS ".ext"
   HBTEST hb_FNameExt( "file" )                            IS ""
   HBTEST hb_FNameExt( ".bashrc" )                         IS ""
   HBTEST hb_FNameExt( "C:\dir.v2\file" )                  IS ""
   HBTEST hb_FNameNameExt( "C:\dir\file.ext" )             IS "file.ext"
   HBTEST hb_FNameNameExt( "file.ext" )                    IS "file.ext"
   HBTEST hb_FNameExtSet( "C:\dir\file.ext", "json" )      IS "C:\dir\file.json"
   HBTEST hb_FNameExtSet( "C:\dir\file.ext", ".json" )     IS "C:\dir\file.json"
   HBTEST hb_FNameExtSet( "C:\dir\file.ext" )              IS "C:\dir\file"
   hb_FNameSplit( "C:\dir\sub\file.ext", @cPath, @cName, @cExt )
   HBTEST AllTrim( cPath )                                 IS "C:\dir\sub\"
   HBTEST AllTrim( cName )                                 IS "file"
   HBTEST AllTrim( cExt )                                  IS ".ext"
   hb_FNameSplit( "file", @cPath, @cName, @cExt )
   HBTEST AllTrim( cPath )                                 IS ""
   HBTEST AllTrim( cExt )                                  IS ""

   /* The path helpers (rtl/hbfilehi.prg) */
   HBTEST hb_ps()                                          IS "\"
   HBTEST hb_DirSepAdd( "C:\dir" )                         IS "C:\dir\"
   HBTEST hb_DirSepAdd( "C:\dir\" )                        IS "C:\dir\"
   HBTEST hb_DirSepAdd( "C:" )                             IS "C:"
   HBTEST hb_DirSepAdd( "" )                               IS ""
   HBTEST hb_PathNormalize( "C:\a\b\..\c" )                IS "C:\a\c"
   HBTEST hb_PathNormalize( "C:\a\.\b" )                   IS "C:\a\b"
   HBTEST hb_PathNormalize( "a\b\..\..\c" )                IS "c"
   HBTEST hb_PathNormalize( "C:\a\b\.." )                  IS "C:\a"
   HBTEST hb_PathNormalize( "" )                           IS ""

   /* Where the program and the temporary files are */
   HBTEST Right( hb_DirBase(), 1 )                         IS "\"
   HBTEST hb_DirBase() == hb_DirSepAdd( hb_DirBase() )     IS .T.
   HBTEST hb_DirTemp() == hb_DirSepAdd( hb_DirTemp() )     IS .T.
   HBTEST hb_DirExists( hb_DirTemp() )                     IS .T.
   /* hb_FTempCreateEx() sets no FError() of its own */
   nHandle := hb_FTempCreateEx( @cName, , "_rtl_", ".tmp" )
   HBTEST hb_FileExists( cName )                           IS .T.
   HBTEST FSeek( nHandle, 0, FS_END )                      IS 0
   HBTEST Len( hb_FNameNameExt( cName ) )                  IS 15
   HBTEST hb_FNameExt( cName )                             IS ".tmp"
   HBTEST FClose( nHandle )                                IS .T.
   HBTEST FErase( cName )                                  IS 0

   /* DiskSpace() and DosError() */
   HBTEST DiskSpace( 0 ) > 0                               IS .T.
   DosError( 0 )
   HBTEST DosError()                                       IS 0
   DosError( 7 )
   HBTEST DosError()                                       IS 7
   DosError( 0 )

   RETURN
