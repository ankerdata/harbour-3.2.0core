rem call "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\Tools\VsDevCmd.bat"
set WindowsSdkDir=\Program Files\Microsoft SDKs\Windows\v6.0A\
call "C:\Program Files (x86)\Microsoft Visual Studio 9.0\VC\vcvarsall.bat"
set HB_COMPILER=msvc
set HB_INSTALL_PREFIX=C:\Users\<username>\dev\harbour-3.2.0dev
set HB_BUILD_MODE=c
set HB_USER_PRGFLAGS=-l-
set HB_BUILD_CONTRIBS=
set HB_WITH_CURL=C:\Users\<username>\dev\curl-7.54.0-win32-mingw\include
set HB_WITH_FREEIMAGE=C:\Users\<username>\dev\FreeImage3170Win32Win64\include
set HB_WITH_OPENSSL=C:\Users\<username>\dev\harbour-3.2.0dev-libs\OpenSSL-Win32\include
set HB_STATIC_OPENSSL=yes
set HB_STATIC_CURL=yes
win-make.exe
win-make.exe install
