import type { UserRole } from '../../../config/api.config.ts';
import type { BaseEntity } from "../../../types/base.entity.ts";

/**
 * User entity from the admin user-management API (/api/admin/users/*).
 *
 * IMPORTANT — dual role schema:
 *   - This `role` field is a NUMERIC enum (0 = Admin, 1 = Staff) matching
 *     the backend Domain.Enums.UserRole C# enum.
 *   - The auth store (features/auth/store/authStore.ts) holds a DIFFERENT
 *     `role` that is the STRING claim from IdentityServer ("Admin" | "Staff").
 *
 * Do not cross-compare them. If you need the current user's role for
 * permission checks, use useAuthStore().user.role (string). If you need
 * to filter rows from the user-management list, compare against
 * API_CONFIG.USER_ROLES (numeric).
 */
interface User extends BaseEntity {
  id: number;
  username: string;
  password: string;
  fullName: string;
  role: UserRole;
}

export type UserNoPass = Omit<User, "password">;
export type UserEntity = User;

// User Details DTO - từ backend UserResponseDto
export interface UserDetailsDto {
    id: number;
    username: string;
    fullName: string;
    role: UserRole;
    totalOrders: number;
    createdAt: string | Date; // Backend trả về string, nhưng có thể convert sang Date
}
