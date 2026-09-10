import { apiClient } from './client';
import type {
  CalculateSettlementRequest,
  SaveSettlementPeriodRequest,
  SettlementPeriodSummary,
  SettlementReport,
} from '../types';

export const settlementApi = {
  /**
   * Tổng hợp số liệu quyết toán Mẫu 16 từ các đơn hàng E52 đã thông quan trong kỳ
   */
  calculateSettlement: async (
    params: CalculateSettlementRequest
  ): Promise<SettlementReport> => {
    const response = await apiClient.post<SettlementReport>(
      '/customs-settlement/calculate',
      params
    );
    return response.data;
  },

  /**
   * Lưu kỳ báo cáo quyết toán vào cơ sở dữ liệu
   */
  saveSettlement: async (
    data: SaveSettlementPeriodRequest
  ): Promise<any> => {
    const response = await apiClient.post(
      '/customs-settlement/save',
      data
    );
    return response.data;
  },

  /**
   * Lấy danh sách các kỳ quyết toán đã lưu
   */
  getSettlementPeriods: async (): Promise<SettlementPeriodSummary[]> => {
    const response = await apiClient.get<SettlementPeriodSummary[]>(
      '/customs-settlement/periods'
    );
    return response.data;
  },

  /**
   * Xem chi tiết kỳ quyết toán theo ID
   */
  getSettlementPeriodById: async (id: number): Promise<SettlementReport> => {
    const response = await apiClient.get<SettlementReport>(
      `/customs-settlement/periods/${id}`
    );
    return response.data;
  },

  /**
   * Xuất file Excel chuẩn Mẫu 16/BCQT-SP-GSQL (Thông tư 39/2018/TT-BTC)
   */
  exportSettlementExcel: async (report: SettlementReport): Promise<Blob> => {
    const response = await apiClient.post(
      '/customs-settlement/export-excel',
      report,
      {
        responseType: 'blob',
      }
    );
    return response.data;
  },

  /**
   * Xuất file Excel Mẫu 16 theo ID kỳ đã lưu
   */
  exportSettlementExcelById: async (periodId: number): Promise<Blob> => {
    const response = await apiClient.get(
      `/customs-settlement/export-excel/${periodId}`,
      {
        responseType: 'blob',
      }
    );
    return response.data;
  },

  /**
   * Xem danh sách chi tiết các tờ khai cấu thành số lượng xuất của mã sản phẩm
   */
  getDrillDown: async (
    productCode: string,
    from: string,
    to: string,
    contractNo?: string
  ) => {
    const params: Record<string, string> = { productCode, from, to };
    if (contractNo) params.contractNo = contractNo;

    const response = await apiClient.get<import('../types').SettlementDrillDownItem[]>(
      '/customs-settlement/drilldown',
      { params }
    );
    return response.data;
  },
};

