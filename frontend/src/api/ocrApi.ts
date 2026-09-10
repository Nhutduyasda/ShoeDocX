import { apiClient } from './client';
import type {
  OcrExtractionResponse,
  BatchOcrScanResult,
  BatchOcrConfirmRequest,
} from '../types';

export const ocrApi = {
  // Gửi ảnh phiếu kho lên backend để nhận diện OCR qua Vision AI
  extractFromImage: async (file: File | Blob): Promise<OcrExtractionResponse> => {
    const formData = new FormData();
    formData.append('file', file, 'receipt.jpg');

    const response = await apiClient.post<OcrExtractionResponse>('/ocr/extract', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });

    return response.data;
  },

  // Gửi nhiều ảnh phiếu kho để nhận diện OCR hàng loạt (Batch Upload)
  batchExtract: async (files: (File | Blob)[]): Promise<BatchOcrScanResult[]> => {
    const formData = new FormData();
    files.forEach((file, index) => {
      const fileName = file instanceof File ? file.name : `receipt_${index + 1}.jpg`;
      formData.append('files', file, fileName);
    });

    const response = await apiClient.post<BatchOcrScanResult[]>('/ocr/batch-extract', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });

    return response.data;
  },

  // Xác nhận xuất toàn bộ lô các đợt ra file ZIP nén
  batchExportZip: async (data: BatchOcrConfirmRequest): Promise<Blob> => {
    const response = await apiClient.post('/ocr/batch-export-zip', data, {
      responseType: 'blob',
    });
    return response.data;
  },
};
