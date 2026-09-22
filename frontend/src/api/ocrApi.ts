import { apiClient } from './client';
import type {
  OcrExtractionResponse,
  BatchOcrImageResult,
  BatchOcrConfirmRequest,
} from '../types';

export const ocrApi = {
  // Gửi ảnh phiếu kho lên backend để nhận diện OCR qua Vision AI
  extractFromImage: async (file: File | Blob): Promise<OcrExtractionResponse> => {
    const formData = new FormData();
    // Preserve the original File/Blob bytes. Preview URLs and UI zoom never enter the OCR request pipeline.
    const fileName = file instanceof File ? file.name : 'receipt.jpg';
    formData.append('file', file, fileName);

    const response = await apiClient.post<OcrExtractionResponse>('/ocr/extract', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });

    return response.data;
  },

  // Gửi nhiều ảnh phiếu kho để nhận diện OCR hàng loạt (Batch Upload)
  batchExtract: async (entries: { clientFileId: string; file: File | Blob }[]): Promise<BatchOcrImageResult[]> => {
    const formData = new FormData();
    entries.forEach(({ clientFileId, file }, index) => {
      const fileName = file instanceof File ? file.name : `receipt_${index + 1}.jpg`;
      formData.append('files', file, fileName);
      formData.append('clientFileIds', clientFileId);
    });

    const response = await apiClient.post<BatchOcrImageResult[]>('/ocr/batch-extract', formData, {
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
