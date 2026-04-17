{ pkgs, profile ? "dev" }:
{
  # ============================================
  # DOTENV - Load secrets from .env.secrets
  # ============================================
  dotenv.enable = true;
  dotenv.filename = ".env.secrets";

  # ============================================
  # PACKAGES - System tools and SDKs
  # ============================================
  packages = with pkgs; [
    # Core utilities
    git
    curl
    jq
    coreutils
    
    # Node.js & Yarn
    nodejs_20
    yarn-berry
    nodePackages.typescript
    nodePackages.eslint
    
    # .NET SDK 10
    dotnet-sdk_10
    
    # PostgreSQL client (for pg_isready, psql)
    postgresql_16
    
    # C# tooling
    omnisharp-roslyn
  ];

  # ============================================
  # LANGUAGES - Enable language support
  # ============================================
  languages.javascript = {
    enable = true;
    package = pkgs.nodejs_20;
  };
  
  languages.typescript.enable = true;
  
  languages.dotnet = {
    enable = true;
    package = pkgs.dotnet-sdk_10;
  };

  # ============================================
  # PROCESSES - Background services (devenv up)
  # ============================================
  processes = {
    frontend.exec = "${pkgs.bash}/bin/bash -c 'cd frontend && yarn dev'";
    backend.exec = "${pkgs.bash}/bin/bash -c 'cd RetailStoreManagement/src/WebApi && dotnet watch run --launch-profile http'";
  };

  # ============================================
  # SCRIPTS - Custom commands
  # ============================================
  scripts = {
    # Setup dependencies
    setup.exec = ''
      echo "📦 Installing frontend dependencies..."
      (cd frontend && yarn install)
      echo ""
      echo "📦 Restoring .NET packages..."
      (cd RetailStoreManagement && dotnet restore)
      echo ""
      echo "✅ Setup complete!"
    '';
    
    # Build all projects
    build-all.exec = ''
      echo "🔨 Building frontend and backend..."
      (cd frontend && yarn build) &
      (cd RetailStoreManagement && dotnet build) &
      wait
      echo "✅ Build complete!"
    '';
    
    # Build individual projects
    build-frontend.exec = ''
      echo "🔨 Building frontend..."
      cd frontend && yarn build
    '';
    
    build-backend.exec = ''
      echo "🔨 Building backend..."
      cd RetailStoreManagement && dotnet build
    '';
    
    # Database connectivity check
    db-check.exec = ''
      echo "🔍 Checking Neon database connectivity..."
      if pg_isready -h ep-lucky-queen-a1u66w5c-pooler.ap-southeast-1.aws.neon.tech -d store_management -U neondb_owner -q 2>/dev/null; then
        echo "✅ Database connection: SUCCESS"
      else
        echo "⚠️  Database connection: FAILED (check network/VPN)"
      fi
    '';
    
    # Script for git pre-commit hook
    lint-frontend.exec = ''
      cd frontend && yarn lint
    '';
  };

  # ============================================
  # ENTERSHELL - Shell initialization
  # ============================================
  enterShell = ''
    echo ""
    echo "🚀 TapHoaNho Development Environment"
    echo "====================================="
    echo "📋 Profile: ${profile}"
    echo ""
    echo "📦 Tools:"
    echo "  • Node.js: $(node --version)"
    echo "  • Yarn: $(yarn --version)"
    echo "  • .NET SDK: $(dotnet --version)"
    echo "  • TypeScript: $(tsc --version)"
    echo ""
    
    # Database connectivity check
    echo "🔍 Database connectivity..."
    if pg_isready -h ep-lucky-queen-a1u66w5c-pooler.ap-southeast-1.aws.neon.tech -d store_management -U neondb_owner -q 2>/dev/null; then
      echo "  ✅ Neon PostgreSQL: Connected"
    else
      echo "  ⚠️  Neon PostgreSQL: Check network"
    fi
    echo ""
    
    echo "🔧 Commands:"
    echo "  • devenv up       - Start frontend + backend"
    echo "  • setup           - Install dependencies"
    echo "  • build-all       - Build all"
    echo "  • build-frontend  - Build frontend"
    echo "  • build-backend   - Build backend"
    echo "  • db-check        - Check DB connection"
    echo ""
  '';

  # ============================================
  # GIT HOOKS - Pre-commit checks
  # ============================================
  git-hooks.hooks = {
    pre-commit = {
      enable = true;
      name = "Lint check";
      entry = "${pkgs.bash}/bin/bash -c 'export PATH=${pkgs.nodejs_20}/bin:${pkgs.yarn-berry}/bin:$PATH && cd frontend && yarn lint'";
      pass_filenames = false;
    };
  };

  # ============================================
  # ENVIRONMENT VARIABLES (non-secret)
  # ============================================
  env = {
    # DOTNET_ROOT is already set by languages.dotnet module
    COREPACK_ENABLE_STRICT = "0";
  };

  # ============================================
  # PROFILES - Environment-specific configurations
  # ============================================
  profiles = {
    # Development profile (default)
    dev.module = {
      env = {
        NODE_ENV = "development";
        ASPNETCORE_ENVIRONMENT = "Development";
        VITE_API_URL = "http://localhost:5175";
      };
    };

    # Testing profile
    test.module = { pkgs, ... }: {
      packages = [ 
        pkgs.playwright-driver.browsers
      ];
      env = {
        NODE_ENV = "test";
        ASPNETCORE_ENVIRONMENT = "Testing";
        VITE_API_URL = "http://localhost:5175";
      };
      scripts.test-frontend.exec = ''
        echo "🧪 Running frontend tests..."
        cd frontend && yarn test
      '';
      scripts.test-backend.exec = ''
        echo "🧪 Running backend tests..."
        cd RetailStoreManagement && dotnet test
      '';
      scripts.test-all.exec = ''
        echo "🧪 Running all tests..."
        (cd frontend && yarn test) &
        (cd RetailStoreManagement && dotnet test) &
        wait
        echo "✅ All tests complete!"
      '';
    };

    # Production profile (for building/staging)
    prod.module = {
      env = {
        NODE_ENV = "production";
        ASPNETCORE_ENVIRONMENT = "Production";
      };
      scripts.build-prod.exec = ''
        echo "🚀 Building for production..."
        (cd frontend && yarn build) &
        (cd RetailStoreManagement && dotnet publish -c Release) &
        wait
        echo "✅ Production build complete!"
      '';
    };

    # Fullstack profile (combines dev + test)
    fullstack.extends = [ "dev" "test" ];
  };
}
