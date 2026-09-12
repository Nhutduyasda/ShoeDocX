import { apiClient } from './client';
import type { LoginRequest, LoginResponse, User, ChangePasswordRequest } from '../types/auth';

export const authApi = {
  login: async (data: LoginRequest): Promise<LoginResponse> => {
    const res = await apiClient.post<LoginResponse>('/auth/login', data);
    return res.data;
  },

  getMe: async (): Promise<User> => {
    const res = await apiClient.get<User>('/auth/me');
    return res.data;
  },

  changePassword: async (data: ChangePasswordRequest): Promise<{ message: string }> => {
    const res = await apiClient.post<{ message: string }>('/auth/change-password', data);
    return res.data;
  },

  logout: () => {
    localStorage.removeItem('auth_token');
    localStorage.removeItem('auth_user');
  },
};
