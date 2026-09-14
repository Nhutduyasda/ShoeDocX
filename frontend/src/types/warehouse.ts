export type WarehouseBatchStatus = 'Draft' | 'SubmittedToXnk' | 'ProcessedByXnk';

export interface WarehouseBatchItem {
  id?: number;
  styleCode: string;
  quantity: number;
  processType: number; // 1: Standard (Thành hình), 2: GoKhongMay (Gò không may)
  processTypeName?: string;
  isPendingReview?: boolean;
  displayOrder?: number;
  note?: string;
}

export interface WarehouseBatch {
  id: number;
  batchName: string;
  batchNumber: string;
  exportDate: string;
  contractNote: string;
  contractFolderId?: number | null;
  contractFolderName?: string | null;
  status: WarehouseBatchStatus;
  statusText: string;
  totalQuantity: number;
  shipmentOrderId?: number | null;
  createdBy?: string | null;
  createdAt: string;
  submittedAt?: string | null;
  version: number;
  items: WarehouseBatchItem[];
}

export interface WarehouseBatchSummary {
  id: number;
  batchName: string;
  batchNumber: string;
  exportDate: string;
  contractNote: string;
  status: WarehouseBatchStatus;
  statusText: string;
  totalQuantity: number;
  itemCount: number;
  goCount: number;
  thanhHinhCount: number;
  createdAt: string;
  submittedAt?: string | null;
  version: number;
}

export interface SaveWarehouseBatchRequest {
  id?: number;
  batchNumber: string;
  exportDate: string;
  contractNote: string;
  contractFolderId?: number | null;
  expectedVersion?: number;
  items: {
    styleCode: string;
    quantity: number;
    processType: number;
    isPendingReview?: boolean;
    note?: string;
  }[];
}

export interface ProductLookupItem {
  id: number;
  styleCode: string;
  description?: string;
  customer?: string;
  folderId?: number | null;
  folderName?: string | null;
  unitPriceCMT?: number;
  unitPriceDAP?: number;
  unitPriceCMT_Go?: number;
  unitPriceDAP_Go?: number;
  unitPriceGoKhongMay?: number;
  hasStandardPrice?: boolean;
  hasGoPrice?: boolean;
}
