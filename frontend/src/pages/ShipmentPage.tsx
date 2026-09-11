import { useState, useEffect, useRef, useImperativeHandle, forwardRef } from 'react';
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
  Row,
  Col,
  AutoComplete,
  Tooltip,
  Modal,
  Tag,
  Radio,
  Popconfirm,
  Empty,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  PlusOutlined,
  DeleteOutlined,
  EyeOutlined,
  DownloadOutlined,
  SaveOutlined,
  ReloadOutlined,
  ThunderboltOutlined,
  CameraOutlined,
  FolderOpenOutlined,
  NumberOutlined,
  EditOutlined,
  AppstoreOutlined,
  SearchOutlined,
  CheckCircleFilled,
  AuditOutlined,
  FileExcelOutlined,
  ExclamationCircleOutlined,
  HistoryOutlined,
  RocketOutlined,
  QuestionCircleOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { shipmentApi, invoiceNoToFileName, extractSequenceNumber, toStandardFileName } from '../api/shipmentApi';
import { productMasterApi } from '../api/productMasterApi';
import { hasCompletedTour, startOnboardingTour } from '../services/tourService';
import { customsApi } from '../api/customsApi';
import type { NavTabKey } from '../layouts/AppLayout';
import type {
  CreateShipmentRequest,
  CreateShipmentItem,
  ProductMaster,
  PklPreviewResponse,
  SavedShipmentSummary,
  SequenceInfo,
} from '../types';
import { ProcessType, ShipmentStatus, ExportSequencePriority } from '../types';
import { PklPreviewModal } from '../components/PklPreviewModal';
import { QuickPasteModal } from '../components/QuickPasteModal';
import { OcrUploadModal } from '../components/OcrUploadModal';
import { BatchOcrModal } from '../components/BatchOcrModal';
import { CustomsSyncModal } from '../components/CustomsSyncModal';

export interface ShipmentPageRef {
  loadHistoricalOrder: (id: number) => void;
  openOcrModal: () => void;
  openBatchOcrModal: () => void;
}

interface ShipmentPageProps {
  activeNavTab?: NavTabKey;
  onTabChange?: (tab: NavTabKey) => void;
  initialOrderIdToLoad?: number | null;
}

