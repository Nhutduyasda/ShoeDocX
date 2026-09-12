import { apiClient } from './client';
import type {
  CreateShipmentRequest,
  PklPreviewResponse,
  ShipmentOrder,
  SavedShipmentSummary,
  ExportSummary,
  SequenceInfo,
} from '../types';

export interface ExportResult {
  /** Blob file (XLSX hoặc ZIP) */
  blob: Blob;
  /** Content-Type từ response */
  contentType: string;
  /** Thông tin tổng kết (từ header X-Export-Info) */
  exportSummary?: ExportSummary;
}

export const shipmentApi = {
  // Xem trước phân rã kiện đóng gói PKL (thùng chẵn/lẻ, dải số kiện, trọng lượng)
  previewPkl: async (request: CreateShipmentRequest): Promise<PklPreviewResponse> => {
    const response = await apiClient.post<PklPreviewResponse>('/shipments/preview-pkl', request);
    return response.data;
  },

  /**
   * Xuất file Excel. Tự động xử lý 2 trường hợp:
   * - 1 file XLSX: khi đơn chỉ có 1 loại hàng
   * - 1 file ZIP (chứa 2 xlsx): khi đơn có cả Thành hình + Gò không may
   * Trả về blob kèm thông tin tổng kết từ header X-Export-Info.
   */
  exportShipmentExcel: async (request: CreateShipmentRequest): Promise<ExportResult> => {
    const response = await apiClient.post('/shipments/export-excel', request, {
      responseType: 'blob',
    });

    const contentType: string = String(response.headers['content-type'] ?? '');
    let exportSummary: ExportSummary | undefined;

    try {
      const infoHeader = response.headers['x-export-info'];
      if (infoHeader) {
        exportSummary = JSON.parse(String(infoHeader)) as ExportSummary;
      }
    } catch {
      // ignore JSON parse errors
    }

    return {
      blob: response.data as Blob,
      contentType,
      exportSummary,
    };
  },

  // Lưu đơn hàng vào database
  createShipment: async (request: CreateShipmentRequest): Promise<ShipmentOrder> => {
    const response = await apiClient.post<ShipmentOrder>('/shipments', request);
    return response.data;
  },

  // Cập nhật đơn hàng đã có trong database
  updateShipment: async (id: number, request: CreateShipmentRequest): Promise<void> => {
    await apiClient.put(`/shipments/${id}`, request);
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

  // ====== Sequence Management ======

  /** Lấy số thứ tự Invoice tiếp theo sẽ được cấp */
  getSequence: async (): Promise<SequenceInfo> => {
    const response = await apiClient.get<SequenceInfo>('/shipments/sequence/current');
    return response.data;
  },

  /** Ghi đè số thứ tự bắt đầu. Lần xuất tiếp theo sẽ dùng nextNumber này. */
  setSequence: async (nextNumber: number): Promise<SequenceInfo> => {
    const response = await apiClient.put<SequenceInfo>('/shipments/sequence', { nextNumber });
    return response.data;
  },
};

/**
 * Trích xuất số thứ tự từ chuỗi Invoice No.
 * Ví dụ: "KMHD-NEW2026-0233" → 233
 */
export function extractSequenceNumber(invoiceNo: string): number | null {
  if (!invoiceNo) return null;
  const match = invoiceNo.trim().match(/\d+$/);
  if (match) {
    const num = parseInt(match[0], 10);
    return num > 0 ? num : null;
  }
  return null;
}

/**
 * Chuyển số thứ tự thành tên file chuẩn.
 * Ví dụ: 233 → "KM3-26-DH233.xlsx"
 */
export function toStandardFileName(sequenceNumber: number): string {
  return `KM3-26-DH${sequenceNumber}.xlsx`;
}

/**
 * Trích xuất tên file chuẩn từ Invoice No.
 * Ví dụ: "KMHD-NEW2026-0233" → "KM3-26-DH233.xlsx"
 * Fallback: "CUSTOM-ABC" → "CUSTOM-ABC.xlsx"
 */
export function invoiceNoToFileName(invoiceNo: string): string {
  const seq = extractSequenceNumber(invoiceNo);
  if (seq !== null) {
    return toStandardFileName(seq);
  }
  const safe = invoiceNo.trim().replace(/[/\\?%*:|"<>]/g, '-');
  return `${safe}.xlsx`;
}
