/**
 * Axios client — DPoP-aware HTTP client
 *
 * Request flow (per RFC 9449):
 *   1. Resolve target URL. Skip DPoP entirely if it doesn't point to the
 *      configured API origin (defense against accidentally leaking the
 *      access token + a bound proof to a third-party host).
 *   2. Retrieve the current access token + DPoP keypair (both managed by
 *      oidc-client-ts in IndexedDB / sessionStorage).
 *   3. Build a DPoP proof JWT and attach:
 *        Authorization: DPoP <access_token>
 *        DPoP: <proof_jwt>
 *
 * 401 with use_dpop_nonce:
 *   Server replies WWW-Authenticate: DPoP error="use_dpop_nonce"
 *                  DPoP-Nonce: <server_nonce>
 *   The interceptor retries exactly once with the nonce in the proof.
 *
 * Token refresh:
 *   On a plain 401 (no nonce challenge), call silentRenew() which exchanges
 *   the refresh token for a new DPoP-bound access token (same keypair).
 *   Then retry the original request.
 */

import axios from 'axios';
import type {
  AxiosError,
  AxiosInstance,
  AxiosResponse,
  InternalAxiosRequestConfig,
} from 'axios';
import type { ApiResponse } from './types/api.types';
import { useAuthStore } from '../../features/auth/store/authStore';
import { getUser, silentRenew, getDPoPKeyPair } from '../oidc/userManager';
import { buildDPoPProof } from '../oidc/dpop';
import { ENDPOINTS } from '../../app/routes/type/routes.endpoint';

// Keep tokenUtils shim so legacy callers don't break at import time.
export const tokenUtils = {
  getToken: (): string | null => null,
  getRefreshToken: (): string | null => null,
  setTokens: (): void => { /* tokens live in oidc-client-ts session storage */ },
  clearAllTokens: (): void => { /* no-op */ },
};

// ─────────────────────────────────────────────────────────────────────────────
// Config
// ─────────────────────────────────────────────────────────────────────────────

const API_BASE_URL = (() => {
  const value = import.meta.env.VITE_API_BASE_URL as string | undefined;
  if (value) return value;
  if (import.meta.env.PROD) {
    throw new Error(
      '[axios] Missing required environment variable VITE_API_BASE_URL in production build.',
    );
  }
  return 'http://localhost:5175';
})();

/**
 * Origin that should receive Authorization + DPoP headers. Anything else
 * (third-party APIs, CDN URLs accidentally fed to axiosClient) is sent
 * unauthenticated.
 */
const API_ORIGIN = new URL(API_BASE_URL).origin;

const axiosClient: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 10000,
  headers: { 'Content-Type': 'application/json' },
  // DPoP uses Authorization header — no cookies needed.
  withCredentials: false,
});
// WARNING: requests whose resolved origin does NOT match API_ORIGIN are
// sent WITHOUT Authorization / DPoP headers (see request interceptor below).
// This is deliberate — it prevents the access token from leaking to a
// third-party host if `baseURL` is accidentally overridden per-request.
// If you legitimately need an authenticated call to a different origin,
// build a separate axios instance for that host.

// ─────────────────────────────────────────────────────────────────────────────
// State
// ─────────────────────────────────────────────────────────────────────────────

let isRefreshing = false;
let pendingQueue: Array<{
  resolve: (token: string) => void;
  reject: (err: unknown) => void;
}> = [];

/** Server-issued DPoP nonce (from DPoP-Nonce response header). */
let dpopNonce: string | undefined;

function drainQueue(err: unknown, token?: string) {
  pendingQueue.forEach(({ resolve, reject }) =>
    err ? reject(err) : resolve(token!),
  );
  pendingQueue = [];
}

// ─────────────────────────────────────────────────────────────────────────────
// Request interceptor — attach DPoP proof + Authorization for API origin only
// ─────────────────────────────────────────────────────────────────────────────

