import { apiClient } from './client';
import type {
  CalculateSettlementRequest,
  SaveSettlementPeriodRequest,
  SettlementPeriodSummary,
  SettlementReport,
  SettlementItem,
  WarehouseDataRow,
  WarehouseImportResult,
} from '../types';

export const settlementApi = {
  /**
   * Tổng hợp số liệu quyết toán đối chiếu nội bộ từ các đơn hàng E52 đã thông quan trong kỳ
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

  finalizeSettlement: async (id: number): Promise<any> => {
    const response = await apiClient.post(`/customs-settlement/${id}/finalize`);
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
   * Xuất file Excel chuẩn đối chiếu nội bộ (Cần xác minh mẫu pháp lý trước khi nộp)
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
   * Xuất file Excel đối chiếu nội bộ theo ID kỳ đã lưu
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

  /**
   * Lấy dữ liệu thống kê phân tích kim ngạch, sản lượng, doanh thu CMT và phân luồng thông quan
   */
  getExportAnalytics: async (year: number): Promise<import('../types').AnalyticsExportStats> => {
    const response = await apiClient.get<import('../types').AnalyticsExportStats>(
      '/analytics/export-stats',
      { params: { year } }
    );
    return response.data;
  },

  /**
   * Nạp file Excel số liệu kho định kỳ và tự động đối soát với danh sách thực xuất
   */
  importWarehouseData: async (
    file: File,
    currentItems: SettlementItem[]
  ): Promise<WarehouseImportResult> => {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('currentItems', JSON.stringify(currentItems));

    const response = await apiClient.post<WarehouseImportResult>(
      '/customs-settlement/import-warehouse-data',
      formData,
      {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      }
    );
    return response.data;
  },

  /**
   * Khớp dữ liệu kho từ danh sách dòng (nhập tay hoặc dán từ clipboard)
   */
  matchWarehouseData: async (
    rows: WarehouseDataRow[],
    currentItems: SettlementItem[]
  ): Promise<WarehouseImportResult> => {
    const response = await apiClient.post<WarehouseImportResult>(
      '/customs-settlement/match-warehouse-data',
      {
        rows,
        currentItems,
      }
    );
    return response.data;
  },
};


