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
- [x] **2.1** DPoP module: **skip** — Duende 7.4 phát hành official package `Duende.AspNetCore.Authentication.JwtBearer 1.0.2` thay cho 11 files trong reference project
- [x] **2.2** Cài `Duende.AspNetCore.Authentication.JwtBearer` vào `WebApi.csproj`
- [x] **2.3** Đổi authentication scheme sang authority-based (`Authority = https://localhost:5001`, `ValidTypes=["at+jwt"]`, `MapInboundClaims=false`)
- [x] **2.4** Register `ConfigureDPoPTokensForScheme("dpoptokenscheme")` với replay detection + in-memory distributed cache
- [x] **2.5** Policy `"RetailApi"` (RequireAuthenticatedUser + scope=retail-api split-aware) làm FallbackPolicy
- [x] **2.6** Xóa cookie fallback trong `OnMessageReceived` (Program.cs rewrite)
- [x] **2.7** `AuthController.Login/Logout/Refresh` → 410 Gone (Phase 4 xóa hoàn toàn)
- [x] **2.8** Swagger: Bearer → OAuth2 Auth Code + PKCE với client `swagger-ui` env-gated
- [x] **2.9** Environment gating: swagger-ui client + AllowBearerTokens chỉ active khi `IsDevelopment()`

### ✅ Phase 3: Frontend — Authorization Code + PKCE + DPoP
- [x] **3.1** Install `oidc-client-ts` v3.5.0
- [x] **3.2** ~~Tạo `lib/dpop/dpopManager.ts`~~ → Library tự lazy-generate keypair P-256 trong `IndexedDbDPoPStore`. Single source of truth.
- [x] **3.3** Tạo `lib/oidc/dpop.ts` — build + sign DPoP proof JWT (ES256, htm/htu/jti/iat/ath/nonce)
- [x] **3.4** Configure `lib/oidc/userManager.ts` — UserManager + DPoP store singleton + `requireEnv()` PROD fail-fast
- [x] **3.5** Refactor `LoginPage.tsx` — redirect đến IdentityServer `/connect/authorize`
- [x] **3.6** Tạo `OidcCallbackPage.tsx` + route `/callback` — exchange code → DPoP-bound tokens
- [x] **3.7** Rewrite `lib/api/axios.ts` — `Authorization: DPoP <token>` + `DPoP: <proof>` với same-origin scoping
- [x] **3.8** Silent renew flow + `use_dpop_nonce` retry + concurrent request queue
- [x] **3.9** `logout()` — clearAuth + clearDPoPKeyPair (rotate) + signoutRedirect → end_session
- [x] **3.10** Rewrite `authStore.ts` — OIDC user (string role), `initFromSession()`, individual selectors
- [x] **3.11** Bootstrap fix: `await initFromSession()` trong `main.tsx` trước render

### ✅ Phase 4: Backend Auth Cleanup
- [x] **4.1** Xóa `AuthController` (login/refresh/logout — hiện đã 410 Gone)
- [x] **4.2** Xóa `AuthService`, `JwtSettings`, helpers cookies
- [x] **4.3** Xóa cookie infrastructure đã không dùng *(đã xóa từ Phase 2, verify lại ở Phase 4)*
- [x] **4.4** Xóa `JwtSettings` khỏi `appsettings.json`
- [x] **4.5** Update CORS — verify không cần thêm IdentityServer origin (WebApi không gọi IDS)
- [ ] **4.6** Redis distributed cache cho replay detection (production) **[MOVED TO PHASE 5]**
- [ ] **4.7** Rate limiting / account lockout (deferred từ Phase 2 review I6) **[MOVED TO PHASE 5]**

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

---

## Phase 2 Implementation Notes

### Package Changes

- **Skipped original plan task 2.1** (copy 11-file DPoP module). Duende 7.4 (released Dec 2025) phát hành official package `Duende.AspNetCore.Authentication.JwtBearer 1.0.2` — thay thế toàn bộ reference code với 1 dòng `ConfigureDPoPTokensForScheme()`.

### Config.cs — Tại Sao Không Cần `ApiResource`?

Access token Duende phát ra có:
- `aud` = scope name (`"retail-api"`) nếu dùng `ApiScope`
- `aud` = resource name nếu dùng `ApiResource`

Mình dùng `ApiScope` (scope-based) → `ValidateAudience = false` + check scope claim trong policy `"RetailApi"`. Nếu sau này cần audience binding chặt hơn → thêm `ApiResource`.

### DPoPOptions Thực Tế (Duende 7.4.7)

API khác với plan/docs cũ:

| Plan ghi | Thực tế |
|----------|---------|
| `ClientClockSkew` | `ProofTokenIssuedAtClockSkew` |
| `Mode = DPoPMode.DPoPAndBearer` | `AllowBearerTokens = true` |
| *(không nhắc)* | `EnableReplayDetection = true` (default false) |

### Replay Detection

`EnableReplayDetection = true` + `AddDistributedMemoryCache()`:
- Lưu `jti` của mỗi proof vào cache
- Proof thứ 2 với cùng `jti` → reject
- **In-memory cache CHỈ cho dev**. Production multi-instance phải swap sang Redis/SQL vì attacker có thể retry trên instance khác.

### Swagger Limitation

Swashbuckle UI đã chuyển từ Bearer input → OAuth2 Auth Code + PKCE. Tester click "Authorize" trong Swagger sẽ redirect IdentityServer login, nhận access_token. **Nhưng**: Swashbuckle không biết cách tạo DPoP proof JWT → mọi request từ Swagger sẽ 401 với `invalid_dpop_proof`.

**Giải pháp:** Tạo client riêng `swagger-ui` (no DPoP) chỉ tồn tại trong Development. Defense in depth 3 lớp ngăn plain-Bearer hoạt động ở Production.

---

## Environment-Based Gating (Dev vs Prod)

### Mục Tiêu

| Environment | Swagger UI | swagger-ui client | Plain Bearer accepted? |
|-------------|-----------|-------------------|----------------------|
| **Development** | ✅ Served | ✅ Tồn tại | ✅ Có (cho test) |
| **Production** | ❌ 404 | ❌ Không tồn tại | ❌ Refuse 401 |

### Cơ Chế Đọc Environment

```
.env.secrets (file user, không commit)
   ASPNETCORE_ENVIRONMENT=Development
            ↓
devenv dotenv load → export shell env vars
            ↓
direnv kích hoạt khi cd vào project root
            ↓
dotnet run kế thừa env vars
            ↓
ASP.NET Core đọc tự động vào IHostEnvironment
            ↓
builder.Environment.IsDevelopment() → true/false
```

Để switch Production: đổi `ASPNETCORE_ENVIRONMENT=Production` trong `.env.secrets`, hoặc set trực tiếp khi deploy (Docker env, systemd, k8s ConfigMap).

### Defense in Depth — 3 Lớp Ngăn Bypass

```
Production (ASPNETCORE_ENVIRONMENT=Production)
─────────────────────────────────────────────────────

🛡️ Lớp 1 — Pipeline gate (WebApi/Program.cs)
   if (app.Environment.IsDevelopment())
       app.UseSwagger();
   → Production: /swagger trả 404, không có UI nào để abuse

🛡️ Lớp 2 — Client registration gate (IdentityServer/Config.cs)
   if (isDevelopment) yield return new Client { ClientId = "swagger-ui", ... };
   → Production: POST /connect/token với client_id=swagger-ui
                 → IdentityServer trả "invalid_client"

🛡️ Lớp 3 — Token validation gate (WebApi/Program.cs)
   AllowBearerTokens = builder.Environment.IsDevelopment();
   → Production: Authorization: Bearer xyz → 401 unconstrained Bearer rejected

➕ Bonus (luôn bật, mọi env):
   cnf.jkt enforcement: token có cnf.jkt → bắt buộc DPoP proof
   → Token bị steal, attacker thử Bearer header → vẫn 401
```

### Files Đã Thay Đổi

| File | Thay đổi |
|------|---------|
| `IdentityServer/Config.cs` | `static IEnumerable<Client> Clients` (property) → `static IEnumerable<Client> Clients(bool isDevelopment)` (method). swagger-ui chỉ yield khi dev. |
| `IdentityServer/Program.cs` | Pass `builder.Environment.IsDevelopment()` vào `Config.Clients(...)`. |
| `WebApi/Program.cs` | `AllowBearerTokens = builder.Environment.IsDevelopment()`. Log quyết định ở startup. |

### Test Matrix

| Scenario | Dev | Prod |
|----------|-----|------|
| Tester mở `/swagger` | ✅ Hoạt động | 404 |
| Login qua `swagger-ui` client | ✅ Token plain Bearer | `invalid_client` |
| Plain Bearer → `/api/products` | ✅ 200 OK | 401 |
| DPoP token + proof → `/api/products` | ✅ 200 OK | ✅ 200 OK |
| DPoP token KHÔNG có proof → `/api/products` | 401 | 401 |

### Tại Sao Không Đơn Giản Set `ASPNETCORE_ENVIRONMENT` Trong appsettings?

