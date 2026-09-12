import { apiClient } from './client';
import type {
  WarehouseBatch,
  WarehouseBatchSummary,
  SaveWarehouseBatchRequest,
  ProductLookupItem,
  WarehouseBatchStatus,
} from '../types/warehouse';

export const warehouseApi = {
  getBatches: async (params?: {
    status?: WarehouseBatchStatus;
    fromDate?: string;
    toDate?: string;
    search?: string;
  }): Promise<WarehouseBatchSummary[]> => {
    const res = await apiClient.get<WarehouseBatchSummary[]>('/warehouse/batches', { params });
    return res.data;
  },

  getBatchById: async (id: number): Promise<WarehouseBatch> => {
    const res = await apiClient.get<WarehouseBatch>(`/warehouse/batches/${id}`);
    return res.data;
  },

  saveBatch: async (data: SaveWarehouseBatchRequest): Promise<WarehouseBatch> => {
    const res = await apiClient.post<WarehouseBatch>('/warehouse/batches', data);
    return res.data;
  },

  submitBatch: async (id: number): Promise<WarehouseBatch> => {
    const res = await apiClient.post<WarehouseBatch>(`/warehouse/batches/${id}/submit`);
    return res.data;
  },

  deleteBatch: async (id: number): Promise<{ success: boolean; message: string }> => {
    const res = await apiClient.delete<{ success: boolean; message: string }>(`/warehouse/batches/${id}`);
    return res.data;
  },

  lookupProducts: async (q?: string, folderId?: number | null): Promise<ProductLookupItem[]> => {
    const res = await apiClient.get<ProductLookupItem[]>('/warehouse/product-lookup', {
      params: { q: q || undefined, folderId: folderId || undefined },
    });
    return res.data;
  },

  markProcessed: async (id: number, shipmentOrderId: number): Promise<{ success: boolean; message: string }> => {
    const res = await apiClient.post<{ success: boolean; message: string }>(
      `/warehouse/batches/${id}/mark-processed`,
      null,
      { params: { shipmentOrderId } }
    );
    return res.data;
  },
};
