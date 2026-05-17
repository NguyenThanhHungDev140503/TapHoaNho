# WebApi/Program.cs — Giải thích chi tiết

File này là **điểm khởi động** của ứng dụng WebApi. Nó làm 2 việc lớn theo thứ tự:

1. **Đăng ký services** (dòng 9–233) — "chuẩn bị nguyên liệu"
2. **Xây dựng middleware pipeline** (dòng 235–280) — "xếp hàng các bộ lọc mà mỗi request phải đi qua"

Tưởng tượng như mở một cửa hàng: phần 1 là thuê nhân viên + mua thiết bị, phần 2 là sắp xếp luồng khách hàng đi vào phải qua bảo vệ → quầy tiếp tân → rồi mới đến quầy hàng.

---

## PHẦN 1: ĐĂNG KÝ SERVICES

### Block 1 — Logging ban đầu (dòng 9–16)

```csharp
var builder = WebApplication.CreateBuilder(args);
var environment = builder.Environment.EnvironmentName;
var logger = LoggerFactory.Create(config => config.AddConsole()).CreateLogger<Program>();
```

**Làm gì:** Tạo app builder và in ra console cho biết đang chạy ở môi trường nào (`Development` hay `Production`).

**Tại sao:** Khi debug, mở terminal lên là biết ngay app đang chạy mode gì. Nhiều cấu hình phía dưới thay đổi theo môi trường, nên biết sớm rất quan trọng.

---

### Block 2 — CORS (dòng 18–44)

```csharp
var allowedOrigins = corsSettings.GetSection("AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddCors(options => {
    policy.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("DPoP-Nonce", "WWW-Authenticate");
});
```

**Làm gì:** Cho phép frontend (React ở port 5173) gọi API (port 5175).

**Vấn đề mà nó giải quyết:** Trình duyệt mặc định **chặn** request từ `localhost:5173` sang `localhost:5175` vì khác port (Same-Origin Policy). CORS là cơ chế "cho phép ngoại lệ".

**Chi tiết quan trọng:**
- `AllowAnyHeader()` / `AllowAnyMethod()` — cho phép mọi header và HTTP method (GET, POST, PUT, DELETE...)
- `.WithExposedHeaders("DPoP-Nonce", "WWW-Authenticate")` — mặc định trình duyệt **giấu** các header response khỏi JavaScript. Nhưng DPoP cần đọc 2 header này:
  - `DPoP-Nonce`: server gửi nonce mới, frontend cần đọc để gắn vào request tiếp theo
  - `WWW-Authenticate`: khi bị 401, frontend đọc header này để biết lý do (ví dụ "nonce hết hạn")

---

### Block 3 — Clean Architecture DI (dòng 49–51)

```csharp
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);
```

**Làm gì:** Đăng ký tất cả services của 2 layer:
- `AddApplication()` — MediatR (CQRS), FluentValidation, AutoMapper
- `AddInfrastructure(config)` — EF Core (database), Repository pattern, UnitOfWork

**Tại sao tách ra 2 method:** Mỗi layer tự biết mình cần đăng ký gì. `Program.cs` không cần biết chi tiết bên trong — chỉ gọi 1 dòng là xong. Đây là nguyên tắc Clean Architecture: **layer ngoài không biết chi tiết layer trong**.

---

### Block 4 — Exception Handler + Rate Limiting (dòng 56–58)

```csharp
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddSetupRateLimiting();
```

**Làm gì:**
- `GlobalExceptionHandler` — bắt mọi exception chưa được xử lý, trả về JSON thống nhất thay vì để app crash hoặc trả HTML lỗi xấu xí. Ví dụ: `NotFoundException` → 404, `ValidationException` → 400 với danh sách lỗi
- `AddProblemDetails()` — format lỗi theo chuẩn RFC 7807
- `AddSetupRateLimiting()` — giới hạn số request/giây để chống spam/DDoS

---

