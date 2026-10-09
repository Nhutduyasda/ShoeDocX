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
  folderId?: number | null;
  folderName?: string | null;
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
  folderId?: number | null;
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
  folderId?: number | null;
}

export interface MasterDataFolder {
  id: number;
  name: string;
  parentId?: number | null;
  customerName?: string | null;
  deliveryAddress?: string | null;
  contractNo?: string | null;
  poSuffix?: string | null;
  defaultPairsPerCarton: number;
  defaultUnit?: string | null;
  displayOrder: number;
  invoiceNoPattern: string;
  fileNamePattern: string;
  currentSequenceNumber: number;
  productCount: number;
  totalProductCount: number;
  children?: MasterDataFolder[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateFolderRequest {
  name: string;
  parentId?: number | null;
  customerName?: string | null;
  deliveryAddress?: string | null;
  contractNo?: string | null;
  poSuffix?: string | null;
  defaultPairsPerCarton?: number;
  defaultUnit?: string | null;
  displayOrder?: number;
  invoiceNoPattern?: string;
  fileNamePattern?: string;
  currentSequenceNumber?: number;
}

export interface UpdateFolderRequest {
  name: string;
  parentId?: number | null;
  customerName?: string | null;
  deliveryAddress?: string | null;
  contractNo?: string | null;
  poSuffix?: string | null;
  defaultPairsPerCarton?: number;
  defaultUnit?: string | null;
  displayOrder?: number;
  invoiceNoPattern?: string;
  fileNamePattern?: string;
  currentSequenceNumber?: number;
}

export interface MoveFolderRequest {
  targetParentId?: number | null;
  displayOrder?: number;
}

export interface BulkMoveProductsRequest {
  productIds: number[];
  targetFolderId?: number | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  avgUnitPriceCMT?: number | null;
  avgUnitPriceDAP?: number | null;
}

export interface ExcelColumnInfo {
  index: number;
  columnLetter: string;
  headerName?: string | null;
  sampleValues: string[];
}

export interface DetectedMapping {
  styleCodeCol: number;
  poSuffixCol?: number | null;
  cmtPriceCol: number;
  dapPriceCol: number;
  descriptionCol: number;
  hsCodeCol?: number | null;
  unitCol?: number | null;
  pairsPerCartonCol?: number | null;
}

export interface PreviewRow {
  rowNumber: number;
  styleCode: string;
  description?: string | null;
  unitPriceCMT: number;
  unitPriceDAP: number;
  hsCode?: string | null;
  unit?: string | null;
  pairsPerCarton: number;
  isGo: boolean;
}

export interface ImportPreviewResponse {
  totalRows: number;
  startRowIndex: number;
  detectedMapping: DetectedMapping;
  availableColumns: ExcelColumnInfo[];
  previewRows: PreviewRow[];
}

export interface ColumnMappingOverride {
  styleCodeCol?: number;
  poSuffixCol?: number;
  cmtPriceCol?: number;
  dapPriceCol?: number;
  descriptionCol?: number;
  hsCodeCol?: number;
  unitCol?: number;
  pairsPerCartonCol?: number;
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

export function normalizeProcessType(val: unknown): ProcessType {
  if (
    val === ProcessType.GoKhongMay ||
    val === 2 ||
    val === '2' ||
    val === 'GoKhongMay' ||
    (typeof val === 'string' && val.toLowerCase().includes('go'))
  ) {
    return ProcessType.GoKhongMay;
  }
  return ProcessType.Standard;
}

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
  contractFolderId?: number | null;
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
  customsAttachmentFilePath?: string;
  isLocked?: boolean;
  status: ShipmentStatus;
  statusName?: string;
  items?: ShipmentOrderItem[];
}

export interface ShipmentOrderItem {
  description: string;
  unit: string;
  pairPerCarton: number;
  id: number;
  shipmentOrderId: number;
  styleCode: string;
  fullItemCode: string;
  quantity: number;
  processType: ProcessType;
  sizeBreakdownJson?: string | null;
  unitPriceCMT: number;
  unitPriceDAP: number;
}

export interface CreateShipmentItem {
  styleCode: string;
  fullItemCode?: string;
  description?: string;
  quantity: number;
  processType: ProcessType;
  sizeBreakdownJson?: string | null;
  unitPriceCMT?: number;
  unitPriceDAP?: number;
  unit?: string;
  pairPerCarton?: number;
}

export interface CreateShipmentRequest {
  orderId?: number;
  invoiceNo: string;
  invoiceDate: string;
  poSuffix: string;
  contractFolderId?: number | null;
  contractNo: string;
  customerName: string;
  address: string;
  deliveryTerms: string;
  paymentTerms: string;
  startInvoiceNumber?: number;
  priority?: ExportSequencePriority;
  templateId?: number;
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

export interface InvoicePreviewItem {
  lineNo: number;
  styleCode: string;
  fullItemCode: string;
  description: string;
  quantity: number;
  unit: string;
  unitPriceCMT: number;
  unitPriceDAP: number;
  amountCMT: number;
  amountDAP: number;
  cartonCount: number;
  pairsPerCarton: number;
  processType: ProcessType;
}

export interface InvoicePreview {
  sellerName: string;
  sellerAddress: string;
  sellerAddressLine1: string;
  sellerAddressLine2: string;
  buyerName: string;
  buyerAddress: string;
  buyerAddressLine1: string;
  buyerAddressLine2: string;
  invoiceNo: string;
  invoiceDate: string;
  contractNo: string;
  deliveryTerms: string;
  paymentTerms: string;
  destinationCountry: string;
  poSuffix: string;
  items: InvoicePreviewItem[];
  totalQuantity: number;
  totalAmountCMT: number;
  totalAmountDAP: number;
  totalAmountDAPInWords: string;
}

export interface DocumentPreviewResponse {
  invoice: InvoicePreview;
  packingList: PklPreviewResponse;
}

export interface SavedShipmentSummary {
  id: number;
  invoiceNo: string;
  invoiceDate: string;
  poSuffix: string;
  contractFolderId?: number | null;
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
  customsAttachmentFilePath?: string;
  isLocked?: boolean;
  status: ShipmentStatus;
  statusName?: string;
}

export interface ShipmentUnlockAudit {
  id: number;
  shipmentOrderId: number;
  reason: string;
  previousStatus: ShipmentStatus;
  unlockedByUserId?: string | null;
  unlockedByUserName?: string | null;
  unlockedAt: string;
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

export interface OcrSourceRegion {
  x?: number | null;
  y?: number | null;
  width?: number | null;
  height?: number | null;
}

export interface OcrDetectedDocument {
  documentId: string;
  title: string;
  items: OcrItem[];
  reportedTotal: number | null;
  calculatedTotal: number;
  hasReportedTotal: boolean;
  isTotalMatched: boolean;
  discrepancy?: number | null;
  sourceRegion?: OcrSourceRegion | null;
  hasStandardItems: boolean;
  hasGoItems: boolean;
  isManuallyConfirmed?: boolean;
  confirmationReason?: string;
}

export interface OcrExtractionResponse {
  documents: OcrDetectedDocument[];
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
  customsHasCmt: boolean;
  isMatched: boolean;
  isPriceMatched: boolean;
  statusText: string;
}

export interface MatchedOrderSummary {
  id: number;
  invoiceNo: string;
  invoiceDate: string;
  poSuffix?: string;
  contractFolderId?: number | null;
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
  isInvoiceMismatch?: boolean;
  invoiceMismatchWarning?: string;
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
// CUSTOMS SETTLEMENT (ĐỐI CHIẾU NỘI BỘ)
// ==========================================

export interface SettlementItem {
  id: number;
  productCode: string;
  processType: ProcessType;
  productName: string;
  unit: string;
  hsCode?: string;
  openingBalance: number;
  inPeriodProduction: number;
  inPeriodExport: number;
  otherExport: number;
  closingBalance: number;
  isNegative?: boolean;
  discrepancy?: number;
  note?: string;
  exportedOrderCount: number;
  relatedDeclarationNos: string[];
}

export interface WarehouseDataRow {
  productCode: string;
  processType: ProcessType;
  openingBalance: number;
  inPeriodProduction: number;
}

export interface WarehouseImportResult {
  matchedCount: number;
  addedFromWarehouseCount: number;
  totalRows: number;
  negativeItemCount: number;
  items: SettlementItem[];
  warnings: string[];
}

export interface MatchWarehouseDataRequest {
  rows: WarehouseDataRow[];
  currentItems: SettlementItem[];
}


export interface SettlementReport {
  periodId?: number;
  version?: number;
  year: number;
  fromDate: string;
  toDate: string;
  contractFolderId?: number | null;
  contractNo?: string;
  customsOffice?: string;
  status?: 'Draft' | 'Finalized';
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
  contractFolderId?: number | null;
  contractNo?: string;
  customsOffice?: string;
}

export interface SaveSettlementPeriodRequest {
  id?: number;
  expectedVersion?: number;
  year: number;
  fromDate: string;
  toDate: string;
  contractFolderId?: number | null;
  contractNo?: string;
  customsOffice?: string;
  status?: 'Draft' | 'Finalized';
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
  contractFolderId?: number | null;
  contractNo?: string;
  customsOffice?: string;
  status?: 'Draft' | 'Finalized';
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
  contractFolderId?: number | null;
  contractNo?: string;
  productCode: string;
  fullItemCode: string;
  quantity: number;
  unitPriceCMT: number;
  unitPriceDAP: number;
  customerName: string;
}

// ==========================================
// EXPORT ANALYTICS & REVENUE TYPES
// ==========================================

export interface MonthlyExportStat {
  month: number;
  monthName: string;
  quantity: number;
  totalDap: number;
  totalCmt: number;
  orderCount: number;
}

export interface TopExportStyle {
  rank: number;
  styleCode: string;
  productName: string;
  quantity: number;
  totalDap: number;
  totalCmt: number;
  percentage: number;
}

export interface CustomsChannelStat {
  greenCount: number;
  yellowCount: number;
  redCount: number;
  totalDeclarations: number;
  greenPercentage: number;
  yellowPercentage: number;
  redPercentage: number;
}

export interface AnalyticsExportStats {
  year: number;
  totalQuantity: number;
  totalDap: number;
  totalCmt: number;
  clearedOrderCount: number;
  monthlyStats: MonthlyExportStat[];
  topStyles: TopExportStyle[];
  channelStats: CustomsChannelStat;
}

// ==========================================
// BATCH OCR TYPES
// ==========================================

export interface BatchOcrImageResult {
  clientFileId: string;
  fileName: string;
  isSuccess: boolean;
  errorMessage?: string;
  documents: OcrDetectedDocument[];
}

export interface BatchScanItemExport {
  batchId: string;
  title: string;
  items: CreateShipmentItem[];
}

export interface BatchOcrConfirmRequest {
  poSuffix?: string;
  contractFolderId?: number | null;
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

// ==========================================
// KIỂM TRA CHÉO & PHÁT HIỆN NHẦM ĐỐI TÁC (PARTNER MISMATCH)
// ==========================================

export interface ValidateItemsRequest {
  currentPartnerFolderId: number;
  styleCodes: string[];
}

export interface ItemValidationDetail {
  rawCode: string;
  normalizedCode: string;
  isMatchedInCurrent: boolean;
  matchedFolderId?: number | null;
  matchedFolderName?: string | null;
  matchedProduct?: ProductMaster | null;
}

export interface ValidateItemsResult {
  hasMismatch: boolean;
  suggestedPartnerFolderId?: number | null;
  suggestedPartnerName?: string | null;
  matchedCountInSuggested: number;
  totalCodes: number;
  details: ItemValidationDetail[];
}

// ==========================================
// CẤU HÌNH BIỂU MẪU XUẤT EXCEL (DOCUMENT TEMPLATE / BYOT)
// ==========================================

export interface InvHeaderCells {
  invoiceNoCell: string;
  dateCell: string;
  contractNoCell: string;
  buyerNameCell: string;
  buyerAddressCell: string;
  deliveryTermsCell: string;
  paymentTermsCell: string;
  destinationCell?: string;
}

export interface InvTableColumns {
  startRow: number;
  itemCodeCol: string;
  descriptionCol: string;
  quantityCol: string;
  unitCol: string;
  cmtUnitPriceCol: string;
  dapUnitPriceCol: string;
  cmtAmountCol: string;
  dapAmountCol: string;
}

export interface InvSheetConfig {
  sheetName: string;
  header: InvHeaderCells;
  table: InvTableColumns;
  totalAmountCell?: string;
  wordsAmountCell?: string;
}

export interface PklSheetConfig {
  sheetName: string;
  startRow: number;
  cartonRangeCol: string;
  itemCodeCol: string;
  descriptionCol: string;
  quantityCol: string;
  unitCol: string;
  cartonsCol: string;
  netWeightCol: string;
  grossWeightCol: string;
}

export interface DocumentTemplateConfig {
  templateName: string;
  invSheet: InvSheetConfig;
  pklSheet: PklSheetConfig;
}

export interface CompanyTemplate {
  id: number;
  name: string;
  templateFileName: string;
  templateFilePath: string;
  configJson: string;
  config?: DocumentTemplateConfig;
  isDefault: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface AiTemplateAnalysisResponse {
  detectedName: string;
  config: DocumentTemplateConfig;
  textGrid: string;
  isAiAnalyzed: boolean;
  remainingCredits?: number;
}

export interface TenantAiCreditsInfo {
  tenantId: string;
  companyName: string;
  aiCredits: number;
}


