import axios from 'axios';
import { message } from 'antd';

const API_BASE_URL = import.meta.env.VITE_API_URL || '/api';
let unauthorizedEventDispatched = false;

export const resetUnauthorizedHandling = () => {
  unauthorizedEventDispatched = false;
};

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor to attach JWT Bearer token
// Response interceptor for unified error notification
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = error.response?.status;
    const requestUrl = String(error.config?.url ?? '');
    const isSilentAuthRequest = requestUrl.includes('/auth/me') || requestUrl.includes('/auth/logout');
    const isLoginRequest = requestUrl.includes('/auth/login');

    if (status === 401 && !isSilentAuthRequest && !isLoginRequest) {
      localStorage.removeItem('auth_user');
      if (!unauthorizedEventDispatched) {
        unauthorizedEventDispatched = true;
        window.dispatchEvent(new Event('auth:unauthorized'));
      }
    }

    const errorMsg =
      error.response?.data?.message ||
      error.response?.data?.title ||
      error.message ||
      'Đã xảy ra lỗi khi kết nối máy chủ';
    
    // In file download or special status codes, caller might handle it
    if (status !== 401 && error.config?.responseType !== 'blob') {
      message.error(errorMsg);
    } else if (isLoginRequest && error.config?.responseType !== 'blob') {
      message.error(errorMsg);
    }
    return Promise.reject(error);
  }
);
