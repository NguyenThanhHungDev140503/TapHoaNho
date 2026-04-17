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

## Cơ Chế DPoP (Demonstrating Proof-of-Possession)

### Vấn Đề DPoP Giải Quyết

Với Bearer token thông thường, ai **giữ** token đều dùng được. Nếu attacker steal token qua XSS/log/MITM → dùng luôn.

DPoP **bind** token vào một keypair mà chỉ client ban đầu giữ. Attacker steal token nhưng thiếu private key → không tạo được DPoP proof → token vô dụng.

```
Bearer:  Cầm token  →  dùng được   (thẻ ATM không PIN)
DPoP:    Cầm token + prove có key →  dùng được   (thẻ ATM có PIN)
```

### Luồng DPoP End-to-End (5 Bước)

#### Bước 1: Browser Generate Keypair (CLIENT-HELD)

Frontend (ở Phase 3) sẽ:

```typescript
const keyPair = await crypto.subtle.generateKey(
  { name: "ECDSA", namedCurve: "P-384" },
  false,                  // extractable = false: JS không đọc được raw bytes
  ["sign"]
);
// Lưu vào IndexedDB để persist qua page refresh
```

**Điểm quan trọng:** Private key là `CryptoKey` object **non-extractable**. Ngay cả XSS cũng không export được ra bytes — chỉ gọi `sign()` được.

#### Bước 2: Token Request với DPoP Proof

Mỗi request đến IdentityServer, client tạo một DPoP proof JWT mới:

```
Header:  { typ: "dpop+jwt", alg: "ES384", jwk: <publicKey-JWK> }
Payload: {
  jti: "<random-uuid>",   // unique mỗi proof → chống replay
  htm: "POST",             // HTTP method
  htu: "https://localhost:5001/connect/token",
  iat: <timestamp>
}
Signature: ECDSA-sign bằng privateKey
```

Request:
```http
POST /connect/token
DPoP: <proof-jwt>          ← Header MỚI
Content-Type: application/x-www-form-urlencoded

grant_type=authorization_code&code=...&code_verifier=...
```

#### Bước 3: IdentityServer Validate & Bind Key

IdentityServer sẽ:
1. Verify chữ ký proof bằng `publicKey` trong header `jwk`
2. Verify `htu`, `htm`, `iat` (trong window thời gian)
3. Check `jti` chưa dùng (cache)
4. Tính `jkt` = SHA-256 thumbprint của `jwk`
5. Phát access_token với claim `cnf.jkt`:

```json
{
  "sub": "1",
  "scope": "retail-api",
  "cnf": { "jkt": "<thumbprint-sha256>" }  ← "confirmation claim"
}
```

Response:
```json
{
  "access_token": "eyJ...",
  "token_type": "DPoP",        ← KHÔNG phải "Bearer"
  "refresh_token": "..."
}
```

#### Bước 4: API Request — Prove Possession

Client tạo proof MỚI cho mỗi API call, thêm `ath`:

```
Payload: {
  jti: "<new-uuid>",
  htm: "GET",
  htu: "https://api/products",
  iat: <timestamp>,
  ath: SHA-256(access_token)   ← access token hash
}
```

Request:
```http
GET /api/products
Authorization: DPoP <access_token>    ← "DPoP" thay vì "Bearer"
DPoP: <new-proof-jwt>
```

#### Bước 5: API Validate

WebApi (Phase 2) sẽ:
1. Verify chữ ký proof
2. Verify `htm`/`htu`/`iat`
3. Verify `ath` = SHA-256(access_token) → proof phải ràng buộc với token cụ thể
4. Check `jti` chưa dùng
5. Extract `jkt` từ `access_token.cnf.jkt`
6. Tính `jkt` từ proof `jwk` → **phải KHỚP** (proof-of-possession check)

### Các Tấn Công DPoP Phòng Ngừa

| Tấn công | Cách phòng ngừa |
|----------|-----------------|
| **Token theft (XSS, log)** | Attacker có token, thiếu private key → không tạo proof |
| **Replay attack** | `jti` unique + server cache |
| **Token injection** | `ath` bind proof với token cụ thể |
| **URL tampering** | `htu` phải khớp URL đích |
| **Method tampering** | `htm` phải khớp HTTP method |
| **Clock skew** | `iat` trong window 1-60s |

### State Hiện Tại

| Component | Trạng thái | Ghi chú |
|-----------|-----------|---------|
| IdentityServer `RequireDPoP = true` | ✅ | Client `react-dpop` bắt buộc DPoP |
| `AddDeveloperSigningCredential()` | ✅ | RSA key sign access_token (dev only) |
| DPoP proof validation trong WebApi | ⬜ | Phase 2 |
| Keypair generate ở Frontend | ⬜ | Phase 3 |
| IndexedDB persistence | ⬜ | Phase 3 |
| Axios DPoP interceptor | ⬜ | Phase 3 |

**Lưu ý:** Hiện tại chưa test được DPoP end-to-end. IdentityServer sẽ reject mọi token request vì client chưa gửi DPoP proof. Phải hoàn tất Phase 3 mới chạy được flow đầu-cuối.

---

## Tại Sao Cần Razor Pages Trong IdentityServer?

### Câu Hỏi

"IdentityServer là server phát token, tại sao lại cần UI (Razor Pages)?"

### Nguyên Tắc OAuth 2.1 Cốt Lõi

> **Client (React SPA) KHÔNG BAO GIỜ được nhìn thấy credential của user.**

Vì thế Authorization Code flow yêu cầu:
- User gõ password → gửi **THẲNG** đến Authorization Server
- Client chỉ nhận lại token, không bao giờ biết password

