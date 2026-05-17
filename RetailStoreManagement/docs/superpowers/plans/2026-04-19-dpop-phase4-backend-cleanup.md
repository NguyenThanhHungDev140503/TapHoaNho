# Phase 4 — Backend Auth Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Xóa toàn bộ legacy JWT + cookie + refresh token code trong backend, di dời `setup-admin` endpoint sang `SetupController` riêng (không generate token).

**Architecture:** Strict deletion order đảm bảo mỗi commit build thành công. Tạo `Setup/` feature mới song song với `Auth/` cũ, chuyển reference, rồi xóa toàn bộ `Auth/`. Giữ nguyên table `user_refresh_tokens` trên DB (orphan) — drop migration thuộc Phase 5.

**Tech Stack:** .NET 9, ASP.NET Core, MediatR, EF Core, xUnit (nếu có), BCrypt.Net.

**Spec:** `docs/superpowers/specs/2026-04-19-dpop-phase4-design.md`

---

## Task 1: Tạo `Setup/` feature skeleton

**Files:**
- Create: `src/Application/Features/Setup/Commands/SetupAdminCommand.cs`
- Create: `src/Application/Features/Setup/Dtos/SetupAdminResponse.cs`

- [ ] **Step 1.1: Tạo `SetupAdminCommand.cs` mới**

File: `src/Application/Features/Setup/Commands/SetupAdminCommand.cs`

```csharp
using Application.Abstractions.Messaging;
using Application.Features.Setup.Dtos;

namespace Application.Features.Setup.Commands;

/// <summary>
/// Command bootstrap: tạo admin đầu tiên của hệ thống.
/// Chỉ chạy được 1 lần khi chưa có admin nào. Sau đó authentication
/// được thực hiện qua Duende IdentityServer (OIDC Authorization Code + PKCE + DPoP).
/// </summary>
public record SetupAdminCommand(
    string Username,
    string Password,
    string? FullName
) : ICommand<SetupAdminResponse>;
```

- [ ] **Step 1.2: Tạo `SetupAdminResponse.cs` mới**

File: `src/Application/Features/Setup/Dtos/SetupAdminResponse.cs`

```csharp
namespace Application.Features.Setup.Dtos;

/// <summary>
/// Response cho SetupAdmin. Không chứa token vì authentication
/// đã chuyển sang IdentityServer — client phải redirect đến IDS login sau setup.
/// </summary>
public class SetupAdminResponse
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string Role { get; set; } = string.Empty;
}
```

- [ ] **Step 1.3: Verify build (Setup/ mới + Auth/ cũ cùng tồn tại)**

Run: `cd RetailStoreManagement && dotnet build src/Application/Application.csproj`

Expected: Build succeeded, 0 errors.

- [ ] **Step 1.4: Commit**

```bash
git add src/Application/Features/Setup/
git commit -m "feat(app): add Setup/ feature skeleton for Phase 4

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Viết `SetupAdminCommandHandler` mới (không dùng `IAuthService`)

**Files:**
- Create: `src/Application/Features/Setup/Handlers/SetupAdminCommandHandler.cs`

- [ ] **Step 2.1: Tạo handler mới**

File: `src/Application/Features/Setup/Handlers/SetupAdminCommandHandler.cs`

```csharp
using Application.Abstractions.Messaging;
using Application.Abstractions.Services;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Setup.Commands;
using Application.Features.Setup.Dtos;
using Domain.Entities;
using Domain.Enums;
using Domain.SeedWork;

namespace Application.Features.Setup.Handlers;

