# Task: Chuyển RetailStoreManagement sang DPoP Authentication

**Branch:** `feature/dpop-implementation`
**Ngày bắt đầu:** 2026-04-17
**Mục tiêu:** Thay thế hệ thống JWT tự phát hành bằng Duende IdentityServer + DPoP (OAuth 2.1 chuẩn)

---

## Tổng Quan Kiến Trúc

```
Before:  Frontend → POST /api/auth/login → WebApi (tự phát JWT) → Cookie/Header
After:   Frontend → IdentityServer (Auth Code + PKCE) → DPoP Access Token → WebApi
```

**Stack:**
- IdentityServer: Duende IdentityServer 7.0 (port 5001)
- API: Duende.IdentityModel để validate DPoP proof
- Frontend: oidc-client-ts + SubtleCrypto (ECDSA P-384)

---

## Checklist Các Phase

### ✅ Phase 0: Chuẩn Bị
- [x] **0.1** Tạo project `src/IdentityServer/` và thêm vào solution
- [x] **0.2** Cài Duende.IdentityServer 7.0, Duende.IdentityServer.AspNetIdentity
- [x] **0.3** Generate ECDSA P-384 key pair (`ecdsa384-private.pem`, `ecdsa384-public.pem`)
- [x] **0.4** Cấu hình port 5001 trong `launchSettings.json`

### ✅ Phase 1: IdentityServer Setup
- [x] **1.1** `Config.cs` — định nghĩa IdentityResources, ApiScopes (`retail-api`), Client (`react-dpop` với Authorization Code + PKCE)
- [x] **1.2** `CustomProfileService.cs` — implement `IProfileService`, load claims từ `ApplicationDbContext`
- [x] `CustomResourceOwnerPasswordValidator.cs` — implement `IResourceOwnerPasswordValidator`, verify BCrypt password
- [x] **1.3** `Program.cs` — register IdentityServer services, Razor Pages, pipeline
- [x] **1.4** Login/Logout UI — Razor Pages tại `/Account/Login`, `/Account/Logout`
- [x] **1.5** Reference `Infrastructure.csproj` để reuse `ApplicationDbContext` + `UserEntity`
- [ ] **1.6** Verify `.well-known/openid-configuration` endpoint hoạt động *(chưa test runtime)*

### ⬜ Phase 2: API — Tích Hợp DPoP Validation
- [ ] **2.1** Copy DPoP module vào `src/WebApi/DPoP/` (từ Duende reference samples)
- [ ] **2.2** Cài `Duende.IdentityModel` vào `WebApi.csproj`
- [ ] **2.3** Đổi authentication scheme sang authority-based (`Authority = https://localhost:5001`)
- [ ] **2.4** Register `ConfigureDPoPTokensForScheme("dpoptokenscheme")`
- [ ] **2.5** Update authorization policies — `RequireClaim("scope", "retail-api")`
- [ ] **2.6** Xóa cookie fallback trong `OnMessageReceived`
- [ ] **2.7** Xóa/deprecate self-issued auth code
- [ ] **2.8** Update Swagger security definition

### ⬜ Phase 3: Frontend — Authorization Code + PKCE + DPoP
- [ ] **3.1** Install `oidc-client-ts`
- [ ] **3.2** Tạo `lib/dpop/dpopManager.ts` — generate/persist ECDSA key pair (IndexedDB)
- [ ] **3.3** Tạo `lib/dpop/dpopProof.ts` — build + sign DPoP proof JWT
- [ ] **3.4** Configure `oidc-client-ts` UserManager
- [ ] **3.5** Refactor Login — redirect đến IdentityServer authorize endpoint
- [ ] **3.6** Tạo `/callback` route — handle OIDC callback, exchange code → tokens
- [ ] **3.7** Update Axios interceptor — thêm `Authorization: DPoP {token}` + `DPoP: {proof}`
- [ ] **3.8** Update token refresh flow
- [ ] **3.9** Update logout — redirect đến `end_session` endpoint
- [ ] **3.10** Update `authStore.ts` — lưu user info từ OIDC userinfo
- [ ] **3.11** IndexedDB key persistence

### ⬜ Phase 4: Backend Auth Cleanup
- [ ] **4.1** Deprecate `AuthController` (login/refresh/logout)
- [ ] **4.2** Deprecate `AuthService`
- [ ] **4.3** Xóa `SetTokenCookies()`, `ClearTokenCookies()`
- [ ] **4.4** Xóa `JwtSettings` khỏi `appsettings.json`
- [ ] **4.5** Update CORS — thêm IdentityServer origin

---

## Các Thay Đổi Đã Thực Hiện

### Files Mới (src/IdentityServer/)

#### `IdentityServer.csproj`
- Target: `net9.0`
- Packages: `Duende.IdentityServer 7.0`, `Duende.IdentityServer.AspNetIdentity 7.0`, `Microsoft.AspNetCore.Authentication.OpenIdConnect 9.0`
- Project reference: `../Infrastructure/Infrastructure.csproj` để dùng chung DbContext + Entity

#### `Program.cs`
- Đăng ký `ApplicationDbContext` với Neon PostgreSQL (dùng chung connection string với WebApi)
- Đăng ký `AddRazorPages()` để serve login UI
- Đăng ký `AddIdentityServer()` với:
  - In-memory resources/scopes/clients (từ `Config.cs`)
  - `CustomProfileService` và `CustomResourceOwnerPasswordValidator`
  - `UserInteraction.LoginUrl = "/Account/Login"` để redirect đúng trang
