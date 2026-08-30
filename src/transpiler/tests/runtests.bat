@echo off
rem Run the transpiler test suite on Windows (counterpart of the .sh scripts).
rem
rem   runtests.bat            gen + prg + cs + run
rem   runtests.bat gen        regenerate hbout/ and csout/ only
rem   runtests.bat prg|cs|run a single stage
rem
rem Overrides:
rem   HB_ARCH        vcvarsall target (default x86, matching the Harbour
rem                  install the tests link against)
rem   HB_INSTALL     Harbour install prefix providing hbmk2
rem   HBTRANSPILER   transpiler binary (default ..\..\..\bin\hbtranspiler.exe)
rem
rem Python is invoked DIRECTLY, never through bash: Git Bash puts
rem /usr/bin ahead of MSVC on PATH, so GNU link shadows link.exe and
rem hbmk2 fails with "link: unknown option -- n".
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
    echo runtests.bat: no vcvarsall.bat found under "%VSROOT%"
    exit /b 9
)

if not exist "%HB_INSTALL%\bin\hbmk2.exe" (
    echo runtests.bat: hbmk2 not found at "%HB_INSTALL%\bin" ^(set HB_INSTALL^)
    exit /b 9
)
set "PATH=%HB_INSTALL%\bin;%PATH%"

py "%~dp0runsuite.py" %*
exit /b %ERRORLEVEL%