public class SetupAdminCommandHandler(
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher)
    : ICommandHandler<SetupAdminCommand, SetupAdminResponse>
{
    public async Task<ApiResponse<SetupAdminResponse>> Handle(
        SetupAdminCommand request,
        CancellationToken cancellationToken)
    {
        // Check if any admin exists
        var adminExists = await unitOfWork.Repository<UserEntity>()
            .AnyAsync(x => x.Role == UserRole.Admin && !x.DeletedAt.HasValue);

        if (adminExists)
            throw new BadRequestException("Đã tồn tại admin trong hệ thống");

        var user = new UserEntity
        {
            Username = request.Username,
            Password = passwordHasher.HashPassword(request.Password),
            FullName = request.FullName,
            Role = UserRole.Admin
        };

        await unitOfWork.Repository<UserEntity>().AddAsync(user);
        await unitOfWork.SaveChangesAsync();

        var response = new SetupAdminResponse
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role.ToString()
        };

        return ApiResponse<SetupAdminResponse>.Success(
            response,
            "Tạo admin thành công. Vui lòng đăng nhập qua IdentityServer."
        );
    }
}
```

- [ ] **Step 2.2: Verify build**

Run: `cd RetailStoreManagement && dotnet build src/Application/Application.csproj`

Expected: Build succeeded, 0 errors.

- [ ] **Step 2.3: Commit**

```bash
git add src/Application/Features/Setup/Handlers/SetupAdminCommandHandler.cs
git commit -m "feat(app): add SetupAdminCommandHandler without IAuthService

Handler tạo admin bootstrap, không generate token (client phải
redirect đến IdentityServer login sau khi setup).

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Tạo `SetupController`

**Files:**
- Create: `src/WebApi/Controllers/SetupController.cs`

- [ ] **Step 3.1: Tạo controller mới**

File: `src/WebApi/Controllers/SetupController.cs`

```csharp
using Application.Features.Setup.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Abstractions;

namespace WebApi.Controllers;

/// <summary>
/// Bootstrap controller — tạo admin đầu tiên của hệ thống khi DB rỗng.
/// Endpoint này là một lần bootstrap, sau đó authentication thực hiện
/// qua Duende IdentityServer (https://localhost:5001).
/// </summary>
[Route("api/setup")]
public class SetupController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Tạo admin đầu tiên. Chỉ thành công khi chưa có admin nào trong DB.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("admin")]
    public async Task<IActionResult> SetupAdmin([FromBody] SetupAdminCommand command)
    {
        return Ok(await Mediator.Send(command));
    }
}
```

- [ ] **Step 3.2: Verify build**

Run: `cd RetailStoreManagement && dotnet build`

Expected: Build succeeded (cả `AuthController` cũ + `SetupController` mới cùng tồn tại).

- [ ] **Step 3.3: Commit**

```bash
git add src/WebApi/Controllers/SetupController.cs
git commit -m "feat(webapi): add SetupController for admin bootstrap

Di dời POST /setup-admin từ AuthController sang SetupController riêng.
Route mới: POST /api/setup/admin. AuthController sẽ bị xóa ở task sau.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Xóa `AuthController`

**Files:**
- Delete: `src/WebApi/Controllers/AuthController.cs`

- [ ] **Step 4.1: Xóa file**

Run:
```bash
rm src/WebApi/Controllers/AuthController.cs
```

- [ ] **Step 4.2: Verify không còn reference**

Run: `grep -rn "AuthController" src/ 2>/dev/null`

Expected: Không match (trừ có thể 1 reference trong doc).

- [ ] **Step 4.3: Verify build**

Run: `cd RetailStoreManagement && dotnet build`

Expected: Build succeeded. `POST /api/auth/login|logout|refresh` giờ trả 404 thay vì 410.

- [ ] **Step 4.4: Commit**

```bash
git add -u src/WebApi/Controllers/
git commit -m "refactor(webapi): remove legacy AuthController

login/logout/refresh endpoints đã 410 Gone từ Phase 2. Frontend giờ
dùng OIDC Authorization Code + PKCE + DPoP qua IdentityServer.
setup-admin đã được di dời sang SetupController.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Xóa folder `Application/Features/Auth/`

**Files:**
- Delete: `src/Application/Features/Auth/` (toàn bộ)

- [ ] **Step 5.1: Verify không còn ai reference `Application.Features.Auth`**

Run: `grep -rn "Application.Features.Auth" src/ 2>/dev/null`

Expected: Không match.

Nếu có match → stop, check reference trước khi xóa.

- [ ] **Step 5.2: Xóa folder**

Run:
```bash
rm -rf src/Application/Features/Auth/
```

- [ ] **Step 5.3: Verify build**

Run: `cd RetailStoreManagement && dotnet build`

Expected: Build có thể FAIL ở `DependencyInjection.cs` vì còn reference `IAuthService`. Đây là expected — fix ở Task 6.

Nếu build FAIL với lỗi khác (không phải `IAuthService`) → stop, investigate.

