# Phase 4 — Backend Auth Cleanup Design

> **Ngày:** 2026-04-19
> **Branch:** `feature/dpop-implementation`
> **Phase trước:** Phase 3 (Frontend OIDC + PKCE + DPoP) — đã merge-ready
> **Liên quan:** `RetailStoreManagement/tasks/dpop-migration.md`

---

## 1. Mục tiêu

Xóa toàn bộ legacy JWT/cookie authentication code ở backend sau khi đã chuyển hoàn toàn sang Duende IdentityServer + DPoP ở Phase 1–3. Phase này là pure code cleanup, không thêm tính năng mới. Các hạng mục production hardening (Redis cache, rate limiting, lockout) tách sang Phase 5.

**Out of scope:**
- Redis distributed cache cho replay detection
- Rate limiting / account lockout
- Drop table `user_refresh_tokens` trên DB (để Phase 5 migration)
- Unit/integration tests mới
- Rotation signing key production

## 2. Architecture Changes

### Before (hiện tại)

```
WebApi/Controllers/AuthController.cs          [410 Gone + POST /setup-admin]
WebApi/Models/LegacyAuthModels.cs              [LegacyLoginResponse, LegacyUserDto]

Application/Features/Auth/
├── Commands/
│   ├── LoginCommand.cs
│   ├── LogoutCommand.cs
│   ├── RefreshTokenCommand.cs
│   └── SetupAdminCommand.cs
├── Handlers/
│   ├── LoginCommandHandler.cs
│   ├── LogoutCommandHandler.cs
│   ├── RefreshTokenCommandHandler.cs
│   └── SetupAdminCommandHandler.cs           [dùng IAuthService để gen token]
├── Dtos/
│   └── LoginResponse.cs
└── Services/
    └── IAuthService.cs

Infrastructure/Services/AuthService.cs        [JWT sign/validate + refresh token store]
Infrastructure/DependencyInjection.cs         [services.AddScoped<IAuthService,...>]

Domain/Entities/UserRefreshToken.cs
Infrastructure/Database/Configurations/
    EntityConfigurations.cs                   [UserRefreshToken config]
Infrastructure/Database/ApplicationDbContext  [DbSet<UserRefreshToken>]

WebApi/appsettings.json                       [JwtSettings section]
WebApi/appsettings.Development.json           [JwtSettings section]
```

### After (Phase 4)

```
WebApi/Controllers/SetupController.cs         [NEW — chỉ POST /api/setup/admin]

Application/Features/Setup/                   [NEW folder — di dời setup-admin]
├── Commands/
│   └── SetupAdminCommand.cs                  [username, password, fullName]
├── Handlers/
│   └── SetupAdminCommandHandler.cs           [không gọi IAuthService; trả user info]
└── Dtos/
    └── SetupAdminResponse.cs                 [userId, username, fullName, role]

Infrastructure/DependencyInjection.cs         [bỏ IAuthService registration]

Domain/Entities/UserEntity.cs                 [giữ nguyên]
Domain/Entities/UserRefreshToken.cs           [XÓA]
Infrastructure/Database/Configurations/       [XÓA binding UserRefreshToken]
Infrastructure/Database/ApplicationDbContext  [XÓA DbSet<UserRefreshToken>, thêm
                                               comment về orphan table]

WebApi/appsettings.json                       [XÓA JwtSettings section]
WebApi/appsettings.Development.json           [XÓA JwtSettings section]
```

### Database

**Không thay đổi schema:** Table `user_refresh_tokens` còn trên Neon DB sẽ trở thành orphan (entity code xóa, DbSet xóa, table còn). Thêm comment trong `ApplicationDbContext.cs` ghi rõ để Phase 5 drop bằng migration.

Lý do giữ table: rollback an toàn, tránh migration cross-session phức tạp. Orphan table không có referential constraint nào tới user_entity → không block bất kỳ query nào.

## 3. Component Changes

### 3.1. `SetupController.cs` (mới)

