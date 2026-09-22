import type { CreateShipmentItem, PklBreakdownItem, ProcessType } from '../../../types';

export interface SubInvoiceAllocation { invoiceSuffixTitle: string; items: CreateShipmentItem[] }
export interface DispatchSourceDocument {
  sourceType: 'ocr-document'; documentId: string; title: string; items: CreateShipmentItem[];
  calculatedTotal: number; reportedTotal?: number | null; sourceFileName?: string; clientFileId?: string;
}
export interface OcrDispatchSourcePayload {
  documentId: string; title: string; items: CreateShipmentItem[]; sourceFileName?: string; clientFileId?: string;
}
export interface ValidateSplitRequest { sourceBatchId?: number; sourceDocument?: OcrDispatchSourcePayload; subInvoices: SubInvoiceAllocation[] }
export interface SplitShipmentRequest extends ValidateSplitRequest {
  contractFolderId?: number; templateId?: number; poSuffix?: string; invoiceDate: string;
}
export interface DispatchValidationMessage { code: string; message: string; styleCode?: string }
export interface ItemAllocationCheck {
  styleCode: string; processType: ProcessType; originalQty: number; allocatedQty: number;
  discrepancy: number; isMatched: boolean;
}
export interface ValidateSplitResult {
  isValid: boolean; originalTotal: number; allocatedTotal: number; discrepancy: number;
  errors: DispatchValidationMessage[]; warnings: DispatchValidationMessage[]; itemChecks: ItemAllocationCheck[];
}
export interface MergeShipmentRequest {
  sourceBatchIds?: number[]; sourceDocuments?: OcrDispatchSourcePayload[]; contractFolderId?: number; poSuffix?: string; invoiceNo?: string;
  invoiceDate: string; templateId?: number;
}
export interface MergeShipmentPreviewResponse {
  totalQuantity: number; totalCartons: number; mergedItems: CreateShipmentItem[];
  pklBreakdown: PklBreakdownItem[]; warnings: DispatchValidationMessage[];
}
export interface DownloadResult { blob: Blob; fileName: string; contentType: string }
