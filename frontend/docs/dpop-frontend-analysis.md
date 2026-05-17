# Phân Tích DPoP Frontend — React SPA

**RFC tham chiếu:** RFC 9449 (DPoP), RFC 7638 (JWK Thumbprint), RFC 9068 (JWT Access Tokens)  
**Thư viện:** `oidc-client-ts` v3.5.0, `axios` v1.x  
**Thuật toán:** ES256 (ECDSA P-256 / SHA-256)

---

## 1. Tổng Quan Kiến Trúc

DPoP (Demonstrating Proof-of-Possession) bind access token vào một ECDSA keypair mà chỉ client nắm giữ. Ngay cả khi token bị đánh cắp, kẻ tấn công không có private key tương ứng nên không thể tạo proof hợp lệ.

```
┌──────────────────────────────────────────────────────────────────┐
│                           Browser                                │
│                                                                  │
│  ┌──────────────────────────┐   ┌──────────────────────────┐    │
│  │  IndexedDB               │   │  oidc-client-ts          │    │
│  │  database: "oidc"        │   │  sessionStorage          │    │
│  │  store:    "dpop"        │   │                          │    │
│  │  key:      "react-dpop"  │   │  access_token            │    │
│  │  value: DPoPState {      │   │  refresh_token           │    │
│  │    keys: CryptoKeyPair   │   │  user profile            │    │
│  │    nonce?: string        │   │                          │    │
│  │  }                       │   │                          │    │
│  │  (privateKey             │   │                          │    │
│  │   non-extractable)       │   │                          │    │
│  └──────────────────────────┘   └──────────────────────────┘    │
│             │                              │                     │
│             │    single source of truth    │                     │
│             │    (library + interceptor    │                     │
│             │     dùng cùng 1 keypair)     │                     │
│             └─────────────┬────────────────┘                     │
│                           │                                      │
│                  axios interceptor                               │
│              (build + attach DPoP proof)                         │
│                           │                                      │
│              module-level dpopNonce variable                     │
│              (server-issued, cập nhật mỗi response)              │
└──────────────────────────────────────────────────────────────────┘
              │                           │
              ▼                           ▼
       IdentityServer               WebApi (API)
       :5001                        :5175
       (Authorization Code          (validate DPoP proof
        + PKCE + DPoP)               + cnf.jkt binding)
```

**Nguyên tắc cốt lõi:**

1. **Non-extractable private key** — Keypair sinh bởi `crypto.subtle.generateKey` với `extractable=false`. JS không thể đọc raw bytes của private key, chỉ gọi `sign()`. XSS cũng không export được.
2. **Single-source-of-truth keypair** — Một `IndexedDbDPoPStore` duy nhất (database `"oidc"`, store `"dpop"`, key = `client_id`). Library dùng nó để ký proof cho `/connect/token`; axios interceptor dùng nó để ký proof cho mọi API call. `cnf.jkt` khớp end-to-end.
3. **Algorithm ES256** — `oidc-client-ts` v3 hardcode `alg:"ES256"` và `namedCurve:"P-256"`. Code phải dùng cùng alg để signature verify thành công và `cnf.jkt` khớp.
4. **Token bị đánh cắp vô dụng** — Attacker không có private key tương ứng, không thể ký proof hợp lệ.

---

## 2. Cấu Trúc File và Vai Trò

```
src/
├── app/
│   ├── main.tsx                              # Bootstrap: await initFromSession() trước render
│   └── routes/
│       ├── modules/auth.routes.ts            # Route config /auth/*
│       └── utils/routeGuards.ts              # createRoleGuard, requireAdmin, requireAuth
├── features/
│   └── auth/
│       ├── pages/
│       │   ├── LoginPage.tsx                 # Redirect sang IdentityServer (không có form)
│       │   └── OidcCallbackPage.tsx          # /callback — exchange auth code → token
│       └── store/
│           └── authStore.ts                  # Zustand store + logout()
└── lib/
    ├── oidc/
    │   ├── dpop.ts                           # buildDPoPProof(), computeJwkThumbprint()
    │   └── userManager.ts                    # UserManager config + getDPoPKeyPair()
    └── api/
        └── axios.ts                          # Axios client với DPoP interceptor
```