```csharp
[Route("api/setup")]
public class SetupController(IMediator mediator) : BaseApiController(mediator)
{
    [AllowAnonymous]
    [HttpPost("admin")]
    public async Task<IActionResult> SetupAdmin([FromBody] SetupAdminCommand command)
        => Ok(await Mediator.Send(command));
}
```

- Namespace: `WebApi.Controllers`
- Route: `POST /api/setup/admin`
- `[AllowAnonymous]` vì bootstrap — chưa có user nào
- Trả `SetupAdminResponse` thay vì `LoginResponse` (không có token)

### 3.2. `Application/Features/Setup/`

Folder mới, song song với `Auth/`. Sau khi xóa `Auth/` toàn bộ:
- `Setup/Commands/SetupAdminCommand.cs` — record với Username, Password, FullName
- `Setup/Dtos/SetupAdminResponse.cs` — UserId, Username, FullName, Role (string)
- `Setup/Handlers/SetupAdminCommandHandler.cs`:
  - Inject: `IUnitOfWork`, `IPasswordHasher`
  - Check admin tồn tại (`AnyAsync(x => x.Role == UserRole.Admin && !x.DeletedAt.HasValue)`)
  - Nếu có → throw `BadRequestException("Đã tồn tại admin")`
  - Tạo `UserEntity { Username, Password = Hash(password), FullName, Role = Admin }`
  - Save
  - Trả `ApiResponse<SetupAdminResponse>.Success(...)` — **không generate token**

### 3.3. Xóa files

| Nhóm | Files |
|------|-------|
| Controllers | `WebApi/Controllers/AuthController.cs` |
| Models | `WebApi/Models/LegacyAuthModels.cs` |
| Auth feature | `Application/Features/Auth/` toàn bộ thư mục |
| Services | `Application/Features/Auth/Services/IAuthService.cs` (nằm trong folder trên) |
| Infrastructure | `Infrastructure/Services/AuthService.cs` |
| Domain | `Domain/Entities/UserRefreshToken.cs` |

### 3.4. Sửa files

| File | Thay đổi |
|------|---------|
| `Infrastructure/DependencyInjection.cs` | Bỏ `services.AddScoped<IAuthService, AuthService>()` |
| `Infrastructure/Database/ApplicationDbContext.cs` | Bỏ `DbSet<UserRefreshToken>`; thêm comment về orphan table |
| `Infrastructure/Database/Configurations/EntityConfigurations.cs` | Bỏ apply của `UserRefreshToken` configuration |
| `WebApi/appsettings.json` | Xóa section `"JwtSettings": {...}` |
| `WebApi/appsettings.Development.json` | Xóa section `"JwtSettings": {...}` |

## 4. Execution Order

Thứ tự bắt buộc để mỗi bước build thành công:

1. **Tạo `Setup/` feature mới** — Commands, Dtos, Handlers (song song `Auth/` cũ)
2. **Tạo `SetupController`** — reference `SetupAdminCommand` mới
3. **Xóa `AuthController`** — không còn controller nào reference `Auth/Commands/SetupAdminCommand` cũ
4. **Xóa `Application/Features/Auth/`** — toàn bộ thư mục (Commands, Handlers, Services, Dtos)
5. **Xóa `LegacyAuthModels.cs`** — không còn controller dùng
6. **Sửa `DependencyInjection.cs`** — bỏ `IAuthService` registration
7. **Xóa `AuthService.cs`** — không còn DI registration
8. **Xóa `UserRefreshToken` entity** — không còn service dùng
9. **Sửa `ApplicationDbContext.cs`** — bỏ DbSet, thêm comment orphan
10. **Sửa `EntityConfigurations.cs`** — bỏ binding
11. **Sửa `appsettings*.json`** — xóa `JwtSettings`
12. **Build** — `dotnet build` không error/warning mới
13. **Manual verification** — xem Section 5

## 5. Verification Matrix