- [ ] **Step 5.4: Commit (có thể broken build, chấp nhận trong sequence)**

```bash
git add -u src/Application/Features/Auth/
git commit -m "refactor(app): remove legacy Auth/ feature folder

Xóa Commands/Handlers/Services/Dtos/Validators của Auth legacy.
Build sẽ fail tạm thời ở Infrastructure.DependencyInjection do còn
reference IAuthService — fix ở commit kế tiếp.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: Sửa `Infrastructure/DependencyInjection.cs` — bỏ `IAuthService` registration

**Files:**
- Modify: `src/Infrastructure/DependencyInjection.cs`

- [ ] **Step 6.1: Read file hiện tại**

Run: `cat src/Infrastructure/DependencyInjection.cs`

- [ ] **Step 6.2: Xóa dòng `using Application.Features.Auth.Services;` và dòng register `IAuthService`**

Sử dụng Edit tool:
- Xóa: `using Application.Features.Auth.Services;`
- Xóa: `services.AddScoped<IAuthService, AuthService>();`

File sau khi sửa:

```csharp
using Domain.Repositories;
using Domain.SeedWork;
using Infrastructure.Database;
using Infrastructure.Repositories;
using Infrastructure.SeedWork;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

/// <summary>
/// Extension methods để đăng ký các services của Infrastructure layer
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database - Keep default naming convention (mixed: PascalCase for base, snake_case configured manually)
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Generic Repository & Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // Specific Repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // Services
        services.AddScoped<Application.Abstractions.Services.IPasswordHasher, PasswordHasher>();
        services.AddScoped<Application.Common.Interfaces.IImageKitService, ImageKitService>();

        return services;
    }
}
```

- [ ] **Step 6.3: Verify build**

Run: `cd RetailStoreManagement && dotnet build`

Expected: Build có thể vẫn FAIL ở `AuthService.cs` vì implement `IAuthService` không còn tồn tại. Đây là expected — fix ở Task 7.

- [ ] **Step 6.4: Commit**

```bash
git add -u src/Infrastructure/DependencyInjection.cs
git commit -m "refactor(infra): remove IAuthService DI registration

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: Xóa `AuthService.cs`

**Files:**
- Delete: `src/Infrastructure/Services/AuthService.cs`

- [ ] **Step 7.1: Xóa file**

Run:
```bash
rm src/Infrastructure/Services/AuthService.cs
```

- [ ] **Step 7.2: Verify build**

Run: `cd RetailStoreManagement && dotnet build`

Expected: Build succeeded, 0 errors. Nếu còn lỗi reference `AuthService` ở đâu đó → grep tìm và xử lý.

- [ ] **Step 7.3: Commit**

```bash
git add -u src/Infrastructure/Services/
git commit -m "refactor(infra): remove AuthService (legacy JWT impl)

AuthService.cs tự sign JWT + quản lý refresh token DB. Code không còn
được gọi từ đâu sau khi xóa AuthController và Auth/ feature.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: Xóa `LegacyAuthModels.cs`

**Files:**
- Delete: `src/WebApi/Models/LegacyAuthModels.cs`

- [ ] **Step 8.1: Verify không còn reference**

Run: `grep -rn "LegacyLoginResponse\|LegacyUserDto\|LegacyAuthModels" src/ 2>/dev/null`

Expected: Không match.

- [ ] **Step 8.2: Xóa file**

Run:
```bash
rm src/WebApi/Models/LegacyAuthModels.cs
```

- [ ] **Step 8.3: Verify build**

Run: `cd RetailStoreManagement && dotnet build`

Expected: Build succeeded, 0 errors.

- [ ] **Step 8.4: Commit**

```bash
git add -u src/WebApi/Models/
git commit -m "refactor(webapi): remove LegacyAuthModels.cs

DTOs không còn được reference sau khi xóa AuthController.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: Xóa navigation `UserRefreshTokens` khỏi `UserEntity`

**Files:**
- Modify: `src/Domain/Entities/UserEntity.cs`

- [ ] **Step 9.1: Read file hiện tại**

Run: `cat src/Domain/Entities/UserEntity.cs`

- [ ] **Step 9.2: Xóa property navigation**

Dùng Edit tool xóa dòng:

```csharp
    public virtual ICollection<UserRefreshToken> UserRefreshTokens { get; set; } = [];
```

