/**
 * Login Page
 *
 * Redirects to IdentityServer /connect/authorize (Authorization Code + PKCE + DPoP).
 * No credentials are sent to the API — login happens entirely at IdentityServer.
 */

import { Card, Spin } from "antd";
import React, { useEffect } from 'react';
import { signinRedirect } from '../../../lib/oidc/userManager';
import { useAuth } from '../store/authStore';

export const LoginPage: React.FC = () => {
  const { isAuthenticated } = useAuth();

  useEffect(() => {
    if (isAuthenticated) {
      window.location.replace('/');
      return;
    }

    // Save intended destination so callback page can redirect back
    const returnTo = new URLSearchParams(window.location.search).get('returnTo');
    if (returnTo) {
      sessionStorage.setItem('oidc_return_to', returnTo);
    }

    // Start OIDC Authorization Code + PKCE + DPoP flow
    signinRedirect().catch((err) => {
      console.error('[OIDC] signinRedirect failed:', err);
    });
  }, [isAuthenticated]);

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 p-6">
      <Card className="w-full max-w-md rounded-2xl shadow-xl p-6 text-center">
        <Spin size="large" />
        <p className="mt-4 text-gray-500">Đang chuyển hướng đến trang đăng nhập…</p>
      </Card>
    </div>
  );
};
