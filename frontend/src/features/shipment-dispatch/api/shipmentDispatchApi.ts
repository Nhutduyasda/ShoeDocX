import type { AxiosResponse } from 'axios';
import { apiClient } from '../../../api/client';
import type { ApiDownloadError, DownloadResult, MergeShipmentPreviewResponse, MergeShipmentRequest, SplitShipmentRequest, ValidateSplitRequest, ValidateSplitResult } from '../types/shipmentDispatch';

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

export async function parseDownloadError(error: unknown): Promise<ApiDownloadError> {
  const responseData = (error as { response?: { data?: unknown } })?.response?.data;
  let value: unknown = responseData ?? error;
  if (value instanceof Blob) {
    const text = await value.text();
    try { value = JSON.parse(text); } catch { value = text; }
  }
  if (typeof value === 'string') return { message: value || 'Không thể tải file.', validationErrors: [] };
  const body = (value && typeof value === 'object' ? value : {}) as Record<string, unknown>;
  const errors = body.errors && typeof body.errors === 'object'
    ? Object.values(body.errors as Record<string, unknown>).flatMap(v => Array.isArray(v) ? v.map(String) : [String(v)]) : [];
  const details = Array.isArray(body.details) ? body.details.map(item => typeof item === 'object' && item && 'message' in item ? String((item as { message: unknown }).message) : String(item)) : [];
  return { code: body.code ? String(body.code) : undefined, title: body.title ? String(body.title) : undefined,
    detail: body.detail ? String(body.detail) : undefined, traceId: body.traceId ? String(body.traceId) : undefined,
    message: String(body.message ?? body.detail ?? body.title ?? (error as Error)?.message ?? 'Không thể tải file.'), validationErrors: [...errors, ...details] };
}
