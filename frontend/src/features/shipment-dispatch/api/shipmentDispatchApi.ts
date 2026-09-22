import type { AxiosResponse } from 'axios';
import { apiClient } from '../../../api/client';
import type { DownloadResult, MergeShipmentPreviewResponse, MergeShipmentRequest, SplitShipmentRequest, ValidateSplitRequest, ValidateSplitResult } from '../types/shipmentDispatch';

function decodeFileName(response: AxiosResponse<Blob>, fallback: string): string {
  const disposition = String(response.headers['content-disposition'] ?? '');
  const utf = disposition.match(/filename\*=UTF-8''([^;]+)/i);
  const plain = disposition.match(/filename="?([^";]+)"?/i);
  try { return decodeURIComponent(utf?.[1] ?? plain?.[1] ?? fallback); } catch { return plain?.[1] ?? fallback; }
}

async function download(url: string, data: unknown, fallback: string): Promise<DownloadResult> {
  const response = await apiClient.post<Blob>(url, data, { responseType: 'blob' });
  return { blob: response.data, fileName: decodeFileName(response, fallback), contentType: String(response.headers['content-type'] ?? '') };
}

export const shipmentDispatchApi = {
  validateSplit: async (data: ValidateSplitRequest) => (await apiClient.post<ValidateSplitResult>('/shipments/split-validate', data)).data,
  exportSplitZip: (data: SplitShipmentRequest) => download('/shipments/split-export-zip', data, `Bo_hoa_don_${data.invoiceDate}.zip`),
  previewMerge: async (data: MergeShipmentRequest) => (await apiClient.post<MergeShipmentPreviewResponse>('/shipments/merge-preview', data)).data,
  exportMerge: (data: MergeShipmentRequest) => download('/shipments/merge-export', data, `Hoa_don_gom_${data.invoiceDate}.xlsx`),
};

export function triggerDownload(result: DownloadResult) {
  const url = URL.createObjectURL(result.blob);
  const anchor = document.createElement('a');
  anchor.href = url; anchor.download = result.fileName; document.body.appendChild(anchor); anchor.click(); anchor.remove();
  URL.revokeObjectURL(url);
}

export async function readBlobValidation(error: unknown): Promise<ValidateSplitResult | null> {
  const response = (error as { response?: { data?: unknown } })?.response;
  if (!(response?.data instanceof Blob)) return null;
  try { return JSON.parse(await response.data.text()) as ValidateSplitResult; } catch { return null; }
}