### Block 5 — Controllers + JSON config (dòng 63–68)

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
```

**Làm gì:** Đăng ký MVC controllers và cấu hình JSON serialization.

**Tại sao cần config JSON:**
- `CamelCase` — C# dùng `PascalCase` (`ProductName`), nhưng frontend JavaScript dùng `camelCase` (`productName`). Config này tự động chuyển đổi
- `CaseInsensitive = true` — khi nhận JSON từ frontend, không phân biệt hoa/thường → linh hoạt hơn

---

### Block 6 — Authentication / DPoP (dòng 72–153)

Đây là trái tim bảo mật của app. Chia thành 4 bước nhỏ:

#### Bước 6a — Khai báo cơ bản (dòng 76–78)

```csharp
const string DPoPScheme = "dpoptokenscheme";
var identityServerAuthority = builder.Configuration["IdentityServer:Authority"]
    ?? "https://localhost:5001";
```

- `DPoPScheme` là constant vì tên scheme được dùng ở **3 chỗ** (dòng 80, 81, 123). Dùng `const` thì compiler bắt lỗi typo ngay.
- `identityServerAuthority` đọc từ config, fallback `localhost:5001` để dev mới clone repo chạy được ngay.

#### Bước 6b — JWT Bearer handler (dòng 80–101)

```csharp
builder.Services.AddAuthentication(DPoPScheme)
    .AddJwtBearer(DPoPScheme, options => {
        options.Authority = identityServerAuthority;
        options.TokenValidationParameters.ValidateAudience = false;
        options.TokenValidationParameters.ValidTypes = ["at+jwt"];
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });
```

Tưởng tượng đây là **bảo vệ cổng** của API. Mỗi request đến, bảo vệ kiểm tra "vé" (token):

| Config | Ý nghĩa | Tại sao |
|--------|----------|---------|
| `Authority` | "Tôi tin vé do ai phát hành" | Tự động lấy public key từ IdentityServer để verify chữ ký token. Không cần share secret key |
| `ValidateAudience = false` | Không kiểm tra "vé này dành cho ai" | Duende v7 dùng scope-based audience, mình validate scope riêng ở Authorization policy (dòng 164) |
| `ValidTypes = ["at+jwt"]` | Chỉ chấp nhận access token chuẩn RFC 9068 | Từ chối ID token hoặc token lạ bị gửi nhầm |
| `MapInboundClaims = false` | Giữ nguyên tên claim | Mặc định .NET đổi `"role"` thành `ClaimTypes.Role` (URL dài ngoằng). Tắt đi cho đơn giản |
| `RequireHttpsMetadata` | Dev: cho HTTP, Prod: bắt buộc HTTPS | Dev chạy localhost không có cert, Prod phải HTTPS |

#### Bước 6c — DPoP layer (dòng 103–132)

```csharp
builder.Services.ConfigureDPoPTokensForScheme(DPoPScheme, opt => {
    opt.ProofTokenIssuedAtClockSkew = TimeSpan.FromSeconds(30);
    opt.AllowBearerTokens = allowBearerTokens;  // Dev: true, Prod: false
    opt.EnableReplayDetection = true;
});
```

Đây là **lớp bảo mật thứ 2** trên JWT. So sánh:

> **JWT thường (Bearer)**: Giống như thẻ vào tòa nhà — ai nhặt được đều vào được.
>
> **DPoP**: Giống thẻ vào tòa nhà **kèm vân tay** — mỗi lần quẹt thẻ phải kèm vân tay đúng. Hacker đánh cắp thẻ (token) cũng vô ích vì không có vân tay (private key nằm trong trình duyệt, không thể copy).

| Config | Ý nghĩa |
|--------|----------|
| `ClockSkew = 30s` | Cho phép đồng hồ client lệch ±30 giây so với server |
| `AllowBearerTokens` | Dev cho phép Bearer thường (để Swagger test), Prod bắt buộc DPoP |
| `EnableReplayDetection` | Ghi nhớ mỗi proof đã dùng → chặn hacker "phát lại" request cũ |

#### Bước 6d — Replay cache (dòng 134–153)

```csharp
if (!string.IsNullOrEmpty(redisConnectionString))
    builder.Services.AddStackExchangeRedisCache(...);
