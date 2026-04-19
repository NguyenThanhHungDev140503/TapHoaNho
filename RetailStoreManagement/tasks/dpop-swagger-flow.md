# Swagger Authentication Flow — Tài liệu kỹ thuật chi tiết

> **Phiên bản:** 1.0
> **Ngày:** 2026-04-19
> **Liên quan:** RFC 9068 (JWT Profile), RFC 7636 (PKCE), OpenAPI 3.0 OAuth2 Security Scheme
> **Scope:** **CHỈ Development environment.** Production hoàn toàn tắt Swagger UI + plain Bearer.

---

## Mục lục

1. [Kiến trúc tổng quan](#1-kiến-trúc-tổng-quan)
2. [Tại sao Swagger không dùng DPoP](#2-tại-sao-swagger-không-dùng-dpop)
3. [Các thành phần và vai trò](#3-các-thành-phần-và-vai-trò)
4. [Defense in Depth — 3 lớp gating](#4-defense-in-depth--3-lớp-gating)
5. [Case 1 — Tester mở Swagger UI lần đầu](#5-case-1--tester-mở-swagger-ui-lần-đầu)
6. [Case 2 — Click Authorize → IdentityServer login](#6-case-2--click-authorize--identityserver-login)
7. [Case 3 — OAuth callback và token exchange](#7-case-3--oauth-callback-và-token-exchange)
8. [Case 4 — Gọi API endpoint từ Swagger UI](#8-case-4--gọi-api-endpoint-từ-swagger-ui)
9. [Case 5 — Token hết hạn](#9-case-5--token-hết-hạn)
10. [Case 6 — Logout](#10-case-6--logout)
11. [Case 7 — Production: Swagger bị chặn 3 lớp](#11-case-7--production-swagger-bị-chặn-3-lớp)
12. [So sánh Frontend (DPoP) vs Swagger (Bearer)](#12-so-sánh-frontend-dpop-vs-swagger-bearer)
13. [Cấu hình hai client trong IdentityServer](#13-cấu-hình-hai-client-trong-identityserver)
14. [Ghi chú kỹ thuật bổ sung](#14-ghi-chú-kỹ-thuật-bổ-sung)

---

## 1. Kiến trúc tổng quan

```
┌─────────────────────────────────────────────────────────────┐
│                  Browser (Tester)                           │
│                                                             │
│  ┌─────────────────────────┐   ┌─────────────────────────┐  │
│  │  Swagger UI             │   │  sessionStorage         │  │
│  │  (localhost:5175/       │   │                         │  │
│  │   swagger/index.html)   │   │  PKCE state +           │  │
│  │                         │   │  code_verifier          │  │
│  │  - SwaggerUIBundle      │   │  (Swagger UI internal)  │  │
│  │  - OAuth2 popup window  │   │                         │  │
│  │  - access_token         │   │  Token stored in        │  │
│  │    in-memory            │   │  Swagger UI runtime     │  │
│  │                         │   │  (NOT IndexedDB)        │  │
│  └─────────────────────────┘   └─────────────────────────┘  │
│             │                                               │
│             │  Authorization: Bearer <plain_token>          │
│             │  (NO DPoP header, NO key binding)             │
└─────────────────────────────────────────────────────────────┘
            │                              │
            ▼                              ▼
   IdentityServer                   WebApi
   :5001                            :5175
   - swagger-ui client              - AllowBearerTokens=true (DEV ONLY)
     (RequireDPoP=false)            - Plain Bearer accepted
   - DEV ONLY                       - cnf.jkt enforcement vẫn bật
                                      (token KHÔNG bị bind sẽ qua;
                                       token bị bind PHẢI có DPoP)
```

**Nguyên tắc cốt lõi:**

1. **Swagger là tooling nội bộ, không phải client production.** Tester là dev/QA team, chạy local. Mục tiêu là khám phá API nhanh, không phải bảo mật end-user.
2. **Plain Bearer chỉ tồn tại trong Development.** Có 3 lớp gating bằng `IsDevelopment()` ngăn nó leak ra production.
3. **Token Swagger phát ra KHÔNG có `cnf.jkt`** — IdentityServer client `swagger-ui` config `RequireDPoP=false`, nên access token không chứa confirmation claim. Khi WebApi nhận token này, nó là plain Bearer hoàn toàn.
4. **Token frontend (react-dpop) và token Swagger (swagger-ui) là HAI loại token khác nhau** — phát từ hai client khác nhau, một có DPoP binding, một không.

---

## 2. Tại sao Swagger không dùng DPoP

| Lý do | Chi tiết |
|-------|---------|
| **Swashbuckle UI không hỗ trợ DPoP** | Swagger UI chỉ build sẵn các flow OAuth2 chuẩn (Auth Code, Implicit, Client Credentials, Password). Không có hook để inject custom header `DPoP: <proof>` cho mỗi request, cũng không có cách generate ECDSA keypair + ký proof JWT trong runtime. |
| **DPoP yêu cầu signed proof per-request** | Mỗi API call phải build proof JWT mới với `jti`, `htm`, `htu`, `iat`, `ath`, `nonce`. Swagger UI không có kiến trúc để chèn logic này vào XHR layer. |
| **Tester là dev internal** | Swagger là công cụ dev/QA local, không expose ra Internet. Risk model khác hẳn end-user (XSS, MITM, log leak ít có cơ hội xảy ra). |
| **Defense in depth bù đắp** | Production tắt cả Swagger UI lẫn `swagger-ui` client + `AllowBearerTokens`. Plain Bearer hoàn toàn vô dụng ngoài Dev. |

**Alternative đã từ chối:**
- **Custom Swagger plugin tự generate DPoP proof:** Quá phức tạp, phải fork Swashbuckle, maintain thêm code crypto trong runtime tester. Chi phí >> lợi ích.
- **Reuse `react-dpop` client cho Swagger:** Sẽ fail vì client `react-dpop` có `RequireDPoP=true`. Token endpoint sẽ refuse Swagger UI nếu không kèm DPoP proof, mà Swagger không build được proof.

---

## 3. Các thành phần và vai trò

| Thành phần | Vai trò |
|-----------|---------|
| `WebApi/Program.cs` (Swagger setup) | Đăng ký `AddSwaggerGen` với `OpenApiSecurityScheme` type `OAuth2` flow `AuthorizationCode`. URLs trỏ đến IdentityServer (`/connect/authorize`, `/connect/token`). |
| `WebApi/Program.cs` (UseSwaggerUI) | Pipeline gate: `if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(...); }`. Set `OAuthClientId("swagger-ui")` + `OAuthUsePkce()`. |
| `WebApi/Program.cs` (DPoP options) | `AllowBearerTokens = builder.Environment.IsDevelopment()` — chỉ dev cho phép plain Bearer. |
| `IdentityServer/Config.cs` (Clients method) | `static IEnumerable<Client> Clients(bool isDevelopment)` — `swagger-ui` client chỉ `yield` khi `isDevelopment=true`. |
| `IdentityServer/Pages/Account/Login` | Razor Pages login form — dùng chung cho cả `react-dpop` và `swagger-ui`. |
| Browser (Swagger UI runtime) | Chạy SwaggerUIBundle, mở OAuth2 popup, lưu access_token in-memory, attach `Authorization: Bearer <token>` cho mọi "Try it out" call. |

---

## 4. Defense in Depth — 3 lớp gating

```
Production environment (ASPNETCORE_ENVIRONMENT=Production)
─────────────────────────────────────────────────────────────────

🛡️  Lớp 1 — Swagger UI Pipeline Gate (WebApi/Program.cs)
─────────────────────────────────────────────────────────────────

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => { ... });
    }

    Production: GET /swagger/index.html → 404 Not Found
                GET /swagger/v1/swagger.json → 404 Not Found
                Không có UI nào để abuse.

🛡️  Lớp 2 — IdentityServer Client Registration Gate
─────────────────────────────────────────────────────────────────

    // IdentityServer/Config.cs
    public static IEnumerable<Client> Clients(bool isDevelopment)
    {
        yield return new Client { ClientId = "react-dpop", ... };

        if (isDevelopment)
        {
            yield return new Client {
                ClientId = "swagger-ui",
                RequireDPoP = false,    // ← DEV ONLY
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                ...
            };
        }
    }

    Production: POST /connect/token với client_id=swagger-ui
                → IdentityServer trả invalid_client
                Không có client nào tên swagger-ui tồn tại.

🛡️  Lớp 3 — Token Validation Gate (WebApi/Program.cs)
─────────────────────────────────────────────────────────────────

    var allowBearerTokens = builder.Environment.IsDevelopment();
    builder.Services.ConfigureDPoPTokensForScheme(DPoPScheme, opt =>
    {
        opt.AllowBearerTokens = allowBearerTokens;
        opt.EnableReplayDetection = true;
    });

    Production: Authorization: Bearer xxx (token KHÔNG có cnf.jkt)
                → 401: DPoP proof required

➕ Bonus (luôn bật mọi env):
─────────────────────────────────────────────────────────────────

    Token có cnf.jkt (DPoP-bound từ react-dpop client)
        → Bắt buộc phải có DPoP proof header + ath claim
        → Plain Bearer ngay cả ở Dev cũng FAIL với token loại này

    Đây là cnf.jkt enforcement — UNCONDITIONAL, không gate by env.
```

**Bảng matrix:**

| Test | Development | Production |
|------|-------------|------------|
| GET `/swagger` | ✅ 200 | ❌ 404 |
| Login qua client `swagger-ui` | ✅ Token (no cnf.jkt) | ❌ `invalid_client` |
| Login qua client `react-dpop` | ✅ Token (cnf.jkt) | ✅ Token (cnf.jkt) |
| Plain `Bearer <swagger-ui token>` → API | ✅ 200 | ❌ 401 |
| Plain `Bearer <react-dpop token>` → API | ❌ 401 (cnf.jkt mismatch) | ❌ 401 |
| `DPoP <react-dpop token>` + proof → API | ✅ 200 | ✅ 200 |

---

## 5. Case 1 — Tester mở Swagger UI lần đầu

```
Tester gõ: http://localhost:5175/
        │
        ▼
WebApi middleware (Program.cs):
    app.Use(async (context, next) =>
    {
        if (context.Request.Path == "/")
        {
            context.Response.Redirect("/swagger");
            return;
        }
        await next();
    });
        │
        └─► 302 Redirect → /swagger
                │
                ▼
        ASP.NET Core trả /swagger/index.html (Swashbuckle static asset)
                │
                ▼
        Browser load Swagger UI
                │
                ├─► SwaggerUIBundle khởi tạo
                │
                ├─► Fetch /swagger/v1/swagger.json (OpenAPI spec)
                │       │
                │       └─► Spec chứa SecurityScheme "oauth2":
                │           {
                │               type: "oauth2",
                │               flows: {
                │                   authorizationCode: {
                │                       authorizationUrl:
                │                           "https://localhost:5001/connect/authorize",
                │                       tokenUrl:
                │                           "https://localhost:5001/connect/token",
                │                       scopes: {
                │                           openid: "OpenID identifier",
                │                           profile: "User profile",
                │                           "retail-api": "Access Retail Store API"
                │                       }
                │                   }
                │               }
                │           }
                │
                ├─► Render danh sách endpoints + nút "Authorize" trên cùng
                │
                └─► Tester thấy:
                        ┌─────────────────────────────────────┐
                        │  Retail Store Management API  v1    │
                        │  [ Authorize 🔓 ]                   │
                        ├─────────────────────────────────────┤
                        │  GET    /api/admin/products         │
                        │  POST   /api/admin/products         │
                        │  GET    /api/orders                 │
                        │  ...                                 │
                        └─────────────────────────────────────┘
```

> **Lưu ý:** Lần đầu chưa có token, mọi endpoint click "Try it out" → "Execute" sẽ trả 401. Phải Authorize trước.

---

## 6. Case 2 — Click Authorize → IdentityServer login

```
Tester click [Authorize] trong Swagger UI
        │
        ▼
Swagger UI mở dialog:
    ┌────────────────────────────────────────────┐
    │  Available authorizations                  │
    │                                            │
    │  oauth2 (OAuth2, authorizationCode)        │
    │  Auth URL: https://localhost:5001/...      │
    │  Token URL: https://localhost:5001/...     │
    │                                            │
    │  client_id: [swagger-ui          ]  ← prefilled
    │  client_secret: [                ]  (không cần)
    │                                            │
    │  Scopes:                                   │
    │  ☑ openid                                  │
    │  ☑ profile                                 │
    │  ☑ retail-api                              │
    │                                            │
    │            [ Authorize ]   [ Close ]       │
    └────────────────────────────────────────────┘
        │
        │  Tester click [Authorize] trong dialog
        ▼
Swagger UI generate PKCE:
        │
        ├─► code_verifier = random(43-128 chars)
        ├─► code_challenge = BASE64URL(SHA256(code_verifier))
        ├─► state = random nonce (chống CSRF)
        ├─► Lưu { code_verifier, state } vào sessionStorage
        │
        └─► Mở popup window:
                window.open(
                    "https://localhost:5001/connect/authorize"
                        + "?response_type=code"
                        + "&client_id=swagger-ui"
                        + "&redirect_uri=http://localhost:5175/swagger/oauth2-redirect.html"
                        + "&scope=openid%20profile%20retail-api"
                        + "&state=" + state
                        + "&code_challenge=" + code_challenge
                        + "&code_challenge_method=S256",
                    "_blank"
                );

> Khác với react-dpop, request KHÔNG có dpop_jkt parameter
> (Swagger UI không build được DPoP proof, nên không bind code với key).
        │
        ▼
IdentityServer nhận /connect/authorize
        │
        ├─► Validate client_id=swagger-ui (chỉ tồn tại trong Dev)
        │       Production: invalid_client → 400, popup hiển thị lỗi
        │
        ├─► Validate redirect_uri trong AllowedRedirectUris của swagger-ui
        ├─► Validate scopes trong AllowedScopes
        │
        ├─► Check user authenticated:
        │       Lần đầu: chưa có cookie → redirect /Account/Login
        │
        └─► Hiển thị Razor Pages login form:
                ┌─────────────────────┐
                │ 🏪 Retail Store     │
                │ Username: [_____]   │
                │ Password: [_____]   │
                │ ☐ Remember me       │
                │         [Đăng nhập] │
                └─────────────────────┘
                URL: localhost:5001/Account/Login?returnUrl=...
        │
        ├─► Tester nhập credentials → POST /Account/Login
        │
        ├─► IdentityServer:
        │       ├─► Query UserEntity từ ApplicationDbContext
        │       ├─► BCrypt.Verify(password, user.PasswordHash)
        │       └─► HttpContext.SignInAsync() → set cookie .AspNetCore.Identity.Application
        │
        └─► Redirect lại /connect/authorize → user giờ đã authenticated
                → Issue authorization code (10s TTL)
                → Redirect popup về:
                    http://localhost:5175/swagger/oauth2-redirect.html
                        ?code=<authorization_code>
                        &state=<state>
                        &session_state=<...>
                        &iss=https://localhost:5001
```

---

## 7. Case 3 — OAuth callback và token exchange

```
Popup load /swagger/oauth2-redirect.html
(Static HTML do Swashbuckle ship sẵn)
        │
        ├─► Đọc URL params: code, state
        ├─► Validate state khớp với state đã lưu (CSRF check)
        │
        ├─► Lấy code_verifier từ sessionStorage (PKCE)
        │
        └─► postMessage về parent window (Swagger UI):
                {
                    type: "authorization_response",
                    code: "<authorization_code>",
                    state: "<state>"
                }

        ▼
Swagger UI parent window nhận message
        │
        └─► POST https://localhost:5001/connect/token
                Content-Type: application/x-www-form-urlencoded

                grant_type=authorization_code
                code=<authorization_code>
                redirect_uri=http://localhost:5175/swagger/oauth2-redirect.html
                client_id=swagger-ui
                code_verifier=<pkce_verifier>

> KHÔNG có DPoP header — swagger-ui client không yêu cầu.

        ▼
IdentityServer xử lý:
        │
        ├─► Validate authorization code (TTL, dùng 1 lần)
        ├─► Validate code_verifier (PKCE)
        │       SHA256(code_verifier) BASE64URL == code_challenge đã lưu
        │
        ├─► Issue tokens:
        │       access_token (JWT, 1 giờ — theo AccessTokenLifetime của swagger-ui):
        │           {
        │               sub: "user_id",
        │               name: "Tester Name",
        │               role: "Admin",
        │               scope: "openid profile retail-api",
        │               // ❌ KHÔNG có cnf.jkt — token KHÔNG bị DPoP-bound
        │               exp: now + 3600,
        │               typ: "at+jwt",
        │               aud: "..."  (tùy ApiResource)
        │           }
        │       id_token (user profile claims)
        │       (KHÔNG có refresh_token nếu swagger-ui không có offline_access)
        │
        └─► Response:
                {
                    "access_token": "eyJ...",
                    "token_type": "Bearer",   ← "Bearer" thay vì "DPoP"
                    "expires_in": 3600,
                    "id_token": "eyJ...",
                    "scope": "openid profile retail-api"
                }
        │
        ▼
Swagger UI lưu access_token in-memory (KHÔNG lưu sessionStorage/IndexedDB)
        │
        ├─► UI cập nhật: nút Authorize → 🔒 (đã unlock)
        │
        └─► Mọi "Try it out" / "Execute" tiếp theo sẽ tự động gắn:
                Authorization: Bearer <access_token>
```

---

## 8. Case 4 — Gọi API endpoint từ Swagger UI

```
Tester expand "GET /api/admin/products"
        │
        ├─► Click [Try it out]
        ├─► Optionally fill query params
        └─► Click [Execute]
        │
        ▼
Swagger UI build request:
        GET http://localhost:5175/api/admin/products
        Accept: application/json
        Authorization: Bearer eyJ...   ← plain Bearer, KHÔNG có DPoP header

        ▼
WebApi pipeline:
    UseCors → UseAuthentication → UseAuthorization → MapControllers
        │
        ▼
[Authentication: scheme "dpoptokenscheme"]
        │
        ├─► Đọc Authorization header → "Bearer eyJ..."
        │
        ├─► Duende DPoP middleware kiểm tra:
        │       │
        │       ├─► Có DPoP header không? → KHÔNG
        │       │
        │       ├─► Token có cnf.jkt không?
        │       │       │
        │       │       ├─► CÓ cnf.jkt → BẮT BUỘC DPoP proof → 401
        │       │       │       (nhưng token Swagger KHÔNG có cnf.jkt → đi tiếp)
        │       │       │
        │       │       └─► KHÔNG có cnf.jkt → kiểm tra AllowBearerTokens
        │       │               │
        │       │               ├─► Dev: AllowBearerTokens = true → ACCEPT
        │       │               └─► Prod: AllowBearerTokens = false → 401
        │       │
        │       └─► JWT validation (chuẩn):
        │               ├─► Verify signature (key từ IdentityServer JWKS)
        │               ├─► Verify exp, iat, nbf
        │               ├─► Verify typ == "at+jwt"
        │               └─► Verify issuer == https://localhost:5001
        │
        └─► User principal được build với claims (sub, name, role, scope...)

        ▼
[Authorization: FallbackPolicy "RetailApi"]
        │
        ├─► RequireAuthenticatedUser() → ✓
        │
        └─► RequireAssertion: scope claim chứa "retail-api"?
                ctx.User.FindAll("scope")
                    .SelectMany(c => c.Value.Split(' '))
                    .Contains("retail-api")
                → ✓

        ▼
[Controller action]
        │
        └─► Trả về 200 OK + { data: [...] }

        ▼
Swagger UI hiển thị:
        ┌─────────────────────────────────────┐
        │  Server response                    │
        │  Code: 200                          │
        │  Response body:                     │
        │  {                                  │
        │    "data": [                        │
        │      { "id": 1, "name": "..." },    │
        │      ...                            │
        │    ]                                │
        │  }                                  │
        │  Response headers:                  │
        │    content-type: application/json   │
        │    date: ...                        │
        └─────────────────────────────────────┘
```

> **Lưu ý quan trọng:** WebApi KHÔNG thấy header `DPoP` ở đây. Token swagger-ui là plain Bearer thuần. Nếu đặt breakpoint ở Duende middleware, thấy `cnf.jkt = null` và đi nhánh `AllowBearerTokens=true`. Khác hoàn toàn với token react-dpop (có `cnf.jkt`, bắt buộc proof).

---

## 9. Case 5 — Token hết hạn

```
access_token (Swagger) hết hạn sau 1 giờ
        │
        ▼
Tester click [Execute] với token cũ
        │
        └─► WebApi middleware:
                ├─► JWT validation: exp < now → fail
                └─► 401 Unauthorized
                    WWW-Authenticate: Bearer error="invalid_token",
                                      error_description="The token expired at ..."

        ▼
Swagger UI:
        ├─► Hiển thị 401 trong Response section
        └─► KHÔNG có cơ chế silent renew built-in cho Auth Code flow

Cách handle (tester thao tác thủ công):
        │
        ├─► Click [Authorize] lần nữa
        ├─► Click [Logout] trong dialog (nếu có nút)
        ├─► Click [Authorize] → popup mở lại
        │
        └─► Vì IdentityServer cookie session vẫn còn (chưa hết hạn),
            user KHÔNG cần nhập lại password — auto redirect về Swagger
            với code mới.
            → Token mới được issue tự động trong 1-2 click.
```

> **Khác biệt với frontend:**
> Frontend dùng `oidc-client-ts` có `signinSilent()` chạy refresh token grant + prompt=none silent renew, attach DPoP proof tự động. Swagger không có gì tương đương — phải click lại.
>
> **Có thể cấp refresh_token cho swagger-ui không?**
> Về kỹ thuật được, set `AllowOfflineAccess = true` + thêm scope `offline_access`. Nhưng Swagger UI cũng không tự động dùng refresh_token — vẫn phải click lại. Không đáng config thêm.

---

## 10. Case 6 — Logout

```
Tester click [Authorize] → Dialog mở
        │
        └─► Click [Logout] trong dialog
                │
                ├─► Swagger UI clear access_token in-memory
                ├─► UI cập nhật: nút Authorize → 🔓 (locked lại)
                │
                └─► KHÔNG gọi end_session endpoint của IdentityServer

⚠️  IdentityServer cookie vẫn còn!
        Tester click [Authorize] lại → popup mở /connect/authorize
        → Vì cookie session vẫn valid, redirect ngay với code mới
        → Token cấp mới mà KHÔNG cần nhập lại password.

Để force re-login từ đầu (nhập lại password):
        Cách 1: Mở popup IdentityServer thủ công và logout:
                https://localhost:5001/Account/Logout

        Cách 2: Clear cookies trong DevTools → Application → Cookies →
                xóa cookie .AspNetCore.Identity.Application của localhost:5001

        Cách 3: Mở Incognito/InPrivate window mới
```

> **Note bảo mật:** Việc Swagger logout không invalidate IdentityServer session là chấp nhận được vì Swagger là dev tool, không expose ra Internet. Nếu lo lắng, dùng Incognito window.

---

## 11. Case 7 — Production: Swagger bị chặn 3 lớp

Giả sử ai đó vô tình deploy code có Swagger reference lên Production. Cả 3 lớp gating cùng kích hoạt:

```
─────────────────────────────────────────────────────────────────
Lớp 1 chặn: GET https://api.example.com/swagger
─────────────────────────────────────────────────────────────────

    if (app.Environment.IsDevelopment())   // ← false trong Prod
    {
        app.UseSwagger();
        app.UseSwaggerUI(...);
    }

    → Pipeline KHÔNG mount /swagger middleware
    → 404 Not Found
    → Tester không thấy UI nào để abuse

─────────────────────────────────────────────────────────────────
Lớp 2 chặn: Giả sử lưu được /swagger/v1/swagger.json từ Dev rồi
            self-host UI để gọi /connect/authorize
─────────────────────────────────────────────────────────────────

    POST https://identity.example.com/connect/token
        client_id=swagger-ui
        ...

    IdentityServer:
        Config.Clients(isDevelopment: false)
            → swagger-ui client KHÔNG yield → không tồn tại
        → invalid_client error
        → 400 Bad Request

    Attacker không lấy được token nào với client_id=swagger-ui

─────────────────────────────────────────────────────────────────
Lớp 3 chặn: Giả sử bypass được lớp 2 (ví dụ misconfiguration tạm
            tạo swagger-ui client trên Prod IDS) → có plain Bearer
─────────────────────────────────────────────────────────────────

    GET https://api.example.com/api/admin/products
        Authorization: Bearer <leaked_token>

    WebApi:
        AllowBearerTokens = builder.Environment.IsDevelopment()
                          = false (Prod)

    Duende DPoP middleware:
        ├─► Có DPoP header? → Không
        ├─► Token có cnf.jkt? → Không (plain Bearer)
        └─► AllowBearerTokens? → false
            → 401 Unauthorized
              error="invalid_token"
              error_description="DPoP proof required"
```

**Kết luận:** Phải có **3 lỗi cấu hình đồng thời** mới có thể plain-Bearer hoạt động ở Prod. Không thể vô tình.

---

## 12. So sánh Frontend (DPoP) vs Swagger (Bearer)

| Khía cạnh | Frontend (react-dpop) | Swagger UI (swagger-ui) |
|-----------|----------------------|-------------------------|
| **Client ID** | `react-dpop` | `swagger-ui` |
| **RequireDPoP** | `true` | `false` |
| **Environment** | Dev + Prod | **Chỉ Dev** |
| **Library** | `oidc-client-ts` v3.5.0 | Swashbuckle.AspNetCore (built-in) |
| **Keypair** | ECDSA P-256, IndexedDB, non-extractable | Không có |
| **Token type** | DPoP-bound (`token_type: DPoP`) | Plain Bearer (`token_type: Bearer`) |
| **`cnf.jkt` trong access token** | Có | Không |
| **Header gửi API** | `Authorization: DPoP <token>` + `DPoP: <proof>` | `Authorization: Bearer <token>` |
| **Per-request proof** | Build proof JWT mới (jti, htm, htu, iat, ath, nonce) | Không |
| **Replay protection** | jti cache (IDistributedCache) | Không (Bearer không có jti) |
| **Token theft impact** | USELESS (thiếu private key) | Token dùng được trong TTL (Dev only) |
| **Refresh flow** | Auto silent renew + DPoP proof | Click Authorize lại thủ công |
| **PKCE** | Có | Có |
| **Authorization Code** | Có | Có |
| **`dpop_jkt` parameter trong /authorize** | Có | Không |
| **Logout** | `end_session` endpoint + clear keypair + rotate | Clear in-memory token (không touching IDS) |
| **Use case** | End-user app | Dev/QA testing local |

---

## 13. Cấu hình hai client trong IdentityServer

```csharp
// IdentityServer/Config.cs
public static IEnumerable<Client> Clients(bool isDevelopment)
{
    // ============================================================
    // CLIENT 1: react-dpop (production + dev)
    // ============================================================
    yield return new Client
    {
        ClientId = "react-dpop",
        ClientName = "Retail Store React SPA",

        AllowedGrantTypes = GrantTypes.Code,
        RequirePkce = true,
        RequireClientSecret = false,

        RequireDPoP = true,                    // ← BẮT BUỘC DPoP
        DPoPValidationMode = DPoPTokenExpirationValidationMode.Iat,

        AllowedScopes = { "openid", "profile", "retail-api", "offline_access" },
        AllowOfflineAccess = true,
        RefreshTokenUsage = TokenUsage.OneTimeOnly,

        AccessTokenLifetime = 900,             // 15 phút
        AbsoluteRefreshTokenLifetime = 604800, // 7 ngày sliding

        RedirectUris = { "http://localhost:5173/callback" },
        PostLogoutRedirectUris = { "http://localhost:5173" },
        AllowedCorsOrigins = { "http://localhost:5173" }
    };

    // ============================================================
    // CLIENT 2: swagger-ui (DEV ONLY)
    // ============================================================
    if (isDevelopment)
    {
        yield return new Client
        {
            ClientId = "swagger-ui",
            ClientName = "Swagger UI (Development)",

            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,

            RequireDPoP = false,               // ← KHÔNG yêu cầu DPoP

            AllowedScopes = { "openid", "profile", "retail-api" },
            // KHÔNG có offline_access — Swagger UI không dùng refresh

            AccessTokenLifetime = 3600,        // 1 giờ (đủ cho session test)

            RedirectUris = { "http://localhost:5175/swagger/oauth2-redirect.html" },
            AllowedCorsOrigins = { "http://localhost:5175" }
        };
    }
}
```

```csharp
// IdentityServer/Program.cs
builder.Services.AddIdentityServer(options => { ... })
    .AddInMemoryClients(Config.Clients(builder.Environment.IsDevelopment()))
    //                                  ↑ Pass env flag vào
    ...
```

---

## 14. Ghi chú kỹ thuật bổ sung

### 14.1. Swagger UI và CORS

WebApi CORS policy whitelist `http://localhost:5173` (frontend). Swagger UI chạy **same-origin** với WebApi (`http://localhost:5175/swagger/...`) nên KHÔNG cần CORS — fetch `/api/admin/products` từ Swagger UI là same-origin XHR.

IdentityServer KHÔNG cần thêm `http://localhost:5175` vào `AllowedCorsOrigins` của `swagger-ui` client vì:
- `/connect/authorize` redirect-based (full page navigation, không cần CORS)
- `/connect/token` được gọi qua iframe/popup từ same-origin code Swagger UI redirect handler
- Tuy nhiên, Duende default thêm origin từ `RedirectUris` → safe.

### 14.2. PKCE bắt buộc với cả hai client

Cả `react-dpop` và `swagger-ui` đều có `RequirePkce = true`:
- `react-dpop` (public client SPA) — bắt buộc theo OAuth 2.1
- `swagger-ui` (dev tool) — best practice, không có lý do gì để tắt

Swagger UI tự generate code_verifier khi `c.OAuthUsePkce()` được set trong `Program.cs`.

### 14.3. Tại sao dùng `Authorization Code` thay vì `Implicit`?

OpenAPI 3.0 hỗ trợ cả 4 flow OAuth2: Implicit, Password, ClientCredentials, AuthorizationCode. Implicit trả token trực tiếp trong URL fragment — bị deprecated trong OAuth 2.1 vì:
- Token leak qua browser history, referer headers, server logs
- Không có PKCE
- Refresh token không khả dụng

Auth Code + PKCE là chuẩn an toàn hiện đại, ngay cả cho Swagger.

### 14.4. Tại sao token Swagger không có cnf.jkt?

Trong Duende IdentityServer:
- Khi client có `RequireDPoP = true` VÀ request có DPoP proof → IDS tính `jkt` từ proof header `jwk` và embed vào token claim `cnf.jkt`.
- Khi client có `RequireDPoP = false` → IDS bỏ qua bước này, token KHÔNG có `cnf.jkt`.

Vì `swagger-ui` config `RequireDPoP = false` và Swagger UI không gửi DPoP header, token issued ra hoàn toàn không có confirmation claim → trở thành plain Bearer hợp pháp.

### 14.5. Audit log

Mọi request từ `swagger-ui` client xuất hiện trong IdentityServer event log với:
```
ClientId: swagger-ui
SubjectId: <user_id>
Endpoint: /connect/token
ResponseType: code
Scope: openid profile retail-api
```

Production grep log thấy `ClientId: swagger-ui` → red flag (vì client này không tồn tại). Có thể setup alert dựa trên pattern này.

### 14.6. Có thể thêm DPoP cho Swagger không?

Về lý thuyết:
- Custom JS injected vào Swagger UI qua `c.InjectJavascript("/dpop-injector.js")`
- Override `fetch` global, intercept request đến API origin, generate ECDSA keypair + sign proof
- Lưu keypair trong IndexedDB

Thực tế:
- Phải maintain crypto code trong tooling (chi phí cao)
- Swashbuckle update có thể break custom injection
- Không có audit/test coverage
- → Không đáng. Defense in depth env-gated đủ an toàn cho dev tool.

### 14.7. Khác biệt với token frontend bị steal

**Token frontend (react-dpop) bị steal:**
- Có `cnf.jkt`
- Attacker thử plain Bearer → 401 (cnf.jkt enforcement bật unconditional)
- Attacker thử DPoP với key khác → 401 (jkt mismatch)
- → USELESS

**Token Swagger (swagger-ui) bị steal:**
- KHÔNG có `cnf.jkt`
- Attacker thử plain Bearer trong Dev → ✓ THÀNH CÔNG (1 giờ TTL)
- Attacker thử trong Prod → 401 (AllowBearerTokens=false)
- → Risk window = 1 giờ trong Dev environment localhost

Mitigation: Dev environment KHÔNG được expose ra Internet. Swagger token có TTL ngắn (1 giờ).
