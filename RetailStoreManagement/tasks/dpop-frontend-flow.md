# DPoP Frontend Flow — Tài liệu kỹ thuật chi tiết

> **Phiên bản:** 1.1 — Phase 3 + 2 vòng code review applied  
> **Ngày:** 2026-04-18  
> **Liên quan:** RFC 9449 (DPoP), RFC 7636 (PKCE), RFC 9068 (JWT Profile), RFC 7638 (JWK Thumbprint)

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
13. [Ghi chú kỹ thuật bổ sung](#13-ghi-chú-kỹ-thuật-bổ-sung)

---

## 1. Kiến trúc tổng quan

```
┌─────────────────────────────────────────────────────────────┐
│                         Browser                             │
│                                                             │
│  ┌─────────────────────────┐   ┌─────────────────┐          │
│  │  IndexedDB              │   │ oidc-client-ts  │          │
│  │  (database "oidc",      │   │  sessionStorage │          │
│  │   store "dpop")         │   │                 │          │
│  │                         │   │ access_token    │          │
│  │  key = <client_id>      │   │ refresh_token   │          │
│  │  value = DPoPState {    │   │ user profile    │          │
│  │    keys: CryptoKeyPair, │   │                 │          │
│  │    nonce?: string       │   │                 │          │
│  │  }                      │   │                 │          │
│  │  (privateKey            │   │                 │          │
│  │   non-extractable)      │   │                 │          │
│  └─────────────────────────┘   └─────────────────┘          │
│          │                              │                   │
│          │   single source of truth     │                   │
│          │   (shared by library +       │                   │
│          │    our axios interceptor)    │                   │
│          │                              │                   │
│          └──────────┬───────────────────┘                   │
│                     │                                       │
│             axios interceptor                               │
│          (build + attach DPoP proof)                        │
│                     │                                       │
│         + Memory-local `dpopNonce`                          │
│           (server-issued, cập nhật mỗi response)            │
└─────────────────────────────────────────────────────────────┘
         │                          │
         ▼                          ▼
  IdentityServer               WebApi (API)
  :5001                        :5175
  (login, token,               (validate DPoP proof
   refresh, logout)             + cnf.jkt binding)
```

**Nguyên tắc cốt lõi:**

1. **Non-extractable private key.** Keypair được `oidc-client-ts` tạo bằng `crypto.subtle.generateKey({name:"ECDSA", namedCurve:"P-256"}, extractable=false, ...)` — không thể export, không thể copy ra khỏi browser.
2. **Single-source-of-truth store.** Chỉ có một `IndexedDbDPoPStore` duy nhất (database `oidc`, object store `dpop`). Cả library (ký proof cho `/connect/token`) và axios interceptor (ký proof cho API calls) đều đọc cùng một keypair qua key = `client_id`. Do đó `cnf.jkt` luôn khớp end-to-end.
3. **Algorithm: ES256.** `oidc-client-ts` v3 hardcode `alg:"ES256"` và `namedCurve:"P-256"`. Code ta dùng cùng alg để signature verify thành công ở cả token endpoint và resource server. P-256 vẫn đạt mức bảo mật ≥128-bit theo NIST.
4. **Token bị đánh cắp vô dụng** vì attacker không có private key tương ứng để ký DPoP proof hợp lệ.

---

## 2. Các file chính và vai trò

| File | Vai trò |
|------|---------|
| `src/lib/oidc/dpop.ts` | Build + ký DPoP proof JWT theo RFC 9449 (ES256/P-256). Tính JWK thumbprint theo RFC 7638. Không quản lý key — key thuộc về library. |
| `src/lib/oidc/userManager.ts` | `oidc-client-ts` `UserManager` config + DPoP store singleton (`IndexedDbDPoPStore`). Export `getDPoPKeyPair()`, `clearDPoPKeyPair()`, `signinRedirect()`, `signinCallback()`, `silentRenew()`, `signoutRedirect()`. Fail-fast nếu thiếu env vars trong PROD. |
| `src/lib/api/axios.ts` | Axios interceptor: gắn `Authorization: DPoP <token>` + `DPoP: <proof>` chỉ cho requests đến `API_ORIGIN` (same-origin scoping); retry 1 lần khi gặp `use_dpop_nonce`; silent renew khi 401. |
| `src/features/auth/store/authStore.ts` | Zustand store lưu OIDC user (role string từ claims); `initFromSession()`, `clearAuth()` (sync); export `logout()` xoay key + redirect end_session. Individual selectors: `useUser`, `useIsAuthenticated`, `useIsAuthLoading`. |
| `src/features/auth/pages/LoginPage.tsx` | Redirect đến IdentityServer (`signinRedirect()`). Không có form. |
| `src/features/auth/pages/OidcCallbackPage.tsx` | Route `/callback` — gọi `signinCallback()`, lưu user, redirect về `returnTo`. |
| `src/app/main.tsx` | `await initFromSession()` TRƯỚC khi `ReactDOM.render()` để route guards thấy session đã restored trên hard reload. |
| `src/app/routes/routeTree.ts` | Mount `/callback` route ở root level (ngoài layouts). |

---

## 3. Case 1 — Lần đầu mở app, chưa có session

```
Browser mở lần đầu
        │
        ▼
main.tsx: bootstrap()
        │
        ├─► await useAuthStore.getState().initFromSession()
        │       │
        │       ├─► getUser() từ oidc-client-ts → null (chưa có gì)
        │       │
        │       └─► finally: isLoading = false
        │
        ├─► authStore: { user: null, isAuthenticated: false, isLoading: false }
        │
        └─► ReactDOM.createRoot().render(...)
                │
                ▼
        Route guard thấy !isAuthenticated → redirect /auth/login
                │
                ▼
        LoginPage mount → useEffect chạy
                │
                └─► signinRedirect() → mgr.signinRedirect()
                        │
                        ├─► [LIBRARY LAZY-GEN] generateDPoPJkt(dpopSettings)
                        │       │
                        │       ├─► dpopStore.get(client_id) → null (lần đầu)
                        │       │
                        │       ├─► CryptoUtils.generateDPoPKeys()
                        │       │       │
                        │       │       └─► crypto.subtle.generateKey(
                        │       │               { name: "ECDSA", namedCurve: "P-256" },
                        │       │               extractable = FALSE   ← KHÔNG THỂ EXPORT
                        │       │               ["sign", "verify"]
                        │       │           )
                        │       │           → CryptoKeyPair { publicKey, privateKey }
                        │       │
                        │       ├─► dpopStore.set(client_id, new DPoPState(keyPair))
                        │       │       → Lưu vào IndexedDB "oidc"/"dpop"
                        │       │
                        │       └─► return CryptoUtils.generateDPoPJkt(keyPair)
                        │
                        ├─► Tạo code_verifier (PKCE random string)
                        ├─► code_challenge = BASE64URL(SHA256(code_verifier))
                        ├─► dpop_jkt = thumbprint của public key  ← bind code với key
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

> **`dpop_jkt`** là JWK thumbprint (SHA-256) của public key. IdentityServer lưu nó vào authorization code — khi frontend exchange code lấy token, phải chứng minh sở hữu cùng private key đó.
>
> **Khác với phiên bản trước:** `oidc-client-ts` v3 tự lazy-generate keypair khi ta gọi `signinRedirect()` lần đầu (do `bind_authorization_code: true`). Trước đây code app tự generate trong `LoginPage.useEffect` rồi seed vào store — giờ giao hết cho library, tránh dual-store divergence.

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
                ├─► [4] [LIBRARY] Build DPoP proof JWT cho token endpoint:
                │           header: {
                │               typ: "dpop+jwt",
                │               alg: "ES256",
                │               jwk: { kty, crv:"P-256", x, y }   ← public key only
                │           }
                │           payload: {
                │               jti: crypto.randomUUID(),
                │               htm: "POST",
                │               htu: "https://localhost:5001/connect/token",
                │               iat: 1713456789
                │               // KHÔNG có ath — chưa có access_token
                │           }
                │           signature: ECDSA-P256(privateKey, header.payload)
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
        ├─► buildFullUrl(config) → "http://localhost:5175/api/admin/products"
        │
        ├─► [SAME-ORIGIN GUARD] new URL(url).origin === API_ORIGIN ?
        │       │
        │       ├─► Khớp → tiếp tục gắn DPoP
        │       └─► Không khớp → return config (gửi unauthenticated)
        │           Bảo vệ chống token leak nếu lỡ gọi axiosClient
        │           với baseURL trỏ third-party host.
        │
        ├─► getUser() từ oidc-client-ts → { access_token: "eyJ...", ... }
        │       │
        │       └─► Nếu null (chưa login) → return config (no auth)
        │
        ├─► getDPoPKeyPair() → đọc dpopStore.get(CLIENT_ID)
        │       → Cùng keypair library đã dùng cho /connect/token (single source)
        │       → Nếu null (chưa login) → return config
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
        │       └─► Sign ECDSA-P256:
        │               header: { typ:"dpop+jwt", alg:"ES256", jwk:{publicKey} }
        │               payload: {
        │                   jti:  crypto.randomUUID(),  ← replay detection
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
        ├─► Verify alg ∈ accepted set (default chấp nhận ES256/ES384/PS256...)
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
User click Logout → logout() (export từ authStore.ts)
        │
        ├─► [1] useAuthStore.getState().clearAuth()
        │           → { user: null, isAuthenticated: false, isLoading: false }
        │           → Mọi route guard redirect về /auth/login ngay lập tức
        │           (Lưu ý: clearAuth là hàm ĐỒNG BỘ, không async)
        │
        ├─► [2] await clearDPoPKeyPair()
        │           → dpopStore.remove(CLIENT_ID)
        │           → Xóa DPoPState (cả keys + nonce) khỏi IndexedDB
        │
        │       Tại sao phải rotate key khi logout?
        │       → Session tiếp theo, library lazy-generate keypair MỚI
        │       → Defense in depth: kể cả attacker giữ được token + proof cũ,
        │         IdentityServer đã revoke session, keypair mới ≠ cũ
        │
        └─► [3] await signoutRedirect() → mgr.signoutRedirect()
                    │
                    └─► Redirect đến IdentityServer:
                        GET https://localhost:5001/connect/endsession
                            ?id_token_hint=<id_token>
                            &post_logout_redirect_uri=http://localhost:5173

                        IdentityServer:
                            ├─► Revoke session
                            ├─► Optionally revoke refresh_token
                            └─► Redirect về http://localhost:5173

Trường hợp user đóng tab giữa chừng (sau bước 2, trước khi end_session hoàn tất):
        → Refresh token còn trong sessionStorage nhưng KHÔNG dùng được
          (cnf.jkt mismatch khi gọi /connect/token vì keypair đã bị xóa)
        → Library sẽ tự cleanup expired user trong getUser() lần kế tiếp
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
    "alg": "ES256",             ← ECDSA P-256 / SHA-256
    "jwk": {                    ← PUBLIC key nhúng trực tiếp
        "kty": "EC",
        "crv": "P-256",
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
ECDSA-P256(
    privateKey,
    BASE64URL(header) + "." + BASE64URL(payload)
)
```

> **Tại sao ES256/P-256 thay vì ES384/P-384?**
> `oidc-client-ts` v3.5.0 hardcode `alg:"ES256"` và `namedCurve:"P-256"` trong `CryptoUtils.generateDPoPProof` và `generateDPoPKeys`. Nếu code app ký proof bằng alg khác, server sẽ vẫn verify được (DPoP cho phép multi-alg) NHƯNG keypair sẽ khác → cnf.jkt mismatch → 401 ngay. Đồng nhất alg là cách duy nhất giữ single-source-of-truth keypair.
> P-256 đạt mức bảo mật ~128-bit, vẫn meets NIST post-2030 requirement và được khuyến nghị cho DPoP.

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

**Fail-fast trong PROD:**  
Cả `VITE_API_BASE_URL` (trong `axios.ts`) và tất cả `VITE_OIDC_*` (trong `userManager.ts`) đều được đọc qua helper `requireEnv()` tương đương. Nếu thiếu trong `import.meta.env.PROD` build, app THROW ngay khi module load — không có fallback localhost âm thầm đi vào production.

---

## 13. Ghi chú kỹ thuật bổ sung

### 13.1. Dual role schema

Hệ thống hiện có **hai source of truth khác nhau** cho role của user — dễ nhầm lẫn:

| Source | Kiểu | Giá trị | Dùng ở đâu |
|--------|------|---------|------------|
| IdentityServer JWT claims (`role` claim) | **string** | `"Admin"` \| `"Staff"` | `authStore.user.role`, route guards (`isAdmin()`, `isStaff()`), UI hiển thị role của user đang login |
| Admin user-management API (`/api/admin/users/*`) | **number** | `0` (Admin) \| `1` (Staff) | `UserEntity.role` — lọc danh sách users, hiển thị bảng quản lý user |

**Không được cross-compare** giữa hai schema. Quy tắc:
- Permission của user đang login → `useAuthStore().user.role === 'Admin'`
- Filter danh sách trong user-management → `user.role === API_CONFIG.USER_ROLES.ADMIN` (số)

### 13.2. Single-store invariant

Keypair được quản lý bởi **đúng một** `IndexedDbDPoPStore` (singleton export từ `userManager.ts`):
- Library dùng store này khi ký proof cho `/connect/token` (lúc code exchange + silent renew)
- Axios interceptor dùng store này khi ký proof cho API calls
- → Cùng một keypair, nên `proof.jwk.thumbprint == token.cnf.jkt` luôn đúng

Nếu trong tương lai ai đó tạo thêm một `IndexedDbDPoPStore` khác hoặc tự implement `DPoPStore`, phải đảm bảo cùng database/store name (`"oidc"` / `"dpop"`) và cùng key (`client_id`).

### 13.3. Same-origin token attachment

Axios interceptor chỉ gắn `Authorization: DPoP <token>` và `DPoP: <proof>` khi **resolved origin khớp `API_ORIGIN`** (parse từ `VITE_API_BASE_URL`). Nếu lỡ gọi `axiosClient.get('https://third-party.example/...')`, request sẽ đi ra KHÔNG kèm token — tránh rò rỉ credential qua third-party host.

Nếu cần authenticated call tới origin khác, tạo axios instance riêng, không override `baseURL` per-request trên `axiosClient`.

### 13.4. Bootstrap ordering

```
main.tsx bootstrap():
   1. await useAuthStore.initFromSession()   ← restore user từ oidc-client-ts
   2. ReactDOM.createRoot().render(...)
```

Phải await bước 1 trước khi render. Nếu fire-and-forget, route guards (`isAuthenticated` đọc đồng bộ) sẽ fire trước khi session restore xong → hard-reload trang protected đá user về `/auth/login` sai.
