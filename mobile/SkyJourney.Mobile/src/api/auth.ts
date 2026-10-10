import { apiRequest } from './client';

/** Mirrors the API's UserProfileDto. */
export type UserProfile = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
  createdAt: string;
};

/** Mirrors the API's AuthResponse. */
export type AuthResponse = {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: UserProfile;
};

export type RegisterRequest = { email: string; password: string; firstName: string; lastName: string };
export type LoginRequest = { email: string; password: string };
export type UpdateProfileRequest = { firstName: string; lastName: string };

// Auth endpoints never carry (or refresh) the access token themselves.
export const authApi = {
  register: (body: RegisterRequest) =>
    apiRequest<AuthResponse>('/api/v1/auth/register', { method: 'POST', body, anonymous: true }),
  login: (body: LoginRequest) => apiRequest<AuthResponse>('/api/v1/auth/login', { method: 'POST', body, anonymous: true }),
  refresh: (refreshToken: string) =>
    apiRequest<AuthResponse>('/api/v1/auth/refresh', { method: 'POST', body: { refreshToken }, anonymous: true }),
  logout: (refreshToken: string) =>
    apiRequest<void>('/api/v1/auth/logout', { method: 'POST', body: { refreshToken }, anonymous: true }),
  getMe: () => apiRequest<UserProfile>('/api/v1/me'),
  updateMe: (body: UpdateProfileRequest) => apiRequest<UserProfile>('/api/v1/me', { method: 'PUT', body }),
};
