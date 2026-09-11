export interface ProductMaster {
  id: number;
  styleCode: string;
  description: string;
  unitPriceCMT: number;
  unitPriceDAP: number;
  /** Đơn giá CMT riêng cho hàng Gò không may (null = dùng chung unitPriceCMT) */
  unitPriceCMT_Go?: number | null;
  /** Đơn giá DAP riêng cho hàng Gò không may (null = dùng chung unitPriceDAP) */
  unitPriceDAP_Go?: number | null;
  hsCode: string;
  unit: string;
  pairPerCarton: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateProductMasterRequest {
  styleCode: string;
  description: string;
  unitPriceCMT: number;
  unitPriceDAP: number;
  unitPriceCMT_Go?: number | null;
  unitPriceDAP_Go?: number | null;
  hsCode: string;
  unit: string;
  pairPerCarton: number;
}

export interface UpdateProductMasterRequest {
  styleCode: string;
  description: string;
  unitPriceCMT: number;
  unitPriceDAP: number;
  unitPriceCMT_Go?: number | null;
  unitPriceDAP_Go?: number | null;
  hsCode: string;
  unit: string;
  pairPerCarton: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ImportErrorDetail {
  rowNumber: number;
  styleCode: string;
  message: string;
}

export interface ImportResult {
  success: boolean;
  totalRowsRead: number;
  importedCount: number;
  message: string;
  totalRows: number;
  createdCount: number;
  updatedCount: number;
  failedCount: number;
  errors: ImportErrorDetail[];
}

export const ProcessType = {
  Standard: 1,
  GoKhongMay: 2,
} as const;

export type ProcessType = typeof ProcessType[keyof typeof ProcessType];

export const ShipmentStatus = {
  Draft: 0,
  Exported: 1,
  Cleared: 2,
  Discrepancy: 3,
} as const;

export type ShipmentStatus = typeof ShipmentStatus[keyof typeof ShipmentStatus];

export const ExportSequencePriority = {
  StandardFirst: 1, // Thành hình trước, Gò sau
  GoFirst: 2,       // Gò trước, Thành hình sau
} as const;

export type ExportSequencePriority = typeof ExportSequencePriority[keyof typeof ExportSequencePriority];

export interface ShipmentOrder {
  id: number;
  invoiceNo: string;
  invoiceDate: string;
  poSuffix?: string;
  contractNo?: string;
  customerName: string;
  address?: string;
  deliveryTerms?: string;
  paymentTerms?: string;
  createdAt: string;
  declarationNo?: string;
  clearanceDate?: string;
  customsDeclarationType?: string;
  customsChannel?: number;
  customsOffice?: string;
  customsPackageQty?: number;
  customsGrossWeight?: number;
  customsTotalDap?: number;
  customsTotalCmt?: number;
  customsAttachmentFileName?: string;
  status: ShipmentStatus;
  statusName?: string;
  items?: ShipmentOrderItem[];
}

export interface ShipmentOrderItem {
  id: number;
  shipmentOrderId: number;
  styleCode: string;
  fullItemCode: string;
  quantity: number;
  processType: ProcessType;
  unitPriceCMT: number;
  unitPriceDAP: number;
}

export interface CreateShipmentItem {
  styleCode: string;
  fullItemCode?: string;
  description?: string;
  quantity: number;
  processType: ProcessType;
  unitPriceCMT?: number;
  unitPriceDAP?: number;
  unit?: string;
  pairPerCarton?: number;
}

export interface CreateShipmentRequest {
  invoiceNo: string;
  invoiceDate: string;
  poSuffix: string;
  contractNo: string;
  customerName: string;
  address: string;
  deliveryTerms: string;
  paymentTerms: string;
  startInvoiceNumber?: number;
  priority?: ExportSequencePriority;
  items: CreateShipmentItem[];
}

export interface PklBreakdownItem {
  styleCode: string;
  fullItemCode: string;
  description: string;
  processType: ProcessType;
  processTypeName: string;
  cartonRange: string;
  fromCarton: number;
  toCarton: number;
  cartonCount: number;
  pairsPerCarton: number;
  standardPairPerCarton?: number;
  quantity: number;
  isOddCarton: boolean;
  netWeight: number;
  grossWeight: number;
}

export interface PklPreviewResponse {
  invoiceNo: string;
  poSuffix: string;
  totalQuantity: number;
  totalCartons: number;
  totalNetWeight: number;
  totalGrossWeight: number;
  breakdownItems: PklBreakdownItem[];
}

export interface SavedShipmentSummary {
  id: number;
  invoiceNo: string;
  invoiceDate: string;
  poSuffix: string;
  contractNo: string;
  customerName: string;
  deliveryTerms: string;
  paymentTerms: string;
  createdAt: string;
  itemCount: number;
  totalQuantity: number;
  totalAmountCMT: number;
  totalAmountDAP: number;
  totalCartons: number;
  declarationNo?: string;
  clearanceDate?: string;
  customsDeclarationType?: string;
  customsChannel?: number;
  customsOffice?: string;
  customsPackageQty?: number;
  customsGrossWeight?: number;
  customsTotalDap?: number;
  customsTotalCmt?: number;
  customsAttachmentFileName?: string;
  status: ShipmentStatus;
  statusName?: string;
}

export interface OcrItem {
  styleCode: string;
  quantity: number;
  note: string;
  processType: ProcessType;
  unitPriceCMT: number;
  unitPriceDAP: number;
  pairPerCarton: number;
  description: string;
  unit: string;
  isMatched: boolean;
}

export interface OcrExtractionResponse {
  title: string;
  items: OcrItem[];
  reportedTotal: number;
  calculatedTotal: number;
  isTotalMatched: boolean;
  isSimulation: boolean;
  rawJsonResponse?: string;
  message?: string;
}

/** Thông tin tổng kết kết quả xuất file Excel (từ response header X-Export-Info) */
export interface ExportSummary {
  hasTwoFiles: boolean;
  // Khi có 2 file (tách Gò + Thành hình)
  goFileName?: string;
  goInvoiceNo?: string;
  goTotalQuantity?: number;
  goSequenceNumber?: number;
  standardFileName?: string;
  standardInvoiceNo?: string;
  standardTotalQuantity?: number;
  standardSequenceNumber?: number;
  // Khi có 1 file duy nhất
  singleFileName?: string;
  singleInvoiceNo?: string;
  singleTotalQuantity?: number;
}

/** Thông tin số thứ tự hiện tại từ API GET /api/shipments/sequence/current */
export interface SequenceInfo {
  nextNumber: number;
  previewInvoiceNo: string;
  previewFileName: string;
}

// ==========================================
// CUSTOMS CLEARANCE & RECONCILIATION TYPES
// ==========================================

export interface CustomsDeclarationItem {
  lineNumber: number;
  hsCode: string;
  styleCode: string;
  rawDescription?: string;
  processType: ProcessType;
  quantity: number;
  unit: string;
  unitPriceDap: number;
  amountDap: number;
  unitPriceCmt: number;
}

export interface CustomsDeclarationParsed {
  declarationNo: string;
  clearanceDate?: string;
  customsDeclarationType: string;
  customsChannel: number;
  customsChannelName: string;
  customsOffice?: string;
  invoiceNo: string;
  packageQty: number;
  grossWeight: number;
  totalDap: number;
  totalCmt: number;
  totalItemQuantity: number;
  fileName: string;
  items: CustomsDeclarationItem[];
}

export interface CustomsComparisonRow {
  index: number;
  styleCode: string;
  processType: ProcessType;
  invoiceQuantity: number;
  customsQuantity: number;
  differenceQuantity: number;
  invoicePriceDap: number;
  customsPriceDap: number;
  invoicePriceCmt: number;
  customsPriceCmt: number;
  isMatched: boolean;
  isPriceMatched: boolean;
  statusText: string;
}

export interface MatchedOrderSummary {
  id: number;
  invoiceNo: string;
  invoiceDate: string;
  poSuffix?: string;
  contractNo?: string;
  customerName: string;
  totalQuantity: number;
  totalAmountDap: number;
  totalAmountCmt: number;
  currentStatus: ShipmentStatus;
}

export interface CustomsReconciliationResult {
  isOrderFound: boolean;
  isFullyMatched: boolean;
  totalQuantityMatched: boolean;
  totalDapMatched: boolean;
  totalCmtMatched: boolean;
  message: string;
  discrepancies: string[];
  matchedOrder?: MatchedOrderSummary;
  declaration: CustomsDeclarationParsed;
  comparisonRows: CustomsComparisonRow[];
}

export interface ConfirmCustomsSyncRequest {
  declarationNo: string;
  clearanceDate?: string;
  customsDeclarationType: string;
  customsChannel: number;
  customsOffice?: string;
  packageQty: number;
  grossWeight: number;
  totalDap: number;
  totalCmt: number;
  isFullyMatched: boolean;
}

// ==========================================
// CUSTOMS SETTLEMENT (MẪU 16/BCQT-SP-GSQL)
// ==========================================

export interface SettlementItem {
  id: number;
  productCode: string;
  productName: string;
  unit: string;
  openingBalance: number;
  inPeriodProduction: number;
  inPeriodExport: number;
  otherExport: number;
  closingBalance: number;
  note?: string;
  exportedOrderCount: number;
  relatedDeclarationNos: string[];
}

export interface SettlementReport {
  periodId?: number;
  year: number;
  fromDate: string;
  toDate: string;
  contractNo?: string;
  companyName: string;
  taxCode: string;
  address: string;
  note?: string;
  items: SettlementItem[];
  totalOpeningBalance: number;
  totalInPeriodProduction: number;
  totalInPeriodExport: number;
  totalOtherExport: number;
  totalClosingBalance: number;
  clearedOrderCount: number;
}

export interface CalculateSettlementRequest {
  year: number;
  fromDate: string;
  toDate: string;
  contractNo?: string;
}

export interface SaveSettlementPeriodRequest {
  id?: number;
  year: number;
  fromDate: string;
  toDate: string;
  contractNo?: string;
  companyName?: string;
  taxCode?: string;
  address?: string;
  note?: string;
  items: SettlementItem[];
}

export interface SettlementPeriodSummary {
  id: number;
  year: number;
  fromDate: string;
  toDate: string;
  contractNo?: string;
  createdAt: string;
  itemCount: number;
  totalExportQuantity: number;
  totalClosingBalance: number;
}

export interface SettlementDrillDownItem {
  orderId: number;
  declarationNo: string;
  clearanceDate?: string;
  invoiceNo: string;
  contractNo?: string;
  productCode: string;
  fullItemCode: string;
  quantity: number;
  unitPriceCMT: number;
  unitPriceDAP: number;
  customerName: string;
}

// ==========================================
// BATCH OCR TYPES
// ==========================================

export interface BatchOcrScanResult {
  batchId: string;
  fileName: string;
  title: string;
  reportedTotal: number;
  calculatedTotal: number;
  isMatched: boolean;
  discrepancy: number;
  items: OcrItem[];
  hasStandardItems: boolean;
  hasGoItems: boolean;
  isSuccess: boolean;
  errorMessage?: string;
}

export interface BatchScanItemExport {
  batchId: string;
  title: string;
  items: CreateShipmentItem[];
}

export interface BatchOcrConfirmRequest {
  poSuffix?: string;
  contractNo?: string;
  customerName?: string;
  address?: string;
  deliveryTerms?: string;
  paymentTerms?: string;
  invoiceDate?: string;
  startInvoiceNumber?: number;
  priority?: ExportSequencePriority;
  batches: BatchScanItemExport[];
}




