/**
 * oidc-client-ts UserManager — OIDC Authorization Code + PKCE + DPoP
 *
 * oidc-client-ts v3 has built-in DPoP support:
 *   - `dpop.bind_authorization_code: true` binds the authorization code to the DPoP key
 *   - `dpop.store: IndexedDbDPoPStore` persists the keypair + nonce to IndexedDB
 *     using oidc-client-ts's own store (wraps our non-extractable CryptoKeyPair)
 *
 * For API requests we build DPoP proofs manually in the Axios interceptor
 * using the same keypair retrieved from IndexedDB.
 */

import {
  UserManager,
  type UserManagerSettings,
  type User,
  IndexedDbDPoPStore,
  DPoPState,
} from 'oidc-client-ts';
import { getDPoPKeyPair } from './dpopKey';

const AUTHORITY = import.meta.env.VITE_IDENTITY_SERVER_AUTHORITY ?? 'https://localhost:5001';
const CLIENT_ID = import.meta.env.VITE_OIDC_CLIENT_ID ?? 'react-dpop';
const REDIRECT_URI = import.meta.env.VITE_OIDC_REDIRECT_URI ?? 'http://localhost:5173/callback';
const POST_LOGOUT_REDIRECT_URI = import.meta.env.VITE_OIDC_POST_LOGOUT_URI ?? 'http://localhost:5173';

let _manager: UserManager | null = null;

export async function getUserManager(): Promise<UserManager> {
  if (_manager) return _manager;

  const keyPair = await getDPoPKeyPair();
  const dpopStore = new IndexedDbDPoPStore('dpop-oidc', 'dpop-keys');

  // Seed the store with our keypair so the library uses it for the
  // token endpoint call during the code exchange and silent renew.
  const dpopState = new DPoPState(keyPair);
  await dpopStore.set(CLIENT_ID, dpopState);

  const settings: UserManagerSettings = {
    authority: AUTHORITY,
    client_id: CLIENT_ID,
    redirect_uri: REDIRECT_URI,
    post_logout_redirect_uri: POST_LOGOUT_REDIRECT_URI,
    response_type: 'code',
    scope: 'openid profile retail-api offline_access',
    automaticSilentRenew: true,

    // DPoP: oidc-client-ts v3 native support
    dpop: {
      bind_authorization_code: true,
      store: dpopStore,
    },
  };

  _manager = new UserManager(settings);

  _manager.events.addSilentRenewError((err) => {
    console.error('[OIDC] Silent renew error:', err);
  });

  return _manager;
}

export async function getUser(): Promise<User | null> {
  const mgr = await getUserManager();
  return mgr.getUser();
}

export async function signinRedirect(): Promise<void> {
  const mgr = await getUserManager();
  await mgr.signinRedirect();
}

export async function signinCallback(): Promise<User> {
  const mgr = await getUserManager();
  return mgr.signinRedirectCallback();
}

export async function signoutRedirect(): Promise<void> {
  const mgr = await getUserManager();
  await mgr.signoutRedirect();
}

/** Exchange refresh token for a new access token (DPoP-bound). */
export async function silentRenew(): Promise<User | null> {
  const mgr = await getUserManager();
  return mgr.signinSilent();
}
