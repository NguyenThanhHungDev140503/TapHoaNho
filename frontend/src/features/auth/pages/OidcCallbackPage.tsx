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

/**
 * Validates returnTo URL to prevent open redirect attacks.
 * Allows: relative paths (/dashboard), same-origin URLs (https://localhost:5173/orders)
 * Blocks: absolute URLs to external domains (https://evil.com), protocol-relative (//evil.com)
 */
function isAllowedReturnTo(returnTo: string | null): boolean {
  if (!returnTo) return false;
  if (returnTo.startsWith('//')) return false;
  if (returnTo.startsWith('/')) return true;
  try {
    const url = new URL(returnTo);
    return url.origin === window.location.origin;
  } catch {
    return false;
  }
}

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
        // Re-validate before redirect to prevent injection via sessionStorage tampering
        if (isAllowedReturnTo(returnTo)) {
          window.location.replace(returnTo);
        } else {
          window.location.replace('/');
        }
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
