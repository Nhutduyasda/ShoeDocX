import { apiClient } from './client';
import type {
  BomMasterOption,
  MaterialRequirementResult,
  SaveProductionOrderRequest,
  SavedProductionOrder,
  Material,
  SaveMaterialRequest,
  BomDefinition,
  SaveBomDefinitionRequest,
  ProductionOrderPlan,
} from '../types/bom';

export const bomApi = {
  async getMaterials(search?: string): Promise<Material[]> {
    const { data } = await apiClient.get<Material[]>('/materials', { params: { search } });
    return data;
  },

  async createMaterial(request: SaveMaterialRequest): Promise<Material> {
    const { data } = await apiClient.post<Material>('/materials', request);
    return data;
  },

  async updateMaterial(id: number, request: SaveMaterialRequest): Promise<Material> {
    const { data } = await apiClient.put<Material>(`/materials/${id}`, request);
    return data;
  },

  async deleteMaterial(id: number): Promise<void> {
    await apiClient.delete(`/materials/${id}`);
  },

  async getDefinitions(): Promise<BomDefinition[]> {
    const { data } = await apiClient.get<BomDefinition[]>('/bom/definitions');
    return data;
  },

  async createDefinition(request: SaveBomDefinitionRequest): Promise<BomDefinition> {
    const { data } = await apiClient.post<BomDefinition>('/bom/definitions', request);
    return data;
  },

  async updateDefinition(id: number, request: SaveBomDefinitionRequest): Promise<BomDefinition> {
    const { data } = await apiClient.put<BomDefinition>(`/bom/definitions/${id}`, request);
    return data;
  },

  async deleteDefinition(id: number): Promise<void> {
    await apiClient.delete(`/bom/definitions/${id}`);
  },

  async getMasters(): Promise<BomMasterOption[]> {
    const { data } = await apiClient.get<BomMasterOption[]>('/bom/masters');
    return data;
  },

  async createOrder(request: SaveProductionOrderRequest): Promise<SavedProductionOrder> {
    const { data } = await apiClient.post<SavedProductionOrder>('/bom/production-orders', request);
    return data;
  },

  async updateOrder(id: number, request: SaveProductionOrderRequest): Promise<SavedProductionOrder> {
    const { data } = await apiClient.put<SavedProductionOrder>(`/bom/production-orders/${id}`, request);
    return data;
  },

  async calculate(id: number): Promise<MaterialRequirementResult> {
    const { data } = await apiClient.post<MaterialRequirementResult>(
      `/bom/production-orders/${id}/calculate`,
    );
    return data;
  },

  async getProductionOrders(): Promise<ProductionOrderPlan[]> {
    const { data } = await apiClient.get<ProductionOrderPlan[]>('/bom/production-orders');
    return data;
  },

  async approveOrder(id: number): Promise<ProductionOrderPlan> {
    const { data } = await apiClient.post<ProductionOrderPlan>(`/bom/production-orders/${id}/approve`);
    return data;
  },

  async issueOrder(id: number): Promise<ProductionOrderPlan> {
    const { data } = await apiClient.post<ProductionOrderPlan>(`/bom/production-orders/${id}/issue`);
    return data;
  },

  async cancelOrder(id: number): Promise<ProductionOrderPlan> {
    const { data } = await apiClient.post<ProductionOrderPlan>(`/bom/production-orders/${id}/cancel`);
    return data;
  },

  async downloadMaterialPlan(id: number, orderNo: string): Promise<void> {
    const response = await apiClient.get(`/bom/production-orders/${id}/export`, { responseType: 'blob' });
    const url = URL.createObjectURL(new Blob([response.data]));
    const link = document.createElement('a');
    link.href = url;
    link.download = `Phieu_cap_phat_${orderNo}.xlsx`;
    link.click();
    URL.revokeObjectURL(url);
  },
};