Giữ nguyên các navigation khác (nếu có).

- [ ] **Step 9.3: Verify build**

Run: `cd RetailStoreManagement && dotnet build`

Expected: Có thể FAIL ở `EntityConfigurations.cs` dòng `.WithMany(u => u.UserRefreshTokens)`. Đây là expected — fix ở Task 10.

- [ ] **Step 9.4: Commit (có thể broken build)**

```bash
git add -u src/Domain/Entities/UserEntity.cs
git commit -m "refactor(domain): remove UserRefreshTokens navigation from UserEntity

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

## Task 10: Xóa `UserRefreshTokenConfiguration` khỏi `EntityConfigurations.cs`

**Files:**
- Modify: `src/Infrastructure/Database/Configurations/EntityConfigurations.cs`

- [ ] **Step 10.1: Xóa class `UserRefreshTokenConfiguration`**

Dùng Edit tool xóa toàn bộ class (dòng 245-265 trong file gốc):

```csharp
public class UserRefreshTokenConfiguration : IEntityTypeConfiguration<UserRefreshToken>
{
    public void Configure(EntityTypeBuilder<UserRefreshToken> builder)
    {
        builder.ToTable("UserRefreshTokens");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("Id");
        // UserRefreshTokens có mixed naming: created_at là snake_case, UpdatedAt/DeletedAt là PascalCase
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
        builder.Property(x => x.DeletedAt).HasColumnName("DeletedAt");

        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Token).HasColumnName("token").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(x => x.IsRevoked).HasColumnName("is_revoked").IsRequired();

        builder.HasOne(x => x.User).WithMany(u => u.UserRefreshTokens).HasForeignKey(x => x.UserId);
    }
}
```

- [ ] **Step 10.2: Verify build**

Run: `cd RetailStoreManagement && dotnet build`

Expected: Có thể FAIL ở `ApplicationDbContext.cs` dòng `DbSet<UserRefreshToken>`. Fix ở Task 11.

- [ ] **Step 10.3: Commit**

```bash
git add -u src/Infrastructure/Database/Configurations/EntityConfigurations.cs
git commit -m "refactor(infra): remove UserRefreshTokenConfiguration

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

## Task 11: Xóa `DbSet<UserRefreshToken>` khỏi `ApplicationDbContext`

**Files:**
- Modify: `src/Infrastructure/Database/ApplicationDbContext.cs`

- [ ] **Step 11.1: Sửa file**

Dùng Edit tool:

Xóa dòng:
```csharp
    public DbSet<UserRefreshToken> UserRefreshTokens => Set<UserRefreshToken>();
```

Thay bằng comment ghi rõ orphan table:

```csharp
    // NOTE: Table `user_refresh_tokens` (PascalCase `UserRefreshTokens`) còn tồn tại
    // trên Neon DB nhưng KHÔNG được map từ entity nào. Đây là orphan table sau khi
    // cleanup legacy JWT auth ở Phase 4. Drop table bằng EF migration ở Phase 5
    // (task 5.5 — xem tasks/dpop-migration.md).
```

- [ ] **Step 11.2: Verify build**

Run: `cd RetailStoreManagement && dotnet build`

Expected: Có thể FAIL nếu còn file nào khác reference `UserRefreshToken`. Fix ở Task 12.

- [ ] **Step 11.3: Commit**

```bash
git add -u src/Infrastructure/Database/ApplicationDbContext.cs
git commit -m "refactor(infra): remove DbSet<UserRefreshToken>, mark table as orphan

Table user_refresh_tokens vẫn còn trên DB cho rollback safety.
Phase 5 task 5.5 sẽ drop table bằng EF migration.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

## Task 12: Xóa entity `UserRefreshToken.cs`

**Files:**
- Delete: `src/Domain/Entities/UserRefreshToken.cs`

- [ ] **Step 12.1: Verify không còn reference**

Run: `grep -rn "UserRefreshToken" src/ 2>/dev/null`

Expected: Không match trong code C# (trừ comment trong `ApplicationDbContext.cs` có chứa `user_refresh_tokens` string — OK).

Nếu còn match ở file `.cs` → stop, fix reference trước.

- [ ] **Step 12.2: Xóa file**

Run:
```bash
rm src/Domain/Entities/UserRefreshToken.cs
```

- [ ] **Step 12.3: Verify build toàn solution**

Run: `cd RetailStoreManagement && dotnet build`

Expected: **Build succeeded, 0 errors, không warning mới.**

- [ ] **Step 12.4: Commit**

```bash
git add -u src/Domain/Entities/
git commit -m "refactor(domain): remove UserRefreshToken entity

