# Devenv Integration Guide

## Tổng quan

Dự án sử dụng **devenv** + **flake-parts** để quản lý môi trường phát triển.

## Cấu trúc files

| File | Mô tả |
|------|-------|
| `flake.nix` | Cấu hình Nix Flakes với flake-parts |
| `devenv.nix` | Cấu hình devenv (packages, scripts, processes) |
| `devenv.yaml` | Inputs cho devenv |
| `.env.secrets` | Environment variables (gitignored) |
| `.env.secrets.example` | Template cho team |

## Quick Start

### 1. Setup lần đầu

```bash
# Copy secrets template
cp .env.secrets.example .env.secrets

# Điền giá trị vào .env.secrets

# Vào môi trường dev
nix develop
```

### 2. Commands có sẵn

| Command | Mô tả |
|---------|-------|
| `devenv up` | Chạy frontend + backend đồng thời |
| `setup` | yarn install + dotnet restore |
| `build-all` | Build tất cả |
| `build-frontend` | Build frontend |
| `build-backend` | Build backend |
| `db-check` | Kiểm tra kết nối database |

### 3. Profiles

```bash
nix develop .#dev   # Development (mặc định)
nix develop .#prod  # Production
nix develop .#test  # Testing
```

## Environment Variables

### .NET Backend

Sử dụng double underscore notation:
- `ConnectionStrings__DefaultConnection`
- `JwtSettings__SecretKey`
- `ImageKit__PublicKey`

### React/Vite Frontend

- `VITE_API_BASE_URL`
- `VITE_APP_NAME`
- `VITE_IMAGEKIT_PUBLIC_KEY`

## Troubleshooting

### Database connection failed

```bash
# Kiểm tra kết nối
db-check

# Nếu fail, kiểm tra:
# - VPN có bật không
# - Firewall có chặn port 5432 không
```

### Yarn install lỗi

```bash
# Reset yarn cache
rm -rf frontend/node_modules
rm -rf frontend/.yarn/cache
setup
```