else
    builder.Services.AddDistributedMemoryCache();
```

Replay detection cần **nhớ** những proof đã thấy. Có Redis → dùng Redis (nhiều server instance share được). Không có → dùng bộ nhớ RAM (chỉ OK cho 1 instance).

---

### Block 7 — Authorization (dòng 155–177)

```csharp
options.AddPolicy("RetailApi", policy => {
    policy.RequireAuthenticatedUser();
    policy.RequireAssertion(ctx =>
        ctx.User.FindAll("scope")
            .SelectMany(c => c.Value.Split(' '))
            .Contains("retail-api"));
});
options.FallbackPolicy = options.GetPolicy("RetailApi");
```

**Làm gì:** Sau khi Authentication xác nhận "bạn là ai", Authorization kiểm tra "bạn có quyền không".

**Luồng logic:**
1. Phải đăng nhập (`RequireAuthenticatedUser`)
2. Token phải chứa scope `retail-api`
3. `FallbackPolicy` = áp dụng cho **mọi endpoint** mà không cần gắn `[Authorize]` thủ công

**Tại sao phải `.Split(' ')`:** Theo chuẩn RFC 9068, scope trong JWT là **1 chuỗi cách nhau bằng dấu cách**: `"openid profile retail-api"` — không phải mảng JSON. Nên phải tách ra rồi kiểm tra.

---

### Block 8 — Swagger (dòng 182–233)

```csharp
builder.Services.AddSwaggerGen(options => {
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme {
        Flows = new OpenApiOAuthFlows {
            AuthorizationCode = new OpenApiOAuthFlow {
                AuthorizationUrl = new Uri($"{identityServerAuthority}/connect/authorize"),
                TokenUrl = new Uri($"{identityServerAuthority}/connect/token"),
                Scopes = { ... }
            }
        }
    });
});
```

**Làm gì:** Cấu hình Swagger UI để dev có thể **test API có authentication** ngay trên trình duyệt.

**Luồng khi bấm "Authorize" trong Swagger:**
1. Swagger redirect đến IdentityServer → đăng nhập
2. IdentityServer trả auth code → Swagger đổi lấy token
3. Swagger tự gắn token vào mọi request test

**3 lớp bảo vệ để Swagger không lọt ra Production:**
1. `UseSwagger()` chỉ gọi khi `IsDevelopment()` (dòng 241)
2. Client `swagger-ui` chỉ tồn tại trong Dev IdentityServer config
3. `AllowBearerTokens = false` ở Prod → token không DPoP bị từ chối

---

## PHẦN 2: MIDDLEWARE PIPELINE (dòng 235–280)

```
Request vào
    │
    ▼
┌─ Development only? ─────────────────────┐
│  Swagger UI + redirect "/" → /swagger    │
└──────────────────────────────────────────┘
    │
    ▼
┌─ Production only? ──────────────────────┐
│  HSTS + HTTPS redirect                  │
└──────────────────────────────────────────┘
    │
    ▼
   CORS            ← Kiểm tra origin có được phép không
    │
    ▼
  Rate Limiter     ← Chặn nếu quá nhiều request
    │
    ▼
  Exception Handler  ← Bọc mọi thứ phía dưới, bắt lỗi
    │
    ▼
  Authentication   ← "Bạn là ai?" — verify token + DPoP proof
    │
    ▼
  Authorization    ← "Bạn có quyền không?" — check scope + role
    │
    ▼
  MapControllers   ← Chuyển request đến đúng controller/action
    │
    ▼
  Response trả về
