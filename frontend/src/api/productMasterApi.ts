import { apiClient } from './client';
import type {
  ProductMaster,
  CreateProductMasterRequest,
  UpdateProductMasterRequest,
  PagedResult,
  ImportResult,
  ImportPreviewResponse,
  ColumnMappingOverride,
  ValidateItemsRequest,
  ValidateItemsResult,
} from '../types';

export const productMasterApi = {
  // Kiểm tra chéo mã hàng phát hiện nhầm lẫn đối tác
  validateItems: async (req: ValidateItemsRequest): Promise<ValidateItemsResult> => {
    const response = await apiClient.post<ValidateItemsResult>('/master-data/validate-items', req);
    return response.data;
  },

  // Lấy danh sách phân trang
  getPaged: async (search?: string, page = 1, pageSize = 10, folderId?: number | null): Promise<PagedResult<ProductMaster>> => {
    const params: Record<string, any> = { search, page, pageSize };
    if (folderId !== undefined && folderId !== null) {
      params.folderId = folderId;
    }
    const response = await apiClient.get<PagedResult<ProductMaster>>('/product-masters', {
      params,
    });
    return response.data;
  },

  // Di chuyển danh sách sản phẩm sang thư mục khác
  bulkMove: async (productIds: number[], targetFolderId?: number | null): Promise<{ success: boolean; message: string }> => {
    const response = await apiClient.post<{ success: boolean; message: string }>('/product-masters/bulk-move', {
      productIds,
      targetFolderId: targetFolderId ?? null,
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

  // Xóa sản phẩm theo thư mục (hoặc toàn bộ nếu không truyền folderId)
  deleteAll: async (folderId?: number | null): Promise<{ message: string; deletedCount: number }> => {
    const params: Record<string, any> = {};
    if (folderId !== undefined && folderId !== null) {
      params.folderId = folderId;
    }
    const response = await apiClient.delete<{ message: string; deletedCount: number }>('/product-masters/all', {
      params,
    });
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

  // Xem trước cấu trúc và tự động nhận diện cột từ file Excel
  previewImport: async (file: File, folderId?: number | null): Promise<ImportPreviewResponse> => {
    const formData = new FormData();
    formData.append('file', file);
    const params: Record<string, any> = {};
    if (folderId !== undefined && folderId !== null) {
      params.folderId = folderId;
    }
    const response = await apiClient.post<ImportPreviewResponse>(
      '/product-masters/preview-import',
      formData,
      {
        params,
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      }
    );
    return response.data;
  },

  // Import từ file Excel (hỗ trợ folderId và column mapping override)
  importExcel: async (
    file: File,
    updateExisting = true,
    folderId?: number | null,
    mapping?: ColumnMappingOverride
  ): Promise<ImportResult> => {
    const formData = new FormData();
    formData.append('file', file);
    const queryParams = new URLSearchParams({ updateExisting: String(updateExisting) });
    if (folderId !== undefined && folderId !== null) {
      queryParams.append('folderId', String(folderId));
    }
    if (mapping) {
      if (mapping.styleCodeCol) queryParams.append('styleCodeCol', String(mapping.styleCodeCol));
      if (mapping.cmtPriceCol) queryParams.append('cmtCol', String(mapping.cmtPriceCol));
      if (mapping.dapPriceCol) queryParams.append('dapCol', String(mapping.dapPriceCol));
      if (mapping.descriptionCol) queryParams.append('descCol', String(mapping.descriptionCol));
      if (mapping.hsCodeCol) queryParams.append('hsCol', String(mapping.hsCodeCol));
      if (mapping.unitCol) queryParams.append('unitCol', String(mapping.unitCol));
      if (mapping.pairsPerCartonCol) queryParams.append('pairCol', String(mapping.pairsPerCartonCol));
    }
    const response = await apiClient.post<ImportResult>(
      `/product-masters/import?${queryParams.toString()}`,
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
