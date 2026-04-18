# DPoP Frontend Flow — Tài liệu kỹ thuật chi tiết

> **Phiên bản:** 1.0 — Phase 3 complete  
> **Ngày:** 2026-04-18  
> **Liên quan:** RFC 9449 (DPoP), RFC 7636 (PKCE), RFC 9068 (JWT Profile)

---

## Mục lục

1. [Kiến trúc tổng quan](#1-kiến-trúc-tổng-quan)
2. [Các file chính và vai trò](#2-các-file-chính-và-vai-trò)
3. [Case 1 — Lần đầu mở app, chưa có session](#3-case-1--lần-đầu-mở-app-chưa-có-session)
4. [Case 2 — IdentityServer login và OIDC callback](#4-case-2--identityserver-login-và-oidc-callback)
5. [Case 3 — Gọi API bình thường (token còn hạn)](#5-case-3--gọi-api-bình-thường-token-còn-hạn)
6. [Case 4 — Server yêu cầu nonce (use_dpop_nonce)](#6-case-4--server-yêu-cầu-nonce-use_dpop_nonce)
7. [Case 5 — Access token hết hạn (silent renew)](#7-case-5--access-token-hết-hạn-silent-renew)
8. [Case 6 — Refresh token hết hạn (force logout)](#8-case-6--refresh-token-hết-hạn-force-logout)
9. [Case 7 — Logout](#9-case-7--logout)
10. [Tại sao kẻ tấn công không dùng được token bị đánh cắp](#10-tại-sao-kẻ-tấn-công-không-dùng-được-token-bị-đánh-cắp)
11. [Cấu trúc DPoP Proof JWT](#11-cấu-trúc-dpop-proof-jwt)
12. [Các biến môi trường cần thiết](#12-các-biến-môi-trường-cần-thiết)

---

## 1. Kiến trúc tổng quan

```
┌─────────────────────────────────────────────────────────────┐
│                         Browser                             │
│                                                             │
│  ┌───────────────┐   ┌─────────────────┐   ┌────────────┐  │
│  │  IndexedDB    │   │ oidc-client-ts  │   │   Memory   │  │
│  │               │   │  sessionStorage │   │            │  │
│  │ CryptoKeyPair │   │ access_token    │   │ dpopNonce  │  │
│  │ (privateKey   │   │ refresh_token   │   │ (từ server)│  │
│  │  non-extract) │   │ user profile    │   │            │  │
│  └───────────────┘   └─────────────────┘   └────────────┘  │
│          │                   │                              │
│          └──────────┬────────┘                              │
│                     │                                       │
│              axios interceptor                              │
│          (build + attach DPoP proof)                        │
└─────────────────────────────────────────────────────────────┘
         │                          │
         ▼                          ▼
  IdentityServer               WebApi (API)
  :5001                        :5175
  (login, token,               (validate DPoP proof
   refresh, logout)             + cnf.jkt binding)
```

**Nguyên tắc cốt lõi:**  
Private key được tạo bằng `crypto.subtle.generateKey(..., extractable=false)` — không thể export, không thể copy ra ngoài browser. Token bị đánh cắp vô dụng vì attacker không có private key tương ứng.

---

## 2. Các file chính và vai trò

| File | Vai trò |
|------|---------|
| `src/lib/oidc/dpop.ts` | Tạo ECDSA P-384 keypair; build + ký DPoP proof JWT theo RFC 9449 |
| `src/lib/oidc/keyStorage.ts` | Lưu/đọc/xóa `CryptoKeyPair` trong IndexedDB (không serialize private key) |
| `src/lib/oidc/dpopKey.ts` | Singleton keypair: load từ IndexedDB, generate mới nếu chưa có |
| `src/lib/oidc/userManager.ts` | `oidc-client-ts` `UserManager` với DPoP built-in (`IndexedDbDPoPStore`) |
| `src/lib/api/axios.ts` | Axios interceptor: gắn `Authorization: DPoP` + `DPoP: <proof>` mỗi request |
| `src/features/auth/store/authStore.ts` | Zustand store lưu OIDC user; `initFromSession()`, `logout()` |
| `src/features/auth/pages/LoginPage.tsx` | Redirect đến IdentityServer thay vì hiển thị form |
| `src/features/auth/pages/OidcCallbackPage.tsx` | Route `/callback` — xử lý code exchange sau khi login |
| `src/app/main.tsx` | Gọi `initFromSession()` khi app khởi động |

---

## 3. Case 1 — Lần đầu mở app, chưa có session

```
Browser mở lần đầu
        │
        ▼
main.tsx: useAuthStore.getState().initFromSession()
        │
        ├─► getUser() từ oidc-client-ts → null (chưa có gì trong sessionStorage)
        │
        └─► authStore: { user: null, isAuthenticated: false, isLoading: false }
                │
                ▼
        Route guard thấy !isAuthenticated → redirect /auth/login
                │
                ▼
        LoginPage mount → useEffect chạy
                │
                ├─► [1] getDPoPKeyPair()
                │           │
                │           ├─► loadKeyPair() từ IndexedDB → null (lần đầu)
                │           │
                │           └─► generateDPoPKeyPair()
                │                   │
                │                   ├─► crypto.subtle.generateKey(
                │                   │       { name: "ECDSA", namedCurve: "P-384" },
                │                   │       extractable = FALSE   ← KHÔNG THỂ EXPORT
                │                   │       ["sign", "verify"]
                │                   │   )
                │                   │   → CryptoKeyPair { publicKey, privateKey }
                │                   │
                │                   └─► saveKeyPair(keyPair) → IndexedDB
                │
                ├─► [2] getUserManager()
                │           │
                │           ├─► new IndexedDbDPoPStore("dpop-oidc", "dpop-keys")
                │           ├─► dpopStore.set("react-dpop", new DPoPState(keyPair))
                │           └─► new UserManager({ authority, client_id, dpop: { store } })
                │
                └─► [3] mgr.signinRedirect()
                            │
                            ├─► Tạo code_verifier (PKCE random string)
                            ├─► code_challenge = BASE64URL(SHA256(code_verifier))
                            ├─► dpop_jkt = JWK thumbprint của publicKey  ← bind code với key
                            ├─► Lưu state + code_verifier vào sessionStorage
                            │
                            └─► Redirect browser đến:
                                GET https://localhost:5001/connect/authorize
                                    ?client_id=react-dpop
                                    &response_type=code
                                    &scope=openid profile retail-api offline_access
                                    &redirect_uri=http://localhost:5173/callback
                                    &code_challenge=<hash>
                                    &code_challenge_method=S256
                                    &dpop_jkt=<key_thumbprint>   ← RFC 9449 § 10
```

> **`dpop_jkt`** là thumbprint (SHA-256 hash) của public key. IdentityServer lưu nó vào authorization code — khi frontend exchange code lấy token, phải chứng minh sở hữu cùng private key đó.

---

## 4. Case 2 — IdentityServer login và OIDC callback

```
IdentityServer nhận /connect/authorize
        │
        ├─► Validate tham số, lưu dpop_jkt vào authorization code
        ├─► Hiển thị Razor Pages login form (/Account/Login)
        │
        └─► User nhập username/password → submit → xác thực credentials
                │
                └─► Redirect về frontend:
                    http://localhost:5173/callback
                        ?code=<authorization_code>
                        &state=<state>   ← dùng để chống CSRF

Browser load /callback → OidcCallbackPage mount
        │
        └─► signinCallback() → mgr.signinRedirectCallback()
                │
                ├─► [1] Đọc code + state từ URL query string
                ├─► [2] Validate state (phải khớp với state đã lưu → chống CSRF)
                ├─► [3] Lấy code_verifier từ sessionStorage (PKCE)
                │
                ├─► [4] Build DPoP proof JWT cho token endpoint:
                │           header: {
                │               typ: "dpop+jwt",
                │               alg: "ES384",
                │               jwk: { kty, crv, x, y }  ← chỉ public key
                │           }
                │           payload: {
                │               jti: "550e8400-...",       ← uuid duy nhất mỗi lần
                │               htm: "POST",
                │               htu: "https://localhost:5001/connect/token",
                │               iat: 1713456789
                │               // KHÔNG có ath — chưa có access_token
                │           }
                │           signature: ECDSA-P384(privateKey, header.payload)
                │
                └─► [5] POST https://localhost:5001/connect/token
                            Content-Type: application/x-www-form-urlencoded
                            DPoP: <proof_jwt>              ← header

                            grant_type=authorization_code
                            code=<authorization_code>
                            redirect_uri=http://localhost:5173/callback
                            client_id=react-dpop
                            code_verifier=<pkce_verifier>

                IdentityServer xử lý:
                        ├─► Verify DPoP proof signature (public key trong jwk header)
                        ├─► Verify proof.jwk.thumbprint == code.dpop_jkt  ← binding
                        ├─► Verify code_verifier (PKCE)
                        │
                        └─► Issue tokens:
                                access_token (JWT, 15 phút):
                                    {
                                        sub: "user_id",
                                        scope: "openid profile retail-api offline_access",
                                        cnf: { jkt: "<key_thumbprint>" },  ← BINDING CLAIM
                                        exp: now + 900,
                                        typ: "at+jwt"
                                    }
                                refresh_token: (opaque, 7 ngày sliding)
                                id_token: (user profile claims)

        OidcCallbackPage nhận User object từ oidc-client-ts
                │
                ├─► authStore.setOidcUser(user)
                │       → lưu { sub, username, fullName, role, accessToken }
                │
                ├─► Đọc sessionStorage['oidc_return_to'] (nếu có)
                │
                └─► window.location.replace(returnTo ?? '/')
```

---

## 5. Case 3 — Gọi API bình thường (token còn hạn)

```
Component: axiosClient.get('/api/admin/products')
        │
        ▼
[Request Interceptor]
        │
        ├─► getUser() từ oidc-client-ts → { access_token: "eyJ...", ... }
        ├─► getDPoPKeyPair() → CryptoKeyPair từ IndexedDB (singleton)
        │
        ├─► buildDPoPProof(keyPair, {
        │       htm: "GET",
        │       htu: "http://localhost:5175/api/admin/products",
        │       accessToken: "eyJ...",
        │       nonce: dpopNonce   ← undefined lần đầu; có giá trị sau lần đầu
        │   })
        │       │
        │       ├─► ath = BASE64URL(SHA256(ASCII(access_token)))
        │       │
        │       └─► Sign ECDSA-P384:
        │               header: { typ:"dpop+jwt", alg:"ES384", jwk:{publicKey} }
        │               payload: {
        │                   jti:  "uuid-mới-mỗi-request",  ← replay detection
        │                   htm:  "GET",
        │                   htu:  "http://localhost:5175/api/admin/products",
        │                   iat:  1713456789,
        │                   ath:  "BASE64URL(SHA256(access_token))",
        │                   nonce: "server_nonce"   ← nếu đã có từ response trước
        │               }
        │
        └─► Gắn headers:
                Authorization: DPoP eyJ...   ← DPoP-bound access token
                DPoP:          eyJ...         ← proof JWT

        ▼
[WebApi — Duende DPoP Middleware]
        │
        ├─► Verify proof signature  (public key lấy từ jwk trong proof header)
        ├─► Verify typ == "dpop+jwt"
        ├─► Verify alg == "ES384"  (hoặc các alg được chấp nhận)
        ├─► Verify htm == "GET"    (khớp HTTP method của request)
        ├─► Verify htu == request URL (không có query string)
        ├─► Verify |now - iat| ≤ 30s  (clock skew = ProofTokenIssuedAtClockSkew)
        ├─► Verify jti chưa từng thấy (IDistributedCache — replay detection)
        ├─► Verify ath == SHA256(access_token trong Authorization header)
        └─► Verify proof.jwk.thumbprint == token.cnf.jkt   ← KEY BINDING

        ▼
        200 OK + { data: [...] }
        + DPoP-Nonce: "abc123"   ← server gửi kèm nonce mới

        ▼
[Response Interceptor]
        │
        ├─► Lưu: dpopNonce = "abc123"  ← request tiếp theo sẽ dùng nonce này
        └─► return response.data
```

---

## 6. Case 4 — Server yêu cầu nonce (use_dpop_nonce)

Xảy ra khi server bật nonce enforcement và proof không kèm nonce (thường lần đầu sau khi server reset nonce).

```
Request gửi đi (proof không có nonce)
        │
        └─► Server trả:
                401 Unauthorized
                WWW-Authenticate: DPoP error="use_dpop_nonce",
                                  error_description="Resource server requires nonce in DPoP proof"
                DPoP-Nonce: "fresh_nonce_xyz"

        ▼
[Response Interceptor] bắt lỗi 401
        │
        ├─► Kiểm tra: status==401
        │             && wwwAuth.includes('use_dpop_nonce')
        │             && serverNonce != null
        │             && !original._nonceRetry     ← tránh retry vô hạn
        │
        ├─► dpopNonce = "fresh_nonce_xyz"   ← lưu nonce mới
        ├─► original._nonceRetry = true
        │
        └─► Retry: axiosClient(originalRequest)
                │
                └─► [Request Interceptor] build proof lại:
                        payload: {
                            ...,
                            nonce: "fresh_nonce_xyz"   ← có nonce lần này
                        }
                        → Server chấp nhận → 200 OK ✓

Các request tiếp theo đều dùng nonce này → không cần retry nữa.
```

---

## 7. Case 5 — Access token hết hạn (silent renew)

```
Access token hết hạn (exp = now - 1s)
        │
        ▼
axiosClient gửi request với token cũ
        │
        └─► Server trả: 401 Unauthorized
                (không có use_dpop_nonce — chỉ là token expired)

        ▼
[Response Interceptor]
        │
        ├─► status==401 && !_retry && không phải nonce challenge
        │
        ├─► isRefreshing = true
        ├─► original._retry = true
        │
        ├─► silentRenew() → mgr.signinSilent()
        │       │
        │       ├─► Lấy refresh_token từ oidc-client-ts sessionStorage
        │       │
        │       ├─► Build DPoP proof cho token endpoint:
        │       │       htm: "POST"
        │       │       htu: "https://localhost:5001/connect/token"
        │       │       // KHÔNG có ath — đang xin token mới
        │       │
        │       └─► POST /connect/token
        │               grant_type=refresh_token
        │               refresh_token=<old_refresh_token>
        │               client_id=react-dpop
        │               DPoP: <proof>
        │
        │   IdentityServer:
        │       ├─► Verify DPoP proof
        │       ├─► Validate refresh_token
        │       ├─► OneTimeOnly → invalidate refresh_token cũ, issue cái mới
        │       └─► Issue access_token mới:
        │               cnf.jkt = CÙNG key thumbprint  ← vẫn bound với keypair cũ
        │               exp = now + 900
        │
        ├─► authStore.setOidcUser(renewed)   ← cập nhật accessToken trong store
        │
        ├─► drainQueue(null, newToken)       ← unlock các request đang chờ
        │
        └─► Retry original request với token mới → 200 OK ✓

Xử lý concurrent requests (nhiều request cùng bị 401 đồng thời):
        │
        ├─► Request 1: isRefreshing = false → bắt đầu renew, set isRefreshing = true
        ├─► Request 2: isRefreshing == true → vào pendingQueue chờ
        ├─► Request 3: isRefreshing == true → vào pendingQueue chờ
        │
        └─► Renew xong:
                drainQueue(null, newToken)
                → Requests 2, 3 nhận token mới và retry đồng thời
```

---

## 8. Case 6 — Refresh token hết hạn (force logout)

```
silentRenew() thất bại
(refresh token expired / revoked / IdentityServer không phản hồi)
        │
        ├─► drainQueue(error)       ← reject tất cả requests đang chờ
        ├─► authStore.clearAuth()   ← { user: null, isAuthenticated: false }
        │
        └─► window.location.href = '/auth/login'
                │
                └─► LoginPage → signinRedirect()
                    → User phải đăng nhập lại từ đầu (Case 1)
```

---

## 9. Case 7 — Logout

```
User click Logout → logout() (trong authStore.ts)
        │
        ├─► [1] authStore.clearAuth()
        │           → { user: null, isAuthenticated: false }
        │           → Mọi route guard redirect về /auth/login ngay lập tức
        │
        ├─► [2] rotateDPoPKeyPair()
        │           ├─► _keyPair = null   (xóa khỏi memory)
        │           └─► clearKeyPair()    (xóa khỏi IndexedDB)
        │
        │       Tại sao phải rotate key khi logout?
        │       → Session tiếp theo dùng keypair MỚI hoàn toàn
        │       → Nếu attacker đã có token cũ + (giả sử) giữ được public key cũ,
        │         token đó cũng đã bị revoke ở IdentityServer
        │       → Defense in depth
        │
        └─► [3] signoutRedirect() → mgr.signoutRedirect()
                    │
                    └─► Redirect đến IdentityServer:
                        GET https://localhost:5001/connect/endsession
                            ?id_token_hint=<id_token>
                            &post_logout_redirect_uri=http://localhost:5173

                        IdentityServer:
                            ├─► Revoke session
                            ├─► Optionally revoke refresh_token
                            └─► Redirect về http://localhost:5173
```

---

## 10. Tại sao kẻ tấn công không dùng được token bị đánh cắp

```
Kịch bản: Attacker đánh cắp được access_token = "eyJ..."
(từ XSS, MITM, log leak, v.v.)

─────────────────────────────────────────────────────────────────
Thử 1: Gửi request không có DPoP proof
─────────────────────────────────────────────────────────────────

    Attacker gửi:
        Authorization: Bearer eyJ...   ← plain Bearer

    WebApi (Production): AllowBearerTokens = false
    → 401: DPoP proof required

─────────────────────────────────────────────────────────────────
Thử 2: Giả mạo DPoP proof với keypair khác
─────────────────────────────────────────────────────────────────

    Attacker tự generate keypair_attacker { pub_a, priv_a }
    Build proof:
        header: { jwk: pub_a }
        payload: { htm, htu, iat, ath: SHA256(stolen_token) }
        signature: ECDSA(priv_a, ...)

    Gửi:
        Authorization: DPoP eyJ...        ← stolen token
        DPoP:          eyJ...attacker...  ← proof ký bằng key attacker

    WebApi verify:
        stolen_token.cnf.jkt = thumbprint(pub_victim)
        proof.jwk.thumbprint  = thumbprint(pub_a)
        → thumbprint(pub_victim) ≠ thumbprint(pub_a)

    → 401: cnf.jkt mismatch — KEY BINDING FAILED

─────────────────────────────────────────────────────────────────
Thử 3: Replay proof cũ (đã từng dùng thành công)
─────────────────────────────────────────────────────────────────

    Attacker bắt được proof_old từ request trước đó
    Gửi lại proof_old cho request mới

    WebApi: jti của proof_old đã có trong IDistributedCache
    → 401: DPoP proof replay detected

─────────────────────────────────────────────────────────────────
Kết luận
─────────────────────────────────────────────────────────────────

    Để dùng token, attacker cần:
        ✓ access_token                  ← đánh cắp được
        ✗ private key tương ứng         ← KHÔNG THỂ: non-extractable CryptoKey
        ✗ build proof với đúng key đó   ← không có private key → không ký được

    Token DPoP-bound = USELESS nếu không có private key
```

---

## 11. Cấu trúc DPoP Proof JWT

```
─── HEADER ──────────────────────────────────────────────────────
{
    "typ": "dpop+jwt",          ← BẮT BUỘC: phân biệt với access token
    "alg": "ES384",             ← ECDSA P-384 / SHA-384
    "jwk": {                    ← PUBLIC key nhúng trực tiếp
        "kty": "EC",
        "crv": "P-384",
        "x":   "BASE64URL...",
        "y":   "BASE64URL..."
        // KHÔNG có "d" (private key) — chỉ public key
    }
}

─── PAYLOAD ─────────────────────────────────────────────────────
{
    "jti": "550e8400-e29b-41d4-a716-446655440000",  ← UUID mới mỗi request
    "htm": "GET",                                   ← HTTP method
    "htu": "http://localhost:5175/api/admin/products", ← URL không có query string
    "iat": 1713456789,                              ← issued at (Unix timestamp)
    "ath": "BASE64URL(SHA256(access_token))",       ← bind proof với token cụ thể
    "nonce": "server_issued_nonce"                  ← optional, khi server yêu cầu
}

─── SIGNATURE ───────────────────────────────────────────────────
ECDSA-P384(
    privateKey,
    BASE64URL(header) + "." + BASE64URL(payload)
)
```

**Tại sao mỗi claim quan trọng:**

| Claim | Mục đích | Bảo vệ khỏi |
|-------|----------|-------------|
| `jti` | UUID duy nhất mỗi request | Replay attack |
| `htm` | Khớp HTTP method | Proof dùng sai endpoint |
| `htu` | Khớp URL (không có query) | Proof dùng sai resource |
| `iat` | Timestamp ±30s | Proof quá cũ/mới |
| `ath` | Hash của access_token | Proof dùng với token khác |
| `nonce` | Server-issued | Replay trong window ngắn |

---

## 12. Các biến môi trường cần thiết

| Biến | Development | Production |
|------|-------------|------------|
| `VITE_API_BASE_URL` | `http://localhost:5175` | `https://api.example.com` |
| `VITE_IDENTITY_SERVER_AUTHORITY` | `https://localhost:5001` | `https://identity.example.com` |
| `VITE_OIDC_CLIENT_ID` | `react-dpop` | `react-dpop` |
| `VITE_OIDC_REDIRECT_URI` | `http://localhost:5173/callback` | `https://example.com/callback` |
| `VITE_OIDC_POST_LOGOUT_URI` | `http://localhost:5173` | `https://example.com` |

> Các giá trị `VITE_OIDC_REDIRECT_URI` và `VITE_OIDC_POST_LOGOUT_URI` phải khớp chính xác với `RedirectUris` và `PostLogoutRedirectUris` trong `IdentityServer/Config.cs`.
