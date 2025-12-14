{
  description = "Harbour - Clipper/xBase-compatible compiler";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
    harbour-src = {
      url = "github:ankerdata/harbour-3.2.0core";
      flake = false;
    };
  };

  outputs = { self, nixpkgs, flake-utils, harbour-src }:
    flake-utils.lib.eachDefaultSystem (system:
      let
        pkgs = import nixpkgs {
          inherit system;
        };

        # Build options - set to true to enable
        buildOpts = {
          # Terminal libraries
          enableNcurses = false;    # for gtcrs terminal library
          enableSlang = false;      # for gtsln terminal library
          enableX11 = false;         # for gtxwc terminal library
          enableGpm = false;        # for console mouse support (gttrm, gtsln, gtcrs)
          
          # Core libraries (commonly used)
          enablePcre = true;         # Perl Compatible Regular Expressions
          enableZlib = true;        # compression library
          enableBzip2 = false;      # bzip2 compression
          enableSqlite3 = false;    # SQLite database
          enableExpat = false;      # XML parser
          
          # Image libraries
          enablePng = false;        # PNG image support
          enableJpeg = false;       # JPEG image support
          enableTiff = false;       # TIFF image support
          enableCairo = false;      # Cairo graphics library
          enableFreeImage = false;  # FreeImage library
          enableGd = false;         # GD Graphics Library
          
          # Network and web
          enableCurl = false;       # libcurl for file transfer
          enableCups = false;       # CUPS printing (Linux only)
          
          # Databases
          enableMysql = false;      # MySQL database
          enablePostgresql = false; # PostgreSQL database
          enableFirebird = false;   # Firebird SQL database
          enableOdbc = false;       # ODBC database access
          
          # Build configuration
          buildDebug = false;       # Create debug build (HB_BUILD_DEBUG)
          buildOptim = true;        # Enable C compiler optimizations (HB_BUILD_OPTIM)
          buildDyn = true;          # Create Harbour dynamic libraries (HB_BUILD_DYN)
          buildContribDyn = true;   # Create contrib dynamic libraries (HB_BUILD_CONTRIB_DYN)
          buildShared = true;       # Create Harbour executables in shared mode (HB_BUILD_SHARED)
          buildStrip = "no";       # Strip symbols: all|bin|lib|no (HB_BUILD_STRIP)
          buildNoGplLib = false;    # Disable GPL 3rd party code (HB_BUILD_NOGPLLIB)
          build3rdExt = true;       # Enable autodetection of 3rd party components (HB_BUILD_3RDEXT)
        };

        # Collect optional dependencies based on build options
        optionalDeps = with pkgs; [
        ] ++ pkgs.lib.optionals buildOpts.enableNcurses [ ncurses ]
          ++ pkgs.lib.optionals buildOpts.enableSlang [ slang ]
          ++ pkgs.lib.optionals buildOpts.enableX11 [ xorg.libX11 ]
          ++ pkgs.lib.optionals buildOpts.enableGpm [ gpm ]
          ++ pkgs.lib.optionals buildOpts.enablePcre [ pcre ]
          ++ pkgs.lib.optionals buildOpts.enableZlib [ zlib ]
          ++ pkgs.lib.optionals buildOpts.enableBzip2 [ bzip2 ]
          ++ pkgs.lib.optionals buildOpts.enableSqlite3 [ sqlite ]
          ++ pkgs.lib.optionals buildOpts.enableExpat [ expat ]
          ++ pkgs.lib.optionals buildOpts.enablePng [ libpng ]
          ++ pkgs.lib.optionals buildOpts.enableJpeg [ libjpeg_turbo ]
          ++ pkgs.lib.optionals buildOpts.enableTiff [ libtiff ]
          ++ pkgs.lib.optionals buildOpts.enableCairo [ cairo ]
          ++ pkgs.lib.optionals buildOpts.enableFreeImage [ freeimage ]
          ++ pkgs.lib.optionals buildOpts.enableGd [ gd ]
          ++ pkgs.lib.optionals buildOpts.enableCurl [ curl ]
          ++ pkgs.lib.optionals buildOpts.enableCups [ cups ]
          ++ pkgs.lib.optionals buildOpts.enableMysql [ mysql80 ]
          ++ pkgs.lib.optionals buildOpts.enablePostgresql [ postgresql ]
          ++ pkgs.lib.optionals buildOpts.enableFirebird [ firebird ]
          ++ pkgs.lib.optionals buildOpts.enableOdbc [ unixODBC ];

        # Build environment variables from options
        buildEnvVars = with buildOpts; ''
          ${if buildDebug then "export HB_BUILD_DEBUG=yes" else ""}
          ${if !buildOptim then "export HB_BUILD_OPTIM=no" else ""}
          ${if !buildDyn then "export HB_BUILD_DYN=no" else ""}
          ${if !buildContribDyn then "export HB_BUILD_CONTRIB_DYN=no" else ""}
          ${if !buildShared then "export HB_BUILD_SHARED=no" else ""}
          ${if buildStrip != "no" then "export HB_BUILD_STRIP=${buildStrip}" else ""}
          ${if buildNoGplLib then "export HB_BUILD_NOGPLLIB=yes" else ""}
          ${if !build3rdExt then "export HB_BUILD_3RDEXT=no" else ""}
        '';

        harbour = pkgs.stdenv.mkDerivation {
          pname = "harbour";
          version = "3.2.0";

          src = harbour-src;

          nativeBuildInputs = with pkgs; [
            gnumake
            gcc
            binutils
          ];

          buildInputs = optionalDeps;

          # Harbour uses GNU Make and auto-detects platform
          # The build system will detect Linux and GCC automatically
          buildPhase = ''
            runHook preBuild
            ${buildEnvVars}
            make
            runHook postBuild
          '';

          installPhase = ''
            runHook preInstall
            make install HB_INSTALL_PREFIX=$out
            runHook postInstall
          '';

          meta = with pkgs.lib; {
            description = "Free software implementation of a multi-platform, multi-threading, object-oriented, scriptable programming language, backward compatible with Clipper/xBase";
            homepage = "https://harbour.github.io/";
            license = licenses.gpl2Plus;
            maintainers = [ ];
            platforms = platforms.unix;
          };
        };
      in
      {
        packages.default = harbour;
        packages.harbour = harbour;

        devShells.default = pkgs.mkShell {
          buildInputs = with pkgs; [
            gnumake
            gcc
            binutils
          ] ++ optionalDeps;
        };
      });
}