- Pipeline: `UseStaticFiles → UseRouting → UseIdentityServer → UseAuthorization → MapRazorPages`

#### `Config.cs`
- **IdentityResources**: `openid`, `profile`
- **ApiScopes**: `retail-api` — scope mà WebApi sẽ yêu cầu khi validate token
- **Client `react-dpop`**:
  - `AllowedGrantTypes = GrantTypes.Code` (Authorization Code only, không có Implicit)
  - `RequirePkce = true` — bắt buộc PKCE, chống authorization code interception
  - `RequireClientSecret = false` — public client (SPA không thể giữ bí mật)
  - `RedirectUris = ["http://localhost:5173/callback"]` — sau login redirect về React app
  - `PostLogoutRedirectUris = ["http://localhost:5173"]`
  - `AllowedCorsOrigins = ["http://localhost:5173"]`
  - `AllowedScopes = [openid, profile, retail-api, offline_access]`
  - `AccessTokenLifetime = 3600` (1 giờ)
  - `AllowOfflineAccess = true` — cấp refresh token

#### `Services/CustomProfileService.cs`
- Implement `IProfileService` của Duende
- `GetProfileDataAsync`: Query `UserEntity` theo `sub` claim, emit claims `username`, `name`, `role` vào access token
- `IsActiveAsync`: Kiểm tra user còn tồn tại trong DB không (phòng trường hợp bị xóa)

#### `Services/CustomResourceOwnerPasswordValidator.cs`
- Implement `IResourceOwnerPasswordValidator`
- Dùng `BCrypt.Net.BCrypt.Verify()` để check password hash (cùng thư viện với WebApi hiện tại)
- Trả về `GrantValidationResult` với `sub = user.Id.ToString()` + role/name claims

#### `Pages/Account/Login/Index.cshtml.cs` (LoginModel)
- `OnGet`: Nhận `returnUrl` từ IdentityServer, set vào form
- `OnPost`:
  - Query user từ DB theo username
  - BCrypt verify password
  - `HttpContext.SignInAsync()` với `IdentityServerConstants.DefaultCookieAuthenticationScheme`
  - Claims được set: `sub`, `username`, `name`, `role`
  - `AuthenticationProperties.IsPersistent` theo checkbox "Ghi nhớ đăng nhập"
  - Redirect về `returnUrl` (IdentityServer authorization endpoint tiếp tục xử lý)

#### `Pages/Account/Login/Index.cshtml`
- Form HTML thuần, không dùng thư viện ngoài
- CSS inline: responsive, màu chính `#4f46e5` (indigo)
- Trường: Username, Password, Remember Me checkbox, nút Đăng nhập
- Hiển thị `ErrorMessage` nếu login fail

#### `Pages/Account/Logout/Index.cshtml.cs` (LogoutModel)
- `OnGet`/`OnPost`: Gọi `HttpContext.SignOutAsync()` để clear session cookie
- `GetLogoutContextAsync(logoutId)`: Lấy `PostLogoutRedirectUri` từ IdentityServer context
- Redirect về frontend sau khi logout

#### `appsettings.json`
- Chứa `ConnectionStrings.DefaultConnection` trỏ đến Neon PostgreSQL (cùng DB với WebApi)
- Không chứa JwtSettings (IdentityServer tự quản lý signing keys)

#### `Properties/launchSettings.json`
- Profile `https`: `applicationUrl = "https://localhost:5001;http://localhost:5000"`
- Đây là địa chỉ `Authority` mà WebApi và Frontend sẽ dùng để discover OIDC metadata

#### `ecdsa384-private.pem` + `ecdsa384-public.pem`
- ECDSA P-384 key pair sinh bằng `openssl ecparam -genkey -name secp384r1`
- Dùng để sign DPoP proof JWT ở phía frontend (SubtleCrypto ES384)
- ⚠️ **TODO**: Thêm `ecdsa384-private.pem` vào `.gitignore` trước khi deploy production

### Files Thay Đổi

#### `RetailStoreManagement.sln`
- Thêm entry project `src/IdentityServer/IdentityServer.csproj`

#### `.gitignore` (repository root `/shiny-carnival/`)
- Thêm `.worktrees/` để git không track nội dung worktree vào repository chính

---

## Quyết Định Thiết Kế

| Quyết định | Lý do |
|-----------|-------|
| Authorization Code + PKCE thay vì ROPC | Chuẩn OAuth 2.1, không để client thấy password |
| Razor Pages cho Login UI | IdentityServer là Authorization Server, cần hosted login page |
| Reuse ApplicationDbContext | Không duplicate user store, không cần migration |
| DPoPAndBearer mode | Backward compatible trong giai đoạn chuyển tiếp |
| Duende 7.0 | Phiên bản stable mới nhất, hỗ trợ DPoP native |

---

## Lưu Ý Khi Tiếp Tục

- **Phase 2**: Cần tìm Duende reference DPoP module tại https://github.com/DuendeSoftware/Samples
- **Phase 3**: Frontend tại `../frontend/`, cần kiểm tra package manager (npm/pnpm/yarn)
- **Private key**: `ecdsa384-private.pem` nên được thêm vào `.gitignore` trong thực tế production
- **appsettings.json**: Connection string đang hardcode — nên dùng user-secrets hoặc env vars khi deploy