### Kiến Trúc 3 Project

```
┌─────────────────────────────────────────────────────┐
│ IdentityServer (localhost:5001) — FULL WEB APP     │
│  • Razor Pages: Login, Logout, Consent             │
│  • OAuth endpoints: /connect/authorize, /connect/token
│  • Cookie-based session để track user đã login     │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│ WebApi (localhost:5175) — REST API ONLY            │
│  • /api/products, /api/orders, ...                 │
│  • Validate DPoP proof                             │
│  • KHÔNG có UI                                      │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│ Frontend (localhost:5173) — React SPA              │
│  • Redirect sang IdentityServer để login           │
│  • Gọi WebApi với DPoP header                      │
└─────────────────────────────────────────────────────┘
```

### Luồng Authorization Code Khi User Login

```
1. User click "Đăng nhập" trong React app (localhost:5173)
   ↓
2. Client redirect toàn bộ browser đến:
   https://localhost:5001/connect/authorize
     ?client_id=react-dpop
     &response_type=code
     &scope=openid profile retail-api
     &redirect_uri=http://localhost:5173/callback
     &code_challenge=<PKCE>
   ↓
3. IdentityServer check: user chưa authenticated
   → Redirect đến /Account/Login
   ↓
4. ⚠️ CHỖ CẦN RAZOR PAGES ⚠️
   Browser hiển thị login form (HTML) tại URL:
   localhost:5001/Account/Login?returnUrl=...

   ┌─────────────────────┐
   │ 🏪 Retail Store     │
   │ Username: [_____]   │
   │ Password: [_____]   │
   │         [Đăng nhập] │
   └─────────────────────┘

   User gõ password → POST localhost:5001/Account/Login
   ↑↑ Password gửi THẲNG đến IDS, KHÔNG qua React
   ↓
5. IdentityServer verify BCrypt password, set session cookie
   Redirect về: localhost:5173/callback?code=XYZ123
   ↓
6. React (callback route) exchange code lấy token với DPoP proof
```

### Tại Sao Cụ Thể Là Razor Pages?

3 lựa chọn host login UI trong ASP.NET Core:

| Lựa chọn | Đánh giá |
|----------|----------|
| **Razor Pages** ✅ | Duende chính thức khuyên dùng. Tích hợp sẵn `HttpContext.SignInAsync()`, antiforgery, page model gọn |
| MVC Views | Tương đương nhưng boilerplate hơn (Controller + View) |
| Static HTML + API | Phải tự implement form auth, cookie set, CSRF protection → phát minh lại bánh xe |

### Alternatives Đã Từ Chối

| Alternative | Lý do từ chối |
|-------------|---------------|
| **Resource Owner Password Grant (ROPC)** | Client nhận password → vi phạm nguyên tắc. Deprecated trong OAuth 2.1. |
| **Redirect về frontend hiển thị form** | Frontend thấy password → vi phạm nguyên tắc. Không phải Authorization Code flow thật. |
| **Social login redirect** | Vẫn cần Razor Page để chọn provider hoặc landing page |

### WebApi Không Bị Ảnh Hưởng

**Quan trọng:** Chỉ `IdentityServer` project (mới) cần Razor Pages. `WebApi` của bạn vẫn thuần REST, không có file `.cshtml` nào cả.

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
- **DPoP keys**: **Private key nằm ở browser (WebCrypto)**, KHÔNG phải server. Phase 3 sẽ generate ở frontend, lưu IndexedDB.
- **Production signing key**: Hiện dùng `AddDeveloperSigningCredential()` (auto-generate tempkey.jwk). Production phải thay bằng `AddSigningCredential()` với key từ secret store.

---

## Code Review Round 1 (2026-04-17)

Reviewer tìm 3 Critical + 8 Important issues. Tất cả đã được fix:

### Critical — Đã fix
- **C1** Credentials trong `appsettings.Development.json`: Xóa secrets, dùng `.env.secrets` qua devenv dotenv. ⚠️ Password Neon cần rotate manual vì vẫn còn trong git history.
- **C2** Thiếu signing credential: Thêm `AddDeveloperSigningCredential()` vào `Program.cs`.
- **C3** ECDSA keys đặt sai chỗ: Xóa 2 file `.pem`. DPoP keys là CLIENT-HELD, sẽ được browser generate ở Phase 3.

### Important — Đã fix
- **I1** Xóa `CustomResourceOwnerPasswordValidator.cs` (ROPC vi phạm OAuth 2.1).
- **I2** `RequireDPoP = true`, `RefreshTokenUsage = OneTimeOnly`, `AccessTokenLifetime = 900s`.
- **I3** Xóa `HostingExtensions.cs` dead code.
- **I4** Login page dùng `IIdentityServerInteractionService.GetAuthorizationContextAsync()` để validate returnUrl + raise events.
- **I5** Logout page POST-only với confirm page (skip nếu Duende context cho phép).
- **I7** Dùng `JwtClaimTypes` constants thay vì string literals.

### Chưa fix (defer)
- **I6** Rate limiting/account lockout cho login — cần thêm ASP.NET Core rate limiting middleware + thêm field `FailedLoginAttempts`/`LockedUntil` vào `UserEntity`. Defer sang Phase 4.
- **I8** Coupling IdentityServer → Infrastructure → Application: sẽ refactor thành thin abstraction ở Phase 4 nếu có thời gian.
- **M2** `http` profile port 5063 trong launchSettings.json — minor, sửa sau.
- **M3** `UseHsts()/UseHttpsRedirection()` — thêm khi chuẩn bị production.
- **M6** `AccessTokenLifetime` đã giảm xuống 900s ở I2.
