# Phân Tích `oidc-client-ts` — Vai Trò Trong DPoP Flow

**Phiên bản:** `oidc-client-ts` v3.5.0  
**Codebase:** `frontend/src/lib/oidc/userManager.ts`

---

## 1. Tổng Quan: Thư Viện Này Làm Gì

`oidc-client-ts` là thư viện TypeScript xử lý toàn bộ phía client của giao thức **OpenID Connect (OIDC) / OAuth 2.1**. Trong codebase này, nó đảm nhận **3 nhiệm vụ cốt lõi**:

| Nhiệm vụ | Mô tả |
|---------|-------|
| **Keypair management** | Tự động generate ECDSA P-256 keypair, lưu vào IndexedDB, tái sử dụng suốt session |
| **Token lifecycle** | Điều phối toàn bộ Authorization Code + PKCE + DPoP flow để lấy token |
| **Silent renew** | Tự động làm mới access token bằng refresh token khi sắp hết hạn |

---

## 2. Kiến Trúc Bên Trong: Các Component Được Dùng

```
oidc-client-ts
│
├── UserManager                      ← Entry point chính của ứng dụng
│   ├── signinRedirect()             ← Bắt đầu login flow
│   ├── signinRedirectCallback()     ← Xử lý callback từ IdentityServer
│   ├── signinSilent()               ← Làm mới token bằng refresh token
│   ├── signoutRedirect()            ← Logout qua end_session endpoint
│   ├── getUser()                    ← Đọc session hiện tại từ storage
│   └── generateDPoPJkt()           ← Tính JWK thumbprint cho dpop_jkt param
│
├── OidcClient                       ← Lớp dưới của UserManager
│   └── getDpopProof()              ← Build DPoP proof JWT cho token endpoint
│
├── IndexedDbDPoPStore               ← Lưu trữ keypair trong IndexedDB
│   ├── get(clientId)               ← Đọc DPoPState { keys, nonce? }
│   ├── set(clientId, state)        ← Lưu DPoPState
│   └── remove(clientId)            ← Xóa (dùng khi logout)
│
└── DPoPState                        ← Data object
    ├── keys: CryptoKeyPair          ← { publicKey, privateKey }
    └── nonce?: string               ← Server-issued nonce (nếu có)
```

### Điểm quan trọng về `IndexedDbDPoPStore`

```typescript
// Khai báo kiểu trong oidc-client-ts:
declare class IndexedDbDPoPStore implements DPoPStore {
  set(key: string, value: DPoPState): Promise<void>;
  get(key: string): Promise<DPoPState>;
  remove(key: string): Promise<DPoPState>;
}
```

Bất kể bạn truyền tham số gì vào constructor, thư viện v3.5.0 **hardcode** tên database là `"oidc"` và tên store là `"dpop"`. Key để lookup là `client_id` (`"react-dpop"`). Đây là lý do `userManager.ts` phải dùng đúng `CLIENT_ID` khi gọi `dpopStore.get(CLIENT_ID)`.

---

## 3. Cơ Chế Keypair — Trái Tim của DPoP

### 3.1 Keypair Được Tạo Ở Đâu và Khi Nào

```
Lần đầu gọi signinRedirect()
    │
    └─► UserManager.generateDPoPJkt(dpopSettings)
        └─► OidcClient gọi IndexedDbDPoPStore.get('react-dpop')
            ├─► Có dữ liệu trong IndexedDB? → Dùng keypair cũ
            └─► Chưa có? → Gọi crypto.subtle.generateKey():
                    {
                      name: 'ECDSA',
                      namedCurve: 'P-256',       ← hardcoded bởi library
                    },
                    extractable = false,          ← KHÔNG THỂ export ra bytes
                    ['sign']
                │
                └─► Lưu DPoPState { keys: CryptoKeyPair } vào IndexedDB
                    Tính JWK thumbprint (SHA-256 của canonical JWK)
                    → dpop_jkt = "abc123..."
```

### 3.2 Tại Sao `extractable = false` Quan Trọng

```
extractable = false có nghĩa:

  crypto.subtle.exportKey('raw', privateKey)  → THROWS DOMException
  crypto.subtle.exportKey('pkcs8', privateKey) → THROWS DOMException

Ngay cả XSS inject code vào trang cũng không thể đọc được private key bytes.
Attacker chỉ có thể gọi crypto.subtle.sign() — và sign gì thì IdentityServer
sẽ verify chính xác key đó có khớp với cnf.jkt trong token hay không.
```

