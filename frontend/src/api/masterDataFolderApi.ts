import { apiClient } from './client';
import type {
  MasterDataFolder,
  CreateFolderRequest,
  UpdateFolderRequest,
  MoveFolderRequest,
  BulkMoveProductsRequest,
} from '../types';

export const masterDataFolderApi = {
  // Lấy cây thư mục kèm số lượng sản phẩm
  getTree: async (): Promise<MasterDataFolder[]> => {
    const response = await apiClient.get<MasterDataFolder[]>('/master-data-folders/tree');
    return response.data;
  },

  // Lấy chi tiết một thư mục
  getById: async (id: number): Promise<MasterDataFolder> => {
    const response = await apiClient.get<MasterDataFolder>(`/master-data-folders/${id}`);
    return response.data;
  },

  // Tạo thư mục mới
  create: async (data: CreateFolderRequest): Promise<MasterDataFolder> => {
    const response = await apiClient.post<MasterDataFolder>('/master-data-folders', data);
    return response.data;
  },

  // Cập nhật thư mục
  update: async (id: number, data: UpdateFolderRequest): Promise<MasterDataFolder> => {
    const response = await apiClient.put<MasterDataFolder>(`/master-data-folders/${id}`, data);
    return response.data;
  },

  // Di chuyển thư mục (đổi cha hoặc đổi thứ tự)
  move: async (id: number, data: MoveFolderRequest): Promise<{ success: boolean; message: string }> => {
    const response = await apiClient.put<{ success: boolean; message: string }>(`/master-data-folders/${id}/move`, data);
    return response.data;
  },

  // Xóa thư mục
  deleteFolder: async (id: number, cascade = false): Promise<void> => {
    await apiClient.delete(`/master-data-folders/${id}?cascade=${cascade}`);
  },

  // Di chuyển danh sách sản phẩm sang thư mục đích
  bulkMoveProducts: async (data: BulkMoveProductsRequest): Promise<{ success: boolean; movedCount: number; message: string }> => {
    const response = await apiClient.post<{ success: boolean; movedCount: number; message: string }>(
      '/master-data-folders/bulk-move-products',
      data
    );
    return response.data;
  },
};
