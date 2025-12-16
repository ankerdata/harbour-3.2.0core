# Adding Harbour to Your Nix Configuration

This guide explains how to add the Harbour compiler from this repository to your personal Nix configuration.

## Prerequisites

- Nix with Flakes enabled
- A NixOS or Home Manager configuration using flakes

## Option 1: Adding to NixOS Configuration

### Step 1: Add the Flake Input

Add this repository as an input to your `flake.nix`:

```nix
{
  inputs = {
    # ... your existing inputs ...
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    
    harbour = {
      url = "github:ankerdata/harbour-3.2.0core";
      inputs.nixpkgs.follows = "nixpkgs";
    };
  };

  outputs = { self, nixpkgs, harbour, ... } @ inputs: {
    nixosConfigurations.your-hostname = nixpkgs.lib.nixosSystem {
      system = "x86_64-linux"; # or your system
      modules = [
        # ... your existing modules ...
        ({ pkgs, ... }: {
          environment.systemPackages = [
            harbour.packages.${pkgs.system}.default
          ];
        })
      ];
    };
  };
}
```

**Important:** After adding the input, run `nix flake update` or `nix flake lock` to update your lock file so the `lib` output is available.

### Step 2: Rebuild Your System

```bash
sudo nixos-rebuild switch --flake .#your-hostname
```

### Customizing Build Options in NixOS

To customize Harbour build options in your NixOS configuration, use the `lib.mkHarbour` function:

```nix
{ pkgs, harbour, ... }: {
  environment.systemPackages = [
    (harbour.lib.mkHarbour {
      pkgs = pkgs;
      buildOpts = {
        # Enable terminal libraries
        enableNcurses = true;
        enableSlang = true;
        
        # Enable image support
        enablePng = true;
        enableJpeg = true;
        
        # Enable database support
        enableSqlite3 = true;
        enablePostgresql = true;
        
        # Enable network support
        enableCurl = true;
      };
    })
  ];
}
```

Or use an overlay for system-wide customization:

```nix
{
  nixpkgs.overlays = [
    harbour.overlays.default
    (final: prev: {
      harbour = harbour.lib.mkHarbour {
        pkgs = final;
        buildOpts = {
          enableNcurses = true;
          enableCurl = true;
          enableSqlite3 = true;
        };
      };
    })
  ];
}
```

## Option 2: Adding to Home Manager Configuration

### Step 1: Add the Flake Input

Add this repository as an input to your `flake.nix`:

```nix
{
  inputs = {
    # ... your existing inputs ...
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    home-manager = {
      url = "github:nix-community/home-manager";
      inputs.nixpkgs.follows = "nixpkgs";
    };
    
    harbour = {
      url = "github:ankerdata/harbour-3.2.0core";
      inputs.nixpkgs.follows = "nixpkgs";
    };
  };

  outputs = { self, nixpkgs, home-manager, harbour, ... } @ inputs: {
    homeConfigurations.your-username = home-manager.lib.homeManagerConfiguration {
      pkgs = nixpkgs.legacyPackages.x86_64-linux; # or your system
      modules = [
        # ... your existing modules ...
        ({ pkgs, ... }: {
          home.packages = [
            harbour.packages.${pkgs.system}.default
          ];
        })
      ];
    };
  };
}
```

**Important:** After adding the input, run `nix flake update` or `nix flake lock` to update your lock file so the `lib` output is available.

### Step 2: Rebuild Home Manager

```bash
home-manager switch --flake .#your-username
```

### Customizing Build Options in Home Manager

To customize Harbour build options in your Home Manager configuration:

```nix
{ pkgs, harbour, ... }: {
  home.packages = [
    (harbour.lib.mkHarbour {
      pkgs = pkgs;
      buildOpts = {
        # Enable terminal libraries
        enableNcurses = true;
        enableSlang = true;
        
        # Enable image support
        enablePng = true;
        enableJpeg = true;
        
        # Enable database support
        enableSqlite3 = true;
        
        # Enable network support
        enableCurl = true;
      };
    })
  ];
}
```

Or import the harbour input directly in your module:

```nix
# In your home.nix or similar
{ pkgs, inputs, ... }:
{
  home.packages = [
    (inputs.harbour.lib.mkHarbour {
      pkgs = pkgs;
      buildOpts = {
        enableNcurses = true;
        enableCurl = true;
        enableSqlite3 = true;
      };
    })
  ];
}
```

## Customizing Build Options from Personal Configs

When using Harbour as a flake input in your NixOS or Home Manager configuration, you can customize build options without modifying the source flake. This is the recommended approach as it keeps your customizations in your own configuration.

### Using `lib.mkHarbour`

The flake exposes a `lib.mkHarbour` function that accepts custom build options:

```nix
harbour.lib.mkHarbour {
  pkgs = pkgs;  # Your nixpkgs instance
  buildOpts = {
    # Override any default options
    enableNcurses = true;
    enableCurl = true;
    enableSqlite3 = true;
    enablePng = true;
    enableJpeg = true;
    # ... any other options
  };
}
```

### Complete Example: NixOS with Custom Options

```nix
{
  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    harbour = {
      url = "github:ankerdata/harbour-3.2.0core";
      inputs.nixpkgs.follows = "nixpkgs";
    };
  };

  outputs = { self, nixpkgs, harbour, ... }:
    let
      # Create a custom Harbour build with your options
      customHarbour = harbour.lib.mkHarbour {
        pkgs = nixpkgs.legacyPackages.x86_64-linux;
        buildOpts = {
          enableNcurses = true;
          enableSlang = true;
          enableCurl = true;
          enableSqlite3 = true;
          enablePng = true;
          enableJpeg = true;
        };
      };
    in
    {
      nixosConfigurations.your-hostname = nixpkgs.lib.nixosSystem {
        system = "x86_64-linux";
        modules = [
          ({ pkgs, ... }: {
            environment.systemPackages = [
              customHarbour
            ];
          })
        ];
      };
    };
}
```

### Complete Example: Home Manager with Custom Options

```nix
{
  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    home-manager = {
      url = "github:nix-community/home-manager";
      inputs.nixpkgs.follows = "nixpkgs";
    };
    harbour = {
      url = "github:ankerdata/harbour-3.2.0core";
      inputs.nixpkgs.follows = "nixpkgs";
    };
  };

  outputs = { self, nixpkgs, home-manager, harbour, ... }:
    let
      customHarbour = harbour.lib.mkHarbour {
        pkgs = nixpkgs.legacyPackages.x86_64-linux;
        buildOpts = {
          enableNcurses = true;
          enableCurl = true;
          enableSqlite3 = true;
        };
      };
    in
    {
      homeConfigurations.your-username = home-manager.lib.homeManagerConfiguration {
        pkgs = nixpkgs.legacyPackages.x86_64-linux;
        modules = [
          ({ pkgs, ... }: {
            home.packages = [
              customHarbour
            ];
          })
        ];
      };
    };
}
```

## Option 3: Using as a Standalone Flake

You can also use this flake directly without adding it to your system configuration:

### Building Harbour

```bash
# Clone or navigate to this repository
cd harbour-3.2.0core

# Optionally customize build options in flake.nix first
# (see "Customizing Build Options" section below)

# Build Harbour
nix build

# The result will be in ./result/bin/
./result/bin/harbour --version
```

**Note:** By default, Harbour is built with PCRE and zlib support enabled. To enable additional features, see the "Customizing Build Options" section below.

### Entering a Development Shell

```bash
# Enter a shell with Harbour build tools
nix develop

# Now you can build Harbour manually
make
```

### Installing to Your Profile

```bash
# Install Harbour to your user profile
nix profile install github:ankerdata/harbour-3.2.0core
```

## Option 4: Using in a Nix Shell

You can use Harbour in a temporary shell:

```bash
nix shell github:ankerdata/harbour-3.2.0core
```

## Pinning a Specific Version

If you want to pin to a specific commit or tag, you can specify it in your flake input:

```nix
harbour = {
  url = "github:ankerdata/harbour-3.2.0core";
  ref = "main";  # or a specific branch
  rev = "abc123...";  # specific commit hash
  inputs.nixpkgs.follows = "nixpkgs";
};
```

## Using Harbour After Installation

Once installed, Harbour will be available in your PATH:

```bash
# Check Harbour version
harbour --version

# Compile a Harbour program
harbour hello.prg

# Use hbmk2 (Harbour's build tool)
hbmk2 hello.prg
```

## Customizing Build Options

The Harbour flake supports many optional dependencies and build configuration options. There are two ways to customize the build:

1. **Edit `flake.nix` directly** - Modify the `defaultBuildOpts` in the source flake
2. **From your personal Nix configuration** - Use the `lib.mkHarbour` function (recommended)

### Available Build Options

The following options are available for customization:

### Terminal Libraries

- `enableNcurses` - Enable ncurses support (for gtcrs terminal library)
- `enableSlang` - Enable slang support (for gtsln terminal library)
- `enableX11` - Enable X11 support (for gtxwc terminal library)
- `enableGpm` - Enable GPM support (for console mouse support)

### Core Libraries

- `enablePcre` - Perl Compatible Regular Expressions (enabled by default)
- `enableZlib` - Compression library (enabled by default)
- `enableBzip2` - bzip2 compression support
- `enableSqlite3` - SQLite database support
- `enableExpat` - XML parser support

### Image Libraries