Entity không còn được reference. Table user_refresh_tokens trên DB
vẫn còn (orphan) — drop ở Phase 5 migration.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

## Task 13: Xóa `JwtSettings` khỏi `appsettings.json`

**Files:**
- Modify: `src/WebApi/appsettings.json`

- [ ] **Step 13.1: Sửa file**

Dùng Edit tool xóa section `JwtSettings`:

```json
  "JwtSettings": {
    "SecretKey": "your-super-secret-key-at-least-32-chars-long-for-production",
    "Issuer": "store-management",
    "Audience": "store-management",
    "ExpiryMinutes": "15",
    "RefreshExpiryDays": "7"
  },
```

Và dấu phẩy phía trước (nếu có) để JSON hợp lệ. Thêm section `IdentityServer` nếu chưa có:

```json
  "IdentityServer": {
    "Authority": "https://localhost:5001"
  },
```

File sau khi sửa (phần top-level keys):

```json
{
  "Logging": { ... },
  "AllowedHosts": "*",
  "ConnectionStrings": { ... },
  "IdentityServer": {
    "Authority": "https://localhost:5001"
  },
  "CorsSettings": { ... },
  "ImageKit": { ... }
}
```

- [ ] **Step 13.2: Verify JSON hợp lệ**

Run: `python3 -c "import json; json.load(open('src/WebApi/appsettings.json'))" && echo "OK"`

Expected: `OK`.

- [ ] **Step 13.3: Commit**

```bash
git add -u src/WebApi/appsettings.json
git commit -m "chore(webapi): remove JwtSettings from appsettings.json

Thêm IdentityServer.Authority làm authoritative config thay thế.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

## Task 14: Xóa `JwtSettings` khỏi `appsettings.Development.json`

**Files:**
- Modify: `src/WebApi/appsettings.Development.json`

- [ ] **Step 14.1: Sửa file**

Dùng Edit tool xóa section `JwtSettings`:

```json
  "JwtSettings": {
    "SecretKey": "",
    "Issuer": "store-management",
    "Audience": "store-management",
    "ExpiryMinutes": "60",
    "RefreshExpiryDays": "7"
  },
```

File sau khi sửa (chỉ giữ Logging, ConnectionStrings, ImageKit):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "ImageKit": {
    "PublicKey": "",
    "PrivateKey": "",
    "UrlEndpoint": "https://ik.imagekit.io/nguyenthanhhungdev"
  }
}
```

- [ ] **Step 14.2: Verify JSON hợp lệ**

Run: `python3 -c "import json; json.load(open('src/WebApi/appsettings.Development.json'))" && echo "OK"`

Expected: `OK`.

- [ ] **Step 14.3: Final build check**

Run: `cd RetailStoreManagement && dotnet build`

Expected: Build succeeded, 0 errors.

- [ ] **Step 14.4: Commit**

```bash
git add -u src/WebApi/appsettings.Development.json
git commit -m "chore(webapi): remove JwtSettings from appsettings.Development.json

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

## Task 15: Global verification

**Files:** (no changes)

- [ ] **Step 15.1: Grep verify không còn legacy reference**

Run:
```bash
grep -rn "JwtSettings\|IAuthService\|AuthService\|UserRefreshToken\|LegacyAuthModels\|AuthController" src/ 2>/dev/null | grep -v ".md"
```

Expected: Chỉ hiển thị comment trong `ApplicationDbContext.cs` (mô tả orphan table `user_refresh_tokens` — lowercase, string trong comment). Không có reference C# identifier nào.

- [ ] **Step 15.2: Build toàn solution**

Run: `cd RetailStoreManagement && dotnet build`

Expected: 0 errors, 0 new warnings.

- [ ] **Step 15.3: Run tests nếu có**

Run: `cd RetailStoreManagement && dotnet test`

Expected: All pass, hoặc "No test is available" nếu chưa có test project.

- [ ] **Step 15.4: Manual smoke test**

Trong shell 1, chạy IdentityServer:
```bash
cd RetailStoreManagement/src/IdentityServer && dotnet run
```

Trong shell 2, chạy WebApi:
```bash
cd RetailStoreManagement/src/WebApi && dotnet run
```

Trong shell 3, gọi setup-admin (nếu DB rỗng):
```bash
curl -k -X POST https://localhost:5175/api/setup/admin \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@123","fullName":"System Admin"}'
```

Expected (DB rỗng): `200 OK`, response chứa `{ "userId": ..., "username": "admin", "role": "Admin" }`, **KHÔNG có** `accessToken` / `refreshToken`.

Gọi lại lần 2:
```bash
curl -k -X POST https://localhost:5175/api/setup/admin \
  -H "Content-Type: application/json" \
  -d '{"username":"admin2","password":"Admin@123","fullName":"X"}'