| File | Vai trò cụ thể |
|------|---------------|
| `lib/oidc/dpop.ts` | Build và ký DPoP proof JWT theo RFC 9449. Tính JWK thumbprint theo RFC 7638. Không quản lý key. |
| `lib/oidc/userManager.ts` | Config `UserManager` với `IndexedDbDPoPStore`. Export `getDPoPKeyPair()`, `clearDPoPKeyPair()`, `signinRedirect()`, `signinCallback()`, `silentRenew()`, `signoutRedirect()`. |
| `lib/api/axios.ts` | Axios instance với request interceptor (gắn DPoP proof) và response interceptor (xử lý nonce challenge + silent renew). |
| `features/auth/store/authStore.ts` | Zustand store lưu user claims. `initFromSession()` restore session. `logout()` xóa keypair + `end_session`. |
| `features/auth/pages/LoginPage.tsx` | Redirect sang IdentityServer. Lưu `returnTo` vào sessionStorage. |
| `features/auth/pages/OidcCallbackPage.tsx` | Nhận `code` từ redirect, exchange lấy DPoP-bound token, lưu user, redirect về `returnTo`. |
| `app/main.tsx` | `await initFromSession()` trước `ReactDOM.render()` — bảo đảm route guards thấy session đúng khi hard reload. |
| `app/routes/utils/routeGuards.ts` | `createRoleGuard(['admin'])`, `requireAuth()` — đọc `useAuthStore.getState()` đồng bộ trong `beforeLoad`. |

---

## 3. Cấu Trúc DPoP Proof JWT

Mỗi API call cần một proof JWT mới, ký bằng private key.

```
─── HEADER ──────────────────────────────────────────────────────
{
  "typ": "dpop+jwt",       ← BẮT BUỘC, phân biệt với access token
  "alg": "ES256",          ← ECDSA P-256 / SHA-256
  "jwk": {                 ← Public key nhúng trực tiếp (không có "d")
    "kty": "EC",
    "crv": "P-256",
    "x": "BASE64URL...",
    "y": "BASE64URL..."
  }
}

─── PAYLOAD ─────────────────────────────────────────────────────
{
  "jti": "550e8400-...",   ← UUID mới mỗi request → chống replay
  "htm": "GET",            ← HTTP method (uppercase)
  "htu": "http://localhost:5175/api/products",  ← URL, không có query/fragment
  "iat": 1713456789,       ← Unix timestamp hiện tại
  "ath": "BASE64URL(SHA256(access_token))",     ← bind proof với token cụ thể
  "nonce": "xyz"           ← Chỉ có khi server đã gửi DPoP-Nonce
}

─── SIGNATURE ───────────────────────────────────────────────────
ECDSA-P256(privateKey, BASE64URL(header) + "." + BASE64URL(payload))
```

**Tại sao mỗi claim quan trọng:**

| Claim | Bảo vệ khỏi |
|-------|-------------|
| `jti` | Replay attack (WebApi cache jti trong IDistributedCache) |
| `htm` | Proof dùng sai HTTP method |
| `htu` | Proof dùng sai endpoint |
| `iat` | Proof cũ quá (clock skew ±30s) |
| `ath` | Proof ghép với token bị đánh cắp khác |
| `nonce` | Replay trong time window ngắn khi server bật nonce mode |

---

## 4. Luồng End-to-End

### 4.1 Lần Đầu Login

