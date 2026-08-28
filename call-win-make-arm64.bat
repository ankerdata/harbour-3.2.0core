setlocal

rem Native ARM64 build of the Harbour core, for ARM64 Windows hosts.
rem Counterpart to call-win-make.bat, which targets x86 and runs under
rem emulation on ARM64 -- a natively-built transpiler scans the easipos
rem corpus in roughly half the time.
rem
rem Contribs are OFF: their vcpkg dependencies (curl, openssl, freeimage)
rem are installed for the x86-windows triplet only. Nothing in the
rem transpiler pipeline needs them -- it consumes contrib .ch HEADERS,
rem which are architecture-independent, via HARBOUR_CONTRIB.
rem
rem No 'install' target either: the transpiler links the libs straight
rem out of lib\win\msvcarm64, so the x86 install prefix is left
rem untouched and keeps serving HARBOUR_INC / HARBOUR_CONTRIB.

rem cmd refuses to run from the current directory when this is set.
set NoDefaultCurrentDirectoryInExePath=

set "VSROOT=C:\Program Files\Microsoft Visual Studio"
if exist "%VSROOT%\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" (
    call "%VSROOT%\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" arm64
) else if exist "%VSROOT%\18\Community\VC\Auxiliary\Build\vcvarsall.bat" (
    call "%VSROOT%\18\Community\VC\Auxiliary\Build\vcvarsall.bat" arm64
) else (
    call "%VSROOT%\18\Insiders\VC\Auxiliary\Build\vcvarsall.bat" arm64
)

set HB_COMPILER=msvcarm64
set HB_BUILD_MODE=c
set HB_USER_PRGFLAGS=-l-
set HB_BUILD_CONTRIBS=no

win-make.exe > build-arm64.log 2>&1
echo win-make exit=%errorlevel%  (see build-arm64.log)

endlocal