```

Expected: `400 Bad Request`, message "Đã tồn tại admin trong hệ thống".

Gọi các endpoint legacy (phải 404):
```bash
curl -k -X POST https://localhost:5175/api/auth/login -w "\nHTTP %{http_code}\n"
curl -k -X POST https://localhost:5175/api/auth/logout -w "\nHTTP %{http_code}\n"
curl -k -X POST https://localhost:5175/api/auth/refresh -w "\nHTTP %{http_code}\n"
```

Expected: All return `HTTP 404`.

Mở Swagger UI ở browser: `https://localhost:5175/swagger`
- Click **Authorize** → redirect IdentityServer login → nhập `admin` / `Admin@123` → callback về Swagger
- Click **Try it out** → **Execute** một endpoint (ví dụ `GET /api/admin/products`)
- Expected: `200 OK` với plain Bearer token.

Mở frontend: `http://localhost:5173`
- Click Login → redirect IdentityServer → login → callback
- Expected: login thành công, list products load được (DPoP flow).

- [ ] **Step 15.5: Commit verification report (optional)**

Nếu tạo file log/report, commit. Nếu không, skip step này.

---

## Task 16: Update `tasks/dpop-migration.md` — mark Phase 4 done, add Phase 5

**Files:**
- Modify: `RetailStoreManagement/tasks/dpop-migration.md`

- [ ] **Step 16.1: Check off các task Phase 4**

Tại section `### ⬜ Phase 4: Backend Auth Cleanup`:
- Đổi `⬜` thành `✅`
- Check `[x]` các task 4.1, 4.2, 4.3, 4.4, 4.5

Giữ nguyên task 4.6 và 4.7 nhưng thêm note "[MOVED TO PHASE 5]".

Sau khi sửa, section thành:

```markdown
### ✅ Phase 4: Backend Auth Cleanup
- [x] **4.1** Xóa `AuthController` (login/refresh/logout — hiện đã 410 Gone)
- [x] **4.2** Xóa `AuthService`, `JwtSettings`, helpers cookies
- [x] **4.3** Xóa cookie infrastructure đã không dùng *(đã xóa từ Phase 2, verify lại ở Phase 4)*
- [x] **4.4** Xóa `JwtSettings` khỏi `appsettings.json`
- [x] **4.5** Update CORS — verify không cần thêm IdentityServer origin (WebApi không gọi IDS)
- [ ] **4.6** Redis distributed cache cho replay detection (production) **[MOVED TO PHASE 5]**
- [ ] **4.7** Rate limiting / account lockout (deferred từ Phase 2 review I6) **[MOVED TO PHASE 5]**
```

- [ ] **Step 16.2: Thêm section Phase 5 mới ở cuối checklist**

Thêm ngay sau Phase 4 section:

```markdown
### ⬜ Phase 5: Production Hardening
- [ ] **5.1** Redis distributed cache cho DPoP replay detection (multi-instance)
- [ ] **5.2** Rate limiting login (ASP.NET Core rate limiter middleware)
- [ ] **5.3** Account lockout — thêm `FailedLoginAttempts` + `LockedUntil` vào `UserEntity` + migration
- [ ] **5.4** Thay `AddDeveloperSigningCredential()` → `AddSigningCredential()` từ secret store
- [ ] **5.5** Drop orphan table `user_refresh_tokens` (EF migration)
- [ ] **5.6** Integration tests: DPoP-bound token qua plain Bearer → 401 (verify `cnf.jkt` enforcement)
- [ ] **5.7** Unit tests: `buildDPoPProof` + `computeJwkThumbprint` (vitest setup)
- [ ] **5.8** Rotate Neon DB password (còn trong git history từ round 1)
- [ ] **5.9** `UseHsts()` / `UseHttpsRedirection()` cho Production pipeline
- [ ] **5.10** Pin package versions trong `Directory.Packages.props`
```