### 3.3 Vì Sao Không Dùng sessionStorage hay localStorage

IndexedDB được chọn vì:
- Hỗ trợ lưu `CryptoKey` object trực tiếp (sessionStorage/localStorage chỉ lưu string, phải serialize)
- `CryptoKey` **không thể** serialize thành string nếu `extractable=false` — JSON.stringify trả về `{}`
- IndexedDB tồn tại qua hard reload (tab đóng mở lại vẫn dùng được keypair cũ)

---

## 4. `oidc-client-ts` Đóng Góp Vào DPoP Flow — Từng Bước

### Bước 1: `/connect/authorize` — Bind Keypair Vào Auth Session

```typescript
// userManager.ts:66-69
dpop: {
  bind_authorization_code: true,  // ← bật binding
  store: dpopStore,
}
```

Khi `signinRedirect()` được gọi, thư viện:

1. Gọi `generateDPoPJkt()` → lấy/tạo keypair → tính thumbprint
2. Thêm `dpop_jkt=<thumbprint>` vào URL authorize

```
GET https://localhost:5001/connect/authorize
  ?client_id=react-dpop
  &response_type=code
  &code_challenge=<S256_PKCE>
  &code_challenge_method=S256
  &scope=openid+profile+retail-api+offline_access
  &redirect_uri=http://localhost:5173/callback
  &dpop_jkt=<SHA256_of_publicKey_JWK>   ← library thêm tự động
  &state=<csrf_state>
```

IdentityServer nhận `dpop_jkt`, ghi vào auth session. Khi exchange code → token, nếu proof không ký bằng keypair có thumbprint khớp `dpop_jkt` → từ chối.

### Bước 2: `/connect/token` — Exchange Code Lấy DPoP-Bound Token

Sau khi user đăng nhập, IdentityServer redirect về `/callback?code=...`. `signinCallback()` gọi `signinRedirectCallback()`:

1. Thư viện đọc keypair từ IndexedDB
2. Build DPoP proof JWT cho token endpoint:
   ```
   POST https://localhost:5001/connect/token
   DPoP: eyJ...   ← library ký bằng privateKey, htm=POST, htu=/connect/token
   Body: code=... &code_verifier=... &grant_type=authorization_code
   ```
3. IdentityServer validate: proof signature khớp → `dpop_jkt` khớp → OK
4. Trả về:
   ```json
   {
     "access_token": "eyJ...",    ← typ=at+jwt, chứa cnf.jkt claim
     "token_type": "DPoP",        ← KHÔNG phải "Bearer"
     "refresh_token": "...",
     "expires_in": 900
   }
   ```
5. Thư viện lưu `User` object (chứa access_token, profile) vào sessionStorage

**`cnf.jkt` trong access token** là thumbprint của keypair → từ đây mọi API call phải dùng ĐÚNG keypair này để ký proof.

### Bước 3: API Calls — `oidc-client-ts` Cung Cấp Nguyên Liệu

Thư viện **không** tự gắn header vào axios. Nhưng nó cung cấp 2 thứ:

```typescript
// axios.ts
const oidcUser = await getUser();             // ← lấy access_token từ sessionStorage
const keyPair = await getDPoPKeyPair();       // ← lấy CryptoKeyPair từ IndexedDB
```

Sau đó `buildDPoPProof()` (code tự viết trong `dpop.ts`) dùng `keyPair.privateKey` để ký proof.

Sơ đồ quan hệ:

```
oidc-client-ts (UserManager)
│   sessionStorage["oidc.user:..."]  →  access_token
│
oidc-client-ts (IndexedDbDPoPStore)
│   IndexedDB["oidc"]["dpop"]["react-dpop"]  →  CryptoKeyPair
│
          ↓ cả hai được đọc bởi axios interceptor ↓
│
axios.ts  →  buildDPoPProof(keyPair, { htm, htu, accessToken, nonce })
          →  Authorization: DPoP <access_token>
          →  DPoP: <proof_jwt>
```

### Bước 4: Silent Renew — Duy Trì DPoP Binding

