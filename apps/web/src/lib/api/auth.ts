import { apiRequest, jsonRequest } from "./client";
import type { AuthResponse, CurrentSession } from "./models";

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest extends LoginRequest {
  firstName: string;
  lastName: string;
  workspaceName: string;
}

export const authApi = {
  current: () => apiRequest<CurrentSession>("/users/me"),
  login: (request: LoginRequest) =>
    apiRequest<AuthResponse>("/auth/login", jsonRequest("POST", request), {
      refreshOnUnauthorized: false,
    }),
  register: (request: RegisterRequest) =>
    apiRequest<AuthResponse>("/auth/register", jsonRequest("POST", request), {
      refreshOnUnauthorized: false,
    }),
  logout: () =>
    apiRequest<Record<string, never>>("/auth/logout", jsonRequest("POST"), {
      refreshOnUnauthorized: false,
    }),
};
