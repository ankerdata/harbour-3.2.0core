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

        harbour = pkgs.stdenv.mkDerivation {
          pname = "harbour";
          version = "3.2.0";

          src = harbour-src;

          nativeBuildInputs = with pkgs; [
            gnumake
            gcc
            binutils
          ];

          buildInputs = with pkgs; [
            # Optional dependencies for various features
            # Uncomment as needed:
            # ncurses      # for gtcrs terminal library
            # slang        # for gtsln terminal library
            # xorg.libX11  # for gtxwc terminal library
            # gpm          # for console mouse support
          ];

          # Harbour uses GNU Make and auto-detects platform
          # The build system will detect Linux and GCC automatically
          buildPhase = ''
            runHook preBuild
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
            # Optional development dependencies
            # ncurses
            # slang
            # xorg.libX11
          ];
        };
      });
}

