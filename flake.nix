{
  description = "F# Development Environment";

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs?ref=nixos-unstable";

    devenv = {
      url = "github:cachix/devenv";
      inputs.nixpkgs.follows = "nixpkgs";
    };

    flake-parts = {
      url = "github:hercules-ci/flake-parts";
    };

    treefmt-nix.url = "github:numtide/treefmt-nix";
  };

  outputs =
    inputs@{
      self,
      devenv,
      flake-parts,
      nixpkgs,
      ...
    }:
    flake-parts.lib.mkFlake { inherit inputs; } {
      imports = [
        inputs.devenv.flakeModule
        inputs.treefmt-nix.flakeModule
      ];
      systems = nixpkgs.lib.systems.flakeExposed;

      perSystem =
        {
          config,
          self',
          inputs',
          pkgs,
          system,
          ...
        }:
        let
          dotnet = pkgs.dotnet-sdk_10;
          version = "1.0.0";
        in
        {
          # This sets `pkgs` to a nixpkgs with allowUnfree option set.
          _module.args.pkgs = import nixpkgs {
            inherit system;
            config.allowUnfree = true;
          };

          packages = {
            # nix build
            default = pkgs.buildDotnetModule {
              inherit version;
              pname = "interval.fs";
              src = ./.;
              projectFile = "src/Interval/Interval.fsproj";
              nugetDeps = ./deps.json;
              dotnet-sdk = dotnet;
            };
          };

          # nix fmt + nix flake check (auto-wired by flakeModule)
          treefmt = {
            projectRootFile = "flake.nix";
            programs.fantomas.enable = true;
            programs.nixfmt.enable = true;
          };

          devenv.shells.ci = {
            # nix develop --impure .#ci
            # Minimal shell for CI: bare essentials to build and run tests.
            packages = [
              pkgs.gnumake
            ];

            languages.dotnet = {
              enable = true;
              package = dotnet;
            };

            enterShell = ''
              echo "Entering CI shell..."
              dotnet --info
            '';
          };

          devenv.shells.default = {
            packages =
              with pkgs;
              [
                bash
                gnumake

                # for dotnet
                netcoredbg
                fsautocomplete
                fantomas
              ]
              ++ [ config.packages.default ];

            languages.dotnet = {
              enable = true;
              package = dotnet;
            };

            enterShell = ''
              echo "Starting Development Environment..."
            '';
          };
        };
    };
}
