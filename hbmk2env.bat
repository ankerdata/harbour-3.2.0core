@echo off

rem *** remember to update mknewdoc.bat & mk2-unicode.bat

set HB_VER=3.4.0dev
set HB_COMPILER=msvc
set INSTALLPATH=..

rem call "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\Tools\VsDevCmd.bat"
set WindowsSdkDir=\Program Files\Microsoft SDKs\Windows\v6.0A\
call "C:\Program Files (x86)\Microsoft Visual Studio 9.0\VC\vcvarsall.bat"

set PATH=%INSTALLPATH%\harbour-%HB_VER%\bin;%PATH%
