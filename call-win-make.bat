setlocal

call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" x86
set HB_COMPILER=msvc
rem Relative paths don't work
rem set HB_INSTALL_PREFIX=..\..\harbour-3.2.0dev
set HB_INSTALL_PREFIX=%USERPROFILE%\dev\harbour-3.2.0dev
set HB_BUILD_MODE=c
set HB_USER_PRGFLAGS=-l-
set HB_BUILD_CONTRIBS=

if "%CURL_PREFIX%"=="" set CURL_PREFIX=%USERPROFILE%\dev\harbour-3.2.0dev-libs\curl-7.54.0-win32-mingw
set HB_WITH_CURL=%CURL_PREFIX%\include

if "%FREEIMAGE_PREFIX%"=="" set FREEIMAGE_PREFIX=%USERPROFILE%\dev\harbour-3.2.0dev-libs\FreeImage3170Win32Win64\FreeImage
set HB_WITH_FREEIMAGE=%FREEIMAGE_PREFIX%\Dist\x32

if "%OPENSSL_PREFIX%"=="" set OPENSSL_PREFIX=%USERPROFILE%\dev\harbour-3.2.0dev-libs\OpenSSL-Win32
set HB_WITH_OPENSSL=%OPENSSL_PREFIX%\include

set HB_STATIC_OPENSSL=yes
set HB_STATIC_CURL=yes
rem I think "win-make.exe clean install" will do the same
rem win-make.exe clean
win-make.exe
win-make.exe install

endlocal