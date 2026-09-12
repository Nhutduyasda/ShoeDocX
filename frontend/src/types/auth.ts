export type Department = 'Admin' | 'Xnk' | 'Kho' | 'KeToan' | 0 | 1 | 2 | 3;

export interface User {
  id: string;
  username: string;
  fullName: string;
  department: Department;
  departmentName: string;
}

export type NavTabKey = 'overview' | 'warehouse' | 'shipment' | 'ocr' | 'history' | 'settlement' | 'products';

export const isKhoUser = (user?: User | null): boolean => {
  if (!user) return false;
  const deptStr = String(user.department).toLowerCase();
  return deptStr === 'kho' || (user.department as unknown) === 2 || (user.departmentName ? user.departmentName.toLowerCase().includes('kho') : false);
};

export const isKeToanUser = (user?: User | null): boolean => {
  if (!user) return false;
  const deptStr = String(user.department).toLowerCase();
  return deptStr === 'ketoan' || (user.department as unknown) === 3 || (user.departmentName ? user.departmentName.toLowerCase().includes('kế toán') : false);
};

export const isXnkUser = (user?: User | null): boolean => {
  if (!user) return false;
  const deptStr = String(user.department).toLowerCase();
  return deptStr === 'xnk' || (user.department as unknown) === 1 || (user.departmentName ? user.departmentName.toLowerCase().includes('xuất nhập khẩu') : false);
};

export const isAdminUser = (user?: User | null): boolean => {
  if (!user) return false;
  const deptStr = String(user.department).toLowerCase();
  return deptStr === 'admin' || (user.department as unknown) === 0 || (user.departmentName ? user.departmentName.toLowerCase().includes('giám đốc') : false);
};

export const getAllowedTabsForUser = (user?: User | null): NavTabKey[] => {
  if (!user) return ['shipment'];
  if (isAdminUser(user)) {
    return ['overview', 'warehouse', 'shipment', 'ocr', 'history', 'settlement', 'products'];
  }
  if (isKhoUser(user)) {
    return ['warehouse', 'ocr', 'products'];
  }
  if (isXnkUser(user)) {
    return ['shipment', 'history', 'ocr', 'products'];
  }
  if (isKeToanUser(user)) {
    return ['overview', 'settlement', 'history', 'products'];
  }
  return ['shipment', 'history', 'products'];
};

export const getDefaultTabForUser = (user?: User | null): NavTabKey => {
  if (!user) return 'shipment';
  if (isAdminUser(user)) return 'overview';
  if (isKhoUser(user)) return 'warehouse';
  if (isKeToanUser(user)) return 'overview';
  if (isXnkUser(user)) return 'shipment';
  return 'shipment';
};

export interface LoginRequest {
  username: string;
  password: string;
  rememberMe?: boolean;
}

export interface LoginResponse {
  token: string;
  user: User;
}

export interface ChangePasswordRequest {
  oldPassword: string;
  newPassword: string;
}