Khi access token hết hạn (900 giây), axios interceptor gọi `silentRenew()` → `signinSilent()`:

1. Thư viện lấy refresh token từ sessionStorage
2. Đọc keypair từ IndexedDB (**cùng keypair cũ**)
3. Build proof JWT mới cho token endpoint
4. POST `/connect/token` với `grant_type=refresh_token` + DPoP proof
5. IdentityServer kiểm tra: `requireDPoP=true` → refresh token PHẢI đi kèm proof
6. Trả về access token mới — `cnf.jkt` **vẫn là thumbprint của cùng keypair**

```
[Session tiếp tục]
access_token mới: cnf.jkt = thumbprint(pub_key cũ) ← KHÔNG THAY ĐỔI
CryptoKeyPair trong IndexedDB: KHÔNG THAY ĐỔI

→ binding end-to-end vẫn nhất quán
```

### Bước 5: Logout — Lifecycle Hoàn Chỉnh

```typescript
// authStore.ts:108-116
export async function logout(): Promise<void> {
  useAuthStore.getState().clearAuth();    // xóa Zustand state
  await clearDPoPKeyPair();              // IndexedDbDPoPStore.remove('react-dpop')
  await signoutRedirect();              // UserManager.signoutRedirect()
}
```

`signoutRedirect()` POST đến `/connect/endsession`, IdentityServer revoke session cookie và refresh token.

---

## 5. Quan Hệ `oidc-client-ts` ↔ Backend

### IdentityServer (`Config.cs` / `appsettings.json`)

```json
{
  "clientId": "react-dpop",
  "requireDPoP": true,             ← bắt buộc proof trên mọi token request
  "requirePkce": true,             ← S256 code challenge
  "requireClientSecret": false,    ← SPA public client
  "accessTokenLifetime": 900,      ← 15 phút
  "refreshTokenUsage": "OneTimeOnly",  ← refresh token rotate sau mỗi lần dùng
  "allowOfflineAccess": true       ← cấp refresh token (scope offline_access)
}
```

`oidc-client-ts` phải config **khớp chính xác** với IdentityServer:

| `userManager.ts` | `appsettings.json` | Mục đích |
|-----------------|-------------------|---------|
| `response_type: 'code'` | `allowedGrantTypes: ["code"]` | Auth Code flow |
| `scope: '...offline_access'` | `allowOfflineAccess: true` | Lấy refresh token |
| `dpop.bind_authorization_code: true` | `requireDPoP: true` | DPoP binding |
| `redirect_uri: '.../callback'` | `redirectUris: ['.../callback']` | Whitelist callback |

### WebApi (`Program.cs`)

```csharp
// Scheme "dpoptokenscheme" validate token + proof
builder.Services.ConfigureDPoPTokensForScheme(DPoPScheme, opt => {
    opt.ProofTokenIssuedAtClockSkew = TimeSpan.FromSeconds(30);
    opt.AllowBearerTokens = builder.Environment.IsDevelopment();
    opt.EnableReplayDetection = true;
});
```

WebApi **không biết** đến `oidc-client-ts`. Nó chỉ nhận HTTP request và validate:
- `access_token.cnf.jkt` khớp `proof.jwk.thumbprint` → do `oidc-client-ts` đảm bảo (cùng keypair)
- `access_token.typ = "at+jwt"` → do IdentityServer đặt
- `proof.htm`, `proof.htu`, `proof.iat`, `proof.jti` → do `dpop.ts` (code tự viết) set đúng

---

## 6. Điều `oidc-client-ts` LÀM và KHÔNG LÀM

### Thư viện TỰ XỬ LÝ

| Việc | Chi tiết |
|------|---------|
| Generate keypair | `crypto.subtle.generateKey` với `extractable=false`, ES256/P-256 |
| Persist keypair | `IndexedDbDPoPStore` — database `"oidc"`, store `"dpop"` |
| Compute `dpop_jkt` | SHA-256 của canonical JWK (RFC 7638) |
| Thêm `dpop_jkt` vào `/authorize` | Khi `bind_authorization_code: true` |
| Ký proof cho `/connect/token` | Cả lần đầu (auth code) lẫn refresh (silent renew) |
| Lưu User (token + profile) | sessionStorage, tự clean up khi hết hạn |
| `automaticSilentRenew` | Tự trigger renew trước khi token hết hạn |