```
1. Browser mở app
   └─► main.tsx: await initFromSession()
       └─► getUser() → null (chưa có session)
       └─► authStore.isAuthenticated = false, isLoading = false

2. Router beforeLoad → requireAuth() → isAuthenticated = false
   └─► redirect('/auth/login?returnTo=...')

3. LoginPage mount
   └─► lưu returnTo vào sessionStorage('oidc_return_to')
   └─► signinRedirect()
       └─► oidc-client-ts gọi IndexedDbDPoPStore.get('react-dpop')
           ├─► IndexedDB rỗng → generate keypair mới
           │   crypto.subtle.generateKey({name:'ECDSA', namedCurve:'P-256'},
           │                              extractable=false, ['sign'])
           └─► tính dpop_jkt = SHA256(canonical JWK)
       └─► GET https://localhost:5001/connect/authorize
           ?client_id=react-dpop
           &response_type=code
           &code_challenge=...  (PKCE S256)
           &dpop_jkt=...        ← bind keypair vào auth session

4. User đăng nhập tại IdentityServer (Razor Pages)
   └─► IdentityServer redirect về /callback?code=...&state=...

5. OidcCallbackPage: signinCallback()
   └─► oidc-client-ts build DPoP proof (htm=POST, htu=/connect/token)
   └─► POST https://localhost:5001/connect/token
       Body: code + code_verifier + grant_type=authorization_code
       Header: DPoP: <proof_jwt>
   └─► IdentityServer validate proof + PKCE
   └─► Response: {
         access_token: "eyJ...",   ← typ=at+jwt, cnf.jkt=<keypair thumbprint>
         token_type: "DPoP",
         refresh_token: "...",
         expires_in: 3600
       }

6. authStore.setOidcUser(user)
   └─► lưu claims vào Zustand (sub, username, role, ...)

7. window.location.replace(returnTo)
```

### 4.2 API Request Thông Thường

```
Component gọi axiosClient.get('/api/products')
    │
    ▼
[Request Interceptor] (axios.ts:106-138)
    │
    ├─► buildFullUrl(config) → "http://localhost:5175/api/products"
    ├─► new URL(url).origin === API_ORIGIN? → YES (same-origin check)
    ├─► getUser() → oidcUser.access_token = "eyJ..."
    ├─► getDPoPKeyPair() → CryptoKeyPair từ IndexedDB
    ├─► buildDPoPProof(keyPair, {
    │     htm: "GET",
    │     htu: "http://localhost:5175/api/products",  ← stripped query/fragment
    │     accessToken: "eyJ...",                       ← ath = SHA256(token)
    │     nonce: dpopNonce                             ← undefined nếu chưa có
    │   })
    └─► proof JWT = header.payload.signature (ES256)
    └─► config.headers.set('Authorization', 'DPoP eyJ...')
    └─► config.headers.set('DPoP', proof)
    │
    ▼
WebApi nhận request
    ├─► Validate JWT (issuer, signature, typ=at+jwt)
    ├─► Validate DPoP proof (signature, typ, alg, htm, htu, iat, jti)
    ├─► Verify ath = SHA256(access_token)
    ├─► Verify proof.jwk.thumbprint == token.cnf.jkt  ← KEY BINDING
    └─► 200 OK + DPoP-Nonce: "abc123"
    │
    ▼
[Response Interceptor] (axios.ts:144-148)
    ├─► lưu dpopNonce = "abc123"  ← request tiếp theo dùng nonce này
    └─► return response.data
```

### 4.3 Nonce Challenge (use_dpop_nonce)

Xảy ra khi server yêu cầu nonce nhưng proof không có (thường lần đầu sau server restart).

```
Request đi → Server trả 401:
    WWW-Authenticate: DPoP error="use_dpop_nonce",
                      error_description="..."
    DPoP-Nonce: "fresh_nonce_xyz"
    │
    ▼
[Response Interceptor] (axios.ts:157-169)
    ├─► status === 401 ✓
    ├─► wwwAuth.includes('use_dpop_nonce') ✓
    ├─► serverNonce = "fresh_nonce_xyz" ✓
    ├─► !original._nonceRetry ✓  ← chống retry vô hạn
    │
    ├─► dpopNonce = "fresh_nonce_xyz"
    ├─► original._nonceRetry = true
    └─► return axiosClient(original)  ← retry
        └─► [Request Interceptor] build proof lại:
            payload: { ..., nonce: "fresh_nonce_xyz" }
            → Server chấp nhận → 200 OK ✓
```

### 4.4 Token Hết Hạn (Silent Renew)