| # | Test | Expected |
|---|------|---------|
| V1 | `dotnet build` toàn solution | 0 errors, không warning mới |
| V2 | `dotnet test` (nếu có test project) | All pass |
| V3 | `grep -r "JwtSettings\|IAuthService\|AuthService\|UserRefreshToken\|LegacyAuthModels\|AuthController" src/` | Không match (ngoài migration file cũ + doc) |
| V4 | `POST /api/setup/admin` (DB chưa có admin) | 200 + `SetupAdminResponse` (userId, username, fullName, role="Admin") |
| V5 | `POST /api/setup/admin` lần 2 | 400 "Đã tồn tại admin" |
| V6 | `POST /api/auth/login` | 404 (controller đã xóa) |
| V7 | `POST /api/auth/logout` | 404 |
| V8 | `POST /api/auth/refresh` | 404 |
| V9 | Swagger UI mở `/swagger` (Dev) | 200, oauth2 scheme trỏ IdentityServer |
| V10 | Swagger Authorize → login → call API | 200 với plain Bearer (per `dpop-swagger-flow.md`) |
| V11 | Frontend login qua IdentityServer (`/auth/login` → `/callback` → app) | 200 với DPoP token |
| V12 | Frontend gọi API với DPoP | 200 |

## 6. Rollback Plan

Nếu cần revert:
- `git revert` commits của Phase 4
- DB schema không đổi (orphan table `user_refresh_tokens` vẫn còn) → không migration reverse
- Frontend/IdentityServer không bị touch → không ảnh hưởng

## 7. Phase 5 — Production Hardening (sẽ bổ sung vào dpop-migration.md)

```markdown
### ⬜ Phase 5: Production Hardening
- [ ] 5.1 Redis distributed cache cho DPoP replay detection (multi-instance)
- [ ] 5.2 Rate limiting login (ASP.NET Core rate limiter middleware)
- [ ] 5.3 Account lockout — thêm FailedLoginAttempts + LockedUntil vào UserEntity + migration
- [ ] 5.4 Thay AddDeveloperSigningCredential → AddSigningCredential từ secret store
- [ ] 5.5 Drop orphan table user_refresh_tokens (migration)
- [ ] 5.6 Integration tests: DPoP-bound token qua plain Bearer → 401 (verify cnf.jkt enforcement)
- [ ] 5.7 Unit tests: buildDPoPProof + computeJwkThumbprint (vitest setup)
- [ ] 5.8 Rotate Neon DB password (còn trong git history từ round 1)
- [ ] 5.9 `UseHsts()/UseHttpsRedirection()` cho Production
- [ ] 5.10 Pin package versions trong Directory.Packages.props
```

## 8. Design Decisions

| Quyết định | Lý do |
|-----------|-------|
| Tách `SetupController` riêng | Phân biệt rõ "bootstrap tooling" với "runtime auth". Sau này có thể gate thêm `[Authorize]` nếu cần rotate admin. |
| `SetupAdminResponse` không có token | Sau setup, frontend redirect đến IdentityServer login bình thường. Không cross-concern với OIDC flow. |
| Giữ table `user_refresh_tokens` | Rollback an toàn. Drop migration tách Phase 5 cùng với các DB migration khác (UserEntity add FailedLoginAttempts). |
| Không viết unit test mới | Ngoài scope cleanup. Tests defer Phase 5. |
| Setup/ folder riêng thay vì gộp vào Users/ | Users/ là CRUD admin-authenticated. Setup/ là anonymous bootstrap — semantics khác. |

## 9. Risks

| Risk | Mitigation |
|------|-----------|
| Build fail do còn reference chưa gỡ | Execution order strict (Section 4) |
| Frontend vỡ do API endpoint đổi | Frontend không còn gọi `/api/auth/*` (đã chuyển qua IdentityServer từ Phase 3) |
| Swagger UI vỡ | Swagger dùng OAuth2 qua IdentityServer (Phase 2), không liên quan legacy |
| Orphan table gây nhầm lẫn | Comment trong `ApplicationDbContext` + Phase 5 task drop |
