/* eslint-disable react-refresh/only-export-components */
import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import type { User, LoginRequest, Department } from '../types/auth';
import { authApi } from '../api/authApi';
import { message } from 'antd';
import { resetUnauthorizedHandling } from '../api/client';

interface AuthContextType {
  user: User | null;
  token: string | null;
  loading: boolean;
  isAuthenticated: boolean;
  login: (data: LoginRequest) => Promise<void>;
  logout: () => void;
  hasDepartment: (...depts: Department[]) => boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<User | null>(() => {
    const saved = localStorage.getItem('auth_user');
    if (saved) {
      try {
        return JSON.parse(saved);
      } catch {
        return null;
      }
    }
    return null;
  });
  const [token, setToken] = useState<string | null>(null);
  const [loading, setLoading] = useState<boolean>(true);

  const clearLocalSession = useCallback(() => {
    setToken(null);
    setUser(null);
    localStorage.removeItem('auth_user');
  }, []);

  const logout = useCallback(() => {
    void authApi.logout();
    clearLocalSession();
  }, [clearLocalSession]);

  // Fetch current user on mount if token exists
  useEffect(() => {
    const initAuth = async () => {
      try {
        const me = await authApi.getMe();
        setUser(me);
        setToken('cookie-session');
        localStorage.setItem('auth_user', JSON.stringify(me));
      } catch {
        clearLocalSession();
      }
      setLoading(false);
    };

    initAuth();

    const handleUnauthorized = () => {
      clearLocalSession();
      message.warning({
        key: 'auth-session-expired',
        content: 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',
      });
    };

    window.addEventListener('auth:unauthorized', handleUnauthorized);
    return () => window.removeEventListener('auth:unauthorized', handleUnauthorized);
  }, [clearLocalSession]);

  const login = async (data: LoginRequest) => {
    const res = await authApi.login(data);
    resetUnauthorizedHandling();
    setToken(res.token);
    setUser(res.user);
    localStorage.setItem('auth_user', JSON.stringify(res.user));
    message.success(`Xin chào ${res.user.fullName} (${res.user.departmentName})`);
  };

  const hasDepartment = (...depts: Department[]): boolean => {
    if (!user) return false;
    if (user.department === 'Admin') return true; // Admin has access to all
    return depts.includes(user.department);
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        loading,
        isAuthenticated: !!user && !!token,
        login,
        logout,
        hasDepartment,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider');
  return ctx;
};
