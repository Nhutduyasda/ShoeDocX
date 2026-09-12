import { apiClient } from './client';
import type {
  CustomsReconciliationResult,
  ConfirmCustomsSyncRequest,
} from '../types';

export interface ConfirmSyncResponse {
  message: string;
  orderId: number;
  invoiceNo: string;
  declarationNo: string;
  clearanceDate?: string;
  customsDeclarationType?: string;
  customsChannel?: number;
  customsOffice?: string;
  customsPackageQty?: number;
  customsGrossWeight?: number;
  customsTotalDap?: number;
  customsTotalCmt?: number;
  customsAttachmentFileName?: string;
  customsAttachmentFilePath?: string;
  isLocked?: boolean;
  status: number;
  statusName: string;
}

export const customsApi = {
  /**
   * Upload file tờ khai VNACCS (.xls hoặc .xlsx), bóc tách và đối soát chéo với đơn hàng
   */
  parseAndCompare: async (
    file: File,
    orderId?: number
  ): Promise<CustomsReconciliationResult> => {
    const formData = new FormData();
    formData.append('file', file);

    const params: Record<string, number> = {};
    if (orderId && orderId > 0) {
      params.orderId = orderId;
    }

    const response = await apiClient.post<CustomsReconciliationResult>(
      '/customs/parse-and-compare',
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

  /**
   * Xác nhận đồng bộ thông tin hải quan vào đơn hàng và lưu file tờ khai
   */
  confirmSync: async (
    orderId: number,
    data: ConfirmCustomsSyncRequest,
    file?: File
  ): Promise<ConfirmSyncResponse> => {
    const formData = new FormData();
    formData.append('orderId', String(orderId));
    formData.append('declarationNo', data.declarationNo);
    if (data.clearanceDate) {
      formData.append('clearanceDate', data.clearanceDate);
    }
    formData.append('customsDeclarationType', data.customsDeclarationType || 'E52');
    formData.append('customsChannel', String(data.customsChannel));
    if (data.customsOffice) {
      formData.append('customsOffice', data.customsOffice);
    }
    formData.append('packageQty', String(data.packageQty || 0));
    formData.append('customsPackageQty', String(data.packageQty || 0));
    formData.append('grossWeight', String(data.grossWeight || 0));
    formData.append('customsGrossWeight', String(data.grossWeight || 0));
    formData.append('totalDap', String(data.totalDap || 0));
    formData.append('customsTotalDap', String(data.totalDap || 0));
    formData.append('totalCmt', String(data.totalCmt || 0));
    formData.append('customsTotalCmt', String(data.totalCmt || 0));
    formData.append('isFullyMatched', String(data.isFullyMatched));

    if (file) {
      formData.append('file', file);
      formData.append('customsFile', file);
    }

    const response = await apiClient.post<ConfirmSyncResponse>(
      '/customs/confirm-sync',
      formData,
      {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      }
    );
    return response.data;
  },

  /**
   * Tải xuống file tờ khai hải quan gốc đã đính kèm theo đơn hàng
   */
  downloadAttachment: async (
    orderId: number
  ): Promise<{ blob: Blob; fileName: string }> => {
    const response = await apiClient.get(`/customs/download/${orderId}`, {
      responseType: 'blob',
    });

    let fileName = `Customs_Declaration_${orderId}.xls`;
    const disposition = response.headers['content-disposition'];
    if (disposition && typeof disposition === 'string') {
      const match = /filename\*?=(?:UTF-8'')?["']?([^"';]+)["']?/i.exec(disposition);
      if (match && match[1]) {
        fileName = decodeURIComponent(match[1]);
      }
    }

    return {
      blob: response.data as Blob,
      fileName,
    };
  },
};
