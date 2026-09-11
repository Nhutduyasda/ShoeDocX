import { apiClient } from './client';
import type {
  ProductMaster,
  CreateProductMasterRequest,
  UpdateProductMasterRequest,
  PagedResult,
  ImportResult,
} from '../types';

export const productMasterApi = {
  // Lấy danh sách phân trang
  getPaged: async (search?: string, page = 1, pageSize = 10): Promise<PagedResult<ProductMaster>> => {
    const response = await apiClient.get<PagedResult<ProductMaster>>('/product-masters', {
      params: { search, page, pageSize },
    });
    return response.data;
  },

  // Lấy toàn bộ danh sách
  getAll: async (): Promise<ProductMaster[]> => {
    const response = await apiClient.get<ProductMaster[]>('/product-masters/all');
    return response.data;
  },

  // Lấy chi tiết theo ID
  getById: async (id: number): Promise<ProductMaster> => {
    const response = await apiClient.get<ProductMaster>(`/product-masters/${id}`);
    return response.data;
  },

  // Tạo mới
  create: async (data: CreateProductMasterRequest): Promise<ProductMaster> => {
    const response = await apiClient.post<ProductMaster>('/product-masters', data);
    return response.data;
  },

  // Cập nhật
  update: async (id: number, data: UpdateProductMasterRequest): Promise<ProductMaster> => {
    const response = await apiClient.put<ProductMaster>(`/product-masters/${id}`, data);
    return response.data;
  },

  // Xóa
  deleteProduct: async (id: number): Promise<void> => {
    await apiClient.delete(`/product-masters/${id}`);
  },

  // Xóa toàn bộ danh mục sản phẩm
  deleteAll: async (): Promise<{ message: string; deletedCount: number }> => {
    const response = await apiClient.delete<{ message: string; deletedCount: number }>('/product-masters/all');
    return response.data;
  },

  // Cập nhật ĐVT đồng loạt cho tất cả sản phẩm
  bulkUpdateUnit: async (unit: string): Promise<{ message: string; updatedCount: number }> => {
    const response = await apiClient.put<{ message: string; updatedCount: number }>('/product-masters/bulk-update-unit', { unit });
    return response.data;
  },

  // Tải file mẫu Excel
  downloadTemplate: async (): Promise<void> => {
    const response = await apiClient.get('/product-masters/template', {
      responseType: 'blob',
    });
    const url = window.URL.createObjectURL(new Blob([response.data]));
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', `ProductMaster_Template_${new Date().toISOString().slice(0, 10)}.xlsx`);
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  },

  // Xuất Excel toàn bộ
  exportExcel: async (): Promise<void> => {
    const response = await apiClient.get('/product-masters/export', {
      responseType: 'blob',
    });
    const url = window.URL.createObjectURL(new Blob([response.data]));
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', `ProductMaster_Export_${new Date().toISOString().slice(0, 10)}.xlsx`);
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  },

  // Import từ file Excel
  importExcel: async (file: File, updateExisting = true): Promise<ImportResult> => {
    const formData = new FormData();
    formData.append('file', file);
    const response = await apiClient.post<ImportResult>(
      `/product-masters/import?updateExisting=${updateExisting}`,
      formData,
      {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      }
    );
    return response.data;
  },
};
