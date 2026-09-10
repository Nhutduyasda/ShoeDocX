import { apiClient } from './client';
import type { OcrExtractionResponse } from '../types';

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
};
