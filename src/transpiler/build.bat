@echo off
rem Build the Harbour transpiler with MSVC (Windows counterpart of build.sh).
rem Mirrors call-win-make.bat: same VS detection and, by default, the same
rem x86 target, so the transpiler links against the libs that build produces.
rem
rem Overrides:
rem   HB_ARCH    vcvarsall target (default x86; e.g. arm64, amd64)
rem   HB_LIBDIR  where hbcommon.lib/hbnortl.lib live
rem              (default lib\win\msvc; ARM64 lands in lib\win\msvcarm64)
rem   HB_OUT     output binary (default bin\hbtranspiler.exe)
setlocal

set NoDefaultCurrentDirectoryInExePath=
if "%HB_ARCH%"=="" set "HB_ARCH=x86"

set "VSROOT=C:\Program Files\Microsoft Visual Studio"
if exist "%VSROOT%\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" (
    call "%VSROOT%\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" %HB_ARCH%
) else if exist "%VSROOT%\18\Community\VC\Auxiliary\Build\vcvarsall.bat" (
    call "%VSROOT%\18\Community\VC\Auxiliary\Build\vcvarsall.bat" %HB_ARCH%
) else (
    call "%VSROOT%\18\Insiders\VC\Auxiliary\Build\vcvarsall.bat" %HB_ARCH%
)

rem Repo root = this script's dir + ..\..
set "ROOT=%~dp0..\.."
pushd "%ROOT%" || exit /b 9
set "ROOT=%CD%"

if "%HB_LIBDIR%"=="" set "HB_LIBDIR=%ROOT%\lib\win\msvc"
if "%HB_OUT%"=="" set "HB_OUT=%ROOT%\bin\hbtranspiler.exe"
if not exist "%HB_LIBDIR%\hbcommon.lib" (
    echo ERROR: hbcommon.lib not found in %HB_LIBDIR%
    echo Build the compiler first: call-win-make.bat
    popd & exit /b 1
)

set "OBJDIR=%ROOT%\src\transpiler\obj\%HB_ARCH%"
if not exist "%OBJDIR%" mkdir "%OBJDIR%"   & rem per-arch: obj\<arch>
if not exist "%ROOT%\bin" mkdir "%ROOT%\bin"

set "T=src\transpiler"
set "SRC=%T%\cmdcheck.c %T%\complex.c %T%\expropta.c %T%\genhb.c %T%\gencsharp.c"
set "SRC=%SRC% %T%\genscan.c %T%\genstubs.c %T%\hbast.c %T%\hbclsparse.c %T%\hbcomp.c"
set "SRC=%SRC% %T%\hbmain.c %T%\hbtypes.c %T%\hbreftab.c %T%\hbdefinemap.c"
set "SRC=%SRC% %T%\hbfieldtypes.c %T%\hbhbxcanon.c %T%\hbfilecase.c %T%\hbfunctab.c"
set "SRC=%SRC% %T%\hbvartypes.c %T%\ppcomp.c %T%\pcodestubs.c %T%\harboury.c"
set "SRC=%SRC% %T%\src\common\expropt2.c"
set "SRC=%SRC% src\compiler\compi18n.c src\compiler\exproptb.c src\compiler\hbdbginf.c"
set "SRC=%SRC% src\compiler\hbfunchk.c src\compiler\hbgenerr.c src\compiler\hbident.c"
set "SRC=%SRC% %T%\src\compiler\hbusage.c"
set "SRC=%SRC% src\main\harbour.c %T%\src\pp\ppcore.c"

echo Compiling transpiler sources...
rem No /MD|/MT here on purpose: the Harbour libs are built without an
rem explicit CRT flag, so we take the same default and avoid a mismatch.
cl.exe /nologo /c /W1 /O2 /DHB_TRANSPILER /D_CRT_SECURE_NO_WARNINGS ^
    /I"%T%\include" /Iinclude /I"%T%" ^
    /Fo"%OBJDIR%\\" %SRC%
if errorlevel 1 (
    echo Build FAILED ^(compile^)
    popd & exit /b 1
)

echo Linking %HB_OUT%...
link.exe /nologo /OUT:"%HB_OUT%" "%OBJDIR%\*.obj" ^
    /LIBPATH:"%HB_LIBDIR%" hbnortl.lib hbcommon.lib ^
    winmm.lib kernel32.lib user32.lib ws2_32.lib iphlpapi.lib advapi32.lib gdi32.lib
if errorlevel 1 (
    echo Build FAILED ^(link^)
    popd & exit /b 1
)

echo Build successful: %HB_OUT%
rem -v needs an include path to resolve std.ch; filter to the banner.
"%HB_OUT%" -v -Iinclude 2>&1 | findstr /B /C:"Harbour Transpiler"
popd
endlocal
exit /b 0
