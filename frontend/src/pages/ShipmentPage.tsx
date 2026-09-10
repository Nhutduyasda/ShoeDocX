import React, { useState, useEffect, useRef } from 'react';
import {
  Form,
  Input,
  DatePicker,
  Button,
  Select,
  InputNumber,
  Table,
  Space,
  message,
  Card,
  Row,
  Col,
  Tabs,
  AutoComplete,
  Tooltip,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  PlusOutlined,
  DeleteOutlined,
  EyeOutlined,
  DownloadOutlined,
  SaveOutlined,
  FileExcelOutlined,
  HistoryOutlined,
  ReloadOutlined,
  ThunderboltOutlined,
  CalculatorOutlined,
  CheckCircleOutlined,
  CameraOutlined,
  FolderOpenOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { shipmentApi } from '../api/shipmentApi';
import { productMasterApi } from '../api/productMasterApi';
import type {
  CreateShipmentRequest,
  CreateShipmentItem,
  ProductMaster,
  PklPreviewResponse,
  SavedShipmentSummary,
} from '../types';
import { ProcessType } from '../types';
import { PklPreviewModal } from '../components/PklPreviewModal';
import { QuickPasteModal } from '../components/QuickPasteModal';
import { OcrUploadModal } from '../components/OcrUploadModal';

export const ShipmentPage: React.FC = () => {
  const [form] = Form.useForm();
  const [quickPasteVisible, setQuickPasteVisible] = useState<boolean>(false);
  const [ocrModalVisible, setOcrModalVisible] = useState<boolean>(false);
  const tableContainerRef = useRef<HTMLDivElement>(null);
  const [items, setItems] = useState<CreateShipmentItem[]>([
    {
      styleCode: '42072-030',
      description: 'Giày có mũ giày bằng vật liệu dệt và đế ngoài bằng plastic, mũi giày không được gắn bảo vệ. Hàng mới 100%.',
      quantity: 36,
      processType: ProcessType.Standard,
      unitPriceCMT: 3.2,
      unitPriceDAP: 8.2,
      unit: 'đôi',
      pairPerCarton: 12,
    },
    {
      styleCode: '45428-2LX',
      description: 'Giày thể thao nam cổ thấp đế cao su. Hàng mới 100%.',
      quantity: 4032,
      processType: ProcessType.GoKhongMay,
      unitPriceCMT: 3.5,
      unitPriceDAP: 8.5,
      unit: 'đôi',
      pairPerCarton: 12,
    },
    {
      styleCode: '51200-1BK',
      description: 'Giày búp bê nữ có quai cài. Hàng mới 100%.',
      quantity: 5,
      processType: ProcessType.Standard,
      unitPriceCMT: 2.9,
      unitPriceDAP: 7.9,
      unit: 'đôi',
      pairPerCarton: 12,
    },
  ]);

  const [products, setProducts] = useState<ProductMaster[]>([]);
  const [previewData, setPreviewData] = useState<PklPreviewResponse | null>(null);
  const [previewVisible, setPreviewVisible] = useState<boolean>(false);
  const [loadingPreview, setLoadingPreview] = useState<boolean>(false);
  const [exporting, setExporting] = useState<boolean>(false);
  const [saving, setSaving] = useState<boolean>(false);

  // Lịch sử hóa đơn
  const [savedShipments, setSavedShipments] = useState<SavedShipmentSummary[]>([]);
  const [loadingHistory, setLoadingHistory] = useState<boolean>(false);
  const [activeTab, setActiveTab] = useState<string>('create');

  useEffect(() => {
    loadProducts();
    loadShipmentsHistory();
  }, []);

  const loadProducts = async () => {
    try {
      const res = await productMasterApi.getAll();
      setProducts(res);
    } catch {
      // Ignored
    }
  };

  const loadShipmentsHistory = async () => {
    try {
      setLoadingHistory(true);
      const list = await shipmentApi.getShipments();
      setSavedShipments(list);
    } catch {
      // Ignored
    } finally {
      setLoadingHistory(false);
    }
  };

  const buildRequestData = async (): Promise<CreateShipmentRequest | null> => {
    try {
      const values = await form.validateFields();
      if (items.length === 0) {
        message.warning('Vui lòng thêm ít nhất 1 mặt hàng vào đơn.');
        return null;
      }

      for (let i = 0; i < items.length; i++) {
        if (!items[i].styleCode) {
          message.error(`Dòng #${i + 1}: Chưa chọn mã hình thể gốc.`);
          return null;
        }
        if (!items[i].quantity || items[i].quantity <= 0) {
          message.error(`Dòng #${i + 1}: Số lượng phải lớn hơn 0.`);
          return null;
        }
      }

      return {
        invoiceNo: values.invoiceNo,
        invoiceDate: values.invoiceDate.format('YYYY-MM-DDTHH:mm:ss'),
        poSuffix: values.poSuffix || '',
        contractNo: values.contractNo || '',
        customerName: values.customerName || '',
        address: values.address || '',
        deliveryTerms: values.deliveryTerms || 'DAP',
        paymentTerms: values.paymentTerms || 'T/T',
        items: items.map((it) => ({
          ...it,
          fullItemCode:
            it.processType === ProcessType.GoKhongMay
              ? `${it.styleCode}.G ${values.poSuffix || ''}`.trim()
              : `${it.styleCode} ${values.poSuffix || ''}`.trim(),
        })),
      };
    } catch {
      message.error('Vui lòng điền đầy đủ các trường thông tin hóa đơn bắt buộc.');
      return null;
    }
  };

  const handlePreviewPkl = async () => {
    const req = await buildRequestData();
    if (!req) return;

    try {
      setLoadingPreview(true);
      const data = await shipmentApi.previewPkl(req);
      setPreviewData(data);
      setPreviewVisible(true);
    } catch {
      message.error('Không thể tải thông tin xem trước PKL');
    } finally {
      setLoadingPreview(false);
    }
  };

  // Tự động sinh số hóa đơn gợi ý
  const handleGenerateInvoiceNo = () => {
    const year = dayjs().year();
    const randomSeq = Math.floor(100 + Math.random() * 900);
    const newInvoiceNo = `KMHD-NEW${year}-0${randomSeq}`;
    form.setFieldsValue({ invoiceNo: newInvoiceNo });
    message.info(`Đã sinh số hóa đơn gợi ý: ${newInvoiceNo}`);
  };

  const handleExportExcel = async () => {
    const req = await buildRequestData();
    if (!req) return;

    try {
      setExporting(true);
      const blob = await shipmentApi.exportShipmentExcel(req);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;

      // Đặt tên file định dạng: {InvoiceNo}_{Date}.xlsx
      const safeInvoice = req.invoiceNo.trim().replace(/[/\\?%*:|"<>]/g, '-');
      const dateFormatted = dayjs(req.invoiceDate).format('YYYY-MM-DD');
      a.download = `${safeInvoice}_${dateFormatted}.xlsx`;

      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
      message.success(`Xuất file Excel thành công: ${a.download}`);
      loadShipmentsHistory();
    } catch {
      message.error('Lỗi khi xuất file Excel.');
    } finally {
      setExporting(false);
    }
  };

  const handleSaveShipment = async () => {
    const req = await buildRequestData();
    if (!req) return;

    try {
      setSaving(true);
      await shipmentApi.createShipment(req);
      message.success('Lưu đơn hàng vào hệ thống thành công!');
      loadShipmentsHistory();
    } catch {
      message.error('Lỗi khi lưu đơn hàng vào hệ thống.');
    } finally {
      setSaving(false);
    }
  };

  const handleDownloadHistorical = async (id: number, invoiceNo: string) => {
    try {
      message.loading({ content: 'Đang chuẩn bị file Excel...', key: 'dl' });
      const blob = await shipmentApi.exportSavedShipmentExcel(id);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      const safeName = invoiceNo.trim().replace(/[/\\?%*:|"<>]/g, '-');
      const dateFormatted = dayjs().format('YYYY-MM-DD');
      a.download = `${safeName}_${dateFormatted}.xlsx`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
      message.success({ content: `Tải file Excel thành công: ${a.download}`, key: 'dl' });
    } catch {
      message.error({ content: 'Lỗi khi tải file Excel.', key: 'dl' });
    }
  };

  const handleLoadHistoricalOrder = async (id: number) => {
    try {
      message.loading({ content: 'Đang tải lại dữ liệu đơn hàng...', key: 'load-order' });
      const order = await shipmentApi.getShipmentById(id);
      form.setFieldsValue({
        invoiceNo: order.invoiceNo,
        invoiceDate: dayjs(order.invoiceDate),
        poSuffix: order.poSuffix || '',
        contractNo: order.contractNo || '',
        customerName: order.customerName || '',
        address: order.address || '',
        deliveryTerms: order.deliveryTerms || 'DAP',
        paymentTerms: order.paymentTerms || 'T/T',
      });

      if (order.items && order.items.length > 0) {
        const reloadedItems: CreateShipmentItem[] = order.items.map((i) => {
          const cleanCode = i.styleCode.toUpperCase().endsWith('.G')
            ? i.styleCode.toUpperCase().slice(0, -2).trim()
            : i.styleCode.toUpperCase().trim();
          const pm = products.find((p) => p.styleCode.toUpperCase() === cleanCode);

          return {
            styleCode: i.styleCode,
            fullItemCode: i.fullItemCode,
            description: pm?.description || '',
            quantity: i.quantity,
            processType: i.processType,
            unitPriceCMT: i.unitPriceCMT,
            unitPriceDAP: i.unitPriceDAP,
            unit: pm?.unit || 'đôi',
            pairPerCarton: pm?.pairPerCarton || 12,
          };
        });
        setItems(reloadedItems);
      }
      setActiveTab('create');
      message.success({ content: `Đã nạp lại đơn hàng ${order.invoiceNo} vào lưới nhập liệu!`, key: 'load-order' });
    } catch {
      message.error({ content: 'Không thể tải chi tiết đơn hàng cũ.', key: 'load-order' });
    }
  };

  const handleApplyOcr = (ocrItems: CreateShipmentItem[], mode: 'replace' | 'append') => {
    if (mode === 'replace') {
      setItems(ocrItems.length > 0 ? ocrItems : [
        {
          styleCode: '',
          description: '',
          quantity: 12,
          processType: ProcessType.Standard,
          unitPriceCMT: 0,
          unitPriceDAP: 0,
          unit: 'đôi',
          pairPerCarton: 12,
        },
      ]);
      message.success(`Đã thay thế toàn bộ bằng ${ocrItems.length} mặt hàng từ OCR!`);
    } else {
      setItems((prev) => {
        const validPrev = prev.filter((p) => p.styleCode.trim() || p.quantity > 0);
        return [...validPrev, ...ocrItems];
      });
      message.success(`Đã thêm nối tiếp ${ocrItems.length} mặt hàng từ OCR!`);
    }
  };

  const handleAddItem = () => {
    setItems((prev) => [
      ...prev,
      {
        styleCode: '',
        description: '',
        quantity: 12,
        processType: ProcessType.Standard,
        unitPriceCMT: 0,
        unitPriceDAP: 0,
        unit: 'đôi',
        pairPerCarton: 12,
      },
    ]);

    // Focus vào ô mã mới sau khi render
    setTimeout(() => {
      const inputs = tableContainerRef.current?.querySelectorAll<HTMLInputElement>('.style-code-input input');
      if (inputs && inputs.length > 0) {
        inputs[inputs.length - 1]?.focus();
      }
    }, 80);
  };

  const handleRemoveItem = (index: number) => {
    if (items.length <= 1) {
      message.warning('Đơn hàng cần giữ lại ít nhất 1 mặt hàng.');
      return;
    }
    const next = [...items];
    next.splice(index, 1);
    setItems(next);
  };

  const handleProductSelect = (index: number, styleCode: string) => {
    const cleanCode = styleCode.trim().toUpperCase();
    const p = products.find((x) => x.styleCode.trim().toUpperCase() === cleanCode);
    const next = [...items];
    if (p) {
      next[index] = {
        ...next[index],
        styleCode: p.styleCode,
        description: p.description,
        unitPriceCMT: p.unitPriceCMT,
        unitPriceDAP: p.unitPriceDAP,
        unit: p.unit || 'đôi',
        pairPerCarton: p.pairPerCarton || 12,
      };
    } else {
      next[index] = { ...next[index], styleCode };
    }
    setItems(next);
  };

  const handleItemChange = <K extends keyof CreateShipmentItem>(
    index: number,
    field: K,
    value: CreateShipmentItem[K]
  ) => {
    const next = [...items];
    next[index] = { ...next[index], [field]: value };
    setItems(next);
  };

  // Nạp nhanh toàn bộ danh mục sản phẩm từ Master Data
  const handlePopulateAllMasterProducts = () => {
    if (products.length === 0) {
      message.warning('Chưa có dữ liệu danh mục để nạp.');
      return;
    }
    const mapped: CreateShipmentItem[] = products.map((p, idx) => ({
      styleCode: p.styleCode,
      description: p.description,
      quantity: idx === 0 ? 36 : idx === 1 ? 4032 : 120,
      processType: idx % 2 === 1 ? ProcessType.GoKhongMay : ProcessType.Standard,
      unitPriceCMT: p.unitPriceCMT,
      unitPriceDAP: p.unitPriceDAP,
      unit: p.unit || 'đôi',
      pairPerCarton: p.pairPerCarton || 12,
    }));
    setItems(mapped);
    message.success(`Đã nạp nhanh ${mapped.length} mã sản phẩm từ Master Data!`);
  };

  // Áp dụng dữ liệu từ Quick Paste Modal
  const handleApplyQuickPaste = (newItems: CreateShipmentItem[], mode: 'replace' | 'append') => {
    if (mode === 'replace') {
      setItems(newItems);
      message.success(`Đã thay thế toàn bộ bằng ${newItems.length} mặt hàng từ Clipboard!`);
    } else {
      setItems((prev) => [...prev, ...newItems]);
      message.success(`Đã thêm nối tiếp ${newItems.length} mặt hàng từ Clipboard!`);
    }
  };

  // Các chỉ số đối soát (Reconciliation KPIs)
  const totalQuantity = items.reduce((sum, it) => sum + (it.quantity || 0), 0);
  const totalCartons = items.reduce(
    (sum, it) => sum + Math.ceil((it.quantity || 0) / (it.pairPerCarton || 12)),
    0
  );
  const totalAmountCMT = items.reduce(
    (sum, it) => sum + (it.quantity || 0) * (it.unitPriceCMT || 0),
    0
  );
  const totalAmountDAP = items.reduce(
    (sum, it) => sum + (it.quantity || 0) * (it.unitPriceDAP || 0),
    0
  );
  const uniqueStyleCodesCount = new Set(items.map((i) => i.styleCode.trim().toUpperCase()).filter(Boolean)).size;

  const columns: ColumnsType<CreateShipmentItem> = [
    {
      title: 'STT',
      key: 'stt',
      width: 45,
      align: 'center',
      render: (_, __, index) => <span className="font-mono text-xs text-slate-400">{index + 1}</span>,
    },
    {
      title: 'Mã hình thể (Style Code)',
      dataIndex: 'styleCode',
      key: 'styleCode',
      width: 220,
      render: (val: string, _, index) => {
        const matchingProduct = products.find(
          (p) => p.styleCode.trim().toUpperCase() === (val || '').trim().toUpperCase()
        );

        return (
          <div className="space-y-1">
            <AutoComplete
              value={val}
              options={products.map((p) => ({
                value: p.styleCode,
                label: (
                  <div className="flex items-center justify-between py-0.5">
                    <span className="font-mono font-semibold text-slate-800">{p.styleCode}</span>
                    <span className="text-[11px] text-slate-400 truncate max-w-[150px]">{p.description}</span>
                  </div>
                ),
              }))}
              filterOption={(inputValue, option) =>
                (option?.value as string)?.toLowerCase().includes(inputValue.toLowerCase())
              }
              onSelect={(code) => handleProductSelect(index, code)}
              onChange={(newVal) => handleItemChange(index, 'styleCode', newVal)}
              placeholder="Nhập hoặc chọn mã..."
              className="w-full font-mono text-xs font-semibold style-code-input"
            />
            {matchingProduct && (
              <div className="flex items-center space-x-1 text-[11px] text-emerald-600">
                <CheckCircleOutlined className="text-[10px]" />
                <span className="truncate max-w-[200px]" title={matchingProduct.description}>
                  {matchingProduct.description}
                </span>
              </div>
            )}
          </div>
        );
      },
    },
    {
      title: 'Số lượng đi hàng (đôi)',
      dataIndex: 'quantity',
      key: 'quantity',
      width: 140,
      align: 'right',
      render: (val: number, _, index) => (
        <InputNumber
          min={1}
          value={val}
          onChange={(q) => handleItemChange(index, 'quantity', q || 1)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault();
              if (index === items.length - 1) {
                handleAddItem();
              }
            }
          }}
          className="w-full font-mono text-right font-bold text-slate-900"
          placeholder="Số lượng"
        />
      ),
    },
    {
      title: 'Loại công đoạn',
      dataIndex: 'processType',
      key: 'processType',
      width: 155,
      render: (val: ProcessType, _, index) => (
        <Select
          value={val}
          onChange={(proc) => handleItemChange(index, 'processType', proc)}
          className="w-full text-xs"
          options={[
            {
              value: ProcessType.Standard,
              label: (
                <span className="text-xs text-slate-700 font-medium">Thành hình</span>
              ),
            },
            {
              value: ProcessType.GoKhongMay,
              label: (
                <span className="text-xs text-purple-700 font-medium">Gò không may (.G)</span>
              ),
            },
          ]}
        />
      ),
    },
    {
      title: 'Quy cách (đôi/thùng)',
      dataIndex: 'pairPerCarton',
      key: 'pairPerCarton',
      width: 120,
      align: 'right',
      render: (val: number, _, index) => (
        <InputNumber
          min={1}
          value={val || 12}
          onChange={(v) => handleItemChange(index, 'pairPerCarton', v || 12)}
          className="w-full font-mono text-right text-xs"
        />
      ),
    },
    {
      title: 'Đơn giá CMT ($)',
      dataIndex: 'unitPriceCMT',
      key: 'unitPriceCMT',
      width: 110,
      align: 'right',
      render: (val: number, _, index) => (
        <InputNumber
          min={0}
          step={0.01}
          value={val}
          onChange={(v) => handleItemChange(index, 'unitPriceCMT', v || 0)}
          className="w-full font-mono text-right text-xs"
        />
      ),
    },
    {
      title: 'Đơn giá DAP ($)',
      dataIndex: 'unitPriceDAP',
      key: 'unitPriceDAP',
      width: 110,
      align: 'right',
      render: (val: number, _, index) => (
        <InputNumber
          min={0}
          step={0.01}
          value={val}
          onChange={(v) => handleItemChange(index, 'unitPriceDAP', v || 0)}
          className="w-full font-mono text-right text-xs"
        />
      ),
    },
    {
      title: 'Thành tiền DAP ($)',
      key: 'amountDAP',
      width: 130,
      align: 'right',
      render: (_, record) => {
        const amt = (record.quantity || 0) * (record.unitPriceDAP || 0);
        return (
          <span className="font-mono text-xs font-semibold text-slate-900">
            ${amt.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </span>
        );
      },
    },
    {
      title: '',
      key: 'actions',
      width: 45,
      align: 'center',
      render: (_, __, index) => (
        <Button
          type="text"
          danger
          size="small"
          icon={<DeleteOutlined />}
          onClick={() => handleRemoveItem(index)}
          title="Xóa mặt hàng này"
        />
      ),
    },
  ];

  const historyColumns: ColumnsType<SavedShipmentSummary> = [
    {
      title: 'Số Hóa đơn (Invoice No)',
      dataIndex: 'invoiceNo',
      key: 'invoiceNo',
      render: (no: string, record) => (
        <div>
          <span className="font-mono font-bold text-slate-900">{no}</span>
          <div className="text-xs text-slate-400">{record.poSuffix}</div>
        </div>
      ),
    },
    {
      title: 'Ngày lập',
      dataIndex: 'invoiceDate',
      key: 'invoiceDate',
      render: (d: string) => <span className="text-xs font-mono">{dayjs(d).format('DD/MM/YYYY')}</span>,
    },
    {
      title: 'Số Hợp đồng',
      dataIndex: 'contractNo',
      key: 'contractNo',
      render: (c: string) => <span className="text-xs font-mono text-slate-600">{c}</span>,
    },
    {
      title: 'Số mặt hàng',
      dataIndex: 'itemCount',
      key: 'itemCount',
      align: 'center',
      render: (cnt: number) => <span className="font-mono">{cnt}</span>,
    },
    {
      title: 'Tổng số đôi',
      dataIndex: 'totalQuantity',
      key: 'totalQuantity',
      align: 'right',
      render: (q: number) => (
        <span className="font-mono font-semibold text-slate-900">{q.toLocaleString()} đôi</span>
      ),
    },
    {
      title: 'Tổng tiền DAP',
      dataIndex: 'totalAmountDAP',
      key: 'totalAmountDAP',
      align: 'right',
      render: (amt: number) => (
        <span className="font-mono font-semibold text-indigo-600">
          ${amt.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
        </span>
      ),
    },
    {
      title: 'Thao tác',
      key: 'action',
      align: 'center',
      width: 220,
      render: (_, record) => (
        <Space size="small">
          <Button
            size="small"
            icon={<FolderOpenOutlined />}
            className="bg-white border-slate-200 text-slate-700 hover:bg-slate-50 text-xs"
            onClick={() => handleLoadHistoricalOrder(record.id)}
          >
            Mở lại dữ liệu
          </Button>
          <Button
            type="primary"
            size="small"
            icon={<DownloadOutlined />}
            className="bg-indigo-600 hover:bg-indigo-700 text-xs"
            onClick={() => handleDownloadHistorical(record.id, record.invoiceNo)}
          >
            Tải Excel
          </Button>
        </Space>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      {/* Header bar */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 border-b border-slate-200/80 pb-4">
        <div>
          <h1 className="text-lg font-semibold text-slate-900 tracking-tight m-0">
            Xuất Hóa đơn & Đóng gói (INV & PKL)
          </h1>
          <p className="text-xs text-slate-500 mt-1 m-0">
            Nhập liệu bảng kê kho, đối soát tổng số đôi và xuất file Excel 3 sheet chuẩn mẫu thực tế
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Button
            icon={<EyeOutlined />}
            loading={loadingPreview}
            onClick={handlePreviewPkl}
            className="bg-white border-slate-200 text-slate-700 hover:bg-slate-50 hover:border-slate-300 text-xs font-medium h-8"
          >
            Xem trước PKL
          </Button>
          <Button
            icon={<SaveOutlined />}
            loading={saving}
            onClick={handleSaveShipment}
            className="bg-white border-slate-200 text-slate-700 hover:bg-slate-50 hover:border-slate-300 text-xs font-medium h-8"
          >
            Lưu đơn hàng
          </Button>
          <Button
            type="primary"
            icon={<DownloadOutlined />}
            loading={exporting}
            onClick={handleExportExcel}
            className="bg-indigo-600 hover:bg-indigo-700 text-white border-none shadow-xs text-xs font-medium h-8"
          >
            Xuất File Excel (.xlsx)
          </Button>
        </div>
      </div>

      <Tabs
        activeKey={activeTab}
        onChange={setActiveTab}
        items={[
          {
            key: 'create',
            label: (
              <span className="flex items-center space-x-2">
                <FileExcelOutlined />
                <span>Lập Hóa đơn & Xuất hàng</span>
              </span>
            ),
            children: (
              <div className="space-y-5">
                {/* 1. Form Thông tin Hóa đơn Header */}
                <Card
                  title={
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-semibold text-slate-800 uppercase tracking-wider">
                        Thông tin Lô hàng (Shipment Header)
                      </span>
                      <span className="text-[11px] text-slate-400 font-mono">File mẫu: KM3-26-DH233.xlsx</span>
                    </div>
                  }
                  className="bg-white border border-slate-200/80 rounded-xl shadow-xs"
                  size="small"
                >
                  <Form
                    form={form}
                    layout="vertical"
                    initialValues={{
                      invoiceNo: 'KMHD-NEW2026-0233',
                      invoiceDate: dayjs(),
                      poSuffix: '(KM3.PO5.26)',
                      contractNo: 'KM-HANEW/01-2025',
                      customerName: 'CÔNG TY TNHH KINGMAKER III (VIỆT NAM) FOOTWEAR',
                      address: 'SỐ 1, ĐƯỜNG 4A, KCN VIỆT NAM SINGAPORE, XÃ THỌ PHONG, TỈNH QUẢNG NGÃI, VIỆT NAM',
                      deliveryTerms: 'DAP',
                      paymentTerms: 'T/T',
                    }}
                  >
                    <Row gutter={16}>
                      <Col xs={24} sm={12} md={6}>
                        <Form.Item
                          label={
                            <span className="text-xs font-medium text-slate-600">Số Hóa đơn (Invoice No)</span>
                          }
                          name="invoiceNo"
                          rules={[{ required: true, message: 'Nhập số hóa đơn' }]}
                        >
                          <Input
                            className="font-mono text-xs font-semibold border-slate-200 focus:border-indigo-500"
                            placeholder="vd: KMHD-NEW2026-0233"
                            suffix={
                              <Tooltip title="Tự sinh số Invoice gợi ý">
                                <ReloadOutlined
                                  className="text-slate-400 hover:text-indigo-600 cursor-pointer"
                                  onClick={handleGenerateInvoiceNo}
                                />
                              </Tooltip>
                            }
                          />
                        </Form.Item>
                      </Col>

                      <Col xs={24} sm={12} md={6}>
                        <Form.Item
                          label={
                            <div className="flex items-center justify-between w-full">
                              <span className="text-xs font-medium text-slate-600">Ngày Hóa đơn</span>
                              <span className="text-[11px] text-slate-400 font-normal">Xuất: {dayjs().format('MMM DD, YYYY').toUpperCase()}</span>
                            </div>
                          }
                          name="invoiceDate"
                          rules={[{ required: true, message: 'Chọn ngày' }]}
                        >
                          <DatePicker format="DD/MM/YYYY" className="w-full text-xs" placeholder="Chọn ngày lập" />
                        </Form.Item>
                      </Col>

                      <Col xs={24} sm={12} md={6}>
                        <Form.Item
                          label={<span className="text-xs font-medium text-slate-600">Số Hợp đồng (Contract No)</span>}
                          name="contractNo"
                          rules={[{ required: true, message: 'Nhập số hợp đồng' }]}
                        >
                          <Input className="font-mono text-xs border-slate-200 focus:border-indigo-500" placeholder="KM-HANEW/01-2025" />
                        </Form.Item>
                      </Col>

                      <Col xs={24} sm={12} md={6}>
                        <Form.Item
                          label={<span className="text-xs font-medium text-slate-600">Đuôi PO (PoSuffix)</span>}
                          name="poSuffix"
                          rules={[{ required: true, message: 'Nhập đuôi PO' }]}
                        >
                          <Input className="font-mono text-xs border-slate-200 focus:border-indigo-500" placeholder="(KM3.PO5.26)" />
                        </Form.Item>
                      </Col>
                    </Row>

                    <Row gutter={16}>
                      <Col xs={24} md={12}>
                        <Form.Item
                          label={<span className="text-xs font-medium text-slate-600">Tên Khách hàng</span>}
                          name="customerName"
                          rules={[{ required: true, message: 'Nhập tên khách hàng' }]}
                        >
                          <Input className="text-xs border-slate-200 focus:border-indigo-500" placeholder="Tên công ty khách hàng" />
                        </Form.Item>
                      </Col>

                      <Col xs={24} md={12}>
                        <Form.Item
                          label={<span className="text-xs font-medium text-slate-600">Địa chỉ giao hàng</span>}
                          name="address"
                        >
                          <Input className="text-xs border-slate-200 focus:border-indigo-500" placeholder="Địa chỉ giao hàng" />
                        </Form.Item>
                      </Col>
                    </Row>
                  </Form>
                </Card>

                {/* 2. Thanh Đối soát & Thống kê (Reconciliation Bar) - Clean Slate Minimalist */}
                <div className="bg-white border border-slate-200/90 rounded-xl p-5 shadow-xs">
                  <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 lg:gap-0">
                    {/* Cột 1: Đối soát tổng số đôi */}
                    <div className="lg:border-r border-slate-100 lg:pr-6">
                      <div className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
                        Tổng số đôi đi hàng
                      </div>
                      <div className="mt-2 flex items-baseline space-x-1.5">
                        <span className="text-2xl font-bold text-slate-900 tracking-tight font-mono">
                          {totalQuantity.toLocaleString()}
                        </span>
                        <span className="text-xs text-slate-500 font-normal">đôi</span>
                      </div>
                      <div className="mt-1 text-xs text-slate-500">
                        {items.length} dòng hàng &bull; {uniqueStyleCodesCount} mã hình thể
                      </div>
                    </div>

                    {/* Cột 2: Số kiện ước tính */}
                    <div className="lg:border-r border-slate-100 lg:px-6">
                      <div className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
                        Số kiện ước tính
                      </div>
                      <div className="mt-2 flex items-baseline space-x-1.5">
                        <span className="text-xl font-semibold text-slate-800 tracking-tight font-mono">
                          {totalCartons.toLocaleString()}
                        </span>
                        <span className="text-xs text-slate-500 font-normal">thùng</span>
                      </div>
                      <div className="mt-1 text-xs text-slate-500">
                        Dựa trên quy cách đóng gói
                      </div>
                    </div>

                    {/* Cột 3: Tổng tiền CMT */}
                    <div className="lg:border-r border-slate-100 lg:px-6">
                      <div className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
                        Tổng tiền CMT
                      </div>
                      <div className="mt-2 flex items-baseline space-x-1.5">
                        <span className="text-xl font-semibold text-slate-800 tracking-tight font-mono">
                          ${totalAmountCMT.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                        </span>
                      </div>
                      <div className="mt-1 text-xs text-slate-500">
                        Gia công CMT xuất khẩu
                      </div>
                    </div>

                    {/* Cột 4: Tổng trị giá DAP */}
                    <div className="lg:pl-6">
                      <div className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
                        Tổng trị giá DAP
                      </div>
                      <div className="mt-2 flex items-baseline space-x-1.5">
                        <span className="text-xl font-semibold text-slate-800 tracking-tight font-mono">
                          ${totalAmountDAP.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                        </span>
                      </div>
                      <div className="mt-1 text-xs text-slate-500">
                        Thành tiền hóa đơn thương mại
                      </div>
                    </div>
                  </div>
                </div>

                {/* 3. Lưới Nhập liệu Mặt hàng Chi tiết */}
                <Card
                  title={
                    <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
                      <div className="flex items-center space-x-2">
                        <CalculatorOutlined className="text-slate-500 text-sm" />
                        <span className="text-xs font-semibold text-slate-800 uppercase tracking-wider">
                          Lưới Nhập liệu Mặt hàng ({items.length} dòng)
                        </span>
                      </div>
                      <Space wrap size="small">
                        <Button
                          icon={<CameraOutlined className="text-indigo-600" />}
                          onClick={() => setOcrModalVisible(true)}
                          className="bg-white border-slate-300 text-slate-700 hover:bg-slate-50 hover:border-slate-400 font-medium text-xs h-7.5"
                        >
                          Quét ảnh phiếu kho (OCR)
                        </Button>
                        <Button
                          icon={<ThunderboltOutlined className="text-indigo-600" />}
                          onClick={() => setQuickPasteVisible(true)}
                          className="bg-white border-slate-300 text-slate-700 hover:bg-slate-50 hover:border-slate-400 font-medium text-xs h-7.5"
                        >
                          Dán nhanh số liệu kho (Paste from Clipboard)
                        </Button>
                        <Button
                          size="small"
                          onClick={handlePopulateAllMasterProducts}
                          className="bg-white border-slate-200 text-slate-600 hover:bg-slate-50 hover:border-slate-300 text-xs h-7.5"
                        >
                          Nạp nhanh từ Master Data
                        </Button>
                        <Button
                          type="dashed"
                          size="small"
                          icon={<PlusOutlined />}
                          onClick={handleAddItem}
                          className="bg-indigo-50/70 border-indigo-200 text-indigo-700 hover:bg-indigo-100 hover:border-indigo-300 text-xs font-medium h-7.5"
                        >
                          Thêm dòng mới (Enter)
                        </Button>
                      </Space>
                    </div>
                  }
                  className="bg-white border border-slate-200/80 rounded-xl shadow-xs overflow-hidden"
                  size="small"
                >
                  <div ref={tableContainerRef}>
                    <Table
                      dataSource={items}
                      columns={columns}
                      rowKey={(_, index) => `${index}`}
                      pagination={false}
                      size="middle"
                      className="border border-slate-100 rounded"
                      summary={() => (
                        <Table.Summary fixed>
                          <Table.Summary.Row className="bg-slate-50/90 font-medium text-slate-800 border-t border-slate-200">
                            <Table.Summary.Cell index={0} colSpan={2}>
                              <span className="pl-2 font-semibold text-xs text-slate-700 uppercase tracking-wider">
                                TỔNG CỘNG LÔ HÀNG:
                              </span>
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={2} align="right">
                              <span className="font-mono text-slate-900 font-bold text-sm">
                                {totalQuantity.toLocaleString()} đôi
                              </span>
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={3} colSpan={2} align="center">
                              <span className="font-mono text-xs text-slate-600">
                                Ước tính: {totalCartons.toLocaleString()} kiện
                              </span>
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={5} align="right">
                              <span className="font-mono text-xs text-slate-600">
                                CMT: ${totalAmountCMT.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                              </span>
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={6} colSpan={2} align="right">
                              <span className="font-mono text-sm text-slate-900 font-bold">
                                DAP: ${totalAmountDAP.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                              </span>
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={8} />
                          </Table.Summary.Row>
                        </Table.Summary>
                      )}
                    />
                  </div>

                  <div className="mt-3 flex flex-col sm:flex-row sm:items-center sm:justify-between text-xs text-slate-400 px-1 gap-1">
                    <span>
                      * Mẹo nhập nhanh: Nhấn <kbd className="px-1.5 py-0.5 bg-slate-100 border border-slate-200 rounded text-[11px] font-mono text-slate-600">Enter</kbd> ở ô Số lượng dòng cuối cùng để tự động thêm dòng mới.
                    </span>
                    <span>
                      Dải số kiện PKL và file Excel đa sheet sẽ tự động sinh khi bấm <strong>Xem trước PKL</strong> hoặc <strong>Xuất File Excel</strong>.
                    </span>
                  </div>
                </Card>
              </div>
            ),
          },
          {
            key: 'history',
            label: (
              <span className="flex items-center space-x-2">
                <HistoryOutlined />
                <span>Lịch sử Hóa đơn ({savedShipments.length})</span>
              </span>
            ),
            children: (
              <Card className="shadow-2xs border-slate-200 rounded-lg">
                <div className="flex justify-between items-center mb-4">
                  <span className="text-sm font-medium text-slate-600">
                    Các hóa đơn Commercial Invoice đã lập và lưu trong hệ thống
                  </span>
                  <Button
                    size="small"
                    icon={<ReloadOutlined />}
                    onClick={loadShipmentsHistory}
                    loading={loadingHistory}
                  >
                    Làm mới
                  </Button>
                </div>
                <Table
                  dataSource={savedShipments}
                  columns={historyColumns}
                  rowKey="id"
                  loading={loadingHistory}
                  pagination={{ pageSize: 10 }}
                  size="middle"
                />
              </Card>
            ),
          },
        ]}
      />

      {/* Modal Dán nhanh số liệu từ Clipboard */}
      <QuickPasteModal
        visible={quickPasteVisible}
        onClose={() => setQuickPasteVisible(false)}
        products={products}
        onApply={handleApplyQuickPaste}
      />

      {/* Modal Xem trước Phân rã Đóng gói PKL */}
      <PklPreviewModal
        visible={previewVisible}
        onClose={() => setPreviewVisible(false)}
        data={previewData}
        onExportExcel={handleExportExcel}
        exporting={exporting}
      />

      {/* Modal Quét ảnh phiếu kho & Bóc tách OCR */}
      <OcrUploadModal
        visible={ocrModalVisible}
        onClose={() => setOcrModalVisible(false)}
        products={products}
        onApply={handleApplyOcr}
      />
    </div>
  );
};
