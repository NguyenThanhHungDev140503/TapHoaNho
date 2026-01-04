{
  description = "TapHoaNho - Retail Store Management System";

  # ============================================
  # INPUTS
  # ============================================
  inputs = {
    nixpkgs.url = "github:cachix/devenv-nixpkgs/rolling";
    devenv.url = "github:cachix/devenv";
    flake-parts.url = "github:hercules-ci/flake-parts";
  };

  nixConfig = {
    extra-trusted-public-keys = "devenv.cachix.org-1:w1cLUi8dv3hnoSPGAuibQv+f9TZLr6cv/Hm9XgU50cw=";
    extra-substituters = "https://devenv.cachix.org";
  };

  # ============================================
  # OUTPUTS
  # ============================================
  outputs = inputs@{ flake-parts, nixpkgs, ... }:
    flake-parts.lib.mkFlake { inherit inputs; } {
      imports = [ inputs.devenv.flakeModule ];
      
      systems = nixpkgs.lib.systems.flakeExposed;

      perSystem = { config, self', inputs', pkgs, system, ... }: {
        # ----------------------------------------
        # Packages
        # ----------------------------------------
        packages.default = pkgs.hello;
        
        # ----------------------------------------
        # Formatter
        # ----------------------------------------
        formatter = pkgs.nixfmt-rfc-style;

        # ----------------------------------------
        # Devenv Shell - Import from devenv.nix
        # ----------------------------------------
        devenv.shells.default = import ./devenv.nix { inherit pkgs; };
      };
    };
}
