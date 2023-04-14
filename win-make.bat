set HB_COMPILER=msvc
set HB_INSTALL_PREFIX=C:\Users\graha\dev\harbour-3.2.0dev
set HB_WITH_CURL=C:\curl\include

rem call "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\Tools\VsDevCmd.bat"
set WindowsSdkDir=\Program Files\Microsoft SDKs\Windows\v6.0A\
call "C:\Program Files (x86)\Microsoft Visual Studio 9.0\VC\vcvarsall.bat"

.\win-make.exe install
