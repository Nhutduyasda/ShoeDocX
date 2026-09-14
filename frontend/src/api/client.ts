import axios from 'axios';
import { message } from 'antd';

const API_BASE_URL = import.meta.env.VITE_API_URL || '/api';

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
    if (error.response?.status === 401) {
      localStorage.removeItem('auth_user');
      window.dispatchEvent(new Event('auth:unauthorized'));
    }

    const errorMsg =
      error.response?.data?.message ||
      error.response?.data?.title ||
      error.message ||
      'Đã xảy ra lỗi khi kết nối máy chủ';
    
    // In file download or special status codes, caller might handle it
    if (error.config?.responseType !== 'blob') {
      message.error(errorMsg);
    }
    return Promise.reject(error);
  }
);
