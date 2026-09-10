import { apiClient } from './client';
import type {
  CreateShipmentRequest,
  PklPreviewResponse,
  ShipmentOrder,
  SavedShipmentSummary,
} from '../types';

export const shipmentApi = {
  // Xem trước phân rã kiện đóng gói PKL (thùng chẵn/lẻ, dải số kiện, trọng lượng)
  previewPkl: async (request: CreateShipmentRequest): Promise<PklPreviewResponse> => {
    const response = await apiClient.post<PklPreviewResponse>('/shipments/preview-pkl', request);
    return response.data;
  },

  // Xuất file Excel đa sheet trực tiếp từ thông tin đơn hàng
  exportShipmentExcel: async (request: CreateShipmentRequest): Promise<Blob> => {
    const response = await apiClient.post('/shipments/export-excel', request, {
      responseType: 'blob',
    });
    return response.data;
  },

  // Lưu đơn hàng vào database
  createShipment: async (request: CreateShipmentRequest): Promise<ShipmentOrder> => {
    const response = await apiClient.post<ShipmentOrder>('/shipments', request);
    return response.data;
  },

  // Lấy danh sách lịch sử các đơn hàng đã tạo
  getShipments: async (): Promise<SavedShipmentSummary[]> => {
    const response = await apiClient.get<SavedShipmentSummary[]>('/shipments');
    return response.data;
  },

  // Lấy chi tiết đơn hàng theo Id
  getShipmentById: async (id: number): Promise<ShipmentOrder> => {
    const response = await apiClient.get<ShipmentOrder>(`/shipments/${id}`);
    return response.data;
  },

  // Xuất file Excel từ đơn hàng đã lưu trong database
  exportSavedShipmentExcel: async (id: number): Promise<Blob> => {
    const response = await apiClient.get(`/shipments/${id}/export-excel`, {
      responseType: 'blob',
    });
    return response.data;
  },
};
