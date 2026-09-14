import React, { useState, useEffect, useMemo, useCallback } from 'react';
import {
  Table,
  Button,
  DatePicker,
  Select,
  Input,
  InputNumber,
  Tag,
  Tooltip,
  message,
  Drawer,
  Modal,
  Form,
  Tabs,
  Progress,
  Popconfirm,
  Upload,
  Radio,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  FileExcelOutlined,
  SaveOutlined,
  HistoryOutlined,
  CalculatorOutlined,
  SearchOutlined,
  WarningOutlined,
  ReloadOutlined,
  FolderOpenOutlined,
  InfoCircleOutlined,
  EyeOutlined,
  DownloadOutlined,
  LockOutlined,
  CheckCircleOutlined,
  BarChartOutlined,
  AuditOutlined,
  RiseOutlined,
  DollarCircleOutlined,
  SafetyCertificateOutlined,
  TrophyOutlined,
  UploadOutlined,
  SnippetsOutlined,
  FilterOutlined,
} from '@ant-design/icons';
import dayjs, { Dayjs } from 'dayjs';
import { masterDataFolderApi } from '../api/masterDataFolderApi';
import type { MasterDataFolder } from '../types';
import { ProcessType, normalizeProcessType } from '../types';
import { SettlementTreePanel } from '../components/SettlementTreePanel';
import { settlementApi } from '../api/settlementApi';
import type {
  SettlementItem,
  SettlementReport,
  SettlementPeriodSummary,
  SaveSettlementPeriodRequest,
  SettlementDrillDownItem,
  AnalyticsExportStats,
  WarehouseDataRow,
  WarehouseImportResult,
} from '../types';

const { RangePicker } = DatePicker;