- `enablePng` - PNG image support
- `enableJpeg` - JPEG image support
- `enableTiff` - TIFF image support
- `enableCairo` - Cairo graphics library
- `enableFreeImage` - FreeImage library
- `enableGd` - GD Graphics Library

### Network and Web

- `enableCurl` - libcurl for file transfer
- `enableCups` - CUPS printing support (Linux only)
- `enableOpenSSL` - OpenSSL cryptographic library

### Databases

- `enableMysql` - MySQL database support
- `enablePostgresql` - PostgreSQL database support
- `enableFirebird` - Firebird SQL database support
- `enableOdbc` - ODBC database access

### Build Configuration

- `buildDebug` - Create debug build (default: `false`)
- `buildOptim` - Enable C compiler optimizations (default: `true`)
- `buildDyn` - Create Harbour dynamic libraries (default: `true`)
- `buildContribDyn` - Create contrib dynamic libraries (default: `true`)
- `buildShared` - Create Harbour executables in shared mode (default: `true`)
- `buildStrip` - Strip symbols: `"all"`, `"bin"`, `"lib"`, or `"no"` (default: `"no"`)
- `buildNoGplLib` - Disable GPL 3rd party code (default: `false`)
- `build3rdExt` - Enable autodetection of 3rd party components (default: `true`)

### Example: Enabling Additional Features

#### Option A: Editing flake.nix Directly

To enable specific features, edit `flake.nix` and modify the `defaultBuildOpts` section:

```nix
defaultBuildOpts = {
  # Enable terminal libraries
  enableNcurses = true;
  enableSlang = true;
  
  # Enable image support
  enablePng = true;
  enableJpeg = true;
  
  # Enable database support
  enableSqlite3 = true;
  enablePostgresql = true;
  
  # Enable network support
  enableCurl = true;
  
  # Build configuration
  buildDebug = false;
  buildOptim = true;
  # ... other options
};
```

After modifying the options, rebuild Harbour:

```bash
nix build
```

#### Option B: From Your Personal Nix Configuration

If you're using this flake as an input in your NixOS or Home Manager configuration, you can customize build options without editing the flake itself. See the sections above for examples.

## Development

To work on Harbour itself:

```bash
# Enter development shell
nix develop

# Build Harbour
make

# Install to a custom prefix
make install HB_INSTALL_PREFIX=/path/to/install
```

## Troubleshooting

### Flake Input Not Found

If you get an error about the flake input not being found, make sure:
1. You've added the input to your `flake.nix`
2. You've run `nix flake update` or `nix flake lock` to update the lock file

### lib Output Not Available

If you get an error that `harbour.lib` is not available or `harbour.lib.mkHarbour` doesn't exist:

1. **Update your flake lock file:**
   ```bash
   nix flake update harbour
   # or
   nix flake lock --update-input harbour
   ```

2. **Verify the lib output exists:**
   ```bash
   nix eval --json 'inputs.harbour.lib' --apply 'x: builtins.hasAttr "mkHarbour" x'
   ```
   This should return `true`.

3. **Check your flake inputs:**
   Make sure `harbour` is properly listed in your `inputs` and that you're accessing it correctly in your `outputs` function.

4. **Clear flake cache (if needed):**
   ```bash
   rm -rf .direnv .nix-flake-cache
   nix flake lock
   ```

### Build Fails

If the build fails:
1. Check that you have the required build dependencies
2. Ensure you're using a compatible Nixpkgs version
3. Check the build logs for specific error messages
4. Verify that optional dependencies you've enabled are available in nixpkgs
5. Some packages may have different names in nixpkgs - check the error messages for suggestions

### Harbour Not in PATH

After installation:
1. Make sure you've rebuilt your configuration
2. Start a new shell session
3. Verify with `which harbour`

### Optional Dependencies Not Found

If you get errors about missing optional dependencies:
1. Check that the package names in `flake.nix` match those available in nixpkgs
2. Some packages may need to be updated to match your nixpkgs version
3. You can check available packages with: `nix search nixpkgs <package-name>`
4. Consider disabling optional features you don't need to reduce build complexity

## Build Options Reference

The Harbour flake supports extensive customization through build options. All available options are documented in the "Customizing Build Options" section above. These options correspond to Harbour's build system environment variables (see `README.md` for the complete Harbour build documentation).

Key points:
- Most optional features are disabled by default to keep builds minimal
- PCRE and zlib are enabled by default as they are commonly used
- Build options can be customized by editing the `buildOpts` section in `flake.nix`
- After modifying options, rebuild with `nix build` or `nixos-rebuild switch`

## Additional Resources

- [Harbour Documentation](https://harbour.github.io/)
- [Harbour README](README.md) - Complete build documentation and options
- [Nix Flakes Documentation](https://nixos.wiki/wiki/Flakes)
- [Home Manager Documentation](https://nix-community.github.io/home-manager/)

