@echo off
rem Run the RTL tests: Harbour's hbtest assertions (and ours) under
rem Harbour and as C#, compared assertion by assertion. See runrtl.py.
rem
rem   runrtl.bat [all|prg|cs|run|report] [name ...]
rem
rem Overrides, as the transpiler suite's runtests.bat:
rem   HB_ARCH        vcvarsall target (default x86, matching the Harbour
rem                  install the tests link against)
rem   HB_INSTALL     Harbour install prefix providing hbmk2 — the reference
rem   HBTRANSPILER   transpiler binary (default ..\..\..\bin\hbtranspiler.exe)
rem
rem Python is invoked directly, never through bash: Git Bash puts
rem /usr/bin ahead of MSVC on PATH and GNU link shadows link.exe.
setlocal

set NoDefaultCurrentDirectoryInExePath=
if "%HB_ARCH%"=="" set "HB_ARCH=x86"
if "%HB_INSTALL%"=="" set "HB_INSTALL=%USERPROFILE%\dev\harbour-3.2.0dev"

set "VSROOT=C:\Program Files\Microsoft Visual Studio"
if exist "%VSROOT%\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" (
    call "%VSROOT%\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" %HB_ARCH% >nul
) else if exist "%VSROOT%\18\Community\VC\Auxiliary\Build\vcvarsall.bat" (
    call "%VSROOT%\18\Community\VC\Auxiliary\Build\vcvarsall.bat" %HB_ARCH% >nul
) else if exist "%VSROOT%\18\Insiders\VC\Auxiliary\Build\vcvarsall.bat" (
    call "%VSROOT%\18\Insiders\VC\Auxiliary\Build\vcvarsall.bat" %HB_ARCH% >nul
) else (
    echo runrtl.bat: no vcvarsall.bat found under "%VSROOT%"
    exit /b 9
)

if not exist "%HB_INSTALL%\bin\hbmk2.exe" (
    echo runrtl.bat: hbmk2 not found at "%HB_INSTALL%\bin" ^(set HB_INSTALL^)
    exit /b 9
)
set "PATH=%HB_INSTALL%\bin;%PATH%"

py "%~dp0runrtl.py" %*
exit /b %ERRORLEVEL%