```

**Thứ tự rất quan trọng:**
- CORS phải trước Authentication — nếu không, preflight request (OPTIONS) bị reject trước khi đến được CORS handler
- Authentication phải trước Authorization — phải biết "bạn là ai" rồi mới kiểm tra "bạn có quyền không"
- ExceptionHandler bọc ngoài cùng — bất kỳ middleware nào throw exception đều được bắt

---

## TÓM TẮT TOÀN BỘ FILE

| Dòng | Block | Một câu tóm tắt |
|------|-------|-----------------|
| 9–16 | Startup log | In môi trường đang chạy |
| 18–44 | CORS | Cho phép frontend gọi API cross-origin, expose DPoP headers |
| 49–51 | Clean Architecture DI | Đăng ký Application + Infrastructure layers |
| 56–58 | Error handling | Bắt exception → trả JSON chuẩn |
| 63–68 | Controllers | Đăng ký controllers + JSON camelCase |
| 76–101 | JWT Authentication | Verify token do IdentityServer phát hành |
| 103–132 | DPoP layer | Bảo mật thêm: proof-of-possession, chống replay |
| 134–153 | Replay cache | Redis hoặc in-memory cho DPoP jti tracking |
| 155–177 | Authorization | Mọi endpoint yêu cầu scope `retail-api` |
| 182–233 | Swagger | Dev-only API testing UI với OAuth flow |
| 235–280 | Pipeline | Xếp thứ tự middleware: CORS → Rate limit → Auth → Controllers |

---

## PHỤ LỤC: DPoP-Nonce là gì?

**Nonce** = "number used once" — một chuỗi ngẫu nhiên mà **server tạo ra** và bắt frontend phải gắn vào DPoP proof tiếp theo.

### Vấn đề mà nó giải quyết

DPoP proof bình thường đã có `jti` (ID duy nhất) + `iat` (thời gian tạo) để chống replay. Nhưng có lỗ hổng:

> Hacker chặn được request, **ngay lập tức** gửi lại (trong vài giây) trước khi request gốc đến server → server chưa thấy `jti` đó → chấp nhận.

Nonce giải quyết bằng cách thêm **yếu tố server-side** mà hacker không thể đoán trước.

### Luồng hoạt động

```
1. Frontend gửi request (chưa có nonce)
       │
       ▼
2. Server trả 401 + header:
       DPoP-Nonce: abc123xyz
       │
       ▼
3. Frontend đọc nonce, tạo DPoP proof MỚI có chứa:
       { "jti": "...", "htm": "GET", "htu": "/api/products",
         "iat": 1714470000, "nonce": "abc123xyz" }
       │
       ▼
4. Frontend gửi lại request với proof mới
       │
       ▼
5. Server verify: nonce trong proof === nonce mình đã phát → OK
```

### Tại sao hacker không bypass được?

- Hacker **không biết nonce tiếp theo** sẽ là gì (server random)
- Nonce **thay đổi định kỳ** — proof cũ với nonce cũ bị từ chối
- Hacker có đánh cắp proof cũng vô ích vì nonce đã hết hạn

### Liên quan đến code trong Program.cs

```csharp
// Dòng 42 — expose header để trình duyệt JavaScript đọc được
.WithExposedHeaders("DPoP-Nonce", "WWW-Authenticate")

// Dòng 125 — cho phép đồng hồ lệch 30 giây
opt.ProofTokenIssuedAtClockSkew = TimeSpan.FromSeconds(30);
```

Nếu không có `.WithExposedHeaders("DPoP-Nonce")`, trình duyệt **giấu** header này khỏi JavaScript → frontend không đọc được nonce → mọi request sau đều fail 401 → app chết.

### So sánh có và không có Nonce

| Không có nonce | Có nonce |
|---|---|
| Proof chỉ cần private key + thời gian | Proof cần private key + thời gian + **chuỗi do server cấp** |
| Hacker replay ngay lập tức có thể thành công | Hacker không có nonce mới → bị từ chối |

Nonce là lớp bảo vệ **thứ 3** sau private key binding và replay detection (`jti`).
