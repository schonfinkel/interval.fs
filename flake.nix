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
    let
      readXmlElement =
        element: file:
        let
          contents = builtins.readFile file;
          opening = "<${element}>";
          closing = "</${element}>";
          openingParts = nixpkgs.lib.splitString opening contents;
          closingParts = nixpkgs.lib.splitString closing contents;
        in
        if builtins.length openingParts != 2 || builtins.length closingParts != 2 then
          throw "readXmlElement: expected exactly one ${opening} and ${closing} in ${toString file}"
        else
          let
            valueParts = nixpkgs.lib.splitString closing (builtins.elemAt openingParts 1);
            valueMatch =
              if builtins.length valueParts == 2 then
                builtins.match "[[:space:]]*([^[:space:]<]+)[[:space:]]*" (builtins.elemAt valueParts 0)
              else
                null;
          in
          if valueMatch == null then
            throw "readXmlElement: ${opening} in ${toString file} must contain one non-empty value"
          else
            builtins.head valueMatch;
    in
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
          version = readXmlElement "Version" ./Directory.Build.props;
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

          # Native Nix devShells replacement
          devShells = {
            # nix develop .#ci
            ci = pkgs.mkShell {
              name = "ci-shell";
              buildInputs = [
                dotnet
                pkgs.gnumake
              ];

              shellHook = ''
                echo "Entering CI shell..."
                dotnet --info
              '';
            };
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
