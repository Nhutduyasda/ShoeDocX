import { useState, useEffect, useEffectEvent, useRef, useImperativeHandle, forwardRef, useMemo } from 'react';
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
  Empty,
  Dropdown,
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
  ShopOutlined,
  EllipsisOutlined,
  InboxOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { shipmentApi, invoiceNoToFileName, extractSequenceNumber, toStandardFileName } from '../api/shipmentApi';
import { productMasterApi } from '../api/productMasterApi';
import { masterDataFolderApi } from '../api/masterDataFolderApi';
import { warehouseApi } from '../api/warehouseApi';
import { hasCompletedTour, startOnboardingTour } from '../services/tourService';
import { customsApi } from '../api/customsApi';
import type { NavTabKey } from '../layouts/AppLayout';
import type { WarehouseBatchSummary } from '../types/warehouse';
import type {
  CreateShipmentRequest,
  CreateShipmentItem,
  ProductMaster,
  PklPreviewResponse,
  SavedShipmentSummary,
  SequenceInfo,
  MasterDataFolder,
  ValidateItemsResult,
} from '../types';
import { ProcessType, ShipmentStatus, ExportSequencePriority } from '../types';
import { PklPreviewModal } from '../components/PklPreviewModal';
import { QuickPasteModal } from '../components/QuickPasteModal';
import { OcrUploadModal } from '../components/OcrUploadModal';
import { BatchOcrModal } from '../components/BatchOcrModal';
import { CustomsSyncModal } from '../components/CustomsSyncModal';
import { QuickAddMasterDataModal } from '../components/QuickAddMasterDataModal';

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
  const isSubmittingRef = useRef<boolean>(false);

  const [items, setItems] = useState<CreateShipmentItem[]>([]);
  const [receiveBatchModalVisible, setReceiveBatchModalVisible] = useState<boolean>(false);
  const [warehouseBatches, setWarehouseBatches] = useState<WarehouseBatchSummary[]>([]);
  const [loadingWarehouseBatches, setLoadingWarehouseBatches] = useState<boolean>(false);
  const [activeWarehouseBatchId, setActiveWarehouseBatchId] = useState<number | null>(null);

  const [products, setProducts] = useState<ProductMaster[]>([]);

  // Quản lý Đối tác / Hồ sơ Khách hàng (Partner Workspace Presets)
  const [partnerFolders, setPartnerFolders] = useState<MasterDataFolder[]>([]);
  const [editingOrderId, setEditingOrderId] = useState<number | undefined>();
  const [readOnly, setReadOnly] = useState(false);
  const [selectedPartnerId, setSelectedPartnerId] = useState<number | null>(null);

  const selectedPartner = useMemo(() => {
    if (!selectedPartnerId) return null;
    return partnerFolders.find((f) => f.id === selectedPartnerId) || null;
  }, [partnerFolders, selectedPartnerId]);

  // Sắp xếp mã Master Data: ưu tiên mã thuộc Thư mục đối tác đang chọn lên trước
  const sortedProducts = useMemo(() => {
    if (!selectedPartnerId) return products;
    return [...products].sort((a, b) => {
      const aIn = a.folderId === selectedPartnerId ? 1 : 0;
      const bIn = b.folderId === selectedPartnerId ? 1 : 0;
      return bIn - aIn;
    });
  }, [products, selectedPartnerId]);

  // Validation Guard Master Data: Kiểm tra mã có tồn tại trong Master Data của Đối tác hiện tại hay không
  const isStyleCodeInMaster = (rawCode?: string): boolean => {
    if (!rawCode || !rawCode.trim()) return false;
    const code = rawCode.trim().toUpperCase();
    const clean = code.endsWith('.G') ? code.slice(0, -2).trim() : code;
    return products.some((p) => {
      if (selectedPartnerId && p.folderId !== selectedPartnerId) {
        return false;
      }
      const pmCode = (p.styleCode || '').trim().toUpperCase();
      return pmCode === code || pmCode === clean;
    });
  };

  const missingMasterCodes = items
    .filter((it) => it.styleCode && it.styleCode.trim() && !isStyleCodeInMaster(it.styleCode))
    .map((it) => it.styleCode.trim());
  const hasMissingMasterData = missingMasterCodes.length > 0;

  // Cảnh báo & Gợi ý chuyển đổi Đối tác thông minh (Smart Partner Mismatch Detection)
  const [suggestionBannerData, setSuggestionBannerData] = useState<ValidateItemsResult | null>(null);
  const [isDismissedMismatch, setIsDismissedMismatch] = useState<boolean>(false);

  const checkPartnerMismatch = async (itemsToCheck: CreateShipmentItem[], currentPartnerId?: number | null) => {
    const partnerId = currentPartnerId ?? selectedPartnerId;
    if (!partnerId) return;

    const styleCodes = itemsToCheck
      .map((it) => (it.styleCode || '').trim())
      .filter(Boolean);

    if (styleCodes.length === 0) {
      setSuggestionBannerData(null);
      return;
    }

    try {
      const res = await productMasterApi.validateItems({
        currentPartnerFolderId: partnerId,
        styleCodes,
      });

      if (res.hasMismatch && res.suggestedPartnerFolderId) {
        setSuggestionBannerData(res);
      } else {
        setSuggestionBannerData(null);
      }
    } catch {
      // Ignored
    }
  };

  const handleAutoSwitchPartner = () => {
    if (!suggestionBannerData || !suggestionBannerData.suggestedPartnerFolderId) return;
    const targetId = suggestionBannerData.suggestedPartnerFolderId;
    const targetPartner = partnerFolders.find((f) => f.id === targetId);
    if (!targetPartner) return;

    // 1. Cập nhật Đối tác & Header
    setSelectedPartnerId(targetPartner.id);
    form.setFieldsValue({
      customerName: targetPartner.customerName || targetPartner.name,
      address: targetPartner.deliveryAddress || '',
      contractNo: targetPartner.contractNo || '',
      poSuffix: targetPartner.poSuffix || '',
    });

    // 2. Tự động ráp đúng đơn giá CMT, DAP, Mô tả của đối tác mới vào bảng hàng hóa (giữ nguyên Số lượng đôi)
    setItems((prev) => {
      return prev.map((item) => {
        const raw = (item.styleCode || '').trim();
        const upper = raw.toUpperCase();
        const base = upper.endsWith('.G') ? upper.slice(0, -2).trim() : upper;
        const isGo = item.processType === ProcessType.GoKhongMay;

        const detail = suggestionBannerData.details?.find(
          (d) => d.rawCode.trim().toUpperCase() === upper ||
                 d.normalizedCode === upper ||
                 d.normalizedCode === base
        );

        const prod = detail?.matchedProduct || products.find(
          (p) => p.folderId === targetPartner.id &&
                 (p.styleCode.trim().toUpperCase() === upper || p.styleCode.trim().toUpperCase() === base)
        );

        if (prod) {
          const cmt = (isGo && prod.unitPriceCMT_Go && prod.unitPriceCMT_Go > 0)
            ? prod.unitPriceCMT_Go
            : prod.unitPriceCMT;
          const dap = (isGo && prod.unitPriceDAP_Go && prod.unitPriceDAP_Go > 0)
            ? prod.unitPriceDAP_Go
            : prod.unitPriceDAP;

          return {
            ...item,
            description: prod.description || item.description,
            unitPriceCMT: cmt,
            unitPriceDAP: dap,
            pairPerCarton: prod.pairPerCarton || targetPartner.defaultPairsPerCarton || 12,
            unit: prod.unit || targetPartner.defaultUnit || 'đôi',
          };
        }
        return item;
      });
    });

    // 3. Tắt banner và thông báo thành công
    setSuggestionBannerData(null);
    setIsDismissedMismatch(false);
    message.success(`Đã chuyển sang hồ sơ ${targetPartner.name} và tự động ráp giá thành công!`);
  };

  const handleKeepCurrentPartner = () => {
    setSuggestionBannerData(null);
    setIsDismissedMismatch(true);
  };

  const handleOpenQuickAdd = (index: number, item: CreateShipmentItem) => {
    setQuickAddRowIndex(index);
    setQuickAddStyleCode(item.styleCode);
    setQuickAddDescription(item.description || '');
    setQuickAddIsGo(item.processType === ProcessType.GoKhongMay);
    setQuickAddVisible(true);
  };

  const handleQuickAddSuccess = async (created: ProductMaster) => {
    // 1. Reload Master Data
    await loadProducts();
    setProducts((prev) => {
      const exists = prev.some((p) => p.styleCode.toUpperCase() === created.styleCode.toUpperCase());
      return exists ? prev : [created, ...prev];
    });

    // 2. Cập nhật ngay dòng hàng bị lỗi trên bảng với đơn giá và thông tin vừa thêm
    if (quickAddRowIndex !== null && quickAddRowIndex < items.length) {
      setItems((prev) => {
        const next = [...prev];
        const current = next[quickAddRowIndex];
        const isGo = current.processType === ProcessType.GoKhongMay;
        const cmt = (isGo && created.unitPriceCMT_Go && created.unitPriceCMT_Go > 0)
          ? created.unitPriceCMT_Go
          : created.unitPriceCMT;
        const dap = (isGo && created.unitPriceDAP_Go && created.unitPriceDAP_Go > 0)
          ? created.unitPriceDAP_Go
          : created.unitPriceDAP;

        next[quickAddRowIndex] = {
          ...current,
          description: created.description,
          unitPriceCMT: cmt,
          unitPriceDAP: dap,
          pairPerCarton: created.pairPerCarton || 12,
          unit: created.unit || 'đôi',
        };
        return next;
      });
    }

    setQuickAddVisible(false);
    setQuickAddRowIndex(null);
  };
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

  // Thêm nhanh vào Master Data (Quick Add Modal)
  const [quickAddVisible, setQuickAddVisible] = useState<boolean>(false);
  const [quickAddRowIndex, setQuickAddRowIndex] = useState<number | null>(null);
  const [quickAddStyleCode, setQuickAddStyleCode] = useState<string>('');
  const [quickAddDescription, setQuickAddDescription] = useState<string>('');
  const [quickAddIsGo, setQuickAddIsGo] = useState<boolean>(false);

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

  const syncNavigation = useEffectEvent(() => {
    if (activeNavTab === 'history') {
      setActiveTab('history');
      void loadShipmentsHistory();
    } else if (activeNavTab === 'shipment' || activeNavTab === 'ocr') {
      setActiveTab('create');
      if (activeNavTab === 'ocr') setOcrModalVisible(true);
    }
  });
  const initialize = useEffectEvent(() => {
    void loadPartnerFolders();
    void loadProducts();
    void loadShipmentsHistory();
    void loadSequence();
  });
  const openInitialOrder = useEffectEvent(() => {
    if (initialOrderIdToLoad) void handleLoadHistoricalOrder(initialOrderIdToLoad);
  });
  useEffect(() => { const timer = setTimeout(() => syncNavigation(), 0); return () => clearTimeout(timer); }, [activeNavTab]);
  useEffect(() => {
    initialize();
    if (!hasCompletedTour()) {
      const timer = setTimeout(startOnboardingTour, 800);
      return () => clearTimeout(timer);
    }
  }, []);
  useEffect(() => { openInitialOrder(); }, [initialOrderIdToLoad]);

  async function loadSequence() {
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
  }

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

  async function loadProducts() {
    try {
      const res = await productMasterApi.getAll();
      setProducts(res);
    } catch {
      // Ignored
    }
  }

  async function loadPartnerFolders() {
    try {
      const tree = await masterDataFolderApi.getTree();
      const flatten = (nodes: MasterDataFolder[], parent?: MasterDataFolder): MasterDataFolder[] =>
        nodes.flatMap(f => {
          const merged = { ...f, customerName: f.customerName || parent?.customerName,
            contractNo: f.contractNo || parent?.contractNo, deliveryAddress: f.deliveryAddress || parent?.deliveryAddress,
            poSuffix: f.poSuffix || parent?.poSuffix };
          return [merged, ...flatten(f.children || [], merged)];
        });
      const roots = flatten(tree);
      setPartnerFolders(roots);

      if (roots.length > 0 && !selectedPartnerId) {
        const km3 = roots.find((r) => r.name.toLowerCase().includes('kingmaker')) || roots[0];
        setSelectedPartnerId(km3.id);
        const curCustomer = form.getFieldValue('customerName');
        if (!curCustomer || curCustomer === 'CÔNG TY TNHH KINGMAKER III (VIỆT NAM) FOOTWEAR') {
          form.setFieldsValue({
            customerName: km3.customerName || km3.name,
            address: km3.deliveryAddress || form.getFieldValue('address'),
            contractNo: km3.contractNo || form.getFieldValue('contractNo'),
            poSuffix: km3.poSuffix || form.getFieldValue('poSuffix'),
          });
        }
      }
    } catch {
      // Ignored
    }
  }

  const applyPartnerPreset = (partner: MasterDataFolder, clearItems = false) => {
    setSelectedPartnerId(partner.id);
    form.setFieldsValue({
      customerName: partner.customerName || partner.name,
      address: partner.deliveryAddress || '',
      contractNo: partner.contractNo || '',
      poSuffix: partner.poSuffix || '',
    });

    setSuggestionBannerData(null);
    setIsDismissedMismatch(false);

    if (clearItems) {
      setItems([]);
    }
    message.success(`Đã áp dụng cấu hình đối tác: ${partner.name} (${partner.defaultPairsPerCarton} đôi/thùng)`);
  };

  const handlePartnerSelect = (targetId: number) => {
    if (targetId === selectedPartnerId) return;
    const targetPartner = partnerFolders.find((f) => f.id === targetId);
    if (!targetPartner) return;

    if (items.length > 0) {
      Modal.confirm({
        title: 'Xác nhận chuyển đổi Đối tác / Hồ sơ khách hàng',
        icon: <ExclamationCircleOutlined className="text-amber-500" />,
        content: (
          <div className="text-xs text-slate-600 space-y-2 mt-2">
            <p>
              Đổi sang đối tác <strong>{targetPartner.name}</strong> sẽ xóa toàn bộ {items.length} dòng hàng hiện tại để tránh lẫn lộn mã hàng, đơn giá và quy cách đóng thùng.
            </p>
            <p className="text-rose-600 font-medium">
              Bạn có chắc chắn muốn chuyển đổi không?
            </p>
          </div>
        ),
        okText: 'Đồng ý chuyển đổi & Xóa hàng cũ',
        okButtonProps: { danger: true },
        cancelText: 'Hủy bỏ',
        onOk: () => {
          applyPartnerPreset(targetPartner, true);
        },
      });
    } else {
      applyPartnerPreset(targetPartner, false);
    }
  };

  async function loadShipmentsHistory() {
    try {
      setLoadingHistory(true);
      const list = await shipmentApi.getShipments();
      setSavedShipments(list);
    } catch {
      // Ignored
    } finally {
      setLoadingHistory(false);
    }
  }

  const buildRequestData = async (): Promise<CreateShipmentRequest | null> => {
    if (readOnly) { message.warning("Đơn đã thông quan, chỉ được xem và tải chứng từ."); return null; }
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
        orderId: editingOrderId,
        contractFolderId: selectedPartnerId,
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

  const handleResetToNewOrder = async () => {
    setReadOnly(false);
    setEditingOrderId(undefined);
    setActiveWarehouseBatchId(null);
    setItems([]);
    form.resetFields();
    await handleGenerateInvoiceNo();
    form.setFieldValue('invoiceDate', dayjs());
    message.success('Đã làm sạch bảng để sẵn sàng lập hóa đơn mới!');
  };

  const executeExport = async (req: CreateShipmentRequest) => {
    if (isSubmittingRef.current || exporting) return;
    try {
      isSubmittingRef.current = true;
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

        Modal.success({
          title: 'Xuất file ZIP & Lưu hóa đơn thành công!',
          content: (
            <div className="space-y-2 mt-2 text-sm text-slate-700">
              <div>
                Đã xuất file <strong>{zipName}</strong> và lưu <strong>2 hóa đơn</strong> vào hệ thống:
              </div>
              <div className="text-xs text-slate-600 pl-2 space-y-1 bg-slate-50 p-2 rounded border border-slate-200">
                <div>{line1}</div>
                <div>{line2}</div>
              </div>
              <div className="text-xs text-slate-500">
                Bạn có muốn làm sạch bảng để chuẩn bị soạn đơn xuất tiếp theo ngay bây giờ không?
              </div>
            </div>
          ),
          okText: '➕ Soạn đơn xuất tiếp theo',
          cancelText: 'Giữ xem đơn này',
          okCancel: true,
          onOk: () => {
            void handleResetToNewOrder();
          },
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

        Modal.success({
          title: 'Xuất file Excel & Lưu đơn thành công!',
          content: (
            <div className="space-y-2 mt-2 text-sm text-slate-700">
              <div>
                Đã xuất file <strong>{fileName}</strong> và tự động lưu hóa đơn <strong>{savedInvoiceNo}</strong> vào Lịch sử.
              </div>
              <div className="text-xs text-slate-500 bg-slate-50 p-2.5 rounded border border-slate-200">
                Bạn có muốn làm sạch bảng để soạn đơn xuất tiếp theo không?
              </div>
            </div>
          ),
          okText: '➕ Soạn đơn xuất tiếp theo',
          cancelText: 'Giữ xem đơn này',
          okCancel: true,
          onOk: () => {
            void handleResetToNewOrder();
          },
        });
      }
    } catch (err: unknown) {
      let errMsg = 'Lỗi khi xuất file Excel.';
      const error = err as { response?: { data?: unknown } };
      if (error?.response?.data instanceof Blob) {
        try {
          const text = await error.response.data.text();
          const json = JSON.parse(text);
          if (json?.message) errMsg = json.message;
        } catch {
          // Ignored
        }
      } else if (typeof error?.response?.data === 'object' && error?.response?.data !== null) {
        const data = error.response.data as { message?: string };
        if (data.message) errMsg = data.message;
      }
      message.error({ content: errMsg, duration: 8 });
    } finally {
      setExporting(false);
      isSubmittingRef.current = false;
    }
  };

  const handleExportExcel = async () => {
    if (hasMissingMasterData) {
      message.error('Vui lòng cập nhật thông tin Master Data cho các mã còn thiếu trước khi xuất file!');
      return;
    }

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
    if (isSubmittingRef.current || saving) return;
    const req = await buildRequestData();
    if (!req) return;

    try {
      isSubmittingRef.current = true;
      setSaving(true);
      const isUpdating = Boolean(editingOrderId);
      let savedInvoiceNo = req.invoiceNo;
      let savedId: number | undefined = editingOrderId;

      if (isUpdating && editingOrderId) {
        await shipmentApi.updateShipment(editingOrderId, req);
      } else {
        const saved = await shipmentApi.createShipment(req);
        savedId = saved.id;
        savedInvoiceNo = saved.invoiceNo;
        setEditingOrderId(saved.id);
      }

      await loadShipmentsHistory();

      if (activeWarehouseBatchId && savedId) {
        warehouseApi.markProcessed(activeWarehouseBatchId, savedId).catch(() => {});
      }

      // Cập nhật số hóa đơn tiếp theo sau khi lưu
      try {
        const nextInfo = await shipmentApi.getSequence();
        setSequenceInfo(nextInfo);
      } catch {
        // Ignored
      }

      Modal.success({
        title: isUpdating ? 'Cập nhật đơn hàng thành công!' : 'Lưu đơn hàng thành công!',
        content: (
          <div className="space-y-2 mt-2 text-sm text-slate-700">
            <div>
              Đã {isUpdating ? 'cập nhật' : 'lưu'} đơn hàng <strong>{savedInvoiceNo}</strong> ({items.length} mặt hàng) vào hệ thống thành công.
            </div>
            <div className="text-xs text-slate-500 bg-slate-50 p-2.5 rounded border border-slate-200">
              Bạn có muốn làm sạch bảng để soạn đơn hàng mới, hay chuyển sang Lịch sử chứng từ?
            </div>
          </div>
        ),
        okText: '➕ Soạn đơn hàng mới',
        cancelText: '📋 Chuyển đến Lịch sử',
        okCancel: true,
        onOk: () => {
          void handleResetToNewOrder();
        },
        onCancel: () => {
          setActiveTab('history');
          onTabChange?.('history');
        },
      });
    } catch (err: unknown) {
      const error = err as { response?: { data?: { message?: string } }; message?: string };
      message.error(error.response?.data?.message || error.message || 'Lỗi khi lưu đơn hàng vào hệ thống.');
    } finally {
      setSaving(false);
      isSubmittingRef.current = false;
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

  async function handleLoadHistoricalOrder(id: number) {
    try {
      message.loading({ content: 'Đang tải lại dữ liệu đơn hàng...', key: 'load-order' });
      const order = await shipmentApi.getShipmentById(id);
      setEditingOrderId(order.id);
      setSelectedPartnerId(order.contractFolderId ?? null);
      setReadOnly(Boolean(order.isLocked || order.status === ShipmentStatus.Cleared));
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
          const pm = products.find((p) => p.folderId === order.contractFolderId && p.styleCode.toUpperCase() === cleanCode);

          return {
            styleCode: i.styleCode,
            fullItemCode: i.fullItemCode,
            description: i.description || pm?.description || '',
            quantity: i.quantity,
            processType: i.processType,
            unitPriceCMT: i.unitPriceCMT,
            unitPriceDAP: i.unitPriceDAP,
            unit: i.unit || pm?.unit || 'đôi',
            pairPerCarton: i.pairPerCarton || pm?.pairPerCarton || 12,
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
  }

  const handleApplyOcr = (ocrItems: CreateShipmentItem[], mode: 'replace' | 'append') => {
    if (readOnly) return;
    let combined: CreateShipmentItem[] = [];
    if (mode === 'replace') {
      combined = ocrItems.length > 0 ? ocrItems : [
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
      ];
      setItems(combined);
      message.success(`Đã thay thế toàn bộ bằng ${ocrItems.length} mặt hàng từ OCR.`);
    } else {
      setItems((prev) => {
        const validPrev = prev.filter((p) => p.styleCode.trim() || p.quantity > 0);
        combined = [...validPrev, ...ocrItems];
        return combined;
      });
      message.success(`Đã thêm nối tiếp ${ocrItems.length} mặt hàng từ OCR.`);
    }
    setIsDismissedMismatch(false);
    checkPartnerMismatch(combined.length > 0 ? combined : ocrItems);
  };

  const handleAddItem = () => {
    if (readOnly) return;
    const defaultPairs = selectedPartner?.defaultPairsPerCarton || 12;
    const defaultUnit = selectedPartner?.defaultUnit || 'đôi';

    setItems((prev) => [
      ...prev,
      {
        styleCode: '',
        description: '',
        quantity: defaultPairs,
        processType: ProcessType.Standard,
        unitPriceCMT: 0,
        unitPriceDAP: 0,
        unit: defaultUnit,
        pairPerCarton: defaultPairs,
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
    if (readOnly) return;
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
    // Ưu tiên tra cứu trong thư mục đối tác đang chọn
    let p = selectedPartnerId
      ? products.find((x) => x.folderId === selectedPartnerId && x.styleCode.trim().toUpperCase() === cleanCode)
      : undefined;
    if (!p) {
      p = products.find((x) => x.styleCode.trim().toUpperCase() === cleanCode);
    }

    const next = [...items];
    const defaultPairs = p?.pairPerCarton && p.pairPerCarton > 0
      ? p.pairPerCarton
      : (selectedPartner?.defaultPairsPerCarton || 12);
    const defaultUnit = p?.unit || selectedPartner?.defaultUnit || 'đôi';

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
        unit: defaultUnit,
        pairPerCarton: defaultPairs,
      };
    } else {
      next[index] = { ...next[index], styleCode, pairPerCarton: defaultPairs, unit: defaultUnit };
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

  const loadPendingWarehouseBatches = async () => {
    setLoadingWarehouseBatches(true);
    try {
      const data = await warehouseApi.getBatches({ status: 'SubmittedToXnk' });
      setWarehouseBatches(data);
    } catch (err) {
      console.error('Lỗi tải danh sách lô hàng từ kho:', err);
    } finally {
      setLoadingWarehouseBatches(false);
    }
  };

  const handleSelectWarehouseBatch = async (batchId: number) => {
    try {
      const batch = await warehouseApi.getBatchById(batchId);
      const defaultPairs = selectedPartner?.defaultPairsPerCarton || 12;
      const defaultUnit = selectedPartner?.defaultUnit || 'đôi';

      const mapped: CreateShipmentItem[] = batch.items.map((item) => {
        const p = products.find((x) => x.styleCode.trim().toUpperCase() === item.styleCode.trim().toUpperCase());
        const isGo = item.processType === 2;
        return {
          styleCode: item.styleCode,
          description: p?.description || '',
          quantity: item.quantity,
          processType: item.processType === 2 ? ProcessType.GoKhongMay : ProcessType.Standard,
          unitPriceCMT: p ? ((isGo && p.unitPriceCMT_Go && p.unitPriceCMT_Go > 0) ? p.unitPriceCMT_Go : p.unitPriceCMT) : 0,
          unitPriceDAP: p ? ((isGo && p.unitPriceDAP_Go && p.unitPriceDAP_Go > 0) ? p.unitPriceDAP_Go : p.unitPriceDAP) : 0,
          unit: p?.unit || defaultUnit,
          pairPerCarton: p?.pairPerCarton || defaultPairs,
        };
      });

      setItems(mapped);
      if (batch.contractFolderId) {
        setSelectedPartnerId(batch.contractFolderId);
        form.setFieldValue('contractFolderId', batch.contractFolderId);
      }
      setActiveWarehouseBatchId(batch.id);
      setReceiveBatchModalVisible(false);
      message.success(`Đã tiếp nhận thành công lô hàng ${batch.batchName} (${mapped.length} mã, ${batch.totalQuantity.toLocaleString()} đôi) từ kho!`);
    } catch (err) {
      console.error(err);
      message.error('Không thể tải chi tiết lô hàng từ kho.');
    }
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
    if (readOnly) return;
    const combined = mode === 'replace' ? newItems : [...items, ...newItems];
    if (mode === 'replace') {
      setItems(newItems);
      message.success(`Đã thay thế toàn bộ bằng ${newItems.length} mặt hàng.`);
    } else {
      setItems((prev) => [...prev, ...newItems]);
      message.success(`Đã thêm nối tiếp ${newItems.length} mặt hàng.`);
    }
    setIsDismissedMismatch(false);
    checkPartnerMismatch(combined);
  };

  const checkLatestMismatch = useEffectEvent(() => { void checkPartnerMismatch(items); });

  // Tự động kích hoạt kiểm tra chéo khi bảng có từ 2 dòng báo đỏ "Mã chưa có trong Master Data"
  useEffect(() => {
    if (items.length === 0) return;
    if (missingMasterCodes.length >= 2 && !isDismissedMismatch && !suggestionBannerData) {
      const timer = setTimeout(() => {
        checkLatestMismatch();
      }, 500);
      return () => clearTimeout(timer);
    }
  }, [items, missingMasterCodes.length, isDismissedMismatch, suggestionBannerData]);

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
      fixed: 'left',
      render: (_, __, index) => <span className="font-mono text-xs text-slate-400">{index + 1}</span>,
    },
    {
      title: 'Mã hình thể (Style Code)',
      dataIndex: 'styleCode',
      key: 'styleCode',
      width: 200,
      fixed: 'left',
      render: (val: string, _, index) => {
        const rawCode = (val || '').trim().toUpperCase();
        const cleanBase = rawCode.endsWith('.G') ? rawCode.slice(0, -2).trim() : rawCode;
        const matchingProduct = products.find((p) => {
          if (selectedPartnerId && p.folderId !== selectedPartnerId) return false;
          const pm = (p.styleCode || '').trim().toUpperCase();
          return pm === rawCode || pm === cleanBase;
        });

        return (
          <div className="space-y-1">
            <AutoComplete
              value={val}
              options={sortedProducts.map((p) => {
                const isCurrentPartner = selectedPartnerId && p.folderId === selectedPartnerId;
                return {
                  value: p.styleCode,
                  label: (
                    <div className="flex items-center justify-between py-0.5">
                      <div className="flex items-center gap-1.5">
                        <span className="font-mono font-semibold text-slate-900">{p.styleCode}</span>
                        {isCurrentPartner && (
                          <span className="text-[10px] px-1 py-0 rounded bg-blue-50 text-blue-700 border border-blue-200 font-sans">
                            {selectedPartner?.name || 'Đối tác'}
                          </span>
                        )}
                      </div>
                      <span className="text-[11px] text-slate-400 truncate max-w-[150px]">{p.description}</span>
                    </div>
                  ),
                };
              })}
              filterOption={(inputValue, option) =>
                (option?.value as string)?.toLowerCase().includes(inputValue.toLowerCase())
              }
              onSelect={(code) => handleProductSelect(index, code)}
              onChange={(newVal) => handleItemChange(index, 'styleCode', newVal)}
              placeholder="Chọn hoặc nhập mã..."
              className="w-full font-mono text-xs style-code-input"
            />
            {matchingProduct ? (
              <div className="flex items-center space-x-1 text-[11px] text-emerald-700 truncate">
                <CheckCircleFilled className="text-[10px] text-emerald-600 shrink-0" />
                <span className="truncate max-w-[180px]" title={matchingProduct.description}>
                  {matchingProduct.description}
                </span>
              </div>
            ) : (val || '').trim() ? (
              <div className="flex items-center space-x-1 text-[11px] text-rose-600 truncate font-medium">
                <ExclamationCircleOutlined className="text-[10px] text-rose-500 shrink-0" />
                <span className="truncate max-w-[180px]">Mã chưa có trong Master Data</span>
              </div>
            ) : null}
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
        <InputNumber disabled={readOnly}
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
        <InputNumber disabled={readOnly}
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
        <InputNumber disabled={readOnly}
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
        <InputNumber disabled={readOnly}
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
      title: 'Master Data',
      key: 'masterStatus',
      width: 175,
      align: 'center',
      render: (_, record, index) => {
        const code = (record.styleCode || '').trim();
        if (!code) {
          return <span className="text-slate-300 text-xs">-</span>;
        }

        const isMatched = isStyleCodeInMaster(code);

        if (isMatched) {
          return (
            <Tag className="text-[11px] px-2 py-0.5 m-0 border-emerald-300 bg-emerald-50 text-emerald-700 font-medium inline-flex items-center">
              <CheckCircleFilled className="mr-1 text-[10px] text-emerald-600" />
              Khớp dữ liệu
            </Tag>
          );
        }

        return (
          <div className="flex flex-col items-center gap-1.5 py-1">
            <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-semibold bg-rose-50 text-rose-700 border border-rose-200 leading-tight text-center">
              ⚠ Mã mới chưa có trong Master Data
            </span>
            <Button
              type="primary"
              size="small"
              icon={<PlusOutlined className="text-[10px]" />}
              disabled={readOnly} onClick={() => handleOpenQuickAdd(index, record)}
              className="bg-rose-600 hover:bg-rose-700 text-white text-[11px] h-6 px-2 font-medium shadow-none inline-flex items-center"
            >
              Thêm vào Master Data
            </Button>
          </div>
        );
      },
    },
    {
      title: 'Thao tác',
      key: 'actions',
      width: 65,
      align: 'center',
      fixed: 'right',
      render: (_, __, index) => (
        <Tooltip title="Xóa dòng này">
          <Button
            type="text"
            danger
            size="small"
            className="hover:bg-rose-50"
            icon={<DeleteOutlined className="text-xs" />}
            disabled={readOnly} onClick={() => handleRemoveItem(index)}
          />
        </Tooltip>
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
      title: 'STT',
      key: 'stt',
      width: 50,
      align: 'center',
      fixed: 'left',
      render: (_, __, index) => <span className="font-mono text-xs text-slate-400">{index + 1}</span>,
    },
    {
      title: 'Số Hóa đơn (Invoice No)',
      dataIndex: 'invoiceNo',
      key: 'invoiceNo',
      width: 170,
      fixed: 'left',
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
      width: 105,
      align: 'center',
      render: (d: string) => <span className="text-xs font-mono text-slate-600">{dayjs(d).format('DD/MM/YYYY')}</span>,
    },
    {
      title: 'Số Hợp đồng',
      dataIndex: 'contractNo',
      key: 'contractNo',
      width: 140,
      render: (c: string) => (
        <Tooltip title={c}>
          <span className="text-xs font-mono text-slate-700 block max-w-[130px] truncate">{c}</span>
        </Tooltip>
      ),
    },
    {
      title: 'Khách hàng',
      dataIndex: 'customerName',
      key: 'customerName',
      width: 220,
      render: (cust: string) => (
        <Tooltip title={cust}>
          <span className="text-xs text-slate-700 block max-w-[210px] truncate font-medium">{cust}</span>
        </Tooltip>
      ),
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
      fixed: 'right',
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
          <div className="flex justify-between items-start flex-wrap gap-4 pb-4 border-b border-slate-200">
            <div>
              <h1 className="text-2xl font-bold text-slate-900 tracking-tight m-0">
                Lịch sử chứng từ xuất hàng
              </h1>
              <p className="text-sm text-slate-500 mt-1 m-0">
                Danh sách hóa đơn Commercial Invoice & Packing List đã lập ({savedShipments.length} chứng từ)
              </p>
            </div>

            <div className="flex-shrink-0 flex items-center flex-wrap gap-2">
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
                className="text-xs h-9 px-3.5 border-slate-300 text-slate-700 hover:bg-slate-50 font-medium"
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
                className="bg-blue-600 hover:bg-blue-700 text-xs h-9 px-4 font-medium shadow-sm"
              >
                Lập hóa đơn mới
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

            <div className="w-full overflow-x-auto min-w-0">
              <Table
                dataSource={filteredHistory}
                columns={historyColumns}
                rowKey="id"
                loading={loadingHistory}
                pagination={{
                  pageSize: 15,
                  showSizeChanger: true,
                  pageSizeOptions: ['15', '30', '50', '100'],
                  showTotal: (total, range) => (
                    <span className="text-xs text-slate-500">
                      {range[0]}-{range[1]} / {total} chứng từ
                    </span>
                  ),
                }}
                size="middle"
                scroll={{ x: 'max-content' }}
              />
            </div>
          </div>
        </div>
      ) : (
        /* ================= MÀN HÌNH LẬP HÓA ĐƠN & PACKING LIST ================= */
        <div className="space-y-6">
          {/* Header */}
          <div className="flex justify-between items-start flex-wrap gap-4 pb-4 border-b border-slate-200">
            <div>
              <h1 className="text-2xl font-bold text-slate-900 tracking-tight m-0">
                Lập Invoice & Packing List (INV & PKL)
              </h1>
              <p className="text-sm text-slate-500 mt-1 m-0">
                Nhập số liệu phiếu kho, đối soát tổng số đôi và xuất file Excel đa sheet chuẩn mẫu nhà máy
              </p>
            </div>

            <div className="flex-shrink-0 flex items-center flex-wrap gap-2">
              <Button
                icon={<HistoryOutlined />}
                onClick={() => {
                  setActiveTab('history');
                  onTabChange?.('history');
                  loadShipmentsHistory();
                }}
                className="text-xs h-9 px-3.5 border-slate-300 text-slate-700 hover:bg-slate-50 font-medium"
              >
                Lịch sử ({savedShipments.length})
              </Button>
              <Button
                icon={<EyeOutlined />}
                loading={loadingPreview}
                onClick={handlePreviewPkl}
                className="text-xs h-9 px-3.5 border-slate-300 text-slate-700 hover:bg-slate-50 font-medium"
              >
                Xem trước PKL
              </Button>
              <Button
                type={editingOrderId || readOnly ? 'primary' : 'default'}
                icon={<PlusOutlined />}
                onClick={handleResetToNewOrder}
                className={
                  editingOrderId || readOnly
                    ? 'bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs h-9 px-3.5 shadow-xs'
                    : 'text-xs h-9 px-3.5 border-slate-300 text-slate-700 hover:bg-slate-50 font-medium'
                }
              >
                ➕ Tạo đơn mới
              </Button>
              <Button
                icon={<SaveOutlined />}
                loading={saving}
                disabled={readOnly || saving || exporting}
                onClick={handleSaveShipment}
                className="text-xs h-9 px-3.5 border-slate-300 text-slate-700 hover:bg-slate-50 font-medium"
              >
                {editingOrderId ? 'Cập nhật đơn hàng' : 'Lưu đơn hàng'}
              </Button>
              <div id="tour-export-button" className="inline-block">
                <Tooltip
                  title={
                    hasMissingMasterData
                      ? 'Vui lòng cập nhật thông tin Master Data cho các mã còn thiếu trước khi xuất file!'
                      : 'Tự động lưu đơn hàng vào hệ thống và tải xuống file Excel (không cần bấm Lưu trước)'
                  }
                >
                  <span>
                    <Button
                      type="primary"
                      icon={<DownloadOutlined />}
                      loading={exporting}
                      disabled={readOnly || items.length === 0 || hasMissingMasterData}
                      onClick={handleExportExcel}
                      className={
                        hasMissingMasterData
                          ? 'bg-slate-300 text-slate-500 border-slate-300 cursor-not-allowed text-xs h-9 px-4 font-medium'
                          : 'bg-blue-600 hover:bg-blue-700 text-white text-xs h-9 px-4 font-medium shadow-sm'
                      }
                    >
                      Xuất File Excel & Lưu đơn
                    </Button>
                  </span>
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

            {/* Bộ Chuyển Đổi Không Gian Làm Việc Theo Đối Tác (Partner Profile Presets) */}
            <div className="bg-gradient-to-r from-blue-50/80 via-indigo-50/40 to-slate-50 border border-blue-200 rounded-lg p-3.5 flex flex-col md:flex-row md:items-center justify-between gap-3 shadow-xs">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-lg bg-blue-600 text-white flex items-center justify-center font-bold text-lg shadow-sm shrink-0">
                  <ShopOutlined />
                </div>
                <div>
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-xs font-bold text-slate-800 uppercase tracking-wide">
                      Hồ sơ Đối tác / Khách hàng (Partner Workspace)
                    </span>
                    {selectedPartner && (
                      <Tag className="text-[11px] border-blue-300 bg-blue-100/70 text-blue-800 font-semibold m-0">
                        {selectedPartner.defaultPairsPerCarton} đôi / thùng
                      </Tag>
                    )}
                  </div>
                  <div className="text-[11px] text-slate-500 mt-0.5">
                    Tự động điền thông tin hợp đồng, địa chỉ giao hàng và áp dụng quy cách đóng thùng chuẩn của đối tác
                  </div>
                </div>
              </div>

              <div className="flex items-center gap-2 md:w-80 shrink-0">
                <span className="text-xs font-semibold text-slate-700 whitespace-nowrap">Chọn Đối tác:</span>
                <Select
                  disabled={readOnly}
                  value={selectedPartnerId}
                  onChange={handlePartnerSelect}
                  className="w-full text-xs font-medium"
                  size="middle"
                  placeholder="-- Chọn Đối tác / Khách hàng --"
                  options={partnerFolders.map((pf) => ({
                    value: pf.id,
                    label: (
                      <div className="flex items-center justify-between">
                        <span className="font-semibold text-slate-800">{pf.name}</span>
                        <span className="text-[11px] text-slate-400 font-mono ml-2">
                          ({pf.defaultPairsPerCarton} đôi/thùng)
                        </span>
                      </div>
                    ),
                  }))}
                />
              </div>
            </div>

            <Form disabled={readOnly}
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
            <div id="tour-data-import" className="flex justify-between items-center flex-wrap gap-3 pb-3 border-b border-slate-100">
              <div className="text-xs font-semibold text-slate-800 uppercase tracking-wider">
                2. Danh sách Hàng hóa ({items.length} dòng)
              </div>
              <div className="flex-shrink-0 flex items-center flex-wrap gap-2">
                <Button
                  icon={<InboxOutlined />}
                  disabled={readOnly}
                  onClick={() => {
                    loadPendingWarehouseBatches();
                    setReceiveBatchModalVisible(true);
                  }}
                  className="text-xs h-8 px-3 border-emerald-400 text-emerald-700 bg-emerald-50 hover:bg-emerald-100 font-semibold"
                >
                  📥 Tiếp nhận từ Kho
                </Button>
                <Button
                  icon={<CameraOutlined />}
                  disabled={readOnly} onClick={() => setOcrModalVisible(true)}
                  className="text-xs h-8 px-3 border-slate-300 text-slate-700 hover:bg-slate-50 font-medium"
                >
                  Quét OCR Phiếu kho
                </Button>
                <Tooltip title="Hỗ trợ dán trực tiếp danh sách mã và số lượng copy từ bảng tính Excel.">
                  <Button
                    icon={<ThunderboltOutlined />}
                    disabled={readOnly} onClick={() => setQuickPasteVisible(true)}
                    className="text-xs h-8 px-3 border-slate-300 text-slate-700 hover:bg-slate-50 font-medium"
                  >
                    Dán nhanh (Clipboard)
                  </Button>
                </Tooltip>
                <Button
                  icon={<RocketOutlined />}
                  disabled={readOnly} onClick={() => setBatchOcrModalVisible(true)}
                  className="text-xs h-8 px-3 border-blue-300 text-blue-700 bg-blue-50/50 hover:bg-blue-100/50 font-medium"
                >
                  Quét OCR hàng loạt (Batch)
                </Button>

                {/* Dropdown "Thao tác khác" gom các chức năng phụ */}
                <Dropdown
                  menu={{
                    items: [
                      {
                        key: 'populate-master',
                        icon: <AppstoreOutlined />,
                        label: 'Nạp toàn bộ từ Master Data',
                        onClick: handlePopulateAllMasterProducts,
                      },
                      {
                        type: 'divider',
                      },
                      {
                        key: 'clear-all',
                        danger: true,
                        icon: <DeleteOutlined />,
                        label: 'Xóa tất cả các dòng',
                        disabled: items.length === 0,
                        onClick: () => {
                          Modal.confirm({
                            title: 'Xóa toàn bộ danh sách mặt hàng?',
                            content: 'Bạn có chắc chắn muốn xóa tất cả các dòng hiện tại không? Thao tác này không thể hoàn tác.',
                            okText: 'Xóa tất cả',
                            okType: 'danger',
                            cancelText: 'Hủy',
                            onOk: handleClearAllItems,
                          });
                        },
                      },
                    ],
                  }}
                  trigger={['click']}
                >
                  <Button
                    icon={<EllipsisOutlined />}
                    className="text-xs h-8 px-2.5 border-slate-300 text-slate-700 hover:bg-slate-50 font-medium"
                  >
                    Thao tác khác
                  </Button>
                </Dropdown>

                <Button
                  type="primary"
                  icon={<PlusOutlined />}
                  disabled={readOnly} onClick={handleAddItem}
                  className="bg-blue-600 hover:bg-blue-700 text-white text-xs h-8 px-3.5 font-medium shadow-sm"
                >
                  Thêm dòng
                </Button>
              </div>
            </div>

            {/* Thanh Cảnh báo Thông minh (Smart Suggestion Banner - Amber/Orange Alert Bar) */}
            {suggestionBannerData && suggestionBannerData.hasMismatch && suggestionBannerData.suggestedPartnerFolderId && (
              <div className="p-4 rounded-lg border-2 border-amber-400 bg-amber-50 text-amber-950 text-xs shadow-sm transition-all animate-fadeIn">
                <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
                  <div className="flex items-start gap-3">
                    <span className="text-xl shrink-0 leading-none mt-0.5">💡</span>
                    <div className="space-y-1">
                      <div className="font-semibold text-amber-900 text-xs uppercase tracking-wider flex items-center gap-1.5">
                        <span>Phát hiện có thể nhầm lẫn Đối tác:</span>
                        <Tag color="orange" className="font-mono text-[10px] m-0 border-amber-300">
                          {suggestionBannerData.matchedCountInSuggested}/{suggestionBannerData.totalCodes} mã trùng khớp
                        </Tag>
                      </div>
                      <div className="text-amber-900 leading-relaxed text-xs">
                        Hệ thống nhận thấy <strong className="text-amber-950 font-semibold">{suggestionBannerData.matchedCountInSuggested}/{suggestionBannerData.totalCodes}</strong> mã bạn vừa nhập thuộc danh mục của [<strong>{suggestionBannerData.suggestedPartnerName}</strong>].
                        <br />
                        Bạn đang chọn: [<strong>{selectedPartner?.name || 'Chưa chọn'}</strong>].
                      </div>
                    </div>
                  </div>
                  <div className="flex items-center gap-2 shrink-0 self-end md:self-center">
                    <Button
                      type="primary"
                      icon={<ThunderboltOutlined />}
                      onClick={handleAutoSwitchPartner}
                      className="bg-amber-600 hover:bg-amber-700 active:bg-amber-800 text-white font-medium text-xs h-8 px-3 shadow-sm border-0"
                    >
                      Chuyển sang {suggestionBannerData.suggestedPartnerName} & Áp dụng mẫu
                    </Button>
                    <Button
                      onClick={handleKeepCurrentPartner}
                      className="text-xs h-8 px-3 text-slate-700 hover:text-slate-900 border-slate-300 bg-white hover:bg-slate-50"
                    >
                      Giữ nguyên
                    </Button>
                  </div>
                </div>
              </div>
            )}

            {hasMissingMasterData && (
              <div className="flex items-center justify-between p-3 rounded-lg border border-rose-200 bg-rose-50/80 text-rose-800 text-xs shadow-sm">
                <div className="flex items-center gap-2.5">
                  <ExclamationCircleOutlined className="text-rose-600 text-base shrink-0" />
                  <span>
                    <strong>Chốt chặn nghiệp vụ (Business Rule Validation Guard):</strong> Phát hiện{' '}
                    <strong className="text-rose-900">{missingMasterCodes.length}</strong> dòng hàng chứa mã chưa có
                    trong Master Data (<span className="font-mono font-semibold text-rose-900">{missingMasterCodes.join(', ')}</span>
                    ). Nút xuất file Excel đang bị khóa. Vui lòng bấm <strong>[+ Thêm nhanh vào Master Data]</strong> ngay
                    tại dòng vi phạm để cập nhật thông tin và mở khóa xuất file.
                  </span>
                </div>
              </div>
            )}

            <div ref={tableContainerRef} className="w-full overflow-x-auto min-w-0">
              <Table
                dataSource={items}
                columns={columns}
                rowKey={(_, index) => `${index}`}
                rowClassName={(record) => {
                  const code = (record.styleCode || '').trim();
                  if (code && !isStyleCodeInMaster(code)) {
                    return 'row-missing-master bg-rose-50/60 border-l-4 border-l-rose-500 hover:bg-rose-50 transition-colors';
                  }
                  return '';
                }}
                pagination={false}
                size="middle"
                scroll={{ x: 'max-content' }}
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
                          disabled={readOnly} onClick={handleAddItem}
                          className="bg-blue-600 hover:bg-blue-700 text-white text-xs h-8 font-medium shadow-sm"
                        >
                          Thêm dòng mới
                        </Button>
                        <Button
                          icon={<ThunderboltOutlined />}
                          disabled={readOnly} onClick={() => setQuickPasteVisible(true)}
                          className="text-xs h-8 border-slate-300 text-slate-700 hover:bg-slate-50"
                        >
                          Dán nhanh từ Excel
                        </Button>
                        <Button
                          icon={<CameraOutlined />}
                          disabled={readOnly} onClick={() => setOcrModalVisible(true)}
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
        selectedPartnerId={selectedPartnerId}
        defaultPairsPerCarton={selectedPartner?.defaultPairsPerCarton || 12}
        onApply={handleApplyQuickPaste}
      />

      {/* Modal Preview PKL */}
      <PklPreviewModal
        visible={previewVisible}
        onClose={() => setPreviewVisible(false)}
        data={previewData}
        onExportExcel={handleExportExcel}
        exporting={exporting}
        disableExport={hasMissingMasterData}
      />

      {/* Modal Thêm nhanh vào Master Data */}
      <QuickAddMasterDataModal
        folderId={selectedPartnerId}
        defaultPairsPerCarton={selectedPartner?.defaultPairsPerCarton || 12}
        visible={quickAddVisible}
        styleCode={quickAddStyleCode}
        initialDescription={quickAddDescription}
        isGoProcess={quickAddIsGo}
        onCancel={() => {
          setQuickAddVisible(false);
          setQuickAddRowIndex(null);
        }}
        onSuccess={handleQuickAddSuccess}
      />

      {/* Modal OCR Phiếu kho */}
      <OcrUploadModal
        visible={ocrModalVisible}
        onClose={() => setOcrModalVisible(false)}
        products={products}
        selectedPartnerId={selectedPartnerId}
        onApply={handleApplyOcr}
      />

      {/* Modal Quét ảnh OCR hàng loạt theo lô (Batch Upload / Multi-Scan) */}
      <BatchOcrModal
        contractFolderId={selectedPartnerId}
        profile={selectedPartner}
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
            <InputNumber disabled={readOnly}
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
        onSyncSuccess={(result) => {
          if (form.getFieldValue('invoiceNo') === result.invoiceNo) setReadOnly(true);
          void loadShipmentsHistory();
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
              <InputNumber disabled={readOnly}
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

      {/* Modal Tiếp nhận Lô hàng từ Kho */}
      <Modal
        title={
          <div className="flex items-center space-x-2 text-sm font-bold text-slate-800">
            <span className="text-base">📥</span>
            <span>Tiếp nhận Đợt hàng từ Kho Thành Phẩm (成品鞋交接)</span>
          </div>
        }
        open={receiveBatchModalVisible}
        onCancel={() => setReceiveBatchModalVisible(false)}
        footer={null}
        width={700}
      >
        <div className="space-y-3 py-2">
          <p className="text-xs text-slate-500 m-0">
            Chọn một đợt xuất kho do Thủ kho bàn giao để tự động nạp danh sách mã giày và số lượng:
          </p>
          {loadingWarehouseBatches ? (
            <div className="text-center py-8 text-slate-500 text-xs">Đang tải danh sách lô hàng...</div>
          ) : warehouseBatches.length === 0 ? (
            <div className="text-center py-8 text-slate-400 text-xs">
              Hiện tại không có đợt hàng mới nào đang chờ tiếp nhận từ kho.
            </div>
          ) : (
            warehouseBatches.map((b) => (
              <div
                key={b.id}
                onClick={() => handleSelectWarehouseBatch(b.id)}
                className="p-3 rounded-lg border border-slate-200 hover:border-emerald-500 hover:bg-emerald-50/40 cursor-pointer transition-all flex items-center justify-between"
              >
                <div>
                  <div className="flex items-center space-x-2">
                    <strong className="text-sm text-slate-800">{b.batchName}</strong>
                    <Tag color="processing" className="text-[10px]">Đã bàn giao</Tag>
                  </div>
                  <div className="text-xs text-slate-500 mt-1">
                    Ngày xuất: {dayjs(b.exportDate).format('DD/MM/YYYY')} • {b.contractNote}
                  </div>
                  <div className="text-[11px] text-slate-400 mt-0.5">
                    {b.itemCount} mã giày (Gò: {b.goCount} | Thành hình: {b.thanhHinhCount}) • Bàn giao lúc: {b.submittedAt ? dayjs(b.submittedAt).format('HH:mm DD/MM') : '-'}
                  </div>
                </div>
                <div className="text-right">
                  <div className="text-base font-extrabold text-emerald-600 font-mono">
                    {b.totalQuantity.toLocaleString()} đôi
                  </div>
                  <Button size="small" type="primary" className="bg-emerald-600 mt-1 text-xs">
                    Tiếp nhận
                  </Button>
                </div>
              </div>
            ))
          )}
        </div>
      </Modal>
    </div>
  );
});

ShipmentPage.displayName = 'ShipmentPage';