### Code CẦN TỰ VIẾT (không có trong thư viện)

| Việc | File | Lý do thư viện không làm |
|------|------|--------------------------|
| Ký proof cho **API calls** | `dpop.ts` + `axios.ts` | Library chỉ handle token endpoint, không biết về axios |
| Gắn `Authorization: DPoP` header | `axios.ts` | Nằm ngoài OIDC scope |
| Xử lý nonce challenge từ WebApi | `axios.ts` | WebApi nonce ≠ IdentityServer nonce |
| Cập nhật Zustand state | `authStore.ts` | UI state management, không thuộc OIDC |
| Route guards | `routeGuards.ts` | App-specific |
| Validate `returnTo` redirect | `LoginPage.tsx`, `OidcCallbackPage.tsx` | Security concern của app |

---

## 7. Vấn Đề Tiềm Ẩn và Ràng Buộc Quan Trọng

### 7.1 Hardcoded Database/Store Name

`IndexedDbDPoPStore` v3.5.0 **hardcode** `database="oidc"` và `store="dpop"`. Tham số constructor bị bỏ qua. Nếu upgrade lên v4 mà thư viện đổi naming convention, keypair sẽ không tìm thấy → user bị logout khi reload.

**Kiểm tra sau mỗi lần upgrade:** Mở DevTools → Application → IndexedDB và confirm database/store name không thay đổi.

### 7.2 Keypair Gắn Với Browser Profile

Keypair trong IndexedDB gắn với browser profile (và origin). Nếu user:
- Đổi máy tính → không có keypair → phải login lại (đúng behavior)
- Xóa browser data → mất keypair → phải login lại (đúng behavior)
- Dùng private/incognito window → IndexedDB thường không persist → mất keypair sau khi đóng tab

### 7.3 `RefreshTokenUsage: OneTimeOnly`

Mỗi lần `signinSilent()` thành công, IdentityServer rotate refresh token. Nếu 2 tab browser cùng gọi `silentRenew()` đồng thời:
- Tab 1 dùng refresh token → nhận token mới + refresh token mới
- Tab 2 dùng refresh token cũ (đã revoked) → lỗi → user bị redirect về login

`oidc-client-ts` có cơ chế chống điều này thông qua lock trong SharedWorker/BroadcastChannel khi nhiều tab, nhưng cần kiểm tra thực tế với browser cụ thể.

### 7.4 `automaticSilentRenew: true`

Thư viện tự theo dõi `expires_in` và schedule renew trước 60 giây. Nếu tab bị dormant (background throttle), timer có thể không fire đúng. Fallback: axios interceptor bắt 401 và gọi `silentRenew()` thủ công.

---

## 8. Sơ Đồ Phụ Thuộc Giữa Các Module

```
main.tsx
  └── authStore.initFromSession()
        └── userManager.getUser()        ← UserManager.getUser() → sessionStorage
              └── [nếu có user] authStore.setOidcUser()

LoginPage
  └── userManager.signinRedirect()
        └── UserManager.signinRedirect()
              ├── IndexedDbDPoPStore.get() → tạo keypair nếu cần
              └── redirect → IdentityServer /authorize?dpop_jkt=...

OidcCallbackPage
  └── userManager.signinCallback()
        └── UserManager.signinRedirectCallback()
              ├── IndexedDbDPoPStore.get() → đọc keypair
              ├── build proof → POST /connect/token
              └── return User → authStore.setOidcUser()

axios.ts (request interceptor)
  ├── userManager.getUser()             ← đọc access_token từ sessionStorage
  ├── userManager.getDPoPKeyPair()      ← đọc CryptoKeyPair từ IndexedDB
  └── dpop.buildDPoPProof()             ← ký proof bằng privateKey

axios.ts (response interceptor 401)
  └── userManager.silentRenew()
        └── UserManager.signinSilent()
              ├── đọc refresh_token từ sessionStorage
              ├── đọc keypair từ IndexedDB
              ├── build proof → POST /connect/token (refresh_token grant)
              └── return User mới → authStore.setOidcUser()

authStore.logout()
  ├── authStore.clearAuth()
  ├── userManager.clearDPoPKeyPair()   ← IndexedDbDPoPStore.remove()
  └── userManager.signoutRedirect()   ← UserManager.signoutRedirect()
```