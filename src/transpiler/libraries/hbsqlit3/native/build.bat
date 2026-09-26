@echo off
rem Build SQLite3 Multiple Ciphers as sqlite3mc.dll for HbSqlit3, the C#
rem hbsqlit3. It is the amalgamation harbour-core vendors for Harbour's own
rem hbsqlit3 (contrib\3rd\sqlite3\sqlite3.c, 5ca87b3ffb: SQLite3 Multiple
rem Ciphers 2.3.3, SQLite 3.53.0), compiled with the flags its sqlite3.hbp
rem uses, so the C# build opens EasiPOS's encrypted databases as 9.0 does:
rem the same engine, the same default cipher (ChaCha20-Poly1305).
rem
rem One DLL per architecture, which HbSqlit3 loads by the process's:
rem   x86\sqlite3mc.dll     the transpiler suite and rtltest (HB_TEST_ARCH x86)
rem   x64\sqlite3mc.dll
rem   arm64\sqlite3mc.dll   .NET on an ARM64 machine
rem The C runtime is linked in (/MT), so the DLL needs nothing installed.
rem
rem   build.bat [x86] [x64] [arm64]      (default: all three)
setlocal

set NoDefaultCurrentDirectoryInExePath=
set "HERE=%~dp0"
set "SRC=%HERE%..\..\..\..\..\contrib\3rd\sqlite3\sqlite3.c"
set "ARCHS=%*"
if "%ARCHS%"=="" set "ARCHS=x86 x64 arm64"

if not exist "%SRC%" (
    echo ERROR: %SRC% not found
    exit /b 1
)

set "VSROOT=C:\Program Files\Microsoft Visual Studio"
set "VCVARS="
if exist "%VSROOT%\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" set "VCVARS=%VSROOT%\2022\Community\VC\Auxiliary\Build\vcvarsall.bat"
if exist "%VSROOT%\18\Community\VC\Auxiliary\Build\vcvarsall.bat" set "VCVARS=%VSROOT%\18\Community\VC\Auxiliary\Build\vcvarsall.bat"
if "%VCVARS%"=="" if exist "%VSROOT%\18\Insiders\VC\Auxiliary\Build\vcvarsall.bat" set "VCVARS=%VSROOT%\18\Insiders\VC\Auxiliary\Build\vcvarsall.bat"
if "%VCVARS%"=="" (
    echo ERROR: vcvarsall.bat not found under %VSROOT%
    exit /b 1
)

for %%A in (%ARCHS%) do (
    call :build %%A
    if errorlevel 1 exit /b 1
)
exit /b 0

rem ---- one architecture, in its own environment ----
:build
setlocal
call "%VCVARS%" %1 >nul
if errorlevel 1 (
    echo ERROR: vcvarsall %1 failed
    exit /b 1
)
if not exist "%HERE%obj\%1" mkdir "%HERE%obj\%1"
if not exist "%HERE%%1" mkdir "%HERE%%1"
echo Building %1\sqlite3mc.dll ...
cl.exe /nologo /O2 /MT /LD /W1 ^
    "/DSQLITE_API=__declspec(dllexport)" ^
    /DSQLITE_ENABLE_FTS3 /DSQLITE_ENABLE_FTS3_PARENTHESIS ^
    /DSQLITE_OMIT_DEPRECATED /DSQLITE_ENABLE_COLUMN_METADATA ^
    /DSQLITE_HAS_CODEC /DSQLITE_TEMP_STORE=2 ^
    /DHAVE_CIPHER_AES_128_CBC=1 /DHAVE_CIPHER_AES_256_CBC=1 ^
    /DHAVE_CIPHER_CHACHA20=1 /DHAVE_CIPHER_SQLCIPHER=1 /DHAVE_CIPHER_RC4=1 ^
    "%SRC%" /Fo"%HERE%obj\%1\\" /Fe"%HERE%%1\sqlite3mc.dll" ^
    /link /IMPLIB:"%HERE%obj\%1\sqlite3mc.lib" >"%HERE%obj\%1\build.log" 2>&1
if errorlevel 1 (
    type "%HERE%obj\%1\build.log"
    echo ERROR: %1 build failed
    exit /b 1
)
echo   %HERE%%1\sqlite3mc.dll
exit /b 0
