export type MaterialType = 'Common' | 'SizeDependent' | 0 | 1;
export type ProcessType = 'Standard' | 'GoKhongMay' | 1 | 2;

export interface Material {
  id: number;
  materialCode: string;
  materialName: string;
  unit: string;
  materialType: MaterialType;
  currentStock: number;
}

export type SaveMaterialRequest = Omit<Material, 'id'>;

export interface BomDefinitionItem {
  id: number;
  materialId: number;
  materialCode: string;
  materialName: string;
  unit: string;
  materialType: MaterialType;
  netConsumption: number;
  wastageRatePercent: number;
}

export interface BomDefinition {
  id: number;
  styleCode: string;
  processType: ProcessType;
  version: string;
  description?: string;
  createdAt: string;
  items: BomDefinitionItem[];
}

export interface SaveBomDefinitionRequest {
  styleCode: string;
  processType: ProcessType;
  version: string;
  description?: string;
  items: Array<Pick<BomDefinitionItem, 'materialId' | 'netConsumption' | 'wastageRatePercent'>>;
}

export interface BomMasterOption {
  id: number;
  styleCode: string;
  version: string;
  description?: string;
  processType: ProcessType;
}

export interface OrderSizeRunInput {
  sizeName: string;
  quantity: number;
}

export interface SaveProductionOrderRequest {
  orderNo: string;
  styleCode: string;
  processType: ProcessType;
  sizeRuns: OrderSizeRunInput[];
}

export interface SavedProductionOrder {
  id: number;
  orderNo: string;
  styleCode: string;
  processType: ProcessType;
  status: ProductionOrderStatus;
  totalQuantity: number;
}

export interface SizeMaterialRequirement {
  sizeName: string;
  orderQuantity: number;
  requiredQuantity: number;
}

export interface MaterialRequirementItem {
  materialId: number;
  materialCode: string;
  materialName: string;
  unit: string;
  materialType: MaterialType;
  netConsumption: number;
  wastageRatePercent: number;
  totalRequiredQuantity: number;
  currentStock: number;
  shortageQuantity: number;
  sizeBreakdown: SizeMaterialRequirement[];
}

export type ProductionOrderStatus = 'Draft' | 'Calculated' | 'Approved' | 'Issued' | 'Cancelled' | 0 | 1 | 2 | 3 | 4;

export interface ProductionOrderPlanItem {
  materialId: number;
  materialCode: string;
  materialName: string;
  unit: string;
  materialType: MaterialType;
  requiredQuantity: number;
  currentStock: number;
  shortageQuantity: number;
  sizeBreakdown: SizeMaterialRequirement[];
}

export interface ProductionOrderPlan {
  id: number;
  orderNo: string;
  styleCode: string;
  processType: ProcessType;
  status: ProductionOrderStatus;
  totalQuantity: number;
  bomMasterId?: number;
  bomVersion?: string;
  calculatedAt?: string;
  approvedAt?: string;
  issuedAt?: string;
  materials: ProductionOrderPlanItem[];
}

export interface MaterialRequirementResult {
  productionOrderId: number;
  orderNo: string;
  styleCode: string;
  bomMasterId: number;
  bomVersion: string;
  totalQuantity: number;
  materials: MaterialRequirementItem[];
}