- [ ] **Step 16.3: Thêm section "Phase 4 Implementation Notes" ở cuối document**

Thêm ngay trước `## Code Review Round...` cuối cùng (hoặc ở cuối nếu không có):

```markdown
---

## Phase 4 Implementation Notes (2026-04-19)

### Approach

Pure code cleanup, không thêm tính năng. Các hạng mục production hardening
(Redis, rate limiting, lockout, signing key) đã tách sang Phase 5.

### Files đã xóa

- `WebApi/Controllers/AuthController.cs`
- `WebApi/Models/LegacyAuthModels.cs`
- `Application/Features/Auth/` (toàn bộ folder)
- `Infrastructure/Services/AuthService.cs`
- `Domain/Entities/UserRefreshToken.cs`

### Files mới

- `WebApi/Controllers/SetupController.cs` — bootstrap endpoint `POST /api/setup/admin`
- `Application/Features/Setup/Commands/SetupAdminCommand.cs`
- `Application/Features/Setup/Dtos/SetupAdminResponse.cs`
- `Application/Features/Setup/Handlers/SetupAdminCommandHandler.cs`

### Files sửa

| File | Thay đổi |
|------|---------|
| `Infrastructure/DependencyInjection.cs` | Bỏ `IAuthService` registration |
| `Infrastructure/Database/ApplicationDbContext.cs` | Bỏ `DbSet<UserRefreshToken>`, thêm comment orphan table |
| `Infrastructure/Database/Configurations/EntityConfigurations.cs` | Bỏ `UserRefreshTokenConfiguration` |
| `Domain/Entities/UserEntity.cs` | Bỏ navigation `UserRefreshTokens` |
| `WebApi/appsettings.json` | Xóa section `JwtSettings`, thêm `IdentityServer.Authority` |
| `WebApi/appsettings.Development.json` | Xóa section `JwtSettings` |

### Quyết định thiết kế

| Quyết định | Lý do |
|-----------|-------|
| Tách `SetupController` riêng thay vì gộp vào `AuthController` | Phân biệt rõ bootstrap tooling vs runtime auth |
| `SetupAdminResponse` không chứa token | Sau setup, client redirect đến IdentityServer login bình thường |
| Giữ table `user_refresh_tokens` (orphan) | Rollback an toàn, drop migration thuộc Phase 5 cùng các DB change khác |
| Không viết unit test | Ngoài scope cleanup phase, defer Phase 5 |

### Swagger flow không bị ảnh hưởng

Tài liệu chi tiết: `tasks/dpop-swagger-flow.md`. Swagger UI dùng OAuth2 Auth Code + PKCE
qua IdentityServer (client `swagger-ui`, env-gated Dev only). Không đụng đến legacy
JWT endpoints nên Phase 4 cleanup không ảnh hưởng.
```

- [ ] **Step 16.4: Commit**

```bash
git add -u RetailStoreManagement/tasks/dpop-migration.md
git commit -m "docs: mark Phase 4 complete, add Phase 5 production hardening

Phase 4 đã hoàn tất backend auth cleanup. Các hạng mục Redis cache,
rate limiting, lockout, signing key rotation, test coverage di dời
sang Phase 5.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

## Task 17: Final summary commit

**Files:** (no changes, just verification)

- [ ] **Step 17.1: Check git log Phase 4**

Run: `git log --oneline cb7056e..HEAD`

Expected: List các commit Task 1-16. Khoảng 15-16 commits.

- [ ] **Step 17.2: Check working tree clean**

Run: `git status`

Expected: `nothing to commit, working tree clean`.

- [ ] **Step 17.3: Run final build + tests**

Run:
```bash
cd RetailStoreManagement
dotnet build
dotnet test 2>/dev/null || echo "No tests"
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 17.4: Done — report completion**

Không có commit step này. Task này chỉ verify + report.
