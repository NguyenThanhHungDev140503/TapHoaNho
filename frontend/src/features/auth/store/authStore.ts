import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import type { User } from 'oidc-client-ts';
import { signoutRedirect, getUser, clearDPoPKeyPair } from '../../../lib/oidc/userManager';

// ─────────────────────────────────────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────────────────────────────────────

/**
 * User shape derived from IdentityServer access-token claims (at+jwt).
 * Role values come from the JwtClaimTypes.Role claim — always one of:
 *   "Admin" | "Staff"
 *
 * Refresh tokens are intentionally NOT stored here. They live only in the
 * oidc-client-ts session storage; mirroring them into zustand state would
 * leak them into devtools and any persisted middleware.
 */
interface OidcUser {
  sub: string;
  username: string;
  fullName: string;
  role: string;
  accessToken: string;
}

interface AuthState {
  user: OidcUser | null;
  isAuthenticated: boolean;
  /** True until initFromSession() has completed at least once. */
  isLoading: boolean;

  setOidcUser: (oidcUser: User) => void;
  clearAuth: () => void;
  initFromSession: () => Promise<void>;

  isAdmin: () => boolean;
  isStaff: () => boolean;
  hasRole: (role: string) => boolean;
}

// ─────────────────────────────────────────────────────────────────────────────
// Store
// ─────────────────────────────────────────────────────────────────────────────

export const useAuthStore = create<AuthState>()(
  devtools(
    (set, get) => ({
      user: null,
      isAuthenticated: false,
      isLoading: true,

      setOidcUser: (oidcUser: User) => {
        const profile = oidcUser.profile;
        set({
          user: {
            sub: profile.sub,
            username: (profile['preferred_username'] as string) ?? profile.sub,
            fullName: (profile['name'] as string) ?? '',
            role: (profile['role'] as string) ?? '',
            accessToken: oidcUser.access_token,
          },
          isAuthenticated: true,
          isLoading: false,
        });
      },

      clearAuth: () => {
        set({ user: null, isAuthenticated: false, isLoading: false });
      },

      /** Restore session from oidc-client-ts storage on app bootstrap. */
      initFromSession: async () => {
        try {
          const oidcUser = await getUser();
          if (oidcUser && !oidcUser.expired) {
            get().setOidcUser(oidcUser);
          } else {
            set({ user: null, isAuthenticated: false, isLoading: false });
          }
        } catch {
          set({ user: null, isAuthenticated: false, isLoading: false });
        } finally {
          // Defensive: guarantee isLoading flips false even if a future
          // change to setOidcUser forgets to clear it. Otherwise the app
          // would hang on the loading splash forever.
          if (get().isLoading) set({ isLoading: false });
        }
      },

      isAdmin: () => get().user?.role === 'Admin',
      isStaff: () => get().user?.role === 'Staff',
      hasRole: (role: string) => {
        const { user } = get();
        if (!user) return false;
        if (user.role === 'Admin') return true; // Admin implicitly satisfies any role
        return user.role === role;
      },
    }),
    { name: 'auth-store' }
  )
);

// ─────────────────────────────────────────────────────────────────────────────
// Logout — exported for components
// ─────────────────────────────────────────────────────────────────────────────

export async function logout(): Promise<void> {
  // Clear in-memory state first so any guard that fires during the redirect
  // sees an unauthenticated session.
  useAuthStore.getState().clearAuth();
  // Drop the persisted DPoP keypair — next session generates a fresh one.
  await clearDPoPKeyPair();
  // Hand control to IdentityServer's end_session endpoint.
  await signoutRedirect();
}

// ─────────────────────────────────────────────────────────────────────────────
// Selector hooks (individual selectors avoid extra re-renders)
// ─────────────────────────────────────────────────────────────────────────────

export const useUser = () => useAuthStore((s) => s.user);
export const useIsAuthenticated = () => useAuthStore((s) => s.isAuthenticated);
export const useIsAuthLoading = () => useAuthStore((s) => s.isLoading);
