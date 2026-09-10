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
  Alert,
  message,
  Drawer,
  Modal,
  Form,
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
} from '@ant-design/icons';
import dayjs, { Dayjs } from 'dayjs';
import { settlementApi } from '../api/settlementApi';
import type {
  SettlementItem,
  SettlementReport,
  SettlementPeriodSummary,
  SaveSettlementPeriodRequest,
  SettlementDrillDownItem,
} from '../types';

const { RangePicker } = DatePicker;

export const CustomsSettlementPage: React.FC = () => {
  // Year & Filter states
  const currentYear = dayjs().year();
  const [selectedYear, setSelectedYear] = useState<number>(currentYear);
  const [dateRange, setDateRange] = useState<[Dayjs, Dayjs]>([
    dayjs(`${currentYear}-01-01`),
    dayjs(),
  ]);
  const [contractNo, setContractNo] = useState<string>('');
  const [tableSearch, setTableSearch] = useState<string>('');

  // Report & Items state
  const [report, setReport] = useState<SettlementReport | null>(null);
  const [items, setItems] = useState<SettlementItem[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [exporting, setExporting] = useState<boolean>(false);
  const [saving, setSaving] = useState<boolean>(false);

  // Drill-down Modal state
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
  const [saveForm] = Form.useForm();

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
    } catch (err) {
      console.error('Lỗi khi tải lịch sử kỳ quyết toán:', err);
    } finally {
      setHistoryLoading(false);
    }
  }, []);

  useEffect(() => {
    loadSavedPeriods();
  }, [loadSavedPeriods]);

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
        contractNo: contractNo.trim() || undefined,
      });

      setReport(res);
      setItems(res.items || []);
      message.success(
        `Đã tổng hợp thành công ${res.items.length} mã hàng từ ${res.clearedOrderCount} đơn hàng E52 đã thông quan.`
      );
    } catch (err: any) {
      console.error('Lỗi tổng hợp quyết toán:', err);
      message.error(err.response?.data?.message || 'Không thể tổng hợp số liệu quyết toán.');
    } finally {
      setLoading(false);
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
    } catch (err) {
      console.error('Lỗi khi drill-down tờ khai:', err);
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

        return updated;
      })
    );
  };

  // Open Save Modal
  const handleOpenSaveModal = () => {
    if (!report || items.length === 0) {
      message.warning('Chưa có dữ liệu quyết toán để lưu.');
      return;
    }

    saveForm.setFieldsValue({
      year: selectedYear || dateRange[0].year(),
      contractNo: contractNo || report.contractNo || '',
      companyName: report.companyName,
      taxCode: report.taxCode,
      address: report.address,
      note: report.note || `Kỳ quyết toán năm ${selectedYear} (${dateRange[0].format('DD/MM/YYYY')} - ${dateRange[1].format('DD/MM/YYYY')})`,
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
        contractNo: values.contractNo?.trim() || undefined,
        companyName: values.companyName?.trim(),
        taxCode: values.taxCode?.trim(),
        address: values.address?.trim(),
        note: values.note?.trim(),
        items: items,
      };

      await settlementApi.saveSettlement(payload);
      message.success('Đã lưu kỳ báo cáo quyết toán thành công vào hệ thống.');
      setSaveModalOpen(false);
      loadSavedPeriods();
    } catch (err: any) {
      console.error('Lỗi khi lưu kỳ quyết toán:', err);
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
      if (data.contractNo) {
        setContractNo(data.contractNo);
      }
      setHistoryDrawerOpen(false);
      message.success(`Đã tải kỳ quyết toán năm ${data.year} (${data.items.length} mã hàng).`);
    } catch (err) {
      console.error('Lỗi khi tải chi tiết kỳ quyết toán:', err);
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
      a.download = `Mau16_BCQT_SP_GSQL_Nam_${year}_${dayjs().format('YYYYMMDDHHmmss')}.xlsx`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
      message.success(`Đã tải file Excel Mẫu 16 năm ${year}.`);
    } catch (err) {
      console.error('Lỗi khi tải file Excel từ kỳ lưu:', err);
      message.error('Không thể tải file Excel.');
    }
  };

  // Export Excel Mẫu 16 current report
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
        contractNo: contractNo.trim() || report.contractNo,
      };

      const blob = await settlementApi.exportSettlementExcel(currentReport);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `Mau16_BCQT_SP_GSQL_Nam_${selectedYear}_${dateRange[0].format('YYYYMMDD')}_${dateRange[1].format('YYYYMMDD')}.xlsx`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);

      message.success('Xuất file Excel Mẫu 16/BCQT-SP-GSQL (kèm Sheet Drill-down) thành công.');
    } catch (err) {
      console.error('Lỗi khi xuất file Excel Mẫu 16:', err);
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

  // Filtered items by search query
  const filteredItems = useMemo(() => {
    if (!tableSearch.trim()) return items;
    const q = tableSearch.toLowerCase().trim();
    return items.filter(
      (item) =>
        item.productCode.toLowerCase().includes(q) ||
        (item.productName && item.productName.toLowerCase().includes(q))
    );
  }, [items, tableSearch]);

  // Columns definition
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
      width: 135,
      render: (code: string) => (
        <span className="font-mono text-xs font-semibold text-slate-900 bg-slate-100 px-1.5 py-0.5 rounded border border-slate-200">
          {code}
        </span>
      ),
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
      render: (name: string) => (
        <Tooltip title={name}>
          <span className="text-xs text-slate-700">{name || '-'}</span>
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
      width: 60,
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
      width: 125,
      align: 'right',
      render: (val: number, record) => (
        <InputNumber
          size="small"
          min={0}
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
      width: 135,
      align: 'right',
      render: (val: number, record) => (
        <InputNumber
          size="small"
          min={0}
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
          <Tooltip title="Xem chi tiết các tờ khai hải quan cấu thành">
            <Button
              type="text"
              size="small"
              icon={<EyeOutlined className="text-blue-600 text-xs" />}
              onClick={() => handleOpenDrillDown(record.productCode)}
              className="h-6 w-6 p-0 hover:bg-blue-100/50 flex items-center justify-center rounded"
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
      width: 110,
      align: 'right',
      render: (val: number, record) => (
        <InputNumber
          size="small"
          min={0}
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
      width: 135,
      align: 'right',
      render: (val: number) => {
        const isNegative = val < 0;
        return (
          <span
            className={`font-mono text-xs font-semibold px-2 py-0.5 rounded border ${
              isNegative
                ? 'text-rose-700 bg-rose-50 border-rose-200'
                : 'text-slate-900 bg-slate-50 border-slate-200'
            }`}
          >
            {val.toLocaleString()}
          </span>
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
      width: 140,
      render: (text: string, record) => (
        <Input
          size="small"
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
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 pb-4 border-b border-slate-200">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-xl font-semibold text-slate-900 tracking-tight m-0">
              Báo cáo Quyết toán Hải quan (Mẫu 16/BCQT-SP-GSQL)
            </h1>
            <Tag className="bg-blue-50 text-blue-700 border-blue-200 text-xs m-0">
              Phụ lục II TT 39/2018/TT-BTC
            </Tag>
          </div>
          <p className="text-xs text-slate-500 mt-1 m-0">
            Tự động tổng hợp số liệu xuất khẩu loại hình gia công (E52) đã thông quan để lập và nộp báo cáo quyết toán Hải quan
          </p>
        </div>

        <div className="flex items-center gap-2">
          <Button
            icon={<HistoryOutlined />}
            onClick={() => {
              loadSavedPeriods();
              setHistoryDrawerOpen(true);
            }}
            className="text-xs text-slate-700 hover:text-blue-600 border-slate-200"
          >
            Kỳ đã lưu ({savedPeriods.length})
          </Button>

          <Button
            icon={<SaveOutlined />}
            onClick={handleOpenSaveModal}
            disabled={items.length === 0}
            className="text-xs text-slate-700 hover:text-blue-600 border-slate-200"
          >
            Lưu bảng quyết toán
          </Button>

          <Button
            type="primary"
            icon={<DownloadOutlined />}
            onClick={handleExportExcel}
            loading={exporting}
            disabled={items.length === 0}
            className="text-xs bg-emerald-600 hover:bg-emerald-700 border-emerald-600 font-medium"
          >
            Xuất Excel Mẫu 16/BCQT-SP-GSQL
          </Button>
        </div>
      </div>

      {/* Filter / Calculation Toolbar */}
      <div className="bg-white border border-slate-200 rounded-lg p-4">
        <div className="grid grid-cols-1 md:grid-cols-12 gap-3 items-center">
          {/* Year selector */}
          <div className="md:col-span-2">
            <span className="text-xs font-medium text-slate-600 block mb-1">
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
            <span className="text-xs font-medium text-slate-600 block mb-1">
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
            <span className="text-xs font-medium text-slate-600 block mb-1">
              Số hợp đồng gia công:
            </span>
            <Input
              value={contractNo}
              onChange={(e) => setContractNo(e.target.value)}
              placeholder="VD: KM-HANEW/01-2025"
              className="w-full text-xs"
              allowClear
            />
          </div>

          {/* Action Button */}
          <div className="md:col-span-3 flex items-end gap-2 pt-5">
            <Button
              type="primary"
              icon={<CalculatorOutlined />}
              onClick={handleCalculate}
              loading={loading}
              className="flex-1 text-xs bg-blue-600 hover:bg-blue-700"
            >
              Tổng hợp Dữ liệu từ Tờ khai E52
            </Button>
            <Button
              icon={<ReloadOutlined />}
              onClick={() => {
                handleYearChange(currentYear);
                setContractNo('');
                setItems([]);
                setReport(null);
              }}
              title="Đặt lại bộ lọc"
              className="text-xs text-slate-600"
            />
          </div>
        </div>
      </div>

      {/* KPI Cards Row */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-white border border-slate-200 rounded-lg p-4">
          <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
            Tổng số mã sản phẩm
          </div>
          <div className="mt-2 flex items-baseline justify-between">
            <span className="text-2xl font-semibold font-mono text-slate-900">
              {items.length}
            </span>
            <span className="text-xs text-slate-400">mã hình thể</span>
          </div>
          <div className="mt-2 text-[11px] text-slate-500">
            {report?.clearedOrderCount ? `Từ ${report.clearedOrderCount} đơn E52 thông quan` : 'Chờ tổng hợp'}
          </div>
        </div>

        <div className="bg-white border border-slate-200 rounded-lg p-4">
          <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
            Lượng tồn đầu kỳ (Cột 5)
          </div>
          <div className="mt-2 flex items-baseline justify-between">
            <span className="text-2xl font-semibold font-mono text-slate-900">
              {totalOpening.toLocaleString()}
            </span>
            <span className="text-xs text-slate-400">đôi</span>
          </div>
          <div className="mt-2 text-[11px] text-slate-500">
            Kế thừa từ kỳ trước hoặc hiệu chỉnh
          </div>
        </div>

        <div className="bg-white border border-slate-200 rounded-lg p-4">
          <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
            Lượng xuất trong kỳ E52 (Cột 7)
          </div>
          <div className="mt-2 flex items-baseline justify-between">
            <span className="text-2xl font-semibold font-mono text-blue-600">
              {totalExport.toLocaleString()}
            </span>
            <span className="text-xs text-slate-400">đôi</span>
          </div>
          <div className="mt-2 text-[11px] text-slate-500">
            Tổng xuất khẩu gia công đã thông quan
          </div>
        </div>

        <div className="bg-white border border-slate-200 rounded-lg p-4">
          <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
            Lượng tồn cuối kỳ (Cột 9)
          </div>
          <div className="mt-2 flex items-baseline justify-between">
            <span
              className={`text-2xl font-semibold font-mono ${
                totalClosing < 0 ? 'text-rose-600' : 'text-emerald-600'
              }`}
            >
              {totalClosing.toLocaleString()}
            </span>
            <span className="text-xs text-slate-400">đôi</span>
          </div>
          <div className="mt-2 text-[11px] text-slate-500">
            {negativeItems.length > 0 ? (
              <span className="text-rose-600 font-medium">
                {negativeItems.length} mã bị âm tồn!
              </span>
            ) : (
              'Cân đối tồn kho hợp lệ'
            )}
          </div>
        </div>
      </div>

      {/* Warning Alert if negative closing balance detected */}
      {negativeItems.length > 0 && (
        <Alert
          type="warning"
          showIcon
          icon={<WarningOutlined className="text-amber-500" />}
          message={
            <span className="font-semibold text-slate-800">
              Cảnh báo số dư cuối kỳ âm tại {negativeItems.length} mã hàng
            </span>
          }
          description={
            <div className="text-xs text-slate-600 mt-1">
              Phát hiện {negativeItems.length} mã hàng có Lượng tồn cuối kỳ &lt; 0 (
              {negativeItems.slice(0, 5).map((i) => i.productCode).join(', ')}
              {negativeItems.length > 5 ? '...' : ''}). Vui lòng cập nhật bổ sung Lượng tồn đầu kỳ (cột 5) hoặc
              Lượng nhập sản xuất trong kỳ (cột 6) để đảm bảo tính pháp lý trước khi nộp báo cáo Hải quan.
            </div>
          }
          className="border-amber-200 bg-amber-50"
        />
      )}

      {/* Settlement Table Panel */}
      <div className="bg-white border border-slate-200 rounded-lg shadow-sm overflow-hidden">
        {/* Table Toolbar */}
        <div className="p-4 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 bg-slate-50/50">
          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold text-slate-800">
              Bảng dữ liệu Quyết toán Sản phẩm Xuất khẩu (Mẫu 16/BCQT-SP-GSQL)
            </span>
            <Tag className="bg-slate-100 text-slate-600 border-slate-200 text-xs m-0">
              {filteredItems.length} / {items.length} mã hàng
            </Tag>
          </div>

          <div className="flex items-center gap-2">
            <Input
              size="small"
              placeholder="Tìm mã sản phẩm, tên..."
              prefix={<SearchOutlined className="text-slate-400" />}
              value={tableSearch}
              onChange={(e) => setTableSearch(e.target.value)}
              className="w-60 text-xs"
              allowClear
            />
          </div>
        </div>

        {/* Table */}
        <Table
          dataSource={filteredItems}
          columns={columns}
          rowKey="id"
          loading={loading}
          size="small"
          scroll={{ x: 1250 }}
          pagination={{
            defaultPageSize: 20,
            showSizeChanger: true,
            pageSizeOptions: ['10', '20', '50', '100'],
            size: 'small',
          }}
          locale={{
            emptyText: (
              <div className="py-12 text-center text-slate-400">
                <InfoCircleOutlined className="text-3xl mb-2 text-slate-300 block" />
                <p className="text-xs m-0">
                  Chưa có dữ liệu quyết toán. Vui lòng thiết lập bộ lọc và bấm{' '}
                  <strong className="text-blue-600">"Tổng hợp Dữ liệu từ Tờ khai E52"</strong>.
                </p>
              </div>
            ),
          }}
          summary={() => {
            if (filteredItems.length === 0) return null;
            return (
              <Table.Summary fixed>
                <Table.Summary.Row className="bg-slate-100 font-semibold text-xs border-t border-slate-300">
                  <Table.Summary.Cell index={0} colSpan={4} align="center">
                    <span className="text-slate-800 tracking-wider">TỔNG CỘNG</span>
                  </Table.Summary.Cell>
                  <Table.Summary.Cell index={4} align="right">
                    <span className="font-mono text-slate-900">{totalOpening.toLocaleString()}</span>
                  </Table.Summary.Cell>
                  <Table.Summary.Cell index={5} align="right">
                    <span className="font-mono text-slate-900">{totalProduction.toLocaleString()}</span>
                  </Table.Summary.Cell>
                  <Table.Summary.Cell index={6} align="right">
                    <span className="font-mono text-blue-600">{totalExport.toLocaleString()}</span>
                  </Table.Summary.Cell>
                  <Table.Summary.Cell index={7} align="right">
                    <span className="font-mono text-slate-900">{totalOther.toLocaleString()}</span>
                  </Table.Summary.Cell>
                  <Table.Summary.Cell index={8} align="right">
                    <span className={`font-mono ${totalClosing < 0 ? 'text-rose-600' : 'text-emerald-600'}`}>
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

      {/* Drill-down Modal: Chi tiết các tờ khai đã thông quan cấu thành */}
      <Modal
        title={
          <div className="flex items-center gap-2">
            <span>Chi tiết Tờ khai Hải quan xuất khẩu (E52) - Mã:</span>
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
        width={900}
      >
        <div className="mt-3">
          <Table
            dataSource={drillDownItems}
            rowKey={(r, idx) => `${r.orderId}_${idx}`}
            loading={drillDownLoading}
            size="small"
            pagination={{ defaultPageSize: 10, size: 'small' }}
            columns={[
              {
                title: 'STT',
                key: 'idx',
                width: 50,
                align: 'center',
                render: (_, __, i) => <span className="text-xs text-slate-500">{i + 1}</span>,
              },
              {
                title: 'Số tờ khai',
                dataIndex: 'declarationNo',
                key: 'declarationNo',
                width: 130,
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
                width: 140,
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
                title: 'Đơn giá DAP',
                dataIndex: 'unitPriceDAP',
                key: 'unitPriceDAP',
                width: 100,
                align: 'right',
                render: (p: number) => (
                  <span className="font-mono text-xs text-slate-700">${p.toFixed(2)}</span>
                ),
              },
            ]}
            summary={(pageData) => {
              const totalQty = pageData.reduce((sum, item) => sum + item.quantity, 0);
              return (
                <Table.Summary.Row className="bg-slate-50 font-semibold text-xs">
                  <Table.Summary.Cell index={0} colSpan={5} align="center">
                    <span>TỔNG CỘNG ({drillDownItems.length} tờ khai)</span>
                  </Table.Summary.Cell>
                  <Table.Summary.Cell index={5} align="right">
                    <span className="font-mono text-blue-600 font-bold">{totalQty.toLocaleString()}</span>
                  </Table.Summary.Cell>
                  <Table.Summary.Cell index={6} />
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
        width={700}
        open={historyDrawerOpen}
        onClose={() => setHistoryDrawerOpen(false)}
      >
        <Table
          dataSource={savedPeriods}
          rowKey="id"
          loading={historyLoading}
          size="small"
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
                </div>
              ),
            },
            {
              title: 'Mã hàng',
              dataIndex: 'itemCount',
              key: 'itemCount',
              width: 70,
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
        title="Lưu bảng báo cáo quyết toán"
        open={saveModalOpen}
        onOk={handleConfirmSave}
        onCancel={() => setSaveModalOpen(false)}
        confirmLoading={saving}
        okText="Xác nhận lưu"
        cancelText="Hủy"
        width={550}
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
    </div>
  );
};
