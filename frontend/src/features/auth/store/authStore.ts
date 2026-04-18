import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import type { User } from 'oidc-client-ts';
import { signoutRedirect, getUser } from '../../../lib/oidc/userManager';
import { rotateDPoPKeyPair } from '../../../lib/oidc/dpopKey';

// ─────────────────────────────────────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────────────────────────────────────

/** Claim shape populated from IdentityServer CustomProfileService. */
interface OidcUser {
  sub: string;          // subject (user ID)
  username: string;
  fullName: string;
  role: string;         // "Admin" | "Staff"  (JwtClaimTypes.Role)
  accessToken: string;  // current DPoP-bound access token
  refreshToken?: string;
}

interface AuthState {
  user: OidcUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;

  // Actions
  setOidcUser: (oidcUser: User) => void;
  clearAuth: () => void;
  initFromSession: () => Promise<void>;

  // Utility
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
            refreshToken: oidcUser.refresh_token ?? undefined,
          },
          isAuthenticated: true,
          isLoading: false,
        });
      },

      clearAuth: async () => {
        set({ user: null, isAuthenticated: false, isLoading: false });
      },

      /** Called on app bootstrap — restores session from oidc-client-ts storage. */
      initFromSession: async () => {
        set({ isLoading: true });
        try {
          const oidcUser = await getUser();
          if (oidcUser && !oidcUser.expired) {
            get().setOidcUser(oidcUser);
          } else {
            set({ user: null, isAuthenticated: false, isLoading: false });
          }
        } catch {
          set({ user: null, isAuthenticated: false, isLoading: false });
        }
      },

      isAdmin: () => get().user?.role === 'Admin',
      isStaff: () => get().user?.role === 'Staff',
      hasRole: (role: string) => {
        const { user } = get();
        if (!user) return false;
        if (user.role === 'Admin') return true; // Admin has all roles
        return user.role === role;
      },
    }),
    { name: 'auth-store' }
  )
);

// ─────────────────────────────────────────────────────────────────────────────
// Logout helper (exported for use in components)
// ─────────────────────────────────────────────────────────────────────────────

export async function logout(): Promise<void> {
  useAuthStore.getState().clearAuth();
  await rotateDPoPKeyPair(); // new keypair on next login
  await signoutRedirect();   // redirects to IdentityServer end_session
}

// ─────────────────────────────────────────────────────────────────────────────
// Selector hooks
// ─────────────────────────────────────────────────────────────────────────────

export const useAuth = () =>
  useAuthStore((state) => ({
    user: state.user,
    isAuthenticated: state.isAuthenticated,
    isLoading: state.isLoading,
  }));
