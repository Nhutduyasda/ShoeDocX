export interface ProductMaster {
  id: number;
  styleCode: string;
  description: string;
  unitPriceCMT: number;
  unitPriceDAP: number;
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
  hsCode: string;
  unit: string;
  pairPerCarton: number;
}

export interface UpdateProductMasterRequest {
  styleCode: string;
  description: string;
  unitPriceCMT: number;
  unitPriceDAP: number;
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

