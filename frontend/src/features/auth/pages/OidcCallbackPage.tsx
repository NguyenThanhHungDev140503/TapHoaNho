/**
 * OIDC Callback Page — /callback
 *
 * IdentityServer redirects here after successful authorization:
 *   https://localhost:5173/callback?code=...&state=...
 *
 * This page:
 *   1. Calls signinCallback() which exchanges the auth code for DPoP-bound tokens
 *   2. Stores the OIDC user in authStore
 *   3. Redirects to the intended destination (or dashboard)
 */

import React, { useEffect, useRef } from 'react';
import { Spin } from 'antd';
import { signinCallback } from '../../../lib/oidc/userManager';
import { useAuthStore } from '../store/authStore';

export const OidcCallbackPage: React.FC = () => {
  const setOidcUser = useAuthStore((s) => s.setOidcUser);
  const handledRef = useRef(false);

  useEffect(() => {
    // Guard against React StrictMode double-invoke
    if (handledRef.current) return;
    handledRef.current = true;

    (async () => {
      try {
        const user = await signinCallback();
        setOidcUser(user);

        // Return to where the user was before login, or fallback to dashboard
        const returnTo = sessionStorage.getItem('oidc_return_to') ?? '/';
        sessionStorage.removeItem('oidc_return_to');
        window.location.replace(returnTo);
      } catch (err) {
        console.error('[OIDC Callback] Error exchanging code:', err);
        window.location.replace('/auth/login');
      }
    })();
  }, [setOidcUser]);

  return (
    <div className="min-h-screen flex items-center justify-center">
      <Spin size="large" tip="Đang xác thực..." />
    </div>
  );
};
