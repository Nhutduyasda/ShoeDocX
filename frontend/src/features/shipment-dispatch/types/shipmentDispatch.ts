import type { CreateShipmentItem, PklBreakdownItem, ProcessType } from '../../../types';

export interface SubInvoiceAllocation { invoiceSuffixTitle: string; items: CreateShipmentItem[] }
export interface DispatchSourceDocument {
  sourceType: 'ocr-document'; documentId: string; title: string; items: CreateShipmentItem[];
  calculatedTotal: number; reportedTotal?: number | null; sourceFileName?: string; clientFileId?: string;
  isManuallyConfirmed?: boolean; confirmationReason?: string;
}
export interface OcrDispatchSourcePayload {
  documentId: string; title: string; items: CreateShipmentItem[]; sourceFileName?: string; clientFileId?: string;
  reportedTotal?: number | null; calculatedTotal: number;
  isManuallyConfirmed?: boolean; confirmationReason?: string;
}
export interface ConsolidatedDispatchSource {
  sourceType: 'merged-ocr-documents'; sourceDocumentIds: string[]; sourceTitles: string[];
  sourceDocuments: OcrDispatchSourcePayload[]; title: string; items: CreateShipmentItem[];
  totalQuantity: number; totalCartons?: number;
}
export interface ValidateSplitRequest { sourceBatchId?: number; sourceDocument?: OcrDispatchSourcePayload; sourceDocuments?: OcrDispatchSourcePayload[]; subInvoices: SubInvoiceAllocation[] }
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
  isExportable: boolean; generatedDocumentCount: number; sourceItemCount: number; mergedItemCount: number; consolidatedItemCount: number;
  totalQuantity: number; totalCartons: number; mergedItems: CreateShipmentItem[];
  pklBreakdown: PklBreakdownItem[]; warnings: DispatchValidationMessage[]; blockingErrors: DispatchValidationMessage[];
  processGroups: { processType: ProcessType; itemCount: number; totalQuantity: number }[];
}
export interface DownloadResult { blob: Blob; fileName: string; contentType: string }
export interface ApiDownloadError { code?: string; message: string; title?: string; detail?: string; traceId?: string; validationErrors: string[] }