ASP.NET Core đọc env theo thứ tự:
1. `--environment` command-line arg
2. `ASPNETCORE_ENVIRONMENT` env var
3. `DOTNET_ENVIRONMENT` env var
4. Default: `Production`

`appsettings.json` **KHÔNG** đặt được env name (đó là chicken-and-egg: env name quyết định file `appsettings.{env}.json` nào load). Phải dùng env var hoặc CLI arg.

→ devenv dotenv là cách clean nhất: 1 file `.env.secrets` cho mọi config + secrets.

---

---

## Code Review Round 2 — Phase 2 (2026-04-18)

Reviewer tìm 2 Critical + 5 Important. Đã fix 4 blockers + 1 minor:

### Critical — Đã fix
- **C1** `RequireClaim("scope", "retail-api")` fail với RFC 9068 space-separated string → thay bằng `RequireAssertion` split claim.
- **C2** Swagger dùng `react-dpop` (RequireDPoP=true) → tạo client riêng `swagger-ui` với DPoP=false. **Sau đó nâng cấp** thành env-gated (xem section "Environment-Based Gating" phía trên) — `swagger-ui` client + `AllowBearerTokens` chỉ active trong Development.

### Important — Đã fix
- **I4** `AuthController.Login/Logout/Refresh` mint self-signed JWT mà API mới reject + NRE vì SecretKey đã xóa → trả 410 Gone với hướng dẫn dùng IdentityServer.
- **I5** Cookie code paths đã dead → xóa cùng I4.

### Important — Defer Phase 2.5
- **I1** Integration test verify DPoP-bound token gửi qua plain Bearer → 401 (verify cnf.jkt enforcement).
- **I2** Redis swap cho replay cache (production multi-instance).
- **I3** `RequireHttpsMetadata = false` only in Development env (đã đúng, chỉ note).

### Minor — Đã fix
- **M4** Expose `WWW-Authenticate` header trong CORS (cho Safari SPA đọc DPoP challenge).

### Minor — Defer
- **M1** Confirm FallbackPolicy + AllowAnonymous interaction (reviewer đã verify safe).
- **M2** Custom scheme name (no current issue).
- **M3** `ValidateAudience = false` justified by current scope-only setup.
- **M5** Pin package version trong Directory.Packages.props.

---

## Phase 3 Implementation Notes (2026-04-18)

### Approach

Frontend Phase 3 migrate React SPA từ cookie-based JWT auth sang OIDC Authorization Code + PKCE + DPoP. Đặc điểm chính:

- **Library `oidc-client-ts` v3.5.0** xử lý hết toàn bộ OAuth + OIDC + DPoP token exchange (auth code grant + silent renew). Code app chỉ build proof cho **API requests** đến WebApi.
- **Single DPoP store**: Library tự lazy-generate keypair P-256 non-extractable trong `IndexedDbDPoPStore` (database `oidc`, store `dpop`, key=client_id). Axios interceptor đọc cùng store qua `getDPoPKeyPair()` → cnf.jkt luôn match end-to-end.
- **ES256/P-256 alg**: Đồng nhất với library hardcode (`CryptoUtils.generateDPoPProof` dùng `alg:"ES256"`, `generateDPoPKeys` dùng `namedCurve:"P-256"`). Mức bảo mật ~128-bit, đủ NIST yêu cầu.
- **Same-origin scoping**: Axios chỉ attach token cho requests đến `API_ORIGIN` — defense token leak.

### Files chính

```
frontend/src/lib/oidc/
├── dpop.ts                  Build + ký proof JWT (ES256), JWK thumbprint (RFC 7638)
└── userManager.ts           UserManager singleton, dpopStore, requireEnv() PROD

frontend/src/lib/api/
└── axios.ts                 Interceptors: DPoP attach (same-origin), nonce retry, silent renew

frontend/src/features/auth/
├── store/authStore.ts       OIDC user + initFromSession + logout + selectors
└── pages/
    ├── LoginPage.tsx        signinRedirect()
    └── OidcCallbackPage.tsx /callback handler

frontend/src/app/
├── main.tsx                 await bootstrap() trước render
└── routes/routeTree.ts      /callback ở root level
```

Doc chi tiết: `RetailStoreManagement/tasks/dpop-frontend-flow.md`

---

## Code Review Round 1 — Phase 3 (2026-04-18)

Reviewer: superpowers:code-reviewer subagent  
Verdict: **Not ready to merge — fix with required changes** (4 critical regressions từ store-shape change)

### Critical (4) — Đã fix

