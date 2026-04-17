# .NET 9 → .NET 10 Migration Design

**Date:** 2026-04-17
**Scope:** Minimal bump — giữ nguyên code, chỉ nâng TargetFramework và các package gắn với TFM.
**Solution:** `RetailStoreManagement/RetailStoreManagement.sln`

## Context

Dự án Clean Architecture gồm 4 project đang target `net9.0`:

- `src/Domain/Domain.csproj`
- `src/Application/Application.csproj`
- `src/Infrastructure/Infrastructure.csproj`
- `src/WebApi/WebApi.csproj`

SDK hiện tại: 9.0.308 (pin qua Nix `dotnet-sdk_9` trong `devenv.nix` — 2 vị trí: `packages` list line 26 và `languages.dotnet.package` line 47).

Nixpkgs đã có `dotnet-sdk_10` (10.0.101) sẵn sàng dùng — không cần overlay.

## Goals

1. Cả 4 project build sạch trên .NET 10 SDK.
2. WebApi khởi động, Swagger load, một request CRUD chạm PostgreSQL qua EF Core 10 thành công.
3. `dotnet ef migrations` vẫn thao tác được với migrations hiện có (không regen).
4. Không thay đổi logic/architecture, không refactor.

## Non-Goals

- Không thay Swashbuckle bằng `Microsoft.AspNetCore.OpenApi`.
- Không bỏ/đổi MediatR, AutoMapper, FluentValidation.
- Không áp dụng C# 14 / .NET 10 features mới.
- Không update frontend.

## Migration Steps

### 1. Nix SDK

Sửa `devenv.nix`:

- Line 26: `dotnet-sdk_9` → `dotnet-sdk_10`
- Line 47: `package = pkgs.dotnet-sdk_9;` → `package = pkgs.dotnet-sdk_10;`

Reload env: `direnv reload` (hoặc `devenv shell` lại). Verify: `dotnet --version` ≥ 10.0.101.

### 2. global.json

Tạo `RetailStoreManagement/global.json`:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestMinor",
    "allowPrerelease": false
  }
}
```

Mục đích: tránh tụt về SDK 9 khi máy khác có nhiều SDK cùng lúc.

### 3. Bump TargetFramework

Đổi `<TargetFramework>net9.0</TargetFramework>` → `<TargetFramework>net10.0</TargetFramework>` trong 4 file:

- `src/Domain/Domain.csproj`
- `src/Application/Application.csproj`
- `src/Infrastructure/Infrastructure.csproj`
- `src/WebApi/WebApi.csproj`

### 4. Update packages gắn với TFM

| Project | Package | Cũ | Mới |
|---|---|---|---|
| Domain | Microsoft.EntityFrameworkCore | 9.0.0 | 10.0.0 |
| Application | Microsoft.EntityFrameworkCore | 9.0.0 | 10.0.0 |
| Application | Microsoft.Extensions.DependencyInjection.Abstractions | 9.0.0 | 10.0.0 |
| Infrastructure | Microsoft.EntityFrameworkCore | 9.0.0 | 10.0.0 |
| Infrastructure | Microsoft.EntityFrameworkCore.Design | 9.0.0 | 10.0.0 |
| Infrastructure | Microsoft.AspNetCore.Authentication.JwtBearer | 9.0.0 | 10.0.0 |
| Infrastructure | Npgsql.EntityFrameworkCore.PostgreSQL | 9.0.2 | 10.0.x (latest cho EF 10) |
| Infrastructure | EFCore.NamingConventions | 9.0.0 | 10.0.x |
| WebApi | Swashbuckle.AspNetCore | 7.2.0 | bản mới nhất tương thích net10 |

**Giữ nguyên** (không gắn với TFM, không cần bump):

- `MediatR` 12.4.1
- `FluentValidation` 11.11.0
- `FluentValidation.DependencyInjectionExtensions` 11.11.0
- `AutoMapper` 13.0.1
- `BCrypt.Net-Next` 4.0.3

Version mới nhất của Npgsql/EFCore.NamingConventions/Swashbuckle sẽ tra cứu tại thời điểm thực thi (dùng `dotnet list package --outdated` hoặc NuGet.org).

### 5. Build & Validate

```bash
cd RetailStoreManagement
dotnet restore
dotnet build
dotnet test   # nếu có test project
```

Fix mọi warning mới do analyzer EF Core 10 / AspNetCore 10 bật thêm (thường là nullable annotations, obsolete API). Nếu có breaking thực sự (hiếm), document lại trong PR.

Smoke test:

```bash
dotnet run --project src/WebApi --launch-profile http
# mở /swagger, gọi 1 endpoint CRUD
```

EF migrations:

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/WebApi
```

### 6. Commit

Một commit duy nhất: `chore: bump to .NET 10 (SDK, TFM, EF Core, AspNetCore packages)`.

## Risks

- **Npgsql 10 behavior**: có thể đổi mapping timestamp/timestamptz → cần test 1 round-trip DateTime qua DB.
- **Swashbuckle 7.2 trên net10**: có thể báo warning nhưng vẫn chạy; nếu fail, bump lên version mới nhất.
- **Nix cache**: `devenv reload` lần đầu sẽ tải dotnet-sdk_10 (~200MB).
- **CI/CD**: nếu có pipeline pin SDK 9, phải update riêng (không nằm trong scope này nhưng cần flag).

## Success Criteria

- [ ] `dotnet --version` ≥ 10.0.101 sau khi reload env
- [ ] `dotnet build` thành công, 0 error
- [ ] Warnings mới (nếu có) được fix hoặc whitelist có chủ đích
- [ ] WebApi chạy, Swagger hiển thị endpoints
- [ ] 1 request CRUD qua DB hoạt động
- [ ] `dotnet ef migrations list` liệt kê đúng migrations hiện có
- [ ] Commit sạch, push được

## Rollback

`git revert` commit bump + `devenv reload` để quay về SDK 9. Không migration nào chạy trên DB trong quá trình này nên không có state phải rollback ở tầng dữ liệu.