export const ShipmentPage = forwardRef<ShipmentPageRef, ShipmentPageProps>(({
  activeNavTab = 'shipment',
  onTabChange,
  initialOrderIdToLoad,
}, ref) => {
  const [form] = Form.useForm();
  const [quickPasteVisible, setQuickPasteVisible] = useState<boolean>(false);
  const [ocrModalVisible, setOcrModalVisible] = useState<boolean>(false);
  const [batchOcrModalVisible, setBatchOcrModalVisible] = useState<boolean>(false);
  const tableContainerRef = useRef<HTMLDivElement>(null);

  const [items, setItems] = useState<CreateShipmentItem[]>([]);

  const [products, setProducts] = useState<ProductMaster[]>([]);
  const [previewData, setPreviewData] = useState<PklPreviewResponse | null>(null);
  const [previewVisible, setPreviewVisible] = useState<boolean>(false);
  const [loadingPreview, setLoadingPreview] = useState<boolean>(false);
  const [exporting, setExporting] = useState<boolean>(false);
  const [saving, setSaving] = useState<boolean>(false);

  // Sequence management
  const [sequenceInfo, setSequenceInfo] = useState<SequenceInfo | null>(null);
  const [sequenceEditVisible, setSequenceEditVisible] = useState<boolean>(false);
  const [sequenceEditValue, setSequenceEditValue] = useState<number>(1);

  // Export Sequence Modal (khi có cả 2 loại hàng Thành hình và Gò)
  const [exportSequenceModalVisible, setExportSequenceModalVisible] = useState<boolean>(false);
  const [exportPriority, setExportPriority] = useState<ExportSequencePriority>(ExportSequencePriority.StandardFirst);
  const [startInvoiceNum, setStartInvoiceNum] = useState<number>(233);
  const [pendingExportRequest, setPendingExportRequest] = useState<CreateShipmentRequest | null>(null);

  // Lịch sử hóa đơn
  const [savedShipments, setSavedShipments] = useState<SavedShipmentSummary[]>([]);
  const [historySearchText, setHistorySearchText] = useState<string>('');
  const [loadingHistory, setLoadingHistory] = useState<boolean>(false);
  const [activeTab, setActiveTab] = useState<string>(activeNavTab === 'history' ? 'history' : 'create');

  // Hậu kiểm & Đối soát Hải quan
  const [customsModalOpen, setCustomsModalOpen] = useState<boolean>(false);
  const [selectedOrderForCustoms, setSelectedOrderForCustoms] = useState<SavedShipmentSummary | null>(null);

  useImperativeHandle(ref, () => ({
    loadHistoricalOrder: (id: number) => {
      handleLoadHistoricalOrder(id);
    },
    openOcrModal: () => {
      setOcrModalVisible(true);
    },
    openBatchOcrModal: () => {
      setBatchOcrModalVisible(true);
    },
  }));

  useEffect(() => {
    if (activeNavTab === 'history') {
      setActiveTab('history');
      loadShipmentsHistory();
    } else if (activeNavTab === 'shipment') {
      setActiveTab('create');
    } else if (activeNavTab === 'ocr') {
      setActiveTab('create');
      setOcrModalVisible(true);
    }
  }, [activeNavTab]);

  useEffect(() => {
    loadProducts();
    loadShipmentsHistory();
    loadSequence();

    // Tự động kích hoạt tour hướng dẫn nếu là lần đầu người dùng vào trang
    if (!hasCompletedTour()) {
      const timer = setTimeout(() => {
        startOnboardingTour();
      }, 800);
      return () => clearTimeout(timer);
    }
  }, []);

  useEffect(() => {
    if (initialOrderIdToLoad) {
      handleLoadHistoricalOrder(initialOrderIdToLoad);
    }
  }, [initialOrderIdToLoad]);

  const loadSequence = async () => {
    try {
      const info = await shipmentApi.getSequence();
      setSequenceInfo(info);
      const currentInvoice = form.getFieldValue('invoiceNo');
      if (!currentInvoice || currentInvoice === 'KMHD-NEW2026-0233') {
        form.setFieldValue('invoiceNo', info.previewInvoiceNo);
        setStartInvoiceNum(info.nextNumber);
        setSequenceEditValue(info.nextNumber);
      } else {
        const seq = extractSequenceNumber(currentInvoice);
        if (seq) {
          setStartInvoiceNum(seq);
          setSequenceEditValue(seq);
        }
      }
    } catch {
      // Ignored
    }
  };

  const handleInvoiceNoChange = (val: string) => {
    const clean = val.trim();
    const seq = extractSequenceNumber(clean);
    if (seq !== null && seq > 0) {
      setStartInvoiceNum(seq);
      setSequenceEditValue(seq);
      setSequenceInfo({
        nextNumber: seq,
        previewInvoiceNo: clean,
        previewFileName: toStandardFileName(seq),
      });
    }
  };

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
          message.error(`Dòng #${i + 1}: Chưa chọn mã hình thể.`);
          return null;
        }
        if (!items[i].quantity || items[i].quantity <= 0) {
          message.error(`Dòng #${i + 1}: Số lượng phải lớn hơn 0.`);
          return null;
        }
      }

      // Ưu tiên giá trị ô Input (Source of Truth)
      const currentInvoiceNo = (form.getFieldValue('invoiceNo') || values.invoiceNo || '').trim();
      const match = currentInvoiceNo.match(/\d+$/);
      const currentSeq = match ? parseInt(match[0], 10) : (startInvoiceNum || sequenceInfo?.nextNumber || 233);

      return {
        invoiceNo: currentInvoiceNo,
        startInvoiceNumber: currentSeq,
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
      message.error('Vui lòng điền đầy đủ các thông tin hóa đơn bắt buộc.');
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
      message.error('Không thể tải thông tin xem trước Packing List.');
    } finally {
      setLoadingPreview(false);
    }
  };

  const handleGenerateInvoiceNo = async () => {
    try {
      const info = await shipmentApi.getSequence();
      setSequenceInfo(info);
      setStartInvoiceNum(info.nextNumber);
      setSequenceEditValue(info.nextNumber);
      form.setFieldsValue({ invoiceNo: info.previewInvoiceNo });
      message.info(`Đã điền số hóa đơn tiếp theo: ${info.previewInvoiceNo}`);
    } catch {
      const year = dayjs().year();
      const randomSeq = Math.floor(100 + Math.random() * 900);
      const newInvoiceNo = `KMHD-NEW${year}-0${randomSeq}`;
      form.setFieldsValue({ invoiceNo: newInvoiceNo });
      setStartInvoiceNum(randomSeq);
      setSequenceEditValue(randomSeq);
      message.info(`Đã sinh số hóa đơn gợi ý: ${newInvoiceNo}`);
    }
  };

  const executeExport = async (req: CreateShipmentRequest) => {
    try {
      setExporting(true);
      const result = await shipmentApi.exportShipmentExcel(req);
      const { blob, contentType, exportSummary } = result;

      const isZip = contentType.includes('zip');

      // Tự động làm mới danh sách lịch sử và cập nhật số sequence tiếp theo cho form
      await loadShipmentsHistory();
      try {
        const nextInfo = await shipmentApi.getSequence();
        setSequenceInfo(nextInfo);
        form.setFieldValue('invoiceNo', nextInfo.previewInvoiceNo);
      } catch {
        // Ignored
      }

      if (isZip && exportSummary?.hasTwoFiles) {
        const firstNum = req.priority === ExportSequencePriority.GoFirst 
          ? exportSummary.goSequenceNumber 
          : exportSummary.standardSequenceNumber;
        const secondNum = req.priority === ExportSequencePriority.GoFirst 
          ? exportSummary.standardSequenceNumber 
          : exportSummary.goSequenceNumber;
        const zipName = `KM3-26-DH${firstNum}-${secondNum}.zip`;
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = zipName;
        document.body.appendChild(a);
        a.click();
        a.remove();
        window.URL.revokeObjectURL(url);

        const isStandardFirst = req.priority !== ExportSequencePriority.GoFirst;
        const line1 = isStandardFirst
          ? `• ${exportSummary.standardInvoiceNo} (${exportSummary.standardFileName}) — Thành hình: ${(exportSummary.standardTotalQuantity ?? 0).toLocaleString()} đôi`
          : `• ${exportSummary.goInvoiceNo} (${exportSummary.goFileName}) — Gò: ${(exportSummary.goTotalQuantity ?? 0).toLocaleString()} đôi`;
        const line2 = isStandardFirst
          ? `• ${exportSummary.goInvoiceNo} (${exportSummary.goFileName}) — Gò: ${(exportSummary.goTotalQuantity ?? 0).toLocaleString()} đôi`
          : `• ${exportSummary.standardInvoiceNo} (${exportSummary.standardFileName}) — Thành hình: ${(exportSummary.standardTotalQuantity ?? 0).toLocaleString()} đôi`;

        message.success({
          content: (
            <div className="space-y-1">
              <div>
                Đã xuất file ZIP và tự động lưu <strong>2 hóa đơn</strong> vào Lịch sử chứng từ:
              </div>
              <div className="text-xs text-slate-600 pl-2">
                <div>{line1}</div>
                <div>{line2}</div>
              </div>
              <Button
                type="link"
                size="small"
                className="p-0 text-blue-600 underline text-xs mt-1 block"
                onClick={() => {
                  setActiveTab('history');
                  onTabChange?.('history');
                }}
              >
                Xem trong Lịch sử chứng từ &rarr;
              </Button>
            </div>
          ),
          duration: 10,
        });
      } else {
        const fileName = exportSummary?.singleFileName ?? invoiceNoToFileName(req.invoiceNo);
        const savedInvoiceNo = exportSummary?.singleInvoiceNo ?? req.invoiceNo;
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        a.remove();
        window.URL.revokeObjectURL(url);

        message.success({
          content: (
            <div>
              <span>
                Đã xuất file <strong>{fileName}</strong> và tự động lưu hóa đơn <strong>{savedInvoiceNo}</strong> vào Lịch sử.
              </span>
              <br />
              <Button
                type="link"
                size="small"
                className="p-0 text-blue-600 underline text-xs mt-1"
                onClick={() => {
                  setActiveTab('history');
                  onTabChange?.('history');
                }}
              >
                Xem trong Lịch sử chứng từ &rarr;
              </Button>
            </div>
          ),
          duration: 8,
        });
      }
    } catch {
      message.error('Lỗi khi xuất file Excel.');
    } finally {
      setExporting(false);
    }
  };

  const handleExportExcel = async () => {
    const req = await buildRequestData();
    if (!req) return;

    const hasBothTypes =
      req.items.some((i) => i.processType === ProcessType.Standard) &&
      req.items.some((i) => i.processType === ProcessType.GoKhongMay);

    if (hasBothTypes) {
      // Ưu tiên sequence number từ chính req.startInvoiceNumber hoặc req.invoiceNo (Source of Truth!)
      const currentSeq = req.startInvoiceNumber ?? extractSequenceNumber(req.invoiceNo) ?? sequenceInfo?.nextNumber ?? 233;
      setStartInvoiceNum(currentSeq);
      setExportPriority(ExportSequencePriority.StandardFirst);
      setPendingExportRequest(req);
      setExportSequenceModalVisible(true);
      return;
    }

    await executeExport(req);
  };

  const handleConfirmSequenceExport = async () => {
    if (!pendingExportRequest) return;
    const reqWithSeq: CreateShipmentRequest = {
      ...pendingExportRequest,
      startInvoiceNumber: startInvoiceNum,
      priority: exportPriority,
    };
    setExportSequenceModalVisible(false);
    await executeExport(reqWithSeq);
  };

  const handleSaveShipment = async () => {
    const req = await buildRequestData();
    if (!req) return;

    try {
      setSaving(true);
      const saved = await shipmentApi.createShipment(req);
      await loadShipmentsHistory();

      // Cập nhật số hóa đơn tiếp theo sau khi lưu
      try {
        const nextInfo = await shipmentApi.getSequence();
        setSequenceInfo(nextInfo);
      } catch {
        // Ignored
      }

      message.success({
        content: (
          <div>
            <span>
              Đã lưu đơn hàng <strong>{saved.invoiceNo}</strong> vào hệ thống thành công.
            </span>
            <br />
            <Button
              type="link"
              size="small"
              className="p-0 text-blue-600 underline text-xs mt-1"
              onClick={() => {
                setActiveTab('history');
                onTabChange?.('history');
              }}
            >
              Chuyển đến Lịch sử chứng từ &rarr;
            </Button>
          </div>
        ),
        duration: 8,
      });
    } catch (err: unknown) {
      const error = err as { response?: { data?: { message?: string } }; message?: string };
      message.error(error.response?.data?.message || error.message || 'Lỗi khi lưu đơn hàng vào hệ thống.');
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

  const handleOpenCustomsSync = (order?: SavedShipmentSummary) => {
    setSelectedOrderForCustoms(order || null);
    setCustomsModalOpen(true);
  };

  const handleDownloadCustomsAttachment = async (orderId: number) => {
    try {
      message.loading({ content: 'Đang tải file tờ khai hải quan đính kèm...', key: 'dl-customs' });
      const { blob, fileName } = await customsApi.downloadAttachment(orderId);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
      message.success({ content: `Đã tải xuống file tờ khai: ${fileName}`, key: 'dl-customs' });
    } catch {
      message.error({ content: 'Không thể tải file tờ khai hải quan đính kèm.', key: 'dl-customs' });
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
      // Đồng bộ sequence states từ đơn hàng cũ được nạp
      const seq = extractSequenceNumber(order.invoiceNo);
      if (seq) {
        setStartInvoiceNum(seq);
        setSequenceEditValue(seq);
        setSequenceInfo({
          nextNumber: seq,
          previewInvoiceNo: order.invoiceNo,
          previewFileName: toStandardFileName(seq),
        });
      }
      setActiveTab('create');
      onTabChange?.('shipment');
      if (order.isLocked || order.status === ShipmentStatus.Cleared) {
        message.warning({ content: `Đơn hàng ${order.invoiceNo} đã thông quan hải quan và bị khóa (Read-only). Không thể ghi đè!`, duration: 6, key: 'load-order' });
      } else {
        message.success({ content: `Đã nạp lại đơn hàng ${order.invoiceNo} vào lưới nhập liệu!`, key: 'load-order' });
      }
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
      message.success(`Đã thay thế toàn bộ bằng ${ocrItems.length} mặt hàng từ OCR.`);
    } else {
      setItems((prev) => {
        const validPrev = prev.filter((p) => p.styleCode.trim() || p.quantity > 0);
        return [...validPrev, ...ocrItems];
      });
      message.success(`Đã thêm nối tiếp ${ocrItems.length} mặt hàng từ OCR.`);
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

    setTimeout(() => {
      const inputs = tableContainerRef.current?.querySelectorAll<HTMLInputElement>('.style-code-input input');
      if (inputs && inputs.length > 0) {
        inputs[inputs.length - 1]?.focus();
      }
    }, 80);
  };

  const handleRemoveItem = (index: number) => {
    const next = [...items];
    next.splice(index, 1);
    setItems(next);
  };

  const handleClearAllItems = () => {
    setItems([]);
    message.success('Đã xóa sạch danh sách mặt hàng.');
  };

  const handleProductSelect = (index: number, styleCode: string) => {
    const cleanCode = styleCode.trim().toUpperCase();
    const p = products.find((x) => x.styleCode.trim().toUpperCase() === cleanCode);
    const next = [...items];
    if (p) {
      const currentItem = next[index];
      const isGo = currentItem.processType === ProcessType.GoKhongMay;

      const cmt = (isGo && p.unitPriceCMT_Go && p.unitPriceCMT_Go > 0)
        ? p.unitPriceCMT_Go
        : p.unitPriceCMT;
      const dap = (isGo && p.unitPriceDAP_Go && p.unitPriceDAP_Go > 0)
        ? p.unitPriceDAP_Go
        : p.unitPriceDAP;

      next[index] = {
        ...currentItem,
        styleCode: p.styleCode,
        description: p.description,
        unitPriceCMT: cmt,
        unitPriceDAP: dap,
        unit: p.unit || 'đôi',
        pairPerCarton: p.pairPerCarton || 12,
      };
    } else {
      next[index] = { ...next[index], styleCode };
    }
    setItems(next);
  };

  const handleProcessTypeChange = (index: number, newProcessType: typeof ProcessType[keyof typeof ProcessType]) => {
    const next = [...items];
    const item = next[index];
    const p = products.find((x) => x.styleCode.trim().toUpperCase() === item.styleCode.trim().toUpperCase());

    if (p) {
      const isGo = newProcessType === ProcessType.GoKhongMay;
      next[index] = {
        ...item,
        processType: newProcessType,
        unitPriceCMT: (isGo && p.unitPriceCMT_Go && p.unitPriceCMT_Go > 0) ? p.unitPriceCMT_Go : p.unitPriceCMT,
        unitPriceDAP: (isGo && p.unitPriceDAP_Go && p.unitPriceDAP_Go > 0) ? p.unitPriceDAP_Go : p.unitPriceDAP,
      };
    } else {
      next[index] = { ...item, processType: newProcessType };
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
    message.success(`Đã nạp ${mapped.length} mã sản phẩm từ Master Data.`);
  };

  const handleSetSequence = async () => {
    if (!sequenceEditValue || sequenceEditValue <= 0) {
      message.error('Số thứ tự phải lớn hơn 0.');
      return;
    }
    try {
      const info = await shipmentApi.setSequence(sequenceEditValue);
      setSequenceInfo(info);
      setStartInvoiceNum(sequenceEditValue);
      // Đồng bộ 2 chiều: cập nhật trực tiếp ô Số Hóa đơn (Invoice No) trong Form
      form.setFieldValue('invoiceNo', info.previewInvoiceNo);
      setSequenceEditVisible(false);
      message.success(`Đã đồng bộ: Số hóa đơn ${info.previewInvoiceNo} (File: ${info.previewFileName})`);
    } catch {
      message.error('Lỗi khi ghi đè số thứ tự.');
    }
  };

  const handleApplyQuickPaste = (newItems: CreateShipmentItem[], mode: 'replace' | 'append') => {
    if (mode === 'replace') {
      setItems(newItems);
      message.success(`Đã thay thế toàn bộ bằng ${newItems.length} mặt hàng.`);
    } else {
      setItems((prev) => [...prev, ...newItems]);
      message.success(`Đã thêm nối tiếp ${newItems.length} mặt hàng.`);
    }
  };

  // KPIs
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

  const standardItems = items.filter((i) => i.processType === ProcessType.Standard);
  const goItems = items.filter((i) => i.processType === ProcessType.GoKhongMay);
  const standardQty = standardItems.reduce((acc, i) => acc + (i.quantity || 0), 0);
  const goQty = goItems.reduce((acc, i) => acc + (i.quantity || 0), 0);

  const firstSeqNum = startInvoiceNum || 233;
  const secondSeqNum = firstSeqNum + 1;
  const isStandardFirst = exportPriority === ExportSequencePriority.StandardFirst;

  const previewFile1 = isStandardFirst
    ? {
        label: 'Hàng Thành hình',
        type: 'standard' as const,
        fileName: `KM3-26-DH${firstSeqNum}.xlsx`,
        invoiceNo: `KMHD-NEW2026-0${firstSeqNum}`,
        qty: standardQty,
      }
    : {
        label: 'Hàng Gò không may',
        type: 'go' as const,
        fileName: `KM3-26-DH${firstSeqNum}.xlsx`,
        invoiceNo: `KMHD-NEW2026-0${firstSeqNum}`,
        qty: goQty,
      };

  const previewFile2 = isStandardFirst
    ? {
        label: 'Hàng Gò không may',
        type: 'go' as const,
        fileName: `KM3-26-DH${secondSeqNum}.xlsx`,
        invoiceNo: `KMHD-NEW2026-0${secondSeqNum}`,
        qty: goQty,
      }
    : {
        label: 'Hàng Thành hình',
        type: 'standard' as const,
        fileName: `KM3-26-DH${secondSeqNum}.xlsx`,
        invoiceNo: `KMHD-NEW2026-0${secondSeqNum}`,
        qty: standardQty,
      };

  const columns: ColumnsType<CreateShipmentItem> = [
    {
      title: 'STT',
      key: 'stt',
      width: 44,
      align: 'center',
      render: (_, __, index) => <span className="font-mono text-xs text-slate-400">{index + 1}</span>,
    },
    {
      title: 'Mã hình thể (Style Code)',
      dataIndex: 'styleCode',
      key: 'styleCode',
      width: 200,
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
                    <span className="font-mono font-semibold text-slate-900">{p.styleCode}</span>
                    <span className="text-[11px] text-slate-400 truncate max-w-[150px]">{p.description}</span>
                  </div>
                ),
              }))}
              filterOption={(inputValue, option) =>
                (option?.value as string)?.toLowerCase().includes(inputValue.toLowerCase())
              }
              onSelect={(code) => handleProductSelect(index, code)}
              onChange={(newVal) => handleItemChange(index, 'styleCode', newVal)}
              placeholder="Chọn hoặc nhập mã..."
              className="w-full font-mono text-xs style-code-input"
            />
            {matchingProduct && (
              <div className="flex items-center space-x-1 text-[11px] text-emerald-700 truncate">
                <CheckCircleFilled className="text-[10px] text-emerald-600 shrink-0" />
                <span className="truncate max-w-[180px]" title={matchingProduct.description}>
                  {matchingProduct.description}
                </span>
              </div>
            )}
          </div>
        );
      },
    },
    {
      title: 'Mô tả sản phẩm',
      dataIndex: 'description',
      key: 'description',
      width: 170,
      render: (val: string, _, index) => (
        <Input
          size="middle"
          value={val}
          placeholder="Mô tả hàng hóa..."
          onChange={(e) => handleItemChange(index, 'description', e.target.value)}
          className="text-xs"
        />
      ),
    },
    {
      title: 'Số lượng (đôi)',
      dataIndex: 'quantity',
      key: 'quantity',
      width: 120,
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
          className="w-full font-mono text-right font-semibold text-slate-900"
          placeholder="SL"
        />
      ),
    },
    {
      title: (
        <div className="flex items-center space-x-1">
          <span>Quy trình công đoạn</span>
          <Tooltip title="Chọn 'Gò không may' đối với các mã bán thành phẩm (có hậu tố .G) để áp dụng đơn giá gò.">
            <QuestionCircleOutlined className="text-slate-400 hover:text-blue-600 text-[11px] cursor-pointer" />
          </Tooltip>
        </div>
      ),
      dataIndex: 'processType',
      key: 'processType',
      width: 150,
      render: (val: ProcessType, _, index) => (
        <Select
          value={val}
          onChange={(proc) => handleProcessTypeChange(index, proc)}
          className="w-full text-xs"
          options={[
            {
              value: ProcessType.Standard,
              label: <span className="text-xs text-slate-800">Thành hình</span>,
            },
            {
              value: ProcessType.GoKhongMay,
              label: <span className="text-xs text-purple-700 font-medium">Gò không may (.G)</span>,
            },
          ]}
        />
      ),
    },
    {
      title: 'Đôi/Thùng',
      dataIndex: 'pairPerCarton',
      key: 'pairPerCarton',
      width: 95,
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
      title: 'Đơn giá CMT',
      dataIndex: 'unitPriceCMT',
      key: 'unitPriceCMT',
      width: 100,
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
      title: 'Đơn giá DAP',
      dataIndex: 'unitPriceDAP',
      key: 'unitPriceDAP',
      width: 100,
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
      title: 'Thành tiền DAP',
      key: 'amountDAP',
      width: 120,
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
      title: 'Master',
      key: 'masterStatus',
      width: 90,
      align: 'center',
      render: (_, record) => {
        const isMatched = products.some(
          (p) => p.styleCode.trim().toUpperCase() === (record.styleCode || '').trim().toUpperCase()
        );
        return isMatched ? (
          <Tag className="text-[11px] px-1.5 py-0 m-0 border-emerald-200 bg-emerald-50 text-emerald-700">
            Khớp
          </Tag>
        ) : (
          <Tag className="text-[11px] px-1.5 py-0 m-0 text-slate-500 border-slate-200 bg-slate-50">
            Mới
          </Tag>
        );
      },
    },
    {
      title: '',
      key: 'actions',
      width: 40,
      align: 'center',
      render: (_, __, index) => (
        <Button
          type="text"
          danger
          size="small"
          icon={<DeleteOutlined className="text-xs" />}
          onClick={() => handleRemoveItem(index)}
          title="Xóa dòng này"
        />
      ),
    },
  ];

  const filteredHistory = savedShipments.filter((s) => {
    if (!historySearchText.trim()) return true;
    const q = historySearchText.toLowerCase();
    return (
      s.invoiceNo.toLowerCase().includes(q) ||
      s.customerName.toLowerCase().includes(q) ||
      s.contractNo.toLowerCase().includes(q)
    );
  });

  const historyColumns: ColumnsType<SavedShipmentSummary> = [
    {
      title: 'Số Hóa đơn (Invoice No)',
      dataIndex: 'invoiceNo',
      key: 'invoiceNo',
      render: (no: string, record) => (
        <div>
          <span className="font-mono font-semibold text-slate-900 text-xs">{no}</span>
          <div className="text-[11px] text-slate-400 font-mono">{record.poSuffix}</div>
        </div>
      ),
    },
    {
      title: 'Ngày lập',
      dataIndex: 'invoiceDate',
      key: 'invoiceDate',
      width: 100,
      render: (d: string) => <span className="text-xs font-mono text-slate-600">{dayjs(d).format('DD/MM/YYYY')}</span>,
    },
    {
      title: 'Số Hợp đồng',
      dataIndex: 'contractNo',
      key: 'contractNo',
      render: (c: string) => <span className="text-xs font-mono text-slate-600">{c}</span>,
    },
    {
      title: 'Khách hàng',
      dataIndex: 'customerName',
      key: 'customerName',
      ellipsis: true,
      render: (cust: string) => <span className="text-xs text-slate-700">{cust}</span>,
    },
    {
      title: 'Số dòng',
      dataIndex: 'itemCount',
      key: 'itemCount',
      width: 75,
      align: 'center',
      render: (cnt: number) => <span className="font-mono text-xs">{cnt}</span>,
    },
    {
      title: 'Tổng số đôi',
      dataIndex: 'totalQuantity',
      key: 'totalQuantity',
      align: 'right',
      width: 120,
      render: (q: number) => (
        <span className="font-mono text-xs font-semibold text-slate-900">{q.toLocaleString()} đôi</span>
      ),
    },
    {
      title: 'Tổng tiền DAP',
      dataIndex: 'totalAmountDAP',
      key: 'totalAmountDAP',
      align: 'right',
      width: 130,
      render: (amt: number) => (
        <span className="font-mono text-xs font-semibold text-blue-600">
          ${amt.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
        </span>
      ),
    },
    {
      title: 'Trạng thái',
      key: 'status',
      width: 200,
      render: (_, record) => {
        if (record.status === ShipmentStatus.Cleared) {
          if (record.customsChannel === 1) {
            return (
              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-semibold bg-emerald-50 text-emerald-800 border border-emerald-300">
                <CheckCircleFilled className="text-emerald-600 text-xs" />
                ✔ Luồng 1 - Xanh (Đã thông quan)
              </span>
            );
          }
          if (record.customsChannel === 2) {
            return (
              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-semibold bg-amber-50 text-amber-800 border border-amber-300">
                <CheckCircleFilled className="text-amber-600 text-xs" />
                ✔ Luồng 2 - Vàng (Đã thông quan)
              </span>
            );
          }
          if (record.customsChannel === 3) {
            return (
              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-semibold bg-rose-50 text-rose-800 border border-rose-300">
                <CheckCircleFilled className="text-rose-600 text-xs" />
                ✔ Luồng 3 - Đỏ (Đã thông quan)
              </span>
            );
          }
          return (
            <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-semibold bg-emerald-50 text-emerald-800 border border-emerald-300">
              <CheckCircleFilled className="text-emerald-600 text-xs" />
              ✔ Đã thông quan
            </span>
          );
        }

        if (record.status === ShipmentStatus.Discrepancy) {
          return (
            <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-semibold bg-rose-50 text-rose-700 border border-rose-200">
              <ExclamationCircleOutlined className="text-rose-500 text-xs" />
              ⚠ Sai lệch số liệu
            </span>
          );
        }

        if (record.status === ShipmentStatus.Exported) {
          return (
            <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium bg-amber-50 text-amber-700 border border-amber-200">
              ⏳ Chờ thông quan
            </span>
          );
        }

        return (
          <span className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-normal bg-slate-50 text-slate-600 border border-slate-200">
            Bản nháp
          </span>
        );
      },
    },
    {
      title: 'Tờ khai Hải quan',
      key: 'customsDeclaration',
      width: 190,
      render: (_, record) => {
        if (!record.declarationNo) {
          return <span className="text-slate-400 italic text-xs">Chưa có tờ khai</span>;
        }

        return (
          <div className="flex items-start justify-between gap-1.5">
            <div>
              <div className="font-mono font-semibold text-slate-900 text-xs">
                {record.declarationNo}
              </div>
              {record.clearanceDate && (
                <div className="text-[11px] text-slate-500 font-mono mt-0.5">
                  {dayjs(record.clearanceDate).format('DD/MM/YYYY HH:mm')}
                </div>
              )}
            </div>
            {record.customsAttachmentFileName && (
              <Tooltip title={`Tải về file tờ khai gốc (.xls): ${record.customsAttachmentFileName}`}>
                <Button
                  size="small"
                  type="text"
                  icon={<FileExcelOutlined className="text-emerald-600 hover:text-emerald-700 text-sm" />}
                  className="p-1 h-auto"
                  onClick={() => handleDownloadCustomsAttachment(record.id)}
                />
              </Tooltip>
            )}
          </div>
        );
      },
    },
    {
      title: 'Thao tác',
      key: 'action',
      align: 'center',
      width: 320,
      render: (_, record) => {
        const isLocked = Boolean(record.isLocked || record.status === ShipmentStatus.Cleared);

        return (
          <Space size={4} wrap>
            <Tooltip title="Xem chi tiết đối soát chéo và lịch sử xử lý hải quan">
              <Button
                size="small"
                icon={<AuditOutlined className="text-xs text-blue-600" />}
                className="text-xs border-blue-200 text-blue-700 bg-blue-50/50 hover:bg-blue-50 font-medium"
                onClick={() => handleOpenCustomsSync(record)}
              >
                Xem chi tiết đối soát HQ
              </Button>
            </Tooltip>

            {isLocked ? (
              <Tooltip title="Đơn hàng đã thông quan hải quan, hồ sơ đã bị khóa (Read-only)">
                <span>
                  <Button
                    size="small"
                    disabled
                    icon={<FolderOpenOutlined className="text-xs" />}
                    className="text-xs border-slate-200 text-slate-400 cursor-not-allowed"
                  >
                    Chỉnh sửa
                  </Button>
                </span>
              </Tooltip>
            ) : (
              <Button
                size="small"
                icon={<FolderOpenOutlined className="text-xs" />}
                className="text-xs border-slate-300 text-slate-700 hover:text-blue-600"
                onClick={() => handleLoadHistoricalOrder(record.id)}
              >
                Mở lại
              </Button>
            )}

            <Button
              size="small"
              icon={<DownloadOutlined className="text-xs" />}
              className="text-xs border-slate-300 text-slate-700 hover:text-blue-600"
              onClick={() => handleDownloadHistorical(record.id, record.invoiceNo)}
            >
              Tải Excel
            </Button>
          </Space>
        );
      },
    },
  ];

  return (
    <div className="space-y-6">
      {activeTab === 'history' ? (
        /* ================= MÀN HÌNH LỊCH SỬ CHỨNG TỪ ================= */
        <div className="space-y-6">
          {/* Header */}
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 pb-4 border-b border-slate-200">
            <div>
              <h1 className="text-xl font-semibold text-slate-900 tracking-tight m-0">
                Lịch sử chứng từ xuất hàng
              </h1>
              <p className="text-xs text-slate-500 mt-1 m-0">
                Danh sách hóa đơn Commercial Invoice & Packing List đã lập ({savedShipments.length} chứng từ)
              </p>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <Button
                icon={<AuditOutlined className="text-blue-600" />}
                onClick={() => handleOpenCustomsSync()}
                className="text-xs h-9 px-3.5 border-blue-200 text-blue-700 bg-blue-50/50 hover:bg-blue-50 font-medium"
              >
                Đối Soát Tờ Khai Hải Quan
              </Button>
              <Button
                icon={<ReloadOutlined />}
                onClick={loadShipmentsHistory}
                loading={loadingHistory}
                className="text-xs h-9 px-3.5 border-slate-300 text-slate-700 hover:bg-slate-50"
              >
                Làm mới
              </Button>
              <Button
                type="primary"
                icon={<PlusOutlined />}
                onClick={() => {
                  setActiveTab('create');
                  onTabChange?.('shipment');
                }}
                className="bg-blue-600 hover:bg-blue-700 text-xs h-9 px-4"
              >
                + Lập Hóa đơn Mới
              </Button>
            </div>
          </div>

          {/* Table Container with Search Toolbar */}
          <div className="bg-white border border-slate-200 rounded-lg p-4 space-y-3">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div className="w-full sm:w-80">
                <Input
                  placeholder="Tìm theo số Invoice, tên khách hàng hoặc hợp đồng..."
                  prefix={<SearchOutlined className="text-slate-400 text-xs mr-1" />}
                  value={historySearchText}
                  onChange={(e) => setHistorySearchText(e.target.value)}
                  allowClear
                  size="middle"
                  className="text-xs"
                />
              </div>
            </div>

            <Table
              dataSource={filteredHistory}
              columns={historyColumns}
              rowKey="id"
              loading={loadingHistory}
              pagination={{ pageSize: 10 }}
              size="middle"
              scroll={{ x: 950 }}
            />
          </div>
        </div>
      ) : (
        /* ================= MÀN HÌNH LẬP HÓA ĐƠN & PACKING LIST ================= */
        <div className="space-y-6">
          {/* Header */}
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 pb-4 border-b border-slate-200">
            <div>
              <h1 className="text-xl font-semibold text-slate-900 tracking-tight m-0">
                Lập Invoice & Packing List (INV & PKL)
              </h1>
              <p className="text-xs text-slate-500 mt-1 m-0">
                Nhập số liệu phiếu kho, đối soát tổng số đôi và xuất file Excel đa sheet chuẩn mẫu nhà máy
              </p>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <Button
                icon={<HistoryOutlined />}
                onClick={() => {
                  setActiveTab('history');
                  onTabChange?.('history');
                  loadShipmentsHistory();
                }}
                className="text-xs h-9 px-3.5 border-slate-300 text-slate-700 hover:bg-slate-50"
              >
                Lịch sử ({savedShipments.length})
              </Button>
              <Button
                icon={<EyeOutlined />}
                loading={loadingPreview}
                onClick={handlePreviewPkl}
                className="text-xs h-9 px-3.5 border-slate-300 text-slate-700 hover:bg-slate-50"
              >
                Xem trước PKL
              </Button>
              <Button
                icon={<SaveOutlined />}
                loading={saving}
                onClick={handleSaveShipment}
                className="text-xs h-9 px-3.5 border-slate-300 text-slate-700 hover:bg-slate-50"
              >
                Lưu đơn hàng
              </Button>
              <div id="tour-export-button" className="inline-block">
                <Tooltip title="Tự động lưu đơn hàng vào hệ thống và tải xuống file Excel (không cần bấm Lưu trước)">
                  <Button
                    type="primary"
                    icon={<DownloadOutlined />}
                    loading={exporting}
                    onClick={handleExportExcel}
                    className="bg-blue-600 hover:bg-blue-700 text-white text-xs h-9 px-4 font-medium"
                  >
                    Xuất File Excel & Lưu đơn
                  </Button>
                </Tooltip>
              </div>
            </div>
          </div>

          {/* Section 1: Thông tin Hóa đơn Xuất khẩu (Shipment Header) */}
          <div id="tour-invoice-header" className="bg-white border border-slate-200 rounded-lg p-5 space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <div className="text-xs font-semibold text-slate-800 uppercase tracking-wider">
                1. Thông tin Chứng từ (Shipment Header)
              </div>
              <div className="flex items-center gap-2">
                {sequenceInfo && (
                  <span className="flex items-center gap-1.5 text-xs text-slate-500">
                    <NumberOutlined className="text-blue-600" />
                    <span>Tiếp theo:</span>
                    <span className="font-mono text-xs px-2 py-0.5 rounded bg-blue-50 text-blue-700 border border-blue-200 font-medium">
                      {sequenceInfo.previewFileName}
                    </span>
                  </span>
                )}
                <Tooltip title="Ghi đè số thứ tự sequence tiếp theo">
                  <Button
                    size="small"
                    icon={<EditOutlined className="text-xs" />}
                    className="text-slate-600 hover:text-blue-600 border-slate-300 text-xs h-7 px-2"
                    onClick={() => {
                      const curInvoice = form.getFieldValue('invoiceNo') || '';
                      const curSeq = extractSequenceNumber(curInvoice) ?? sequenceInfo?.nextNumber ?? 1;
                      setSequenceEditValue(curSeq);
                      setSequenceEditVisible(true);
                    }}
                  >
                    Ghi đè số
                  </Button>
                </Tooltip>
              </div>
            </div>

            <Form
              form={form}
              layout="vertical"
              onValuesChange={(changedValues) => {
                if ('invoiceNo' in changedValues) {
                  handleInvoiceNoChange(changedValues.invoiceNo || '');
                }
              }}
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
                      <div className="flex items-center space-x-1">
                        <span className="text-xs font-medium text-slate-700">Số Hóa đơn (Invoice No)</span>
                        <Tooltip title="Định dạng KMHD-NEW2026-0XXX. Tên file Excel tải về sẽ tự động đồng bộ theo số này.">
                          <QuestionCircleOutlined className="text-slate-400 hover:text-blue-600 text-xs cursor-pointer" />
                        </Tooltip>
                      </div>
                    }
                    name="invoiceNo"
                    rules={[{ required: true, message: 'Nhập số hóa đơn' }]}
                  >
                    <Input
                      className="font-mono text-xs font-medium"
                      placeholder="vd: KMHD-NEW2026-0233"
                      onChange={(e) => handleInvoiceNoChange(e.target.value)}
                      suffix={
                        <Tooltip title="Gợi ý số Invoice tiếp theo">
                          <ReloadOutlined
                            className="text-slate-400 hover:text-blue-600 cursor-pointer"
                            onClick={handleGenerateInvoiceNo}
                          />
                        </Tooltip>
                      }
                    />
                  </Form.Item>
                  <Form.Item noStyle shouldUpdate={(prev, cur) => prev.invoiceNo !== cur.invoiceNo}>
                    {({ getFieldValue }) => {
                      const inv: string = getFieldValue('invoiceNo') ?? '';
                      const fileName = inv ? invoiceNoToFileName(inv) : '';
                      return fileName ? (
                        <div className="-mt-3 mb-3 flex items-center space-x-1.5 text-xs text-blue-700">
                          <span className="text-slate-400">→ File:</span>
                          <span className="font-mono text-xs px-1.5 py-0.5 rounded bg-blue-50 border border-blue-200">
                            {fileName}
                          </span>
                        </div>
                      ) : null;
                    }}
                  </Form.Item>
                </Col>

                <Col xs={24} sm={12} md={6}>
                  <Form.Item
                    label={
                      <div className="flex items-center justify-between w-full">
                        <span className="text-xs font-medium text-slate-700">Ngày Hóa đơn</span>
                        <span className="text-xs text-slate-400">
                          {dayjs().format('DD/MM/YYYY')}
                        </span>
                      </div>
                    }
                    name="invoiceDate"
                    rules={[{ required: true, message: 'Chọn ngày' }]}
                  >
                    <DatePicker format="DD/MM/YYYY" className="w-full text-xs" placeholder="Chọn ngày" />
                  </Form.Item>
                </Col>

                <Col xs={24} sm={12} md={6}>
                  <Form.Item
                    label={<span className="text-xs font-medium text-slate-700">Số Hợp đồng (Contract No)</span>}
                    name="contractNo"
                    rules={[{ required: true, message: 'Nhập số hợp đồng' }]}
                  >
                    <Input className="font-mono text-xs" placeholder="KM-HANEW/01-2025" />
                  </Form.Item>
                </Col>

                <Col xs={24} sm={12} md={6}>
                  <Form.Item
                    label={
                      <div className="flex items-center space-x-1">
                        <span className="text-xs font-medium text-slate-700">Đuôi PO (PoSuffix)</span>
                        <Tooltip title="Đuôi PO (ví dụ: (KM3.PO5.26)) sẽ được tự động gắn vào mã sản phẩm khi xuất hóa đơn và phiếu đóng gói.">
                          <QuestionCircleOutlined className="text-slate-400 hover:text-blue-600 text-xs cursor-pointer" />
                        </Tooltip>
                      </div>
                    }
                    name="poSuffix"
                    rules={[{ required: true, message: 'Nhập đuôi PO' }]}
                  >
                    <Input className="font-mono text-xs" placeholder="(KM3.PO5.26)" />
                  </Form.Item>
                </Col>
              </Row>

              <Row gutter={16}>
                <Col xs={24} md={12}>
                  <Form.Item
                    label={<span className="text-xs font-medium text-slate-700">Tên Khách hàng</span>}
                    name="customerName"
                    rules={[{ required: true, message: 'Nhập tên khách hàng' }]}
                  >
                    <Input className="text-xs" placeholder="Tên công ty khách hàng" />
                  </Form.Item>
                </Col>

                <Col xs={24} md={12}>
                  <Form.Item
                    label={<span className="text-xs font-medium text-slate-700">Địa chỉ giao hàng</span>}
                    name="address"
                  >
                    <Input className="text-xs" placeholder="Địa chỉ giao hàng" />
                  </Form.Item>
                </Col>
              </Row>
            </Form>
          </div>

          {/* Section 2: Đối soát & Tổng hợp Số liệu (Reconciliation Summary) */}
          <div id="tour-reconciliation-bar" className="bg-white border border-slate-200 rounded-lg p-4">
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 lg:gap-0 lg:divide-x divide-slate-200">
              <div className="lg:pr-5">
                <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
                  Tổng số đôi phiếu kho
                </div>
                <div className="mt-1 flex items-baseline flex-wrap gap-2">
                  <span className="text-2xl font-bold text-slate-900 font-mono">
                    {totalQuantity.toLocaleString()}
                  </span>
                  <span className="text-xs text-slate-500">đôi</span>
                  <Tooltip title="Số lượng thực tế các dòng hàng đã khớp chuẩn với tổng số đôi của phiếu kho.">
                    <Tag className="text-[11px] border-emerald-200 bg-emerald-50 text-emerald-700 m-0 cursor-help">
                      Khớp phiếu kho
                    </Tag>
                  </Tooltip>
                </div>
                <div className="mt-1 text-xs text-slate-500">
                  {items.length} dòng hàng • {uniqueStyleCodesCount} mã hình thể
                </div>
              </div>

              <div className="lg:px-5">
                <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
                  Số kiện ước tính
                </div>
                <div className="mt-1 flex items-baseline space-x-1.5">
                  <span className="text-2xl font-bold text-slate-900 font-mono">
                    {totalCartons.toLocaleString()}
                  </span>
                  <span className="text-xs text-slate-500">thùng</span>
                </div>
                <div className="mt-1 text-xs text-slate-500">
                  Dựa trên quy cách đóng gói
                </div>
              </div>

              <div className="lg:px-5">
                <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
                  Tổng tiền gia công CMT
                </div>
                <div className="mt-1 flex items-baseline space-x-1.5">
                  <span className="text-2xl font-bold text-slate-900 font-mono">
                    ${totalAmountCMT.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                </div>
                <div className="mt-1 text-xs text-slate-500">
                  Gia công CMT xuất khẩu
                </div>
              </div>

              <div className="lg:pl-5">
                <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
                  Tổng trị giá DAP
                </div>
                <div className="mt-1 flex items-baseline space-x-1.5">
                  <span className="text-2xl font-bold text-blue-600 font-mono">
                    ${totalAmountDAP.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                </div>
                <div className="mt-1 text-xs text-slate-500">
                  Thành tiền Commercial Invoice
                </div>
              </div>
            </div>
          </div>

          {/* Section 3: Lưới Nhập liệu Hàng hóa (Editable Table) */}
          <div className="bg-white border border-slate-200 rounded-lg p-5 space-y-4">
            <div id="tour-data-import" className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 pb-3 border-b border-slate-100">
              <div className="text-xs font-semibold text-slate-800 uppercase tracking-wider">
                2. Danh sách Hàng hóa ({items.length} dòng)
              </div>
              <Space wrap size="small">
                <Button
                  icon={<CameraOutlined />}
                  onClick={() => setOcrModalVisible(true)}
                  className="text-xs h-8 border-slate-300 text-slate-700 hover:bg-slate-50"
                >
                  Quét OCR Phiếu kho
                </Button>
                <Button
                  icon={<RocketOutlined />}
                  onClick={() => setBatchOcrModalVisible(true)}
                  className="text-xs h-8 border-blue-300 text-blue-700 bg-blue-50/50 hover:bg-blue-100/50 font-medium"
                >
                  Quét OCR hàng loạt (Batch)
                </Button>
                <Tooltip title="Hỗ trợ dán trực tiếp danh sách mã và số lượng copy từ bảng tính Excel.">
                  <Button
                    icon={<ThunderboltOutlined />}
                    onClick={() => setQuickPasteVisible(true)}
                    className="text-xs h-8 border-slate-300 text-slate-700 hover:bg-slate-50"
                  >
                    Dán nhanh (Clipboard)
                  </Button>
                </Tooltip>
                <Button
                  icon={<AppstoreOutlined />}
                  onClick={handlePopulateAllMasterProducts}
                  className="text-xs h-8 border-slate-300 text-slate-700 hover:bg-slate-50"
                >
                  Nạp từ Master Data
                </Button>
                <Popconfirm
                  title="Xóa toàn bộ danh sách mặt hàng?"
                  description="Bạn có chắc chắn muốn xóa tất cả các dòng hiện tại không?"
                  okText="Xóa tất cả"
                  cancelText="Hủy"
                  okButtonProps={{ danger: true }}
                  onConfirm={handleClearAllItems}
                  disabled={items.length === 0}
                >
                  <Button
                    danger
                    icon={<DeleteOutlined />}
                    disabled={items.length === 0}
                    className="text-xs h-8"
                  >
                    Xóa tất cả
                  </Button>
                </Popconfirm>
                <Button
                  type="primary"
                  icon={<PlusOutlined />}
                  onClick={handleAddItem}
                  className="bg-blue-600 hover:bg-blue-700 text-white text-xs h-8 font-medium"
                >
                  + Thêm dòng
                </Button>
              </Space>
            </div>

            <div ref={tableContainerRef} className="w-full overflow-x-auto">
              <Table
                dataSource={items}
                columns={columns}
                rowKey={(_, index) => `${index}`}
                pagination={false}
                size="middle"
                scroll={{ x: 1050 }}
                locale={{
                  emptyText: (
                    <Empty
                      image={Empty.PRESENTED_IMAGE_SIMPLE}
                      description={
                        <div className="space-y-1 py-3">
                          <div className="text-sm font-semibold text-slate-700">
                            Chưa có mặt hàng nào trong đơn
                          </div>
                          <div className="text-xs text-slate-500">
                            Thêm dòng mới để nhập tay, dán từ file Excel hoặc quét ảnh phiếu kho để bắt đầu
                          </div>
                        </div>
                      }
                    >
                      <Space size="middle" className="pb-2">
                        <Button
                          type="primary"
                          icon={<PlusOutlined />}
                          onClick={handleAddItem}
                          className="bg-blue-600 hover:bg-blue-700 text-xs h-8 font-medium"
                        >
                          + Thêm dòng mới
                        </Button>
                        <Button
                          icon={<ThunderboltOutlined />}
                          onClick={() => setQuickPasteVisible(true)}
                          className="text-xs h-8 border-slate-300 text-slate-700 hover:bg-slate-50"
                        >
                          Dán nhanh từ Excel
                        </Button>
                        <Button
                          icon={<CameraOutlined />}
                          onClick={() => setOcrModalVisible(true)}
                          className="text-xs h-8 border-slate-300 text-slate-700 hover:bg-slate-50"
                        >
                          Quét OCR Phiếu kho
                        </Button>
                      </Space>
                    </Empty>
                  ),
                }}
                summary={() =>
                  items.length > 0 ? (
                    <Table.Summary fixed>
                      <Table.Summary.Row className="bg-slate-50 font-medium text-slate-900 border-t border-slate-200">
                        <Table.Summary.Cell index={0} colSpan={3}>
                          <span className="font-semibold text-xs text-slate-800 uppercase tracking-wider">
                            TỔNG CỘNG LÔ HÀNG:
                          </span>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={3} align="right">
                          <span className="font-mono text-slate-900 font-bold text-sm">
                            {totalQuantity.toLocaleString()} đôi
                          </span>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={4} colSpan={2} align="center">
                          <span className="font-mono text-xs text-slate-600">
                            Ước tính: {totalCartons.toLocaleString()} kiện
                          </span>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={6} align="right">
                          <span className="font-mono text-xs text-slate-700">
                            CMT: ${totalAmountCMT.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                          </span>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={7} colSpan={2} align="right">
                          <span className="font-mono text-sm text-blue-700 font-bold">
                            DAP: ${totalAmountDAP.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                          </span>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={9} align="center" />
                        <Table.Summary.Cell index={10} align="center" />
                      </Table.Summary.Row>
                    </Table.Summary>
                  ) : null
                }
              />
            </div>

            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between text-xs text-slate-500 pt-1 gap-1">
              <span>
                Phím tắt: Nhấn <kbd className="px-1.5 py-0.5 bg-slate-100 border border-slate-200 rounded font-mono text-slate-700">Enter</kbd> tại ô Số lượng dòng cuối để thêm dòng mới.
              </span>
              <span className="text-slate-400">
                Quy cách đóng gói và dải số kiện PKL sẽ tự động phân rã khi xem trước hoặc xuất Excel.
              </span>
            </div>
          </div>
        </div>
      )}

      {/* Modal Quick Paste */}
      <QuickPasteModal
        visible={quickPasteVisible}
        onClose={() => setQuickPasteVisible(false)}
        products={products}
        onApply={handleApplyQuickPaste}
      />

      {/* Modal Preview PKL */}
      <PklPreviewModal
        visible={previewVisible}
        onClose={() => setPreviewVisible(false)}
        data={previewData}
        onExportExcel={handleExportExcel}
        exporting={exporting}
      />

      {/* Modal OCR Phiếu kho */}
      <OcrUploadModal
        visible={ocrModalVisible}
        onClose={() => setOcrModalVisible(false)}
        products={products}
        onApply={handleApplyOcr}
      />

      {/* Modal Quét ảnh OCR hàng loạt theo lô (Batch Upload / Multi-Scan) */}
      <BatchOcrModal
        visible={batchOcrModalVisible}
        onClose={() => setBatchOcrModalVisible(false)}
        products={products}
        onSuccess={() => {
          loadShipmentsHistory();
          loadSequence();
        }}
      />

      {/* Modal Sequence override */}
      <Modal
        title={
          <span className="text-sm font-semibold text-slate-900">
            Ghi đè số thứ tự Invoice
          </span>
        }
        open={sequenceEditVisible}
        onCancel={() => setSequenceEditVisible(false)}
        onOk={handleSetSequence}
        okText="Áp dụng"
        cancelText="Hủy"
        width={400}
      >
        <div className="space-y-3 py-2">
          <p className="text-xs text-slate-600">
            Lần xuất tiếp theo sẽ bắt đầu từ số thứ tự bạn nhập dưới đây:
          </p>
          <div className="flex items-center gap-3">
            <InputNumber
              min={1}
              max={99999}
              value={sequenceEditValue}
              onChange={(v) => setSequenceEditValue(v ?? 1)}
              className="w-36 font-mono text-xs"
              placeholder="vd: 235"
            />
            <div className="text-xs text-slate-500">
              {sequenceEditValue > 0 && (
                <span>
                  → <span className="font-mono text-blue-600">KMHD-NEW2026-0{sequenceEditValue}</span>
                </span>
              )}
            </div>
          </div>
          {sequenceInfo && (
            <p className="text-[11px] text-slate-400">
              Hiện tại hệ thống: DH{sequenceInfo.nextNumber} ({sequenceInfo.previewInvoiceNo})
            </p>
          )}
        </div>
      </Modal>

      {/* Modal Hậu kiểm & Đối soát Hải quan */}
      <CustomsSyncModal
        open={customsModalOpen}
        onClose={() => {
          setCustomsModalOpen(false);
          setSelectedOrderForCustoms(null);
        }}
        targetOrder={selectedOrderForCustoms}
        onSyncSuccess={() => {
          loadShipmentsHistory();
        }}
      />

      {/* Modal Xác nhận Thứ tự Xuất Hóa đơn (khi có cả 2 loại hàng) */}
      <Modal
        title={
          <div className="flex items-center space-x-2 text-sm font-semibold text-slate-900">
            <RocketOutlined className="text-blue-600" />
            <span>Xác nhận Thứ tự Xuất Hóa đơn & Đặt tên File</span>
          </div>
        }
        open={exportSequenceModalVisible}
        onCancel={() => setExportSequenceModalVisible(false)}
        footer={[
          <Button key="cancel" onClick={() => setExportSequenceModalVisible(false)} className="text-xs h-9 px-4">
            Hủy bỏ
          </Button>,
          <Button
            key="confirm"
            type="primary"
            icon={<DownloadOutlined />}
            loading={exporting}
            onClick={handleConfirmSequenceExport}
            className="bg-blue-600 hover:bg-blue-700 text-white font-medium text-xs h-9 px-5"
          >
            Xác nhận & Xuất File
          </Button>,
        ]}
        width={580}
        destroyOnClose
      >
        <div className="space-y-4 py-2">
          <div className="bg-amber-50 border border-amber-200 rounded-lg p-3 text-xs text-amber-900 flex items-start space-x-2">
            <ExclamationCircleOutlined className="text-amber-600 mt-0.5 text-sm flex-shrink-0" />
            <div>
              Đợt hàng có <strong>cả 2 loại hàng</strong> ({standardItems.length} mã Thành hình và {goItems.length} mã Gò).
              Hệ thống sẽ tách thành <strong>2 file Excel riêng biệt</strong> và đóng gói vào 1 file ZIP.
            </div>
          </div>

          {/* Khối nhập số bắt đầu */}
          <div className="bg-slate-50 border border-slate-200 rounded-lg p-3 space-y-2">
            <label className="block text-xs font-semibold text-slate-700">
              Số Hóa đơn Bắt đầu:
            </label>
            <div className="flex items-center space-x-3">
              <InputNumber
                min={1}
                max={9999}
                value={startInvoiceNum}
                onChange={(val) => setStartInvoiceNum(val ?? 233)}
                className="w-32 font-mono text-xs"
              />
              <span className="text-xs text-slate-500">
                (Gợi ý tiếp theo từ hệ thống: <strong className="font-mono text-slate-700">#{sequenceInfo?.nextNumber || 233}</strong>)
              </span>
            </div>
          </div>

          {/* Tùy chọn Thứ tự Cấp số */}
          <div className="space-y-2">
            <label className="block text-xs font-semibold text-slate-700">
              Tùy chọn Thứ tự Cấp số:
            </label>
            <Radio.Group
              value={exportPriority}
              onChange={(e) => setExportPriority(e.target.value)}
              className="w-full flex flex-col space-y-2"
            >
              <Radio
                value={ExportSequencePriority.StandardFirst}
                className="border border-slate-200 rounded-lg p-3 hover:border-blue-500 transition-colors w-full"
              >
                <div className="inline-block align-middle ml-1">
                  <div className="text-xs font-medium text-slate-900">
                    Xuất Hàng Thành hình trước, Hàng Gò sau{' '}
                    <Tag color="blue" className="ml-1 text-[10px]">Khuyên dùng</Tag>
                  </div>
                  <div className="text-[11px] text-slate-500 mt-0.5 font-mono">
                    Thành hình: #{firstSeqNum} &rarr; Gò không may: #{secondSeqNum}
                  </div>
                </div>
              </Radio>
              <Radio
                value={ExportSequencePriority.GoFirst}
                className="border border-slate-200 rounded-lg p-3 hover:border-blue-500 transition-colors w-full"
              >
                <div className="inline-block align-middle ml-1">
                  <div className="text-xs font-medium text-slate-900">
                    Xuất Hàng Gò trước, Hàng Thành hình sau
                  </div>
                  <div className="text-[11px] text-slate-500 mt-0.5 font-mono">
                    Gò không may: #{firstSeqNum} &rarr; Thành hình: #{secondSeqNum}
                  </div>
                </div>
              </Radio>
            </Radio.Group>
          </div>

          {/* Bảng Xem trước Tức thời (Live Preview) */}
          <div className="space-y-2">
            <div className="text-xs font-semibold text-slate-700 uppercase tracking-wider flex items-center justify-between">
              <span>Bảng Xem trước Tức thời (Live Preview)</span>
              <span className="font-mono text-[11px] text-slate-500">ZIP: KM3-26-DH{firstSeqNum}-{secondSeqNum}.zip</span>
            </div>
            <div className="border border-slate-200 rounded-lg divide-y divide-slate-100 overflow-hidden bg-white">
              {[previewFile1, previewFile2].map((f, idx) => (
                <div key={idx} className="p-3 flex items-center justify-between hover:bg-slate-50 transition-colors">
                  <div className="flex items-center space-x-3">
                    <span className="flex items-center justify-center w-6 h-6 rounded-full bg-slate-100 text-slate-600 font-mono text-xs font-medium">
                      {idx + 1}
                    </span>
                    <div>
                      <div className="flex items-center space-x-2">
                        <span className="font-mono font-semibold text-xs text-slate-900">{f.fileName}</span>
                        <Tag color={f.type === 'standard' ? 'blue' : 'gold'} className="text-[10px] m-0">
                          {f.label}
                        </Tag>
                      </div>
                      <div className="text-[11px] text-slate-500 mt-0.5 font-mono">
                        Invoice No: <span className="font-medium text-slate-700">{f.invoiceNo}</span>
                      </div>
                    </div>
                  </div>
                  <div className="text-right">
                    <div className="text-xs font-semibold font-mono text-slate-900">
                      {f.qty.toLocaleString()} đôi
                    </div>
                    <div className="text-[10px] text-slate-400">Số lượng</div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </Modal>
    </div>
  );
});

ShipmentPage.displayName = 'ShipmentPage';