axiosClient.interceptors.request.use(
  async (config: InternalAxiosRequestConfig) => {
    const url = buildFullUrl(config);

    // Same-origin scoping: never attach access tokens to non-API hosts.
    let targetOrigin: string;
    try {
      targetOrigin = new URL(url).origin;
    } catch {
      return config;
    }
    if (targetOrigin !== API_ORIGIN) return config;

    const oidcUser = await getUser();
    const accessToken = oidcUser?.access_token;
    if (!accessToken) return config;

    const keyPair = await getDPoPKeyPair();
    if (!keyPair) return config;

    const proof = await buildDPoPProof(keyPair, {
      htm: (config.method ?? 'GET').toUpperCase(),
      htu: url,
      accessToken,
      nonce: dpopNonce,
    });

    config.headers.set('Authorization', `DPoP ${accessToken}`);
    config.headers.set('DPoP', proof);
    return config;
  },
  (err) => Promise.reject(err),
);

// ─────────────────────────────────────────────────────────────────────────────
// Response interceptor
// ─────────────────────────────────────────────────────────────────────────────

axiosClient.interceptors.response.use(
  (response: AxiosResponse) => {
    const nonce = response.headers['dpop-nonce'];
    if (nonce) dpopNonce = nonce;
    return response.data as never;
  },
  async (error: AxiosError<ApiResponse>) => {
    const original = error.config as InternalAxiosRequestConfig & {
      _retry?: boolean;
      _nonceRetry?: boolean;
    };
    if (!original) return Promise.reject(error);

    // ── DPoP-Nonce challenge ─────────────────────────────────────────────────
    const wwwAuth = error.response?.headers['www-authenticate'] as string | undefined;
    const serverNonce = error.response?.headers['dpop-nonce'] as string | undefined;

    if (
      error.response?.status === 401 &&
      wwwAuth?.includes('use_dpop_nonce') &&
      serverNonce &&
      !original._nonceRetry
    ) {
      dpopNonce = serverNonce;
      original._nonceRetry = true;
      return axiosClient(original);
    }

    // ── Token expired (silent renew) ──────────────────────────────────────────
    if (error.response?.status === 401 && !original._retry) {
      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          pendingQueue.push({ resolve, reject });
        }).then((token) => {
          original._retry = true;
          original.headers.set('Authorization', `DPoP ${token}`);
          return axiosClient(original);
        });
      }

      original._retry = true;
      isRefreshing = true;

      try {
        const renewed = await silentRenew();
        if (!renewed) throw new Error('Silent renew returned null');

        useAuthStore.getState().setOidcUser(renewed);
        drainQueue(null, renewed.access_token);
        return axiosClient(original);
      } catch (renewErr) {
        drainQueue(renewErr);
        useAuthStore.getState().clearAuth();
        window.location.href = ENDPOINTS.AUTH.LOGIN;
        return Promise.reject(renewErr);
      } finally {
        isRefreshing = false;
      }
    }

    // ── Error normalisation ───────────────────────────────────────────────────
    if (error.response?.data) {
      const d = error.response.data as Record<string, unknown>;
      let message = 'Có lỗi xảy ra';
      if (d['isError'] === true && d['message']) {
        message = d['message'] as string;
      } else if (typeof d['message'] === 'string') {
        message = d['message'];
      } else if (typeof d['title'] === 'string') {
        message = d['detail'] ? `${d['title']}: ${d['detail']}` : (d['title'] as string);
      } else if (typeof d['detail'] === 'string') {
        message = d['detail'];
      }
      return Promise.reject({ ...error, message, data: d });
    }

    if (error.code === 'ECONNABORTED') {
      return Promise.reject({ ...error, message: 'Yêu cầu bị timeout. Vui lòng thử lại.' });
    }
    if (!error.response) {
      return Promise.reject({ ...error, message: 'Không thể kết nối đến server.' });
    }

    return Promise.reject(error);
  },
);

export default axiosClient;

// ─────────────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────────────

function buildFullUrl(config: InternalAxiosRequestConfig): string {
  const path = config.url ?? '';
  if (/^https?:\/\//i.test(path)) return path;
  const base = (config.baseURL ?? API_BASE_URL).replace(/\/$/, '');
  return `${base}${path.startsWith('/') ? '' : '/'}${path}`;
}