| # | Vấn đề | Fix |
|---|--------|-----|
| C1 | `useloginPage.ts` còn gọi `setAuth()` (đã đổi thành `setOidcUser`) → runtime TypeError | Xóa luôn `useloginPage.ts` + `LoginForm.tsx` + `authApi.ts` + `auth/types/api.ts` (dead code) |
| C2 | `staff.layout.tsx`: `user.role !== 1` (số) — role giờ là string `"Staff"` | Đổi sang `user.role !== 'Staff'` |
| C3 | `ProfilePage.tsx`: nhiều chỗ so sánh `API_CONFIG.USER_ROLES.ADMIN/STAFF` (số) với `user.role` (string) | Rewrite ProfilePage dùng store mới + `logout()`, label theo string |
| C4 | `initFromSession()` fire-and-forget trong `main.tsx` race với route guards | `await bootstrap()` trước `ReactDOM.render()` |

### Important (6/7) — Đã fix

| # | Vấn đề | Fix |
|---|--------|-----|
| I5 | `userManager` overwrite `dpopStore` mỗi call → wipe nonce | Bỏ seed; để library lazy-generate |
| I6 | **Hai IndexedDB store khác nhau** (custom `dpop-key-store` vs library `oidc/dpop`) → cnf.jkt mismatch risk | Xóa hết `keyStorage.ts` + `dpopKey.ts`; dùng library `IndexedDbDPoPStore` làm single source |
| I8 (alg) | Code app dùng ES384/P-384 còn library dùng ES256/P-256 | Switch `dpop.ts` sang ES256/P-256 |
| I9 | `clearAuth` declared `async` nhưng không có await | Bỏ keyword `async` |
| I10 | Token leak: interceptor attach `Authorization` cho mọi URL kể cả non-API | Same-origin guard: `new URL(url).origin === API_ORIGIN` |
| I11 | `LoginPage` dùng `window.location.replace` thay vì TanStack router | Đổi sang `navigate({to:'/'})` |

I7 (multi-tab race khi tạo keypair lần đầu) → giờ là vấn đề của library, defer.

### Minor — Đã fix

- M5 `API_BASE_URL` cũng throw trong PROD nếu thiếu env var (parity với userManager) — round 2
- M7 jsdoc warning trên `axiosClient` về per-request baseURL overrides — round 2
- M1 ProfilePage Alert khi `Number(user.sub)` = NaN — round 2
- I1 (round 2) `initFromSession` thêm `finally` reset `isLoading` defensively
- Drop `refreshToken` khỏi zustand state (chỉ tồn tại trong oidc-client-ts session storage)
- Individual selectors `useUser` / `useIsAuthenticated` / `useIsAuthLoading` (tránh fresh-object-per-render)

### Minor — Defer

- Unit tests cho `buildDPoPProof` + `computeJwkThumbprint` (chưa có vitest setup)
- Multi-tab race khi tạo keypair lần đầu (rất hiếm; mitigation: BroadcastChannel hoặc IDB transaction)
- `.env.production` URL placeholder (cần set trong PR/deploy doc)

---

## Code Review Round 2 — Phase 3 (2026-04-18)

Verdict: **Ready to merge: Yes, with the small I1 hardening** ✅

Reviewer xác nhận TẤT CẢ critical/important round 1 đều thực sự đã fix:
- Single-store invariant clean
- Alg switch ES384→ES256 consistent end-to-end
- Bootstrap race resolved
- Same-origin scoping correct
- `logout()` ordering đúng + defense in depth
- `requireEnv()` fail fast PROD

Các fix nhỏ round 2:
- I1: `initFromSession` defensive `finally` reset isLoading
- M1: ProfilePage Alert thay silent hide
- M5: `API_BASE_URL` PROD fail-fast parity
- M7: jsdoc warning per-request baseURL override

Lưu ý phát hiện riêng (không block merge): **dual role schema**
- `useAuthStore.user.role` = string từ IdentityServer claims (`"Admin" | "Staff"`)
- `UserEntity.role` = number từ admin user-management API (`0 | 1`)
- Hai schema khác nhau là intentional, đã thêm comment làm rõ trong `features/users/types/entity.ts`

### Phase 3 commits

```
32c36ad fix(frontend): address Phase 3 round 2 code review
d63bf58 fix(frontend): address Phase 3 code review
b85a08a docs: add DPoP frontend flow document
cb7056e feat(frontend): Phase 3 — OIDC Authorization Code + PKCE + DPoP
```

---

## Phase 4 Implementation Notes (2026-04-25)

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
| Giữ table `user_refresh_tokens` (orphan) | Rollback an toàn, drop migration thuộc Phase 5 |
| Không viết unit test | Ngoài scope cleanup phase, defer Phase 5 |