export const CustomsSettlementPage: React.FC = () => {
  const currentYear = dayjs().year();
  const [activeTab, setActiveTab] = useState<string>('settlement');

  // ==========================================
  // TAB 1: SETTLEMENT MANAGER STATES
  // ==========================================
  const [selectedTreeKey, setSelectedTreeKey] = useState<string>('all');
  const [processTypeFilter, setProcessTypeFilter] = useState<'all' | 'Standard' | 'GoKhongMay'>('all');
  const [selectedYear, setSelectedYear] = useState<number>(currentYear);
  const [dateRange, setDateRange] = useState<[Dayjs, Dayjs]>([
    dayjs(`${currentYear}-01-01`),
    dayjs(),
  ]);
  const [contractNo, setContractNo] = useState<string>('');
  const [contractFolderId, setContractFolderId] = useState<number | null>(null);
  const [contractFolders, setContractFolders] = useState<MasterDataFolder[]>([]);
  useEffect(() => {
    masterDataFolderApi.getTree().then(tree => {
      const flatten = (nodes: MasterDataFolder[]): MasterDataFolder[] => nodes.flatMap(f => [f, ...flatten(f.children || [])]);
      setContractFolders(flatten(tree));
    }).catch(() => message.error('Không thể tải danh sách hợp đồng.'));
  }, []);
  const [customsOffice, setCustomsOffice] = useState<string>('Chi cục Hải quan Quản lý Hàng gia công');
  const [tableSearch, setTableSearch] = useState<string>('');

  const [report, setReport] = useState<SettlementReport | null>(null);
  const [items, setItems] = useState<SettlementItem[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [exporting, setExporting] = useState<boolean>(false);
  const [saving, setSaving] = useState<boolean>(false);

  // Drill-down modal state
  const [drillDownModalOpen, setDrillDownModalOpen] = useState<boolean>(false);
  const [drillDownProductCode, setDrillDownProductCode] = useState<string>('');
  const [drillDownItems, setDrillDownItems] = useState<SettlementDrillDownItem[]>([]);
  const [drillDownLoading, setDrillDownLoading] = useState<boolean>(false);

  // History Drawer state
  const [historyDrawerOpen, setHistoryDrawerOpen] = useState<boolean>(false);
  const [savedPeriods, setSavedPeriods] = useState<SettlementPeriodSummary[]>([]);
  const [historyLoading, setHistoryLoading] = useState<boolean>(false);

  // Save Modal state
  const [saveModalOpen, setSaveModalOpen] = useState<boolean>(false);
  const [saveStatus, setSaveStatus] = useState<'Draft' | 'Finalized'>('Draft');
  const [saveForm] = Form.useForm();

  // Warehouse import states
  const [importExcelModalOpen, setImportExcelModalOpen] = useState<boolean>(false);
  const [importExcelFile, setImportExcelFile] = useState<File | null>(null);
  const [importExcelLoading, setImportExcelLoading] = useState<boolean>(false);

  const [pasteModalOpen, setPasteModalOpen] = useState<boolean>(false);
  const [pasteText, setPasteText] = useState<string>('');
  const [pasteLoading, setPasteLoading] = useState<boolean>(false);

  const [importResultModalOpen, setImportResultModalOpen] = useState<boolean>(false);
  const [importResult, setImportResult] = useState<WarehouseImportResult | null>(null);

  // Negative balance filter toggle
  const [onlyNegativeFilter, setOnlyNegativeFilter] = useState<boolean>(false);

  // ==========================================
  // TAB 2: ANALYTICS & REVENUE STATES
  // ==========================================
  const [analyticsYear, setAnalyticsYear] = useState<number>(currentYear);
  const [analyticsData, setAnalyticsData] = useState<AnalyticsExportStats | null>(null);
  const [analyticsLoading, setAnalyticsLoading] = useState<boolean>(false);
  const [hoveredMonth, setHoveredMonth] = useState<number | null>(null);

  // Handle year change
  const handleYearChange = (year: number) => {
    setSelectedYear(year);
    if (year === currentYear) {
      setDateRange([dayjs(`${year}-01-01`), dayjs()]);
    } else {
      setDateRange([dayjs(`${year}-01-01`), dayjs(`${year}-12-31`)]);
    }
  };

  // Load saved periods list
  const loadSavedPeriods = useCallback(async () => {
    try {
      setHistoryLoading(true);
      const list = await settlementApi.getSettlementPeriods();
      setSavedPeriods(list);
    } catch {
      message.error('Không thể tải lịch sử kỳ quyết toán.');
    } finally {
      setHistoryLoading(false);
    }
  }, []);

  // Load analytics data
  const loadAnalytics = useCallback(async (year: number) => {
    try {
      setAnalyticsLoading(true);
      const data = await settlementApi.getExportAnalytics(year);
      setAnalyticsData(data);
    } catch {
      message.error('Không thể tải dữ liệu thống kê kim ngạch.');
    } finally {
      setAnalyticsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadSavedPeriods();
  }, [loadSavedPeriods]);

  useEffect(() => {
    void loadAnalytics(analyticsYear);
  }, [loadAnalytics, analyticsYear]);

  // Handle tree node selection from SettlementTreePanel
  const handleSelectTreeNode = (
    folderId: number | null,
    year: number | null,
    status: 'Draft' | 'Finalized' | null,
    periodId?: number | null
  ) => {
    if (periodId) {
      setSelectedTreeKey(`period_${periodId}`);
      void handleLoadSavedPeriod(periodId);
      return;
    }

    let key = 'all';
    if (folderId !== null && folderId !== undefined) {
      setContractFolderId(folderId);
      const matched = contractFolders.find((f) => f.id === folderId);
      if (matched) {
        setContractNo(matched.name);
      }
      key = `folder_${folderId}`;
    } else {
      setContractFolderId(null);
    }

    if (year) {
      handleYearChange(year);
      key += `_year_${year}`;
    }
    if (status) {
      key += `_${status.toLowerCase()}`;
    }
    setSelectedTreeKey(key);
  };

  // Calculate / aggregate settlement data
  const handleCalculate = async () => {
    if (!dateRange || !dateRange[0] || !dateRange[1]) {
      message.warning('Vui lòng chọn khoảng thời gian báo cáo.');
      return;
    }

    try {
      setLoading(true);
      const res = await settlementApi.calculateSettlement({
        year: selectedYear,
        fromDate: dateRange[0].format('YYYY-MM-DD'),
        toDate: dateRange[1].format('YYYY-MM-DD'),
        contractFolderId,
        contractNo: contractNo.trim() || undefined,
        customsOffice: customsOffice.trim() || undefined,
      });

      setReport(res);
      setItems(res.items || []);
      message.success(
        `Đã tổng hợp thành công ${res.items.length} mã hàng từ ${res.clearedOrderCount} đơn hàng E52 đã thông quan.`
      );
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Không thể tổng hợp số liệu quyết toán.');
    } finally {
      setLoading(false);
    }
  };

  // Handle Warehouse Excel Import
  const handleImportWarehouseExcel = async () => {
    if (!importExcelFile) {
      message.warning('Vui lòng chọn file Excel để tải lên.');
      return;
    }

    try {
      setImportExcelLoading(true);
      const res = await settlementApi.importWarehouseData(importExcelFile, items);
      setItems(res.items);
      setImportResult(res);
      setImportExcelModalOpen(false);
      setImportExcelFile(null);
      setImportResultModalOpen(true);

      message.success(
        `Đã đối soát kho thành công: Khớp ${res.matchedCount} mã, thêm mới ${res.addedFromWarehouseCount} mã.`
      );
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Không thể đối soát file số liệu kho.');
    } finally {
      setImportExcelLoading(false);
    }
  };

  // Handle Clipboard Paste Import
  const handleParseAndMatchClipboard = async () => {
    if (!pasteText.trim()) {
      message.warning('Vui lòng dán dữ liệu từ bảng tính vào khung.');
      return;
    }

    try {
      setPasteLoading(true);
      const lines = pasteText.split(/\r?\n/).filter((l) => l.trim().length > 0);
      const rows: WarehouseDataRow[] = [];

      for (const line of lines) {
        const parts = line.includes('\t') ? line.split('\t') : line.split(',');
        if (parts.length >= 2) {
          const rawCode = parts[0].trim();
          const lower = rawCode.toLowerCase();
          if (
            lower.includes('mã') ||
            lower.includes('style') ||
            lower.includes('code') ||
            lower.includes('tổng cộng') ||
            lower.includes('total')
          ) {
            continue;
          }

          const cleanNum = (str?: string) => {
            if (!str) return 0;
            const parsed = parseFloat(str.replace(/,/g, '').trim());
            return isNaN(parsed) ? 0 : parsed;
          };

          const opening = cleanNum(parts[1]);
          const prod = parts.length >= 3 ? cleanNum(parts[2]) : 0;

          rows.push({
            productCode: rawCode,
            processType: rawCode.toUpperCase().endsWith('.G') ? ProcessType.GoKhongMay : ProcessType.Standard,
            openingBalance: Math.max(0, opening),
            inPeriodProduction: Math.max(0, prod),
          });
        }
      }

      if (rows.length === 0) {
        message.warning('Không tìm thấy dòng dữ liệu hợp lệ. Vui lòng kiểm tra lại định dạng (Mã SP, Tồn đầu, Nhập SX).');
        return;
      }

      const res = await settlementApi.matchWarehouseData(rows, items);
      setItems(res.items);
      setImportResult(res);
      setPasteModalOpen(false);
      setPasteText('');
      setImportResultModalOpen(true);

      message.success(
        `Đã ghép thành công ${rows.length} dòng dữ liệu: Khớp ${res.matchedCount} mã, thêm mới ${res.addedFromWarehouseCount} mã.`
      );
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Không thể đối soát dữ liệu từ Clipboard.');
    } finally {
      setPasteLoading(false);
    }
  };

  // Drill-down customs declarations handler
  const handleOpenDrillDown = async (productCode: string) => {
    if (!dateRange || !dateRange[0] || !dateRange[1]) return;
    setDrillDownProductCode(productCode);
    setDrillDownModalOpen(true);
    setDrillDownLoading(true);

    try {
      const data = await settlementApi.getDrillDown(
        productCode,
        dateRange[0].format('YYYY-MM-DD'),
        dateRange[1].format('YYYY-MM-DD'),
        contractNo.trim() || undefined
      );
      setDrillDownItems(data);
    } catch {
      message.error('Không thể tải chi tiết tờ khai hải quan.');
    } finally {
      setDrillDownLoading(false);
    }
  };

  // Inline update item quantity
  const handleItemChange = (
    id: number,
    field: 'openingBalance' | 'inPeriodProduction' | 'otherExport' | 'note',
    value: any
  ) => {
    if (report?.status === 'Finalized') {
      message.warning('Kỳ quyết toán này đã được chốt khóa số liệu (Finalized). Không thể chỉnh sửa.');
      return;
    }

    setItems((prev) =>
      prev.map((item) => {
        if (item.id !== id) return item;

        const updated = { ...item, [field]: value };
        // Recalculate closing balance: (5) + (6) - (7) - (8)
        const op = Number(updated.openingBalance) || 0;
        const prod = Number(updated.inPeriodProduction) || 0;
        const exp = Number(updated.inPeriodExport) || 0;
        const other = Number(updated.otherExport) || 0;
        updated.closingBalance = op + prod - exp - other;
        updated.isNegative = updated.closingBalance < 0;

        return updated;
      })
    );
  };

  // Open Save Modal
  const handleOpenSaveModal = (statusToSave: 'Draft' | 'Finalized' = 'Draft') => {
    if (!report || items.length === 0) {
      message.warning('Chưa có dữ liệu quyết toán để lưu.');
      return;
    }

    setSaveStatus(statusToSave);
    saveForm.setFieldsValue({
      year: selectedYear || (dateRange && dateRange[0] ? dateRange[0].year() : dayjs().year()),
      contractNo: contractNo || report.contractNo || '',
      customsOffice: customsOffice || report.customsOffice || '',
      companyName: report.companyName || '',
      taxCode: report.taxCode || '',
      address: report.address || '',
      note: report.note || (dateRange && dateRange[0] && dateRange[1] ? `Kỳ quyết toán năm ${selectedYear} (${dateRange[0].format('DD/MM/YYYY')} - ${dateRange[1].format('DD/MM/YYYY')})` : `Kỳ quyết toán năm ${selectedYear}`),
    });
    setSaveModalOpen(true);
  };

  // Save settlement period
  const handleConfirmSave = async () => {
    try {
      const values = await saveForm.validateFields();
      setSaving(true);

      const payload: SaveSettlementPeriodRequest = {
        id: report?.periodId,
        year: values.year,
        fromDate: dateRange[0].format('YYYY-MM-DD'),
        toDate: dateRange[1].format('YYYY-MM-DD'),
        contractFolderId: report?.contractFolderId ?? contractFolderId,
        contractNo: values.contractNo?.trim() || undefined,
        customsOffice: values.customsOffice?.trim() || undefined,
        status: saveStatus,
        companyName: values.companyName?.trim(),
        taxCode: values.taxCode?.trim(),
        address: values.address?.trim(),
        note: values.note?.trim(),
        items: items,
      };

      const saved = await settlementApi.saveSettlement(payload);
      const refreshed = await settlementApi.getSettlementPeriodById(saved.id);
      setReport(refreshed);
      setItems(refreshed.items);

      if (saveStatus === 'Finalized') {
        message.success(`Đã CHỐT SỔ & KHÓA KỲ quyết toán năm ${values.year} thành công.`);
      } else {
        message.success(`Đã lưu bản nháp kỳ quyết toán năm ${values.year} thành công.`);
      }

      setSaveModalOpen(false);
      loadSavedPeriods();
    } catch (err: any) {
      if (err.errorFields) return;
      message.error(err.response?.data?.message || 'Không thể lưu kỳ báo cáo.');
    } finally {
      setSaving(false);
    }
  };

  // Load a saved period by ID
  const handleLoadSavedPeriod = async (id: number) => {
    try {
      setLoading(true);
      const data = await settlementApi.getSettlementPeriodById(id);
      setReport(data);
      setItems(data.items || []);
      if (data.year) {
        setSelectedYear(data.year);
      }
      if (data.fromDate && data.toDate) {
        setDateRange([dayjs(data.fromDate), dayjs(data.toDate)]);
      }
      setContractFolderId(data.contractFolderId ?? null);
      if (data.contractNo) {
        setContractNo(data.contractNo);
      }
      if (data.customsOffice) {
        setCustomsOffice(data.customsOffice);
      }
      setHistoryDrawerOpen(false);
      message.success(`Đã tải kỳ quyết toán năm ${data.year} (${data.items.length} mã hàng - ${data.status === 'Finalized' ? 'Đã chốt sổ' : 'Bản nháp'}).`);
    } catch {
      message.error('Không thể tải chi tiết kỳ quyết toán.');
    } finally {
      setLoading(false);
    }
  };

  // Export Excel directly from Saved Period ID
  const handleDownloadSavedExcel = async (periodId: number, year: number) => {
    try {
      const blob = await settlementApi.exportSettlementExcelById(periodId);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `DoiChieuNoiBo_XNK_Nam_${year}_Ky_${periodId}_${dayjs().format('YYYYMMDDHHmmss')}.xlsx`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
      message.success(`Đã tải file Excel đối chiếu nội bộ năm ${year}.`);
    } catch {
      message.error('Không thể tải file Excel.');
    }
  };

  // Export Excel đối chiếu nội bộ current report
  const handleExportExcel = async () => {
    if (!report || items.length === 0) {
      message.warning('Không có dữ liệu để xuất file Excel.');
      return;
    }

    try {
      setExporting(true);
      const currentReport: SettlementReport = {
        ...report,
        items,
        fromDate: dateRange[0].format('YYYY-MM-DD'),
        toDate: dateRange[1].format('YYYY-MM-DD'),
        contractFolderId: report.contractFolderId,
        contractNo: report.contractNo,
        customsOffice: customsOffice.trim() || report.customsOffice,
      };

      const blob = await settlementApi.exportSettlementExcel(currentReport);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `DoiChieuNoiBo_XNK_Nam_${selectedYear}_${dateRange[0].format('YYYYMMDD')}_${dateRange[1].format('YYYYMMDD')}.xlsx`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);

      message.success('Xuất file Excel đối chiếu nội bộ (kèm Sheet Drill-down tờ khai) thành công.');
    } catch {
      message.error('Không thể xuất file Excel.');
    } finally {
      setExporting(false);
    }
  };

  // Metrics calculations
  const totalOpening = useMemo(() => items.reduce((sum, i) => sum + (Number(i.openingBalance) || 0), 0), [items]);
  const totalProduction = useMemo(() => items.reduce((sum, i) => sum + (Number(i.inPeriodProduction) || 0), 0), [items]);
  const totalExport = useMemo(() => items.reduce((sum, i) => sum + (Number(i.inPeriodExport) || 0), 0), [items]);
  const totalOther = useMemo(() => items.reduce((sum, i) => sum + (Number(i.otherExport) || 0), 0), [items]);
  const totalClosing = useMemo(() => items.reduce((sum, i) => sum + (Number(i.closingBalance) || 0), 0), [items]);
  const negativeItems = useMemo(() => items.filter((i) => i.closingBalance < 0), [items]);

  // Phân loại Thành hình (Standard) vs Gò không may (.G)
  const standardItems = useMemo(
    () => items.filter((i) => normalizeProcessType(i.processType) === ProcessType.Standard),
    [items]
  );
  const goItems = useMemo(
    () => items.filter((i) => normalizeProcessType(i.processType) === ProcessType.GoKhongMay),
    [items]
  );
  const standardExport = useMemo(
    () => standardItems.reduce((sum, i) => sum + (Number(i.inPeriodExport) || 0), 0),
    [standardItems]
  );
  const goExport = useMemo(
    () => goItems.reduce((sum, i) => sum + (Number(i.inPeriodExport) || 0), 0),
    [goItems]
  );

  // Filtered items by search query, processType filter, and negative balance filter
  const filteredItems = useMemo(() => {
    let result = items;
    if (processTypeFilter === 'Standard') {
      result = result.filter((i) => normalizeProcessType(i.processType) === ProcessType.Standard);
    } else if (processTypeFilter === 'GoKhongMay') {
      result = result.filter((i) => normalizeProcessType(i.processType) === ProcessType.GoKhongMay);
    }
    if (onlyNegativeFilter) {
      result = result.filter((i) => i.closingBalance < 0);
    }
    if (!tableSearch.trim()) return result;
    const q = tableSearch.toLowerCase().trim();
    return result.filter(
      (item) =>
        item.productCode.toLowerCase().includes(q) ||
        (item.productName && item.productName.toLowerCase().includes(q)) ||
        (item.hsCode && item.hsCode.toLowerCase().includes(q))
    );
  }, [items, processTypeFilter, onlyNegativeFilter, tableSearch]);

  const isFinalized = report?.status === 'Finalized';

  // Columns definition for đối chiếu nội bộ
  const columns: ColumnsType<SettlementItem> = [
    {
      title: (
        <div className="text-center leading-tight">
          <span>STT</span>
          <div className="text-[10px] text-slate-400 font-normal">(1)</div>
        </div>
      ),
      key: 'stt',
      width: 50,
      fixed: 'left',
      align: 'center',
      render: (_, __, idx) => <span className="text-xs text-slate-500">{idx + 1}</span>,
    },
    {
      title: (
        <div className="leading-tight">
          <span>Mã sản phẩm</span>
          <div className="text-[10px] text-slate-400 font-normal">(2)</div>
        </div>
      ),
      dataIndex: 'productCode',
      key: 'productCode',
      width: 145,
      fixed: 'left',
      render: (code: string, record) => {
        const isNeg = record.closingBalance < 0;
        return (
          <div className="flex items-center gap-1.5">
            <span
              className={`font-mono text-xs font-semibold px-1.5 py-0.5 rounded border ${
                isNeg
                  ? 'text-rose-900 bg-rose-100 border-rose-300'
                  : 'text-slate-900 bg-slate-100 border-slate-200'
              }`}
            >
              {code}
            </span>
            <Tag color={record.processType === ProcessType.GoKhongMay ? 'orange' : 'blue'} className="m-0 text-[10px]">
              {record.processType === ProcessType.GoKhongMay ? 'Gò không may' : 'Thành hình'}
            </Tag>
            {isNeg && (
              <Tooltip title={`Cảnh báo: Âm tồn ${Math.abs(record.closingBalance).toLocaleString()} đôi!`}>
                <WarningOutlined className="text-rose-600 text-xs shrink-0" />
              </Tooltip>
            )}
          </div>
        );
      },
    },
    {
      title: (
        <div className="leading-tight">
          <span>Tên sản phẩm</span>
          <div className="text-[10px] text-slate-400 font-normal">(3)</div>
        </div>
      ),
      dataIndex: 'productName',
      key: 'productName',
      ellipsis: true,
      render: (name: string, record) => (
        <Tooltip title={name}>
          <div className="text-xs text-slate-700 truncate">
            <span>{name || '-'}</span>
            {record.hsCode && (
              <span className="ml-1.5 text-[10px] font-mono text-slate-400">[{record.hsCode}]</span>
            )}
          </div>
        </Tooltip>
      ),
    },
    {
      title: (
        <div className="text-center leading-tight">
          <span>ĐVT</span>
          <div className="text-[10px] text-slate-400 font-normal">(4)</div>
        </div>
      ),
      dataIndex: 'unit',
      key: 'unit',
      width: 55,
      align: 'center',
      render: (u: string) => <span className="text-xs text-slate-500">{u || 'đôi'}</span>,
    },
    {
      title: (
        <div className="text-right leading-tight">
          <span>Lượng tồn đầu kỳ</span>
          <div className="text-[10px] text-slate-400 font-normal">(5)</div>
        </div>
      ),
      dataIndex: 'openingBalance',
      key: 'openingBalance',
      width: 120,
      align: 'right',
      render: (val: number, record) => (
        <InputNumber
          size="small"
          min={0}
          disabled={isFinalized}
          value={val}
          onChange={(newVal) => handleItemChange(record.id, 'openingBalance', newVal ?? 0)}
          className="w-full text-right font-mono text-xs"
          formatter={(v) => `${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}
          parser={(v) => v?.replace(/\$\s?|(,*)/g, '') as any}
        />
      ),
    },
    {
      title: (
        <div className="text-right leading-tight">
          <span>Lượng nhập trong kỳ (SX)</span>
          <div className="text-[10px] text-slate-400 font-normal">(6)</div>
        </div>
      ),
      dataIndex: 'inPeriodProduction',
      key: 'inPeriodProduction',
      width: 130,
      align: 'right',
      render: (val: number, record) => (
        <InputNumber
          size="small"
          min={0}
          disabled={isFinalized}
          value={val}
          onChange={(newVal) => handleItemChange(record.id, 'inPeriodProduction', newVal ?? 0)}
          className="w-full text-right font-mono text-xs"
          placeholder="Nhập SL SX"
          formatter={(v) => `${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}
          parser={(v) => v?.replace(/\$\s?|(,*)/g, '') as any}
        />
      ),
    },
    {
      title: (
        <div className="text-right leading-tight">
          <span>Lượng xuất trong kỳ (XK)</span>
          <div className="text-[10px] text-slate-400 font-normal">(7)</div>
        </div>
      ),
      dataIndex: 'inPeriodExport',
      key: 'inPeriodExport',
      width: 145,
      align: 'right',
      render: (val: number, record) => (
        <div className="flex items-center justify-end gap-1.5">
          <span className="font-mono text-xs font-semibold text-blue-600 bg-blue-50 px-2 py-0.5 rounded border border-blue-100">
            {val.toLocaleString()}
          </span>
          <Tooltip title="Xem danh sách các tờ khai hải quan E52 cấu thành (Drill-down)">
            <Button
              type="text"
              size="small"
              icon={<EyeOutlined className="text-blue-600 text-xs" />}
              onClick={() => handleOpenDrillDown(record.productCode)}
              className="h-6 w-6 p-0 hover:bg-blue-100/60 flex items-center justify-center rounded cursor-pointer"
            />
          </Tooltip>
        </div>
      ),
    },
    {
      title: (
        <div className="text-right leading-tight">
          <span>Lượng xuất khác</span>
          <div className="text-[10px] text-slate-400 font-normal">(8)</div>
        </div>
      ),
      dataIndex: 'otherExport',
      key: 'otherExport',
      width: 105,
      align: 'right',
      render: (val: number, record) => (
        <InputNumber
          size="small"
          min={0}
          disabled={isFinalized}
          value={val}
          onChange={(newVal) => handleItemChange(record.id, 'otherExport', newVal ?? 0)}
          className="w-full text-right font-mono text-xs"
          formatter={(v) => `${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}
          parser={(v) => v?.replace(/\$\s?|(,*)/g, '') as any}
        />
      ),
    },
    {
      title: (
        <div className="text-right leading-tight">
          <span>Lượng tồn cuối kỳ</span>
          <div className="text-[10px] text-slate-400 font-normal">(9)=(5)+(6)-(7)-(8)</div>
        </div>
      ),
      dataIndex: 'closingBalance',
      key: 'closingBalance',
      width: 145,
      align: 'right',
      render: (val: number) => {
        const isNegative = val < 0;
        return (
          <div className="flex flex-col items-end">
            <span
              className={`font-mono text-xs font-semibold px-2 py-0.5 rounded border ${
                isNegative
                  ? 'text-rose-700 bg-rose-100 border-rose-300'
                  : 'text-slate-900 bg-slate-50 border-slate-200'
              }`}
            >
              {val.toLocaleString()}
            </span>
            {isNegative && (
              <span className="text-[10px] text-rose-600 font-medium mt-0.5 whitespace-nowrap">
                Thiếu: {Math.abs(val).toLocaleString()} đôi
              </span>
            )}
          </div>
        );
      },
    },
    {
      title: (
        <div className="leading-tight">
          <span>Ghi chú</span>
          <div className="text-[10px] text-slate-400 font-normal">(10)</div>
        </div>
      ),
      dataIndex: 'note',
      key: 'note',
      width: 130,
      render: (text: string, record) => (
        <Input
          size="small"
          disabled={isFinalized}
          value={text}
          onChange={(e) => handleItemChange(record.id, 'note', e.target.value)}
          placeholder="Ghi chú..."
          className="text-xs"
        />
      ),
    },
  ];

  return (
    <div className="space-y-6">
      {/* Top Main Page Header */}
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-3 pb-4 border-b border-slate-200">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2 flex-wrap">
            <h1 className="text-2xl font-bold text-slate-900 tracking-tight m-0">
              Quyết toán Hải quan & Thống kê Phân tích Kim ngạch
            </h1>
            <Tag className="bg-blue-50 text-blue-700 border-blue-200 text-xs m-0">
              Cần xác minh mẫu pháp lý trước khi nộp
            </Tag>
            {report?.status === 'Finalized' && (
              <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-[#F0FDF4] text-[#15803D] border border-[#BBF7D0]">
                ĐÃ CHỐT SỔ
              </span>
            )}
            {report?.status === 'Draft' && (
              <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-[#FFFBEB] text-[#B45309] border border-[#FDE68A]">
                BẢN NHÁP
              </span>
            )}
          </div>
          <p className="text-xs text-[#6B7280] mt-1 m-0">
            Tự động tổng hợp số liệu xuất khẩu gia công (E52) từ các đơn hàng Đã thông quan, xuất Excel đối chiếu nội bộ và phân tích doanh thu CMT/DAP
          </p>
        </div>

        {/* Global Toolbar Action Buttons */}
        <div className="flex items-center flex-wrap gap-2 min-w-0">
          <Button
            icon={<HistoryOutlined />}
            onClick={() => {
              loadSavedPeriods();
              setHistoryDrawerOpen(true);
            }}
            className="text-xs bg-white text-[#374151] hover:text-[#111827] border-[#D1D5DB] hover:border-[#9CA3AF] shadow-xs"
          >
            Kỳ đã lưu ({savedPeriods.length})
          </Button>

          <Button
            icon={<SaveOutlined />}
            onClick={() => handleOpenSaveModal('Draft')}
            disabled={items.length === 0 || isFinalized}
            className="text-xs bg-white text-[#374151] hover:text-[#111827] border-[#D1D5DB] hover:border-[#9CA3AF] shadow-xs"
          >
            Lưu nháp
          </Button>

          <Popconfirm
            title="Xác nhận chốt kỳ báo cáo quyết toán?"
            description="Sau khi chốt sổ, kỳ báo cáo này sẽ được khóa (Finalized) để đảm bảo tính pháp lý khi nộp Hải quan."
            onConfirm={() => handleOpenSaveModal('Finalized')}
            okText="Đồng ý chốt"
            cancelText="Hủy"
            disabled={items.length === 0 || isFinalized}
          >
            <Button
              icon={<LockOutlined />}
              disabled={items.length === 0 || isFinalized}
              className="text-xs bg-white text-[#374151] hover:text-[#111827] border-[#D1D5DB] hover:border-[#9CA3AF] shadow-xs"
            >
              Chốt kỳ báo cáo
            </Button>
          </Popconfirm>

          <Button
            type="primary"
            icon={<DownloadOutlined />}
            onClick={handleExportExcel}
            loading={exporting}
            disabled={items.length === 0}
            className="text-xs bg-[#2563EB] hover:bg-[#1D4ED8] font-medium"
          >
            Xuất Excel đối chiếu nội bộ
          </Button>
        </div>
      </div>

      {/* Main Tabs Container */}
      <Tabs
        activeKey={activeTab}
        onChange={setActiveTab}
        type="card"
        className="settlement-tabs"
        items={[
          {
            key: 'settlement',
            label: (
              <span className="flex items-center gap-1.5 px-1">
                <AuditOutlined />
                <span>Báo cáo Quyết toán đối chiếu nội bộ (BCQT-SP-GSQL)</span>
              </span>
            ),
            children: (
              <div className="flex flex-col lg:flex-row gap-4 pt-2 items-start">
                {/* Cây thư mục Quyết toán: Khách hàng/Hợp đồng -> Năm tài chính -> Bản nháp/Chính thức */}
                <div className="w-full lg:w-72 shrink-0">
                  <SettlementTreePanel
                    folders={contractFolders}
                    savedPeriods={savedPeriods}
                    selectedKey={selectedTreeKey}
                    onSelectNode={handleSelectTreeNode}
                  />
                </div>

                {/* Nội dung chính phân hệ quyết toán */}
                <div className="flex-1 min-w-0 space-y-5 w-full">
                  {/* Filter Toolbar */}
                <div className="bg-white border border-[#E5E7EB] rounded-lg p-4 shadow-[0_1px_2px_rgba(0,0,0,0.05)]">
                  <div className="grid grid-cols-1 md:grid-cols-12 gap-3 items-center">
                    {/* Year selector */}
                    <div className="md:col-span-2">
                      <span className="text-xs font-medium text-[#4B5563] block mb-1">
                        Năm quyết toán:
                      </span>
                      <Select
                        value={selectedYear}
                        onChange={handleYearChange}
                        className="w-full text-xs"
                        options={[
                          { value: 2026, label: 'Năm 2026' },
                          { value: 2025, label: 'Năm 2025' },
                          { value: 2024, label: 'Năm 2024' },
                          { value: 2023, label: 'Năm 2023' },
                        ]}
                      />
                    </div>

                    {/* Date Range Picker */}
                    <div className="md:col-span-4">
                      <span className="text-xs font-medium text-[#4B5563] block mb-1">
                        Khoảng ngày thông quan:
                      </span>
                      <RangePicker
                        value={dateRange}
                        onChange={(dates) => dates && setDateRange(dates as [Dayjs, Dayjs])}
                        format="DD/MM/YYYY"
                        className="w-full text-xs"
                        allowClear={false}
                      />
                    </div>

                    {/* Contract Number */}
                    <div className="md:col-span-3">
                      <span className="text-xs font-medium text-[#4B5563] block mb-1">
                        Số hợp đồng gia công:
                      </span>
                      <Select
                        value={contractFolderId}
                        options={contractFolders.map(f => ({value: f.id, label: f.name + (f.contractNo ? ' / ' + f.contractNo : '')}))}
                        onChange={(id) => { setContractFolderId(id ?? null); setContractNo(contractFolders.find(f => f.id === id)?.contractNo || ''); }}
                        placeholder="VD: KM-HANEW/01-2025"
                        className="w-full text-xs"
                        allowClear
                      />
                    </div>

                    {/* Customs Office */}
                    <div className="md:col-span-3">
                      <span className="text-xs font-medium text-[#4B5563] block mb-1">
                        Chi cục Hải quan tiếp nhận:
                      </span>
                      <Input
                        value={customsOffice}
                        onChange={(e) => setCustomsOffice(e.target.value)}
                        placeholder="Chi cục Hải quan quản lý"
                        className="w-full text-xs"
                        allowClear
                      />
                    </div>
                  </div>
                </div>

                {/* 3-Step Action Workflow Bar */}
                <div className="bg-white border border-[#E5E7EB] rounded-lg p-3 shadow-[0_1px_2px_rgba(0,0,0,0.05)] flex flex-wrap items-center justify-between gap-3">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="text-[11px] font-semibold text-[#6B7280] uppercase tracking-wide mr-1">
                      Quy trình 3 bước:
                    </span>
                    <Button
                      type="primary"
                      icon={<CalculatorOutlined />}
                      onClick={handleCalculate}
                      loading={loading}
                      className="text-xs bg-[#2563EB] hover:bg-[#1D4ED8] font-medium h-8"
                    >
                      1. Tổng hợp Lượng Thực xuất (E52)
                    </Button>
                    <span className="text-[#D1D5DB]">→</span>
                    <Button
                      icon={<UploadOutlined />}
                      onClick={() => setImportExcelModalOpen(true)}
                      className="text-xs bg-white text-[#374151] hover:text-[#111827] border-[#D1D5DB] hover:border-[#9CA3AF] font-medium h-8 shadow-xs"
                    >
                      2. Nạp File Số liệu Kho (.xlsx)
                    </Button>
                    <Button
                      icon={<SnippetsOutlined />}
                      onClick={() => setPasteModalOpen(true)}
                      className="text-xs bg-white text-[#374151] hover:text-[#111827] border-[#D1D5DB] hover:border-[#9CA3AF] h-8 shadow-xs"
                    >
                      Dán từ Clipboard
                    </Button>
                  </div>

                  <div className="flex items-center gap-2">
                    {negativeItems.length > 0 && (
                      <Button
                        danger={onlyNegativeFilter}
                        type={onlyNegativeFilter ? 'primary' : 'default'}
                        icon={<FilterOutlined />}
                        onClick={() => setOnlyNegativeFilter(!onlyNegativeFilter)}
                        className="text-xs font-medium h-8"
                      >
                        {onlyNegativeFilter ? 'Xem tất cả mã hàng' : `Chỉ xem mã âm tồn (${negativeItems.length})`}
                      </Button>
                    )}
                    <Button
                      icon={<ReloadOutlined />}
                      onClick={() => {
                        handleYearChange(currentYear);
                        setContractNo(''); setContractFolderId(null);
                        setCustomsOffice('Chi cục Hải quan Quản lý Hàng gia công');
                        setItems([]);
                        setReport(null);
                        setOnlyNegativeFilter(false);
                      }}
                      title="Đặt lại dữ liệu"
                      className="text-xs bg-white text-[#4B5563] hover:text-[#111827] border-[#D1D5DB] h-8 shadow-xs"
                    >
                      Làm mới
                    </Button>
                  </div>
                </div>

                {/* KPI Cards Row */}
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                  <div className="bg-white border border-[#E5E7EB] rounded-lg p-4 shadow-[0_1px_2px_rgba(0,0,0,0.05)]">
                    <div className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
                      Tổng số mã sản phẩm
                    </div>
                    <div className="mt-2 flex items-baseline justify-between">
                      <span className="text-2xl font-semibold font-mono text-[#111827]">
                        {items.length}
                      </span>
                      <span className="text-xs text-[#9CA3AF]">mã hình thể</span>
                    </div>
                    <div className="mt-2 text-[11px] text-[#6B7280]">
                      {report?.clearedOrderCount ? `Từ ${report.clearedOrderCount} đơn E52 thông quan` : 'Chờ tổng hợp'}
                    </div>
                  </div>

                  <div className="bg-white border border-[#E5E7EB] rounded-lg p-4 shadow-[0_1px_2px_rgba(0,0,0,0.05)]">
                    <div className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
                      Lượng tồn đầu kỳ (Cột 5)
                    </div>
                    <div className="mt-2 flex items-baseline justify-between">
                      <span className="text-2xl font-semibold font-mono text-[#111827]">
                        {totalOpening.toLocaleString()}
                      </span>
                      <span className="text-xs text-[#9CA3AF]">đôi</span>
                    </div>
                    <div className="mt-2 text-[11px] text-[#6B7280]">
                      Kế thừa từ kỳ trước hoặc đối soát kho
                    </div>
                  </div>

                  <div className="bg-white border border-[#E5E7EB] rounded-lg p-4 shadow-[0_1px_2px_rgba(0,0,0,0.05)]">
                    <div className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
                      Lượng xuất trong kỳ E52 (Cột 7)
                    </div>
                    <div className="mt-2 flex items-baseline justify-between">
                      <span className="text-2xl font-semibold font-mono text-[#2563EB]">
                        {totalExport.toLocaleString()}
                      </span>
                      <span className="text-xs text-[#9CA3AF]">đôi</span>
                    </div>
                    <div className="mt-2 text-[11px] text-[#6B7280]">
                      Tổng xuất khẩu gia công đã thông quan
                    </div>
                  </div>

                  <div className="bg-white border border-[#E5E7EB] rounded-lg p-4 shadow-[0_1px_2px_rgba(0,0,0,0.05)]">
                    <div className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
                      Lượng tồn cuối kỳ (Cột 9)
                    </div>
                    <div className="mt-2 flex items-baseline justify-between">
                      <span
                        className={`text-2xl font-semibold font-mono ${
                          totalClosing < 0 ? 'text-[#B91C1C]' : 'text-[#15803D]'
                        }`}
                      >
                        {totalClosing.toLocaleString()}
                      </span>
                      <span className="text-xs text-[#9CA3AF]">đôi</span>
                    </div>
                    <div className="mt-2 text-[11px] text-[#6B7280]">
                      {negativeItems.length > 0 ? (
                        <span className="text-[#B91C1C] font-semibold">
                          ⚠ {negativeItems.length} mã bị âm tồn!
                        </span>
                      ) : (
                        'Cân đối tồn kho hợp lệ'
                      )}
                    </div>
                  </div>
                </div>

                {/* Phân loại Loại hình sản phẩm (Thành hình vs Gò không may) */}
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div
                    onClick={() => setProcessTypeFilter(processTypeFilter === 'Standard' ? 'all' : 'Standard')}
                    className={`p-3.5 rounded-lg border cursor-pointer transition-all ${
                      processTypeFilter === 'Standard'
                        ? 'border-blue-500 bg-blue-50/50 shadow-sm ring-1 ring-blue-400'
                        : 'border-slate-200 bg-white hover:border-blue-300'
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2.5">
                        <span className="text-xl">👟</span>
                        <div>
                          <div className="text-xs font-bold text-slate-800">
                            Hàng Thành hình (Standard)
                          </div>
                          <div className="text-[11px] text-slate-500">
                            Định mức NPL hoàn chỉnh (chặt, may, gò, đế)
                          </div>
                        </div>
                      </div>
                      <div className="text-right">
                        <div className="text-base font-extrabold text-blue-600 font-mono">
                          {standardExport.toLocaleString()} đôi
                        </div>
                        <div className="text-[11px] text-slate-400 font-mono">
                          {standardItems.length} mã sản phẩm
                        </div>
                      </div>
                    </div>
                  </div>

                  <div
                    onClick={() => setProcessTypeFilter(processTypeFilter === 'GoKhongMay' ? 'all' : 'GoKhongMay')}
                    className={`p-3.5 rounded-lg border cursor-pointer transition-all ${
                      processTypeFilter === 'GoKhongMay'
                        ? 'border-purple-500 bg-purple-50/50 shadow-sm ring-1 ring-purple-400'
                        : 'border-slate-200 bg-white hover:border-purple-300'
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2.5">
                        <span className="text-xl">🟣</span>
                        <div>
                          <div className="text-xs font-bold text-purple-900">
                            Hàng Gò không may (.G)
                          </div>
                          <div className="text-[11px] text-purple-600">
                            Bán thành phẩm gò, bảo vệ định mức riêng biệt
                          </div>
                        </div>
                      </div>
                      <div className="text-right">
                        <div className="text-base font-extrabold text-purple-600 font-mono">
                          {goExport.toLocaleString()} đôi
                        </div>
                        <div className="text-[11px] text-slate-400 font-mono">
                          {goItems.length} mã sản phẩm
                        </div>
                      </div>
                    </div>
                  </div>
                </div>

                {/* Audit Risk Banner when negative closing balance detected */}
                {negativeItems.length > 0 && (
                  <div className="rounded-lg border border-[#FCA5A5] bg-[#FEF2F2] p-4 shadow-[0_1px_2px_rgba(0,0,0,0.05)]">
                    <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
                      <div className="flex items-start gap-3">
                        <div className="p-2 bg-[#FEE2E2] rounded-full text-[#B91C1C] text-base flex items-center justify-center shrink-0">
                          <WarningOutlined />
                        </div>
                        <div>
                          <div className="text-sm font-semibold text-[#991B1B] leading-tight">
                            CẢNH BÁO PHÁP LÝ: Phát hiện {negativeItems.length} mã hàng bị ÂM TỒN KHO
                          </div>
                          <div className="text-xs text-[#B91C1C] mt-1">
                            (Số lượng thực xuất lớn hơn số lượng kho báo sản xuất). Vui lòng kiểm tra lại trước khi xuất file nộp Hải quan.
                          </div>
                          <div className="text-[11px] text-[#DC2626] font-mono mt-1">
                            Mã bị âm: {negativeItems.slice(0, 8).map((i) => `${i.productCode} (${i.closingBalance.toLocaleString()})`).join(', ')}
                            {negativeItems.length > 8 ? ` ...và ${negativeItems.length - 8} mã khác` : ''}
                          </div>
                        </div>
                      </div>
                      <div className="flex items-center gap-2 shrink-0">
                        <Button
                          danger
                          type={onlyNegativeFilter ? 'primary' : 'default'}
                          icon={<FilterOutlined />}
                          onClick={() => setOnlyNegativeFilter(!onlyNegativeFilter)}
                          className="text-xs font-semibold"
                        >
                          {onlyNegativeFilter ? 'Hiển thị toàn bộ mã hàng' : `Chỉ xem các mã bị âm tồn (${negativeItems.length})`}
                        </Button>
                      </div>
                    </div>
                  </div>
                )}

                {/* Settlement Table Panel */}
                <div className="bg-white border border-[#E5E7EB] rounded-lg shadow-[0_1px_2px_rgba(0,0,0,0.05)] overflow-hidden">
                  {/* Table Toolbar */}
                  <div className="p-4 border-b border-[#E5E7EB] flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 bg-[#F9FAFB]">
                    <div className="flex items-center gap-2">
                      <span className="text-xs font-semibold text-[#111827]">
                        Bảng dữ liệu Quyết toán Sản phẩm Xuất khẩu (đối chiếu nội bộ)
                      </span>
                      <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-[#F3F4F6] text-[#4B5563] border border-[#E5E7EB]">
                        {filteredItems.length} / {items.length} mã hàng
                      </span>
                      {onlyNegativeFilter && (
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-[#FEF2F2] text-[#B91C1C] border border-[#FCA5A5]">
                          Đang lọc {filteredItems.length} mã âm tồn
                        </span>
                      )}
                      {isFinalized && (
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-[#F0FDF4] text-[#15803D] border border-[#BBF7D0]">
                          Khóa sửa (Đã chốt)
                        </span>
                      )}
                    </div>

                    <div className="flex items-center gap-2">
                      {negativeItems.length > 0 && (
                        <Button
                          size="small"
                          danger={onlyNegativeFilter}
                          type={onlyNegativeFilter ? 'primary' : 'default'}
                          icon={<FilterOutlined />}
                          onClick={() => setOnlyNegativeFilter(!onlyNegativeFilter)}
                          className="text-xs"
                        >
                          {onlyNegativeFilter ? 'Xem tất cả mã' : `Chỉ xem mã âm (${negativeItems.length})`}
                        </Button>
                      )}
                      <Input
                        size="small"
                        placeholder="Tìm mã sản phẩm, HS, tên..."
                        prefix={<SearchOutlined className="text-[#9CA3AF]" />}
                        value={tableSearch}
                        onChange={(e) => setTableSearch(e.target.value)}
                        className="w-60 text-xs"
                        allowClear
                      />
                    </div>
                  </div>

                  {/* Process Type Segmentation Sub-bar */}
                  <div className="px-4 py-2.5 bg-slate-50/80 border-b border-slate-200 flex flex-wrap items-center justify-between gap-2">
                    <div className="flex items-center gap-2">
                      <span className="text-xs text-slate-500 font-medium">Lọc danh sách:</span>
                      <Radio.Group
                        value={processTypeFilter}
                        onChange={(e) => setProcessTypeFilter(e.target.value)}
                        size="small"
                        buttonStyle="solid"
                      >
                        <Radio.Button value="all">
                          Tất cả ({items.length})
                        </Radio.Button>
                        <Radio.Button value="Standard">
                          👟 Thành hình ({standardItems.length})
                        </Radio.Button>
                        <Radio.Button value="GoKhongMay">
                          🟣 Gò không may .G ({goItems.length})
                        </Radio.Button>
                      </Radio.Group>
                    </div>
                    <div className="text-[11px] text-slate-400">
                      💡 Mẫu 16 Quyết toán: Phân định rõ định mức tiêu hao giữa hàng Thành hình và hàng Gò
                    </div>
                  </div>

                  {/* Table */}
                  <div className="w-full overflow-x-auto min-w-0">
                    <Table
                      dataSource={filteredItems}
                      columns={columns}
                      rowKey="id"
                      loading={loading}
                      size="small"
                      rowClassName={(record) => (record.closingBalance < 0 ? 'bg-[#FEF2F2]/70 font-medium' : '')}
                      scroll={{ x: 'max-content' }}
                      pagination={{
                        defaultPageSize: 15,
                        showSizeChanger: true,
                        pageSizeOptions: ['15', '30', '50', '100'],
                        size: 'small',
                        showTotal: (total, range) => `${range[0]}-${range[1]} của ${total} mục`,
                      }}
                    locale={{
                      emptyText: (
                        <div className="py-12 text-center text-[#9CA3AF]">
                          <InfoCircleOutlined className="text-3xl mb-2 text-[#D1D5DB] block" />
                          <p className="text-xs m-0">
                            Chưa có dữ liệu quyết toán. Vui lòng thiết lập bộ lọc và bấm{' '}
                            <strong className="text-[#2563EB]">"Tổng hợp E52"</strong>.
                          </p>
                        </div>
                      ),
                    }}
                    summary={() => {
                      if (filteredItems.length === 0) return null;
                      return (
                        <Table.Summary fixed>
                          <Table.Summary.Row className="bg-[#F9FAFB] font-semibold text-xs border-t border-[#E5E7EB]">
                            <Table.Summary.Cell index={0} colSpan={4} align="center">
                              <span className="text-[#374151] tracking-wider">TỔNG CỘNG</span>
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={4} align="right">
                              <span className="font-mono text-[#111827]">{totalOpening.toLocaleString()}</span>
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={5} align="right">
                              <span className="font-mono text-[#111827]">{totalProduction.toLocaleString()}</span>
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={6} align="right">
                              <span className="font-mono text-[#2563EB]">{totalExport.toLocaleString()}</span>
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={7} align="right">
                              <span className="font-mono text-[#111827]">{totalOther.toLocaleString()}</span>
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={8} align="right">
                              <span className={`font-mono ${totalClosing < 0 ? 'text-[#B91C1C]' : 'text-[#15803D]'}`}>
                                {totalClosing.toLocaleString()}
                              </span>
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={9} />
                          </Table.Summary.Row>
                        </Table.Summary>
                      );
                    }}
                  />
                  </div>
                </div>
              </div>
            </div>
          ),
          },
          {
            key: 'analytics',
            label: (
              <span className="flex items-center gap-1.5 px-1">
                <BarChartOutlined />
                <span>Dashboard Kim ngạch & Doanh thu CMT</span>
              </span>
            ),
            children: (
              <div className="space-y-6 pt-2">
                {/* Year Selection Toolbar */}
                <div className="bg-white border border-slate-200 rounded-lg p-4 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 shadow-xs">
                  <div className="flex items-center gap-3">
                    <span className="text-xs font-semibold text-slate-700">Năm phân tích:</span>
                    <Select
                      value={analyticsYear}
                      onChange={(y) => {
                        setAnalyticsYear(y);
                        loadAnalytics(y);
                      }}
                      className="w-32 text-xs"
                      options={[
                        { value: 2026, label: 'Năm 2026' },
                        { value: 2025, label: 'Năm 2025' },
                        { value: 2024, label: 'Năm 2024' },
                      ]}
                    />
                    <span className="text-xs text-slate-400">|</span>
                    <span className="text-xs text-slate-500">
                      Thống kê toàn diện từ các đơn hàng xuất khẩu gia công (E52) đạt trạng thái Đã thông quan
                    </span>
                  </div>

                  <Button
                    size="small"
                    icon={<ReloadOutlined />}
                    onClick={() => loadAnalytics(analyticsYear)}
                    loading={analyticsLoading}
                    className="text-xs text-slate-600 hover:text-blue-600"
                  >
                    Làm mới số liệu
                  </Button>
                </div>

                {/* 4 KPI Cards */}
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                  {/* Card 1: Total Export Volume */}
                  <div className="bg-white border border-slate-200 rounded-lg p-5 shadow-xs relative overflow-hidden">
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-medium text-slate-500 uppercase tracking-wider">
                        Tổng sản lượng xuất khẩu
                      </span>
                      <div className="w-8 h-8 rounded-full bg-blue-50 text-blue-600 flex items-center justify-center text-sm">
                        <RiseOutlined />
                      </div>
                    </div>
                    <div className="mt-3 flex items-baseline gap-2">
                      <span className="text-2xl font-bold font-mono text-slate-900">
                        {analyticsData?.totalQuantity.toLocaleString() ?? '0'}
                      </span>
                      <span className="text-xs text-slate-400 font-medium">đôi giày</span>
                    </div>
                    <div className="mt-2 text-xs text-slate-500 flex items-center gap-1">
                      <span>Từ</span>
                      <strong className="text-slate-700 font-mono">{analyticsData?.clearedOrderCount ?? 0}</strong>
                      <span>đơn hàng E52 đã thông quan</span>
                    </div>
                  </div>

                  {/* Card 2: Total DAP Value */}
                  <div className="bg-white border border-slate-200 rounded-lg p-5 shadow-xs relative overflow-hidden">
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-medium text-slate-500 uppercase tracking-wider">
                        Tổng kim ngạch DAP
                      </span>
                      <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-600 flex items-center justify-center text-sm">
                        <DollarCircleOutlined />
                      </div>
                    </div>
                    <div className="mt-3 flex items-baseline gap-2">
                      <span className="text-2xl font-bold font-mono text-emerald-600">
                        ${analyticsData?.totalDap.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) ?? '0.00'}
                      </span>
                      <span className="text-xs text-slate-400 font-medium">USD</span>
                    </div>
                    <div className="mt-2 text-xs text-slate-500">
                      Trị giá khai báo Hải quan theo điều kiện DAP
                    </div>
                  </div>

                  {/* Card 3: Total CMT Revenue */}
                  <div className="bg-white border border-slate-200 rounded-lg p-5 shadow-xs relative overflow-hidden">
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-medium text-slate-500 uppercase tracking-wider">
                        Doanh thu gia công CMT
                      </span>
                      <div className="w-8 h-8 rounded-full bg-indigo-50 text-indigo-600 flex items-center justify-center text-sm">
                        <AuditOutlined />
                      </div>
                    </div>
                    <div className="mt-3 flex items-baseline gap-2">
                      <span className="text-2xl font-bold font-mono text-indigo-600">
                        ${analyticsData?.totalCmt.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) ?? '0.00'}
                      </span>
                      <span className="text-xs text-slate-400 font-medium">USD</span>
                    </div>
                    <div className="mt-2 text-xs text-slate-500">
                      Doanh thu tiền công gia công thực thu
                    </div>
                  </div>

                  {/* Card 4: Customs Clearance Channels */}
                  <div className="bg-white border border-slate-200 rounded-lg p-5 shadow-xs relative overflow-hidden">
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-medium text-slate-500 uppercase tracking-wider">
                        Phân luồng tờ khai
                      </span>
                      <div className="w-8 h-8 rounded-full bg-amber-50 text-amber-600 flex items-center justify-center text-sm">
                        <SafetyCertificateOutlined />
                      </div>
                    </div>
                    <div className="mt-3 flex items-center gap-2">
                      <Tag color="success" className="m-0 text-xs font-mono">
                        Xanh: {analyticsData?.channelStats.greenCount ?? 0} ({analyticsData?.channelStats.greenPercentage ?? 0}%)
                      </Tag>
                      <Tag color="warning" className="m-0 text-xs font-mono">
                        Vàng: {analyticsData?.channelStats.yellowCount ?? 0}
                      </Tag>
                      <Tag color="error" className="m-0 text-xs font-mono">
                        Đỏ: {analyticsData?.channelStats.redCount ?? 0}
                      </Tag>
                    </div>
                    <div className="mt-2 text-xs text-slate-500">
                      Tổng {analyticsData?.channelStats.totalDeclarations ?? 0} tờ khai được ghi nhận
                    </div>
                  </div>
                </div>

                {/* Monthly Volume & Revenue Interactive Chart */}
                <div className="bg-white border border-slate-200 rounded-lg p-5 shadow-xs">
                  <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between pb-4 border-b border-slate-100 gap-2">
                    <div>
                      <h3 className="text-sm font-semibold text-slate-900 m-0">
                        Biểu đồ Phân bổ Sản lượng & Doanh thu CMT theo 12 Tháng (Năm {analyticsYear})
                      </h3>
                      <p className="text-xs text-slate-500 mt-0.5 m-0">
                        Rê chuột vào cột tháng để xem chi tiết sản lượng đôi, kim ngạch DAP và doanh thu CMT
                      </p>
                    </div>

                    <div className="flex items-center gap-3 text-xs">
                      <div className="flex items-center gap-1.5">
                        <span className="w-3 h-3 rounded-sm bg-blue-500 inline-block"></span>
                        <span className="text-slate-600 font-medium">Sản lượng xuất (đôi)</span>
                      </div>
                      <div className="flex items-center gap-1.5">
                        <span className="w-3 h-3 rounded-sm bg-indigo-600 inline-block"></span>
                        <span className="text-slate-600 font-medium">Doanh thu CMT ($ USD)</span>
                      </div>
                    </div>
                  </div>

                  {/* SVG Bar / Chart Container */}
                  <div className="mt-6">
                    {(() => {
                      const months = analyticsData?.monthlyStats || [];
                      const maxQty = Math.max(...months.map((m) => m.quantity), 100);

                      return (
                        <div className="space-y-4">
                          <div className="grid grid-cols-12 gap-2 h-56 items-end pt-6 px-2">
                            {months.map((m) => {
                              const qtyHeight = Math.round((m.quantity / maxQty) * 160);
                              const isHovered = hoveredMonth === m.month;

                              return (
                                <div
                                  key={m.month}
                                  className="flex flex-col items-center justify-end h-full group relative cursor-pointer"
                                  onMouseEnter={() => setHoveredMonth(m.month)}
                                  onMouseLeave={() => setHoveredMonth(null)}
                                >
                                  {/* Tooltip on Hover */}
                                  {isHovered && (
                                    <div className="absolute bottom-full mb-2 z-20 bg-slate-900 text-white rounded-md p-2.5 shadow-lg text-[11px] min-w-[160px] pointer-events-none">
                                      <div className="font-semibold text-blue-300 border-b border-slate-700 pb-1 mb-1">
                                        {m.monthName} / {analyticsYear}
                                      </div>
                                      <div className="flex justify-between py-0.5">
                                        <span className="text-slate-300">Sản lượng:</span>
                                        <span className="font-mono font-bold text-white">{m.quantity.toLocaleString()} đôi</span>
                                      </div>
                                      <div className="flex justify-between py-0.5">
                                        <span className="text-slate-300">Doanh thu CMT:</span>
                                        <span className="font-mono text-indigo-300 font-medium">${m.totalCmt.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
                                      </div>
                                      <div className="flex justify-between py-0.5">
                                        <span className="text-slate-300">Kim ngạch DAP:</span>
                                        <span className="font-mono text-emerald-300 font-medium">${m.totalDap.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
                                      </div>
                                      <div className="flex justify-between py-0.5 text-slate-400 text-[10px]">
                                        <span>Số đơn hàng:</span>
                                        <span>{m.orderCount} đơn</span>
                                      </div>
                                    </div>
                                  )}

                                  {/* Bar container */}
                                  <div className="w-full max-w-[32px] flex items-end justify-center h-44 pb-1">
                                    <div
                                      style={{ height: `${Math.max(qtyHeight, 4)}px` }}
                                      className={`w-full rounded-t-md transition-all duration-300 ${
                                        isHovered
                                          ? 'bg-blue-600 shadow-md scale-105'
                                          : m.quantity > 0
                                          ? 'bg-blue-500 hover:bg-blue-600'
                                          : 'bg-slate-100'
                                      }`}
                                    />
                                  </div>

                                  {/* Month label */}
                                  <div className={`mt-2 text-[11px] font-medium text-center truncate ${isHovered ? 'text-blue-600 font-bold' : 'text-slate-500'}`}>
                                    T{m.month}
                                  </div>
                                </div>
                              );
                            })}
                          </div>

                          <div className="border-t border-slate-100 pt-3 text-[11px] text-slate-400 text-center">
                            Trục hoành: 12 tháng trong năm • Chiều cao cột: Sản lượng xuất khẩu (đôi)
                          </div>
                        </div>
                      );
                    })()}
                  </div>
                </div>

                {/* Bottom Row: Top 5 Styles and Monthly Table */}
                <div className="grid grid-cols-1 lg:grid-cols-12 gap-5">
                  {/* Top 5 Styles Card */}
                  <div className="lg:col-span-6 bg-white border border-slate-200 rounded-lg p-5 shadow-xs flex flex-col justify-between">
                    <div>
                      <div className="flex items-center justify-between pb-3 border-b border-slate-100">
                        <div className="flex items-center gap-2">
                          <TrophyOutlined className="text-amber-500 text-base" />
                          <h3 className="text-sm font-semibold text-slate-900 m-0">
                            Top 5 Mã Giày Xuất Khẩu Nhiều Nhất
                          </h3>
                        </div>
                        <span className="text-xs text-slate-400">Năm {analyticsYear}</span>
                      </div>

                      <div className="mt-4 space-y-3.5">
                        {analyticsData?.topStyles.length ? (
                          analyticsData.topStyles.map((style) => (
                            <div key={style.styleCode} className="space-y-1">
                              <div className="flex items-center justify-between text-xs">
                                <div className="flex items-center gap-2">
                                  <span
                                    className={`w-5 h-5 rounded-full flex items-center justify-center font-bold text-[10px] ${
                                      style.rank === 1
                                        ? 'bg-amber-100 text-amber-800'
                                        : style.rank === 2
                                        ? 'bg-slate-200 text-slate-700'
                                        : style.rank === 3
                                        ? 'bg-orange-100 text-orange-800'
                                        : 'bg-slate-100 text-slate-500'
                                    }`}
                                  >
                                    #{style.rank}
                                  </span>
                                  <span className="font-mono font-semibold text-slate-900 bg-slate-100 px-1.5 py-0.5 rounded border border-slate-200">
                                    {style.styleCode}
                                  </span>
                                  <span className="text-slate-600 truncate max-w-[170px]" title={style.productName}>
                                    {style.productName}
                                  </span>
                                </div>

                                <div className="text-right">
                                  <span className="font-mono font-bold text-blue-600">
                                    {style.quantity.toLocaleString()} đôi
                                  </span>
                                  <span className="text-slate-400 text-[11px] ml-1.5">
                                    ({style.percentage}%)
                                  </span>
                                </div>
                              </div>

                              <Progress
                                percent={style.percentage}
                                showInfo={false}
                                strokeColor="#2563EB"
                                size="small"
                                className="m-0"
                              />

                              <div className="flex justify-between text-[10px] text-slate-400 pt-0.5">
                                <span>DAP: ${style.totalDap.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
                                <span>CMT: ${style.totalCmt.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
                              </div>
                            </div>
                          ))
                        ) : (
                          <div className="py-8 text-center text-slate-400 text-xs">
                            Chưa có dữ liệu xuất khẩu cho năm {analyticsYear}.
                          </div>
                        )}
                      </div>
                    </div>
                  </div>

                  {/* Monthly Table Summary */}
                  <div className="lg:col-span-6 bg-white border border-slate-200 rounded-lg p-5 shadow-xs">
                    <div className="flex items-center justify-between pb-3 border-b border-slate-100">
                      <h3 className="text-sm font-semibold text-slate-900 m-0">
                        Bảng Thống Kê Chi Tiết 12 Tháng
                      </h3>
                      <span className="text-xs text-slate-400">Năm {analyticsYear}</span>
                    </div>

                    <div className="mt-3">
                      <Table
                        dataSource={analyticsData?.monthlyStats || []}
                        rowKey="month"
                        size="small"
                        pagination={false}
                        scroll={{ x: 'max-content', y: 240 }}
                        columns={[
                          {
                            title: 'Tháng',
                            dataIndex: 'monthName',
                            key: 'monthName',
                            width: 80,
                            render: (name: string) => <span className="font-medium text-xs text-slate-800">{name}</span>,
                          },
                          {
                            title: 'Số đơn',
                            dataIndex: 'orderCount',
                            key: 'orderCount',
                            width: 65,
                            align: 'center',
                            render: (c: number) => <span className="font-mono text-xs text-slate-600">{c}</span>,
                          },
                          {
                            title: 'Sản lượng (đôi)',
                            dataIndex: 'quantity',
                            key: 'quantity',
                            align: 'right',
                            render: (q: number) => (
                              <span className="font-mono text-xs font-semibold text-blue-600">
                                {q.toLocaleString()}
                              </span>
                            ),
                          },
                          {
                            title: 'Doanh thu CMT ($)',
                            dataIndex: 'totalCmt',
                            key: 'totalCmt',
                            align: 'right',
                            render: (cmt: number) => (
                              <span className="font-mono text-xs text-indigo-600">
                                ${cmt.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                              </span>
                            ),
                          },
                          {
                            title: 'Kim ngạch DAP ($)',
                            dataIndex: 'totalDap',
                            key: 'totalDap',
                            align: 'right',
                            render: (dap: number) => (
                              <span className="font-mono text-xs text-emerald-600">
                                ${dap.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                              </span>
                            ),
                          },
                        ]}
                      />
                    </div>
                  </div>
                </div>
              </div>
            ),
          },
        ]}
      />

      {/* Drill-down Modal: Chi tiết các tờ khai đã thông quan cấu thành */}
      <Modal
        title={
          <div className="flex items-center gap-2">
            <span>Chi tiết Tờ khai Hải quan xuất khẩu (E52) - Mã hình thể:</span>
            <Tag className="font-mono font-bold text-blue-700 bg-blue-50 border-blue-200">
              {drillDownProductCode}
            </Tag>
          </div>
        }
        open={drillDownModalOpen}
        onCancel={() => setDrillDownModalOpen(false)}
        footer={[
          <Button key="close" onClick={() => setDrillDownModalOpen(false)} className="text-xs">
            Đóng
          </Button>,
        ]}
        width="min(1050px, 96vw)"
        style={{ top: 20 }}
      >
        <div className="mt-3 w-full overflow-x-auto min-w-0">
          <Table
            dataSource={drillDownItems}
            rowKey={(r, idx) => `${r.orderId}_${idx}`}
            loading={drillDownLoading}
            size="small"
            scroll={{ x: 'max-content' }}
            pagination={{ defaultPageSize: 10, size: 'small' }}
            columns={[
              {
                title: 'STT',
                key: 'idx',
                width: 50,
                align: 'center',
                fixed: 'left',
                render: (_, __, i) => <span className="text-xs text-slate-500">{i + 1}</span>,
              },
              {
                title: 'Số tờ khai',
                dataIndex: 'declarationNo',
                key: 'declarationNo',
                width: 140,
                fixed: 'left',
                render: (no: string) => (
                  <span className="font-mono text-xs font-semibold text-slate-900 bg-slate-100 px-1.5 py-0.5 rounded border border-slate-200">
                    {no}
                  </span>
                ),
              },
              {
                title: 'Ngày thông quan',
                dataIndex: 'clearanceDate',
                key: 'clearanceDate',
                width: 130,
                align: 'center',
                render: (d: string) => (
                  <span className="text-xs text-slate-600 font-mono">
                    {d ? dayjs(d).format('DD/MM/YYYY HH:mm') : '-'}
                  </span>
                ),
              },
              {
                title: 'Số hóa đơn (INV)',
                dataIndex: 'invoiceNo',
                key: 'invoiceNo',
                width: 130,
                render: (inv: string) => <span className="text-xs font-mono text-slate-800">{inv}</span>,
              },
              {
                title: 'Mã chi tiết / Diễn giải',
                dataIndex: 'fullItemCode',
                key: 'fullItemCode',
                ellipsis: true,
                render: (txt: string) => <span className="text-xs text-slate-600">{txt}</span>,
              },
              {
                title: 'Số lượng (đôi)',
                dataIndex: 'quantity',
                key: 'quantity',
                width: 110,
                align: 'right',
                render: (q: number) => (
                  <span className="font-mono text-xs font-semibold text-blue-600">
                    {q.toLocaleString()}
                  </span>
                ),
              },
              {
                title: 'Đơn giá CMT',
                dataIndex: 'unitPriceCMT',
                key: 'unitPriceCMT',
                width: 95,
                align: 'right',
                render: (p: number) => (
                  <span className="font-mono text-xs text-indigo-700">${p.toFixed(2)}</span>
                ),
              },
              {
                title: 'Đơn giá DAP',
                dataIndex: 'unitPriceDAP',
                key: 'unitPriceDAP',
                width: 95,
                align: 'right',
                render: (p: number) => (
                  <span className="font-mono text-xs text-emerald-700">${p.toFixed(2)}</span>
                ),
              },
            ]}
            summary={(pageData) => {
              const totalQty = pageData.reduce((sum, item) => sum + item.quantity, 0);
              return (
                <Table.Summary.Row className="bg-slate-50 font-semibold text-xs">
                  <Table.Summary.Cell index={0} colSpan={5} align="center">
                    <span>TỔNG CỘNG ({drillDownItems.length} dòng tờ khai)</span>
                  </Table.Summary.Cell>
                  <Table.Summary.Cell index={5} align="right">
                    <span className="font-mono text-blue-600 font-bold">{totalQty.toLocaleString()}</span>
                  </Table.Summary.Cell>
                  <Table.Summary.Cell index={6} colSpan={2} />
                </Table.Summary.Row>
              );
            }}
          />
        </div>
      </Modal>

      {/* Drawer: Danh sách các kỳ quyết toán đã lưu */}
      <Drawer
        title="Lịch sử các kỳ quyết toán đã lưu"
        placement="right"
        width={750}
        open={historyDrawerOpen}
        onClose={() => setHistoryDrawerOpen(false)}
      >
        <Table
          dataSource={savedPeriods}
          rowKey="id"
          loading={historyLoading}
          size="small"
          scroll={{ x: 'max-content' }}
          pagination={{ defaultPageSize: 10, size: 'small' }}
          columns={[
            {
              title: 'Năm',
              dataIndex: 'year',
              key: 'year',
              width: 70,
              render: (y: number) => <span className="font-mono text-xs font-semibold">{y}</span>,
            },
            {
              title: 'Kỳ báo cáo',
              key: 'period',
              render: (_, r) => (
                <div className="text-xs">
                  <div className="font-medium text-slate-800">
                    {dayjs(r.fromDate).format('DD/MM/YYYY')} - {dayjs(r.toDate).format('DD/MM/YYYY')}
                  </div>
                  {r.contractNo && (
                    <div className="text-[11px] text-slate-500">HĐ: {r.contractNo}</div>
                  )}
                  {r.customsOffice && (
                    <div className="text-[10px] text-slate-400 truncate max-w-[200px]">{r.customsOffice}</div>
                  )}
                </div>
              ),
            },
            {
              title: 'Trạng thái',
              dataIndex: 'status',
              key: 'status',
              width: 95,
              align: 'center',
              render: (st: string) => (
                st === 'Finalized' ? (
                  <Tag color="success" className="text-[11px] m-0">Đã chốt</Tag>
                ) : (
                  <Tag color="warning" className="text-[11px] m-0">Bản nháp</Tag>
                )
              ),
            },
            {
              title: 'Mã hàng',
              dataIndex: 'itemCount',
              key: 'itemCount',
              width: 65,
              align: 'center',
              render: (c: number) => <span className="text-xs font-mono">{c}</span>,
            },
            {
              title: 'Tổng xuất (đôi)',
              dataIndex: 'totalExportQuantity',
              key: 'totalExportQuantity',
              align: 'right',
              width: 110,
              render: (q: number) => (
                <span className="font-mono text-xs text-blue-600 font-medium">
                  {q.toLocaleString()}
                </span>
              ),
            },
            {
              title: 'Thao tác',
              key: 'actions',
              width: 150,
              align: 'center',
              fixed: 'right',
              render: (_, record) => (
                <div className="flex items-center justify-center gap-1">
                  <Button
                    size="small"
                    type="link"
                    icon={<FolderOpenOutlined />}
                    onClick={() => handleLoadSavedPeriod(record.id)}
                    className="text-xs text-blue-600 p-0"
                  >
                    Mở lại
                  </Button>
                  <span className="text-slate-300">|</span>
                  <Button
                    size="small"
                    type="link"
                    icon={<FileExcelOutlined />}
                    onClick={() => handleDownloadSavedExcel(record.id, record.year)}
                    className="text-xs text-emerald-600 p-0"
                  >
                    Tải Excel
                  </Button>
                </div>
              ),
            },
          ]}
        />
      </Drawer>

      {/* Modal: Lưu kỳ báo cáo quyết toán */}
      <Modal
        title={saveStatus === 'Finalized' ? 'Chốt sổ & Khóa kỳ báo cáo quyết toán' : 'Lưu bản nháp bảng quyết toán'}
        open={saveModalOpen}
        onOk={handleConfirmSave}
        onCancel={() => setSaveModalOpen(false)}
        confirmLoading={saving}
        okText={saveStatus === 'Finalized' ? 'Xác nhận chốt khóa' : 'Lưu bản nháp'}
        cancelText="Hủy"
        width="min(580px, 96vw)"
      >
        <Form form={saveForm} layout="vertical" className="mt-4">
          <div className="grid grid-cols-2 gap-3">
            <Form.Item
              name="year"
              label="Năm quyết toán"
              rules={[{ required: true, message: 'Vui lòng nhập năm' }]}
            >
              <InputNumber className="w-full text-xs font-mono" min={2000} max={2100} />
            </Form.Item>

            <Form.Item name="contractNo" label="Số hợp đồng gia công">
              <Input placeholder="VD: KM-HANEW/01-2025" className="text-xs" />
            </Form.Item>
          </div>

          <Form.Item name="customsOffice" label="Chi cục Hải quan tiếp nhận / quản lý">
            <Input placeholder="VD: Chi cục Hải quan Quản lý Hàng gia công" className="text-xs" />
          </Form.Item>

          <Form.Item name="companyName" label="Tên doanh nghiệp">
            <Input className="text-xs" />
          </Form.Item>

          <div className="grid grid-cols-2 gap-3">
            <Form.Item name="taxCode" label="Mã số thuế">
              <Input className="text-xs font-mono" />
            </Form.Item>
            <Form.Item name="address" label="Địa chỉ">
              <Input className="text-xs" />
            </Form.Item>
          </div>

          <Form.Item name="note" label="Ghi chú kỳ báo cáo">
            <Input.TextArea rows={2} placeholder="Ghi chú lưu trữ..." className="text-xs" />
          </Form.Item>

          <div className="bg-slate-50 p-3 rounded border border-slate-200 text-xs text-slate-600 space-y-1">
            <div className="flex justify-between">
              <span>Trạng thái lưu:</span>
              <span className="font-semibold text-slate-800">
                {saveStatus === 'Finalized' ? 'Khóa số liệu (Finalized)' : 'Bản nháp (Draft)'}
              </span>
            </div>
            <div className="flex justify-between">
              <span>Tổng số mã hàng:</span>
              <span className="font-mono font-semibold text-slate-800">{items.length} mã</span>
            </div>
            <div className="flex justify-between">
              <span>Tổng lượng xuất E52:</span>
              <span className="font-mono font-semibold text-blue-600">{totalExport.toLocaleString()} đôi</span>
            </div>
            <div className="flex justify-between">
              <span>Tổng tồn cuối kỳ:</span>
              <span className="font-mono font-semibold text-emerald-600">{totalClosing.toLocaleString()} đôi</span>
            </div>
          </div>
        </Form>
      </Modal>

      {/* Modal: Nạp file Excel số liệu kho */}
      <Modal
        title={
          <div className="flex items-center gap-2 text-slate-900">
            <FileExcelOutlined className="text-emerald-600 text-lg" />
            <span>Nạp File Số liệu Kho (.xlsx) đối soát đối chiếu nội bộ</span>
          </div>
        }
        open={importExcelModalOpen}
        onCancel={() => {
          setImportExcelModalOpen(false);
          setImportExcelFile(null);
        }}
        onOk={handleImportWarehouseExcel}
        confirmLoading={importExcelLoading}
        okText="Bắt đầu đối soát dữ liệu"
        cancelText="Hủy"
        width={560}
      >
        <div className="py-2 space-y-4">
          <p className="text-xs text-slate-600 m-0">
            Hệ thống sẽ tự động quét các cột trong file: <strong>Mã sản phẩm / Style</strong>,{' '}
            <strong>Lượng tồn đầu kỳ</strong>, <strong>Lượng nhập sản xuất trong kỳ</strong>.
            Các hậu tố như <code>.G</code>, <code>-PO...</code> sẽ được tự động chuẩn hóa để khớp với tờ khai xuất khẩu.
          </p>

          <Upload.Dragger
            accept=".xlsx,.xls"
            maxCount={1}
            beforeUpload={(file) => {
              setImportExcelFile(file);
              return false;
            }}
            onRemove={() => setImportExcelFile(null)}
            fileList={importExcelFile ? [importExcelFile as any] : []}
            className="p-4"
          >
            <p className="ant-upload-drag-icon text-emerald-500 mb-2 text-3xl">
              <UploadOutlined />
            </p>
            <p className="text-xs font-semibold text-slate-700 m-0">
              Nhấp hoặc kéo thả file Excel báo cáo kho vào đây
            </p>
            <p className="text-[11px] text-slate-400 mt-1 m-0">
              Hỗ trợ định dạng .xlsx, .xls xuất từ phần mềm Kho / ERP
            </p>
          </Upload.Dragger>

          <div className="bg-slate-50 p-3 rounded border border-slate-200 text-xs text-slate-600">
            <div className="font-semibold text-slate-700 mb-1">Quy tắc ghép nối & bù trừ:</div>
            <ul className="list-disc list-inside space-y-0.5 text-[11px] text-slate-500 m-0">
              <li>Mã trùng khớp: Tự động cập nhật Cột (5) Tồn đầu và Cột (6) Nhập sản xuất.</li>
              <li>Mã kho có nhưng chưa xuất khẩu trong kỳ: Tự động thêm dòng mới với Lượng xuất = 0.</li>
              <li>Sau khi ghép, hệ thống tự động tính lại Cột (9) Tồn cuối và kích hoạt cảnh báo nếu âm tồn.</li>
            </ul>
          </div>
        </div>
      </Modal>

      {/* Modal: Dán từ Clipboard */}
      <Modal
        title={
          <div className="flex items-center gap-2 text-slate-900">
            <SnippetsOutlined className="text-purple-600 text-lg" />
            <span>Dán số liệu Kho từ Clipboard</span>
          </div>
        }
        open={pasteModalOpen}
        onCancel={() => {
          setPasteModalOpen(false);
          setPasteText('');
        }}
        onOk={handleParseAndMatchClipboard}
        confirmLoading={pasteLoading}
        okText="Ghép và tính toán số liệu"
        cancelText="Hủy"
        width="min(600px, 96vw)"
      >
        <div className="py-2 space-y-3">
          <p className="text-xs text-slate-600 m-0">
            Copy các cột từ Excel theo thứ tự: <strong>Mã sản phẩm</strong> [Tab] <strong>Lượng tồn đầu</strong> [Tab] <strong>Nhập sản xuất</strong> rồi dán vào bên dưới:
          </p>
          <Input.TextArea
            rows={8}
            value={pasteText}
            onChange={(e) => setPasteText(e.target.value)}
            placeholder="Ví dụ dán từ Excel:&#10;42072-030	1500	5000&#10;45428-2LX	200	3400&#10;SHOE-TEST	0	1200"
            className="font-mono text-xs"
          />
          <div className="text-[11px] text-slate-400">
            Hỗ trợ phân cách bằng phím Tab hoặc dấu phẩy (,). Dòng tiêu đề sẽ được tự động bỏ qua.
          </div>
        </div>
      </Modal>

      {/* Modal: Tổng kết đối soát kho */}
      <Modal
        title={
          <div className="flex items-center gap-2">
            {importResult?.negativeItemCount ? (
              <WarningOutlined className="text-amber-500 text-lg" />
            ) : (
              <CheckCircleOutlined className="text-emerald-600 text-lg" />
            )}
            <span>Kết quả đối soát số liệu kho</span>
          </div>
        }
        open={importResultModalOpen}
        onOk={() => setImportResultModalOpen(false)}
        onCancel={() => setImportResultModalOpen(false)}
        footer={[
          <Button key="close" type="primary" onClick={() => setImportResultModalOpen(false)}>
            Đóng
          </Button>,
        ]}
        width="min(560px, 96vw)"
      >
        {importResult && (
          <div className="py-2 space-y-4">
            <div className="grid grid-cols-3 gap-3">
              <div className="bg-blue-50 border border-blue-200 rounded p-3 text-center">
                <div className="text-[11px] text-blue-600 font-medium">Đã khớp mã XK</div>
                <div className="text-xl font-bold font-mono text-blue-800 mt-1">
                  {importResult.matchedCount}
                </div>
              </div>
              <div className="bg-purple-50 border border-purple-200 rounded p-3 text-center">
                <div className="text-[11px] text-purple-600 font-medium">Thêm mới từ kho</div>
                <div className="text-xl font-bold font-mono text-purple-800 mt-1">
                  {importResult.addedFromWarehouseCount}
                </div>
              </div>
              <div
                className={`border rounded p-3 text-center ${
                  importResult.negativeItemCount > 0 ? 'bg-rose-50 border-rose-200' : 'bg-emerald-50 border-emerald-200'
                }`}
              >
                <div
                  className={`text-[11px] font-medium ${
                    importResult.negativeItemCount > 0 ? 'text-rose-600' : 'text-emerald-600'
                  }`}
                >
                  {importResult.negativeItemCount > 0 ? 'Mã âm tồn kho' : 'Trạng thái'}
                </div>
                <div
                  className={`text-xl font-bold font-mono mt-1 ${
                    importResult.negativeItemCount > 0 ? 'text-rose-700' : 'text-emerald-700'
                  }`}
                >
                  {importResult.negativeItemCount > 0 ? `${importResult.negativeItemCount} mã` : 'Hợp lệ'}
                </div>
              </div>
            </div>

            {importResult.warnings && importResult.warnings.length > 0 && (
              <div className="bg-amber-50 border border-amber-200 rounded p-3 text-xs">
                <div className="font-semibold text-amber-800 mb-1 flex items-center gap-1.5">
                  <WarningOutlined />
                  <span>Cảnh báo chênh lệch tồn kho ({importResult.warnings.length} mã):</span>
                </div>
                <div className="max-h-40 overflow-y-auto space-y-1 font-mono text-[11px] text-amber-900 pr-1">
                  {importResult.warnings.map((w, idx) => (
                    <div key={idx} className="bg-amber-100/60 p-1.5 rounded">
                      {w}
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}
      </Modal>
    </div>
  );
};
