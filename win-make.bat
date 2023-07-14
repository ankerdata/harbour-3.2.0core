rem call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"
set WindowsSdkDir="C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\"
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" x86
set HB_COMPILER=msvc
set HB_INSTALL_PREFIX=C:\Users\graha\dev\harbour-3.2.0dev
set HB_BUILD_MODE=c
set HB_USER_PRGFLAGS=-l-
set HB_BUILD_CONTRIBS=
set HB_WITH_CURL=C:\Users\graha\dev\harbour-3.2.0dev-libs\curl-7.54.0-win32-mingw\include
set HB_WITH_FREEIMAGE=C:\Users\graha\dev\harbour-3.2.0dev-libs\FreeImage3170Win32Win64\FreeImage\Dist\x32
set HB_WITH_OPENSSL=C:\Users\graha\dev\harbour-3.2.0dev-libs\OpenSSL-Win32\include
set HB_STATIC_OPENSSL=yes
set HB_STATIC_CURL=yes
win-make.exe clean
win-make.exe
win-make.exe install
