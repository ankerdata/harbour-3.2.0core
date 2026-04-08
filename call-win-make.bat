setlocal

if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" (
    call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" x86
) else if exist "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvarsall.bat" (
    call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvarsall.bat" x86
) else (
    call "C:\Program Files\Microsoft Visual Studio\18\Insiders\VC\Auxiliary\Build\vcvarsall.bat" x86
)

set HB_COMPILER=msvc
set HB_INSTALL_PREFIX=%USERPROFILE%\dev\harbour-3.2.0dev
set HB_BUILD_MODE=c
set HB_USER_PRGFLAGS=-l-
set HB_BUILD_CONTRIBS=

set VCPKG=%USERPROFILE%\dev\harbour-3.2.0dev-vcpkg\vcpkg\installed\x86-windows\include

set HB_WITH_CURL=%VCPKG%
set HB_WITH_OPENSSL=%VCPKG%

rem This has about a million dependencies with vcpkg, so we will just disable it for now
rem set HB_WITH_FREEIMAGE=%VCPKG%

if "%FREEIMAGE_PREFIX%"=="" set FREEIMAGE_PREFIX=%USERPROFILE%\dev\harbour-3.2.0dev-libs\FreeImage3170Win32Win64\FreeImage
set HB_WITH_FREEIMAGE=%FREEIMAGE_PREFIX%\Dist\x32

set HB_STATIC_OPENSSL=yes
set HB_STATIC_CURL=yes

rem Clean build
rem win-make.exe clean install > build.log 2>&1

rem Changes only
win-make.exe install > build.log 2>&1

endlocal