```
Request đi → Server trả 401 (token expired, không phải nonce)
    │
    ▼
[Response Interceptor] (axios.ts:173-201)
    ├─► status === 401 ✓, !original._retry ✓
    │
    ├─► isRefreshing = false?
    │   ├─► YES: set isRefreshing = true
    │   │        silentRenew() → signinSilent()
    │   │        └─► oidc-client-ts build proof (htm=POST, htu=/connect/token)
    │   │            POST /connect/token (grant_type=refresh_token)
    │   │            ← new DPoP-bound access_token (CÙNG keypair → cnf.jkt giữ nguyên)
    │   │        authStore.setOidcUser(renewed)
    │   │        drainQueue(null, renewed.access_token)
    │   │        return axiosClient(original)
    │   │
    │   └─► NO (đang refresh): push vào pendingQueue
    │       └─► đợi drainQueue() gọi resolve(newToken)
    │           └─► retry với token mới
    │
    └─► Nếu silentRenew() throw:
        drainQueue(err)
        authStore.clearAuth()
        window.location.href = '/auth/login'
```

### 4.5 Logout

```
user click "Đăng xuất"
    └─► logout() (authStore.ts:108-116)
        ├─► authStore.clearAuth()     ← in-memory state rỗng ngay lập tức
        │                               (route guards thấy unauthenticated)
        ├─► clearDPoPKeyPair()        ← xóa keypair khỏi IndexedDB
        │                               (next session sinh key mới)
        └─► signoutRedirect()         ← POST /connect/endsession
                                        IdentityServer revoke session + cookie
```

---

## 5. Security Model

### 5.1 Tại Sao Token Bị Đánh Cắp Vô Dụng

```
Attacker đánh cắp được: access_token = "eyJ..."

Thử 1: Gửi plain Bearer
   Authorization: Bearer eyJ...
   → WebApi (prod): AllowBearerTokens=false → 401

Thử 2: Giả mạo proof với keypair khác
   Attacker generate keypair_attacker { pub_a, priv_a }
   Build proof ký bằng priv_a, header.jwk = pub_a
   → WebApi:
       stolen_token.cnf.jkt = thumbprint(pub_victim)
       proof.jwk.thumbprint  = thumbprint(pub_a)
       → MISMATCH → 401

Thử 3: Replay proof cũ
   Gửi lại proof đã dùng thành công trước đó
   → WebApi: jti đã có trong IDistributedCache → 401

Kết luận:
   Cần đồng thời:  ✓ access_token  (đánh cắp được)
                   ✗ private key   (non-extractable CryptoKey — không thể)
```

### 5.2 Same-Origin Token Scoping

```typescript
// axios.ts:111-117
const targetOrigin = new URL(url).origin;
if (targetOrigin !== API_ORIGIN) return config;  // ← không gắn token
```

Nếu `axiosClient` được dùng để gọi third-party URL (CDN, external API), token và proof sẽ **không** được đính kèm. Tránh credential leak qua SSRF hoặc misconfiguration.

### 5.3 Open Redirect Prevention

`LoginPage` và `OidcCallbackPage` đều validate `returnTo` trước khi redirect:

```typescript
function isAllowedReturnTo(returnTo: string | null): boolean {
  if (!returnTo) return false;
  if (returnTo.startsWith('//')) return false;      // protocol-relative
  if (returnTo.startsWith('/')) return true;         // relative path OK
  try {
    const url = new URL(returnTo);
    return url.origin === window.location.origin;   // same-origin absolute OK
  } catch { return false; }
}
```

### 5.4 Environment Variables — Fail Fast in Production

```typescript
// userManager.ts:30-38
function requireEnv(name: string, devFallback: string): string {
  const value = import.meta.env[name] ?? '';
  if (value) return value;
  if (import.meta.env.PROD) throw new Error(`Missing ${name}`);
  return devFallback;
}
```

Nếu thiếu `VITE_IDENTITY_SERVER_AUTHORITY` hay `VITE_OIDC_CLIENT_ID` trong production build, app throw ngay khi load — không silent fail.

### 5.5 StrictMode Guard

```typescript
// OidcCallbackPage.tsx
const handledRef = useRef(false);
useEffect(() => {
  if (handledRef.current) return;
  handledRef.current = true;
  // exchange code...
}, []);
```

React StrictMode mount component 2 lần trong dev. `handledRef` bảo đảm `signinCallback()` chỉ gọi 1 lần — gọi 2 lần sẽ fail vì auth code đã consumed.

---

## 6. Môi Trường và Cấu Hình

### Environment Variables

