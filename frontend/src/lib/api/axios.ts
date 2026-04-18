/**
 * Axios client — DPoP-aware HTTP client
 *
 * Request flow (per RFC 9449):
 *   1. Retrieve current access token from oidc-client-ts session storage.
 *   2. Build a DPoP proof JWT:
 *        header: { typ:"dpop+jwt", alg:"ES384", jwk:<public_key> }
 *        payload: { jti, htm, htu, iat, ath: BASE64URL(SHA-256(access_token)), nonce? }
 *   3. Attach:
 *        Authorization: DPoP <access_token>
 *        DPoP: <proof_jwt>
 *
 * 401 with DPoP-Nonce:
 *   Server replies with `DPoP-Nonce: <server_nonce>` when proof nonce is required.
 *   The interceptor retries exactly once with the nonce included in the proof.
 *
 * Token refresh:
 *   On 401 without nonce (token expired), calls silentRenew() which exchanges
 *   the refresh token for a new DPoP-bound access token via oidc-client-ts.
 *   Then retries the original request.
 */

import axios from 'axios';
import type {
  AxiosError,
  AxiosInstance,
  AxiosResponse,
  InternalAxiosRequestConfig,
} from 'axios';
import { API_CONFIG } from '../../config/api.config';
import type { ApiResponse } from './types/api.types';
import { useAuthStore } from '../../features/auth/store/authStore';
import { getUser, silentRenew } from '../oidc/userManager';
import { buildDPoPProof } from '../oidc/dpop';
import { getDPoPKeyPair } from '../oidc/dpopKey';
import { ENDPOINTS } from '../../app/routes/type/routes.endpoint';

// Keep tokenUtils shim so other files that import it don't break.
export const tokenUtils = {
  getToken: (): string | null => null,
  getRefreshToken: (): string | null => null,
  setTokens: (): void => { /* no-op — tokens live in oidc-client-ts session storage */ },
  clearAllTokens: (): void => { /* no-op */ },
};

// ─────────────────────────────────────────────────────────────────────────────
// Axios instance
// ─────────────────────────────────────────────────────────────────────────────

const axiosClient: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5175',
  timeout: 10000,
  headers: { 'Content-Type': 'application/json' },
  // DPoP uses Authorization header — no cookies needed for auth.
  withCredentials: false,
});

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
// Request interceptor — attach DPoP proof + Authorization
// ─────────────────────────────────────────────────────────────────────────────

axiosClient.interceptors.request.use(
  async (config: InternalAxiosRequestConfig) => {
    const oidcUser = await getUser();
    const accessToken = oidcUser?.access_token;

    if (!accessToken) return config;

    const keyPair = await getDPoPKeyPair();
    const url = buildFullUrl(config);
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
    // Capture server-issued nonce for future proofs
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

    // ── DPoP-Nonce challenge (server wants a nonce in the proof) ─────────────
    // Duende sends:  401 + WWW-Authenticate: DPoP error="use_dpop_nonce"
    //                DPoP-Nonce: <nonce>
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
      return axiosClient(original); // retry with nonce in proof
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
  const base = (config.baseURL ?? (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5175')).replace(/\/$/, '');
  const path = config.url ?? '';
  if (path.startsWith('http')) return path;
  return `${base}${path.startsWith('/') ? '' : '/'}${path}`;
}