| Variable | Dev | Production |
|----------|-----|-----------|
| `VITE_API_BASE_URL` | `http://localhost:5175` | URL production |
| `VITE_IDENTITY_SERVER_AUTHORITY` | `https://localhost:5001` | URL IdentityServer prod |
| `VITE_OIDC_CLIENT_ID` | `react-dpop` | `react-dpop` |
| `VITE_OIDC_REDIRECT_URI` | `http://localhost:5173/callback` | `https://your-domain/callback` |
| `VITE_OIDC_POST_LOGOUT_URI` | `http://localhost:5173` | `https://your-domain` |

### UserManager Settings

```typescript
// userManager.ts:54-70
{
  authority: AUTHORITY,
  client_id: 'react-dpop',
  redirect_uri: REDIRECT_URI,
  response_type: 'code',
  scope: 'openid profile retail-api offline_access',
  automaticSilentRenew: true,
  dpop: {
    bind_authorization_code: true,  // ← bind dpop_jkt vào /authorize
    store: dpopStore,               // ← IndexedDbDPoPStore singleton
  }
}
```

`bind_authorization_code: true` bảo đảm `dpop_jkt` được gửi trong `/connect/authorize`, IdentityServer check xem keypair có nhất quán từ đầu hay không.

---

## 7. Điểm Đáng Chú Ý Khi Maintain

### 7.1 Keypair là Single Source of Truth

**Không được** tạo thêm `IndexedDbDPoPStore` instance khác hay tự implement `DPoPStore` với database/store name khác. Sẽ phá `cnf.jkt` binding vì library và interceptor dùng keypair khác nhau → 401 liên tục.

### 7.2 Bootstrap Ordering

```typescript
// main.tsx
async function bootstrap() {
  await useAuthStore.getState().initFromSession();  // ← PHẢI await trước render
  ReactDOM.createRoot(root).render(...);
}
```

Nếu bỏ `await`, route guards (`beforeLoad`) fire trước khi session restore xong → hard reload vào trang protected đá user về login sai.

### 7.3 Nonce là Module-Level State

```typescript
// axios.ts:93
let dpopNonce: string | undefined;
```

Nonce được lưu trong module scope (không phải per-request). Server có thể gửi `DPoP-Nonce` mới trong bất kỳ response nào (200 hay 401), interceptor luôn cập nhật. Điều này đúng theo RFC 9449 — nonce là "hint" để dùng cho request tiếp theo.

### 7.4 `_nonceRetry` vs `_retry` Flags

Hai flag riêng biệt, xử lý hai trường hợp khác nhau:

| Flag | Khi nào set | Mục đích |
|------|------------|---------|
| `_nonceRetry` | Bắt `401 use_dpop_nonce` | Chống retry nonce vô hạn |
| `_retry` | Bắt `401` thông thường | Chống retry silent renew vô hạn |

Quan trọng: nonce retry **không** set `_retry`. Nếu sau khi retry nonce mà vẫn 401 (do token thực sự expired), sẽ vào nhánh silent renew bình thường.

### 7.5 pendingQueue cho Concurrent 401

Khi nhiều request cùng lúc nhận 401 (token expired):

- Request đầu tiên: set `isRefreshing = true`, gọi `silentRenew()`
- Các request sau: push vào `pendingQueue`, đợi
- Khi renew xong: `drainQueue()` resolve tất cả với token mới

Tránh gọi `signinSilent()` nhiều lần đồng thời (race condition + server revoke refresh token sau lần dùng đầu).

---

## 8. Quan Hệ với Backend

| Frontend | Backend |
|---------|---------|
| `VITE_OIDC_CLIENT_ID=react-dpop` | `Config.cs`: client `react-dpop` với `RequireDPoP=true` |
| `scope: 'retail-api'` | `WebApi/Program.cs`: policy `RetailApi` yêu cầu scope `retail-api` |
| `Authorization: DPoP <token>` | `AddAuthentication("dpoptokenscheme")` |
| `DPoP: <proof>` | Duende DPoP middleware validate proof + cnf.jkt |
| `dpop-nonce` response header | WebApi gửi nonce để tăng replay protection |
| silent renew dùng refresh_token | IdentityServer: `AllowOfflineAccess=true`, scope `offline_access` |
