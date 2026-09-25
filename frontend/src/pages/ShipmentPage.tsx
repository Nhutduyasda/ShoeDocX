import { useState, useEffect, useRef, useImperativeHandle, forwardRef, useMemo } from 'react';
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
  SettingOutlined,
  InboxOutlined,
  FileSearchOutlined,
  CloseOutlined,
  DownOutlined,
  ApartmentOutlined,
  NodeIndexOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { shipmentApi, invoiceNoToFileName, extractSequenceNumber, formatInvoiceNo, formatPartnerFileName } from '../api/shipmentApi';
import { productMasterApi } from '../api/productMasterApi';
import { masterDataFolderApi } from '../api/masterDataFolderApi';
import { warehouseApi } from '../api/warehouseApi';
import { templateApi } from '../api/templateApi';
import { hasCompletedTour, startOnboardingTour } from '../services/tourService';
import { customsApi } from '../api/customsApi';
import { useAuth } from '../contexts/AuthContext';
import { canUnlockClearedShipment, isKeToanUser } from '../types/auth';
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
  DocumentPreviewResponse,
  CompanyTemplate,
} from '../types';
import { ProcessType, ShipmentStatus, ExportSequencePriority, normalizeProcessType } from '../types';
import { PklPreviewModal } from '../components/PklPreviewModal';
import { QuickPasteModal } from '../components/QuickPasteModal';
import { OcrUploadModal } from '../components/OcrUploadModal';
import { BatchOcrModal } from '../components/BatchOcrModal';
import { CustomsSyncModal } from '../components/CustomsSyncModal';
import { QuickAddMasterDataModal } from '../components/QuickAddMasterDataModal';
import { DocumentPreviewModal } from '../components/DocumentPreviewModal';
import { SizeBreakdownModal } from '../components/SizeBreakdownModal';
import { UnlockAuditsModal } from '../components/UnlockAuditsModal';
import { TemplateConfigModal } from '../components/TemplateConfigModal';
import { CustomsArchiveTreePanel, type CustomsTreeFilter } from '../components/CustomsArchiveTreePanel';
import { SplitMatrixModal } from '../features/shipment-dispatch/components/SplitMatrixModal';

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
  const { user } = useAuth();
  const isKeToan = isKeToanUser(user);
  const canUnlockCleared = canUnlockClearedShipment(user);
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
  const [splitMatrixVisible, setSplitMatrixVisible] = useState(false);

  const [products, setProducts] = useState<ProductMaster[]>([]);

  // Quản lý Đối tác / Hồ sơ Khách hàng (Partner Workspace Presets)
  const [partnerFolders, setPartnerFolders] = useState<MasterDataFolder[]>([]);
  const [editingOrderId, setEditingOrderId] = useState<number | undefined>();
  const [readOnly, setReadOnly] = useState(false);
  const [selectedPartnerId, setSelectedPartnerId] = useState<number | null>(null);

  // Biểu mẫu xuất Excel (Bring Your Own Template - BYOT)
  const [templates, setTemplates] = useState<CompanyTemplate[]>([]);
  const [selectedTemplateId, setSelectedTemplateId] = useState<number | null>(null);
  const [templateModalVisible, setTemplateModalVisible] = useState<boolean>(false);

  const loadTemplates = async () => {
    try {
      const data = await templateApi.getAll();
      setTemplates(data);
      const defaultTpl = data.find((t) => t.isDefault) || data[0];
      if (defaultTpl) {
        setSelectedTemplateId((prev) => prev || defaultTpl.id);
      }
    } catch (err) {
      console.error('Không thể nạp danh sách biểu mẫu xuất:', err);
    }
  };

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
      invoiceNo: formatInvoiceNo(targetPartner.invoiceNoPattern, targetPartner.currentSequenceNumber),
      customerName: targetPartner.customerName || targetPartner.name,
      address: targetPartner.deliveryAddress || '',
      contractNo: targetPartner.contractNo || '',
      poSuffix: targetPartner.poSuffix || '',
    });
    setStartInvoiceNum(targetPartner.currentSequenceNumber);
    setSequenceEditValue(targetPartner.currentSequenceNumber);
    setSequenceInfo({
      nextNumber: targetPartner.currentSequenceNumber,
      previewInvoiceNo: formatInvoiceNo(targetPartner.invoiceNoPattern, targetPartner.currentSequenceNumber),
      previewFileName: formatPartnerFileName(targetPartner.fileNamePattern, targetPartner.currentSequenceNumber),
    });

    // 2. Tự động ráp đúng đơn giá CMT, DAP, Mô tả của đối tác mới vào bảng hàng hóa (giữ nguyên Số lượng đôi)
    setItems((prev) => {
      return prev.map((item) => {
        const raw = (item.styleCode || '').trim();
        const upper = raw.toUpperCase();
        const base = upper.endsWith('.G') ? upper.slice(0, -2).trim() : upper;
        const isGo = normalizeProcessType(item.processType) === ProcessType.GoKhongMay;

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
            processType: normalizeProcessType(item.processType),
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
    setQuickAddIsGo(normalizeProcessType(item.processType) === ProcessType.GoKhongMay);
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
        const isGo = normalizeProcessType(current.processType) === ProcessType.GoKhongMay;
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
  const [activeTab, setActiveTabState] = useState<string>(activeNavTab === 'history' || isKeToan ? 'history' : 'create');
  const setActiveTab = (tab: string) => {
    if (isKeToan && tab === 'create') return;
    setActiveTabState(tab);
  };

  // Hậu kiểm & Đối soát Hải quan
  const [customsModalOpen, setCustomsModalOpen] = useState<boolean>(false);
  const [selectedOrderForCustoms, setSelectedOrderForCustoms] = useState<SavedShipmentSummary | null>(null);

  // Thêm nhanh vào Master Data (Quick Add Modal)
  const [quickAddVisible, setQuickAddVisible] = useState<boolean>(false);
  const [quickAddRowIndex, setQuickAddRowIndex] = useState<number | null>(null);
  const [quickAddStyleCode, setQuickAddStyleCode] = useState<string>('');
  const [quickAddDescription, setQuickAddDescription] = useState<string>('');
  const [quickAddIsGo, setQuickAddIsGo] = useState<boolean>(false);

  // Modal xem trước chứng từ
  const [documentPreviewVisible, setDocumentPreviewVisible] = useState<boolean>(false);
  const [documentPreviewData, setDocumentPreviewData] = useState<DocumentPreviewResponse | null>(null);

  // Size Breakdown Modal
  const [sizeModalOpen, setSizeModalOpen] = useState<boolean>(false);
  const [sizeModalIndex, setSizeModalIndex] = useState<number>(0);
  const [sizeModalItem, setSizeModalItem] = useState<CreateShipmentItem | null>(null);

  // Unlock Audits Modal
  const [auditModalOpen, setAuditModalOpen] = useState<boolean>(false);
  const [auditModalOrderId, setAuditModalOrderId] = useState<number | null>(null);
  const [auditModalInvoiceNo, setAuditModalInvoiceNo] = useState<string>('');

  // Customs Archive Tree State
  const [customsTreeKey, setCustomsTreeKey] = useState<string>('all');
  const [customsTreeFilter, setCustomsTreeFilter] = useState<CustomsTreeFilter>({});

  // History table multi-selection & batch print state
  const [selectedOrderIds, setSelectedOrderIds] = useState<number[]>([]);
  const [batchPrintType, setBatchPrintType] = useState<'INV' | 'PKL' | 'ALL'>('ALL');
  const [batchPrinting, setBatchPrinting] = useState<boolean>(false);

  const currentFolderName = useMemo(() => {
    if (selectedOrderIds.length === 0) return '';
    const firstOrder = savedShipments.find((s) => s.id === selectedOrderIds[0]);
    if (!firstOrder?.contractFolderId) return '';
    const folder = partnerFolders.find((p) => p.id === firstOrder.contractFolderId);
    return folder ? folder.name : '';
  }, [selectedOrderIds, savedShipments, partnerFolders]);

  const handleClearSelection = () => {
    setSelectedOrderIds([]);
  };

  const handleBatchPrint = async (printType: 'INV' | 'PKL' | 'ALL' = batchPrintType) => {
    if (selectedOrderIds.length === 0) return;
    try {
      setBatchPrintType(printType);
      setBatchPrinting(true);
      message.loading({ content: 'Đang chuẩn bị dữ liệu in hàng loạt...', key: 'batch-print' });
      const res = await shipmentApi.getBatchPrintData(selectedOrderIds, printType);
      if (!res.documents || res.documents.length === 0) {
        message.warning({ content: 'Không tìm thấy dữ liệu chứng từ phù hợp để in.', key: 'batch-print' });
        return;
      }
      message.success({ content: `Đã chuẩn bị ${res.documents.length} bộ chứng từ. Đang mở cửa sổ xem trước & in...`, key: 'batch-print' });
      setDocumentPreviewData(res.documents[0]);
      setDocumentPreviewVisible(true);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Không thể tải dữ liệu in hàng loạt.';
      message.error({ content: msg, key: 'batch-print' });
    } finally {
      setBatchPrinting(false);
    }
  };

  const handleOpenSizeModal = (index: number, item: CreateShipmentItem) => {
    setSizeModalIndex(index);
    setSizeModalItem(item);
    setSizeModalOpen(true);
  };

  const handleSaveSizeBreakdown = (updatedJson: string) => {
    setItems((prev) => {
      const next = [...prev];
      if (next[sizeModalIndex]) {
        next[sizeModalIndex] = {
          ...next[sizeModalIndex],
          sizeBreakdownJson: updatedJson,
        };
      }
      return next;
    });
    message.success(`Đã cập nhật Size Breakdown dòng #${sizeModalIndex + 1}`);
  };

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
    if (activeNavTab === 'history' || isKeToan) {
      setActiveTab('history');
      void loadShipmentsHistory();
    } else if (activeNavTab === 'shipment' || activeNavTab === 'ocr') {
      if (!isKeToan) {
        setActiveTab('create');
        if (activeNavTab === 'ocr') {
          setOcrModalVisible(true);
        }
      }
    }
  }, [activeNavTab, isKeToan]);

  useEffect(() => {
    void loadPartnerFolders();
    void loadProducts();
    void loadShipmentsHistory();
    void loadTemplates();
    if (!hasCompletedTour()) {
      const timer = setTimeout(startOnboardingTour, 800);
      return () => clearTimeout(timer);
    }
  }, []);

  useEffect(() => {
    if (initialOrderIdToLoad) {
      void handleLoadHistoricalOrder(initialOrderIdToLoad);
    }
  }, [initialOrderIdToLoad]);

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
        previewFileName: formatPartnerFileName(selectedPartner?.fileNamePattern, seq),
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
          const merged = {
            ...f, customerName: f.customerName || parent?.customerName,
            contractNo: f.contractNo || parent?.contractNo, deliveryAddress: f.deliveryAddress || parent?.deliveryAddress,
            poSuffix: f.poSuffix || parent?.poSuffix,
            invoiceNoPattern: f.invoiceNoPattern || parent?.invoiceNoPattern || 'KMHD-NEW2026-{SEQ:4}',
            fileNamePattern: f.fileNamePattern || parent?.fileNamePattern || 'KM3-26-DH{SEQ}.xlsx',
            currentSequenceNumber: f.currentSequenceNumber || parent?.currentSequenceNumber || 1
          };
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
            invoiceNo: formatInvoiceNo(km3.invoiceNoPattern, km3.currentSequenceNumber),
            customerName: km3.customerName || km3.name,
            address: km3.deliveryAddress || form.getFieldValue('address'),
            contractNo: km3.contractNo || form.getFieldValue('contractNo'),
            poSuffix: km3.poSuffix || form.getFieldValue('poSuffix'),
          });
          setStartInvoiceNum(km3.currentSequenceNumber);
          setSequenceEditValue(km3.currentSequenceNumber);
          setSequenceInfo({
            nextNumber: km3.currentSequenceNumber,
            previewInvoiceNo: formatInvoiceNo(km3.invoiceNoPattern, km3.currentSequenceNumber),
            previewFileName: formatPartnerFileName(km3.fileNamePattern, km3.currentSequenceNumber),
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
      invoiceNo: formatInvoiceNo(partner.invoiceNoPattern, partner.currentSequenceNumber),
      customerName: partner.customerName || partner.name,
      address: partner.deliveryAddress || '',
      contractNo: partner.contractNo || '',
      poSuffix: partner.poSuffix || '',
    });
    setStartInvoiceNum(partner.currentSequenceNumber);
    setSequenceEditValue(partner.currentSequenceNumber);
    setSequenceInfo({
      nextNumber: partner.currentSequenceNumber,
      previewInvoiceNo: formatInvoiceNo(partner.invoiceNoPattern, partner.currentSequenceNumber),
      previewFileName: formatPartnerFileName(partner.fileNamePattern, partner.currentSequenceNumber),
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
        if (items[i].sizeBreakdownJson?.trim()) {
          try {
            const breakdown = JSON.parse(items[i].sizeBreakdownJson!) as Record<string, unknown>;
            const quantities = Object.values(breakdown);
            if (Array.isArray(breakdown) || quantities.some((v) => !Number.isInteger(v) || Number(v) < 0)) throw new Error();
            const total = quantities.reduce<number>((sum, value) => sum + Number(value), 0);
            if (total !== items[i].quantity) {
              message.error(`Dòng #${i + 1} (${items[i].styleCode}): tổng Size Breakdown ${total} đôi không bằng số lượng ${items[i].quantity} đôi.`);
              return null;
            }
          } catch {
            message.error(`Dòng #${i + 1} (${items[i].styleCode}): Size Breakdown không đúng JSON số lượng theo size.`);
            return null;
          }
        }
      }

      // Ưu tiên giá trị ô Input (Source of Truth)
      const currentInvoiceNo = (form.getFieldValue('invoiceNo') || values.invoiceNo || '').trim();
      const match = currentInvoiceNo.match(/\d+$/);
      const currentSeq = match ? parseInt(match[0], 10) : (startInvoiceNum || sequenceInfo?.nextNumber || 233);

      return {
        orderId: editingOrderId,
        templateId: selectedTemplateId ?? undefined,
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
        items: items.map((it) => {
          const normType = normalizeProcessType(it.processType);
          return {
            ...it,
            processType: normType,
            fullItemCode:
              normType === ProcessType.GoKhongMay
                ? `${it.styleCode}.G ${values.poSuffix || ''}`.trim()
                : `${it.styleCode} ${values.poSuffix || ''}`.trim(),
          };
        }),
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

  const [loadingDocumentPreview, setLoadingDocumentPreview] = useState<boolean>(false);

  const handlePreviewDocument = async () => {
    const req = await buildRequestData();
    if (!req) return;

    try {
      setLoadingDocumentPreview(true);
      const data = await shipmentApi.previewDocument(req);
      setDocumentPreviewData(data);
      setDocumentPreviewVisible(true);
    } catch {
      message.error('Không thể tải thông tin xem trước Hóa đơn & PKL.');
    } finally {
      setLoadingDocumentPreview(false);
    }
  };

  const handleGenerateInvoiceNo = async () => {
    try {
      if (selectedPartnerId) {
        const partner = await masterDataFolderApi.getById(selectedPartnerId);
        const invoiceNo = formatInvoiceNo(partner.invoiceNoPattern, partner.currentSequenceNumber);
        const previewFileName = formatPartnerFileName(partner.fileNamePattern, partner.currentSequenceNumber);
        setPartnerFolders((prev) => prev.map((p) => p.id === partner.id ? { ...p, ...partner } : p));
        setSequenceInfo({ nextNumber: partner.currentSequenceNumber, previewInvoiceNo: invoiceNo, previewFileName });
        setStartInvoiceNum(partner.currentSequenceNumber);
        setSequenceEditValue(partner.currentSequenceNumber);
        form.setFieldsValue({ invoiceNo });
        message.info(`Đã điền số hóa đơn tiếp theo: ${invoiceNo}`);
        return;
      }
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
      if (selectedPartnerId) {
        try {
          const partner = await masterDataFolderApi.getById(selectedPartnerId);
          const nextInfo = {
            nextNumber: partner.currentSequenceNumber,
            previewInvoiceNo: formatInvoiceNo(partner.invoiceNoPattern, partner.currentSequenceNumber),
            previewFileName: formatPartnerFileName(partner.fileNamePattern, partner.currentSequenceNumber),
          };
          setPartnerFolders((prev) => prev.map((p) => p.id === partner.id ? { ...p, ...partner } : p));
          setSequenceInfo(nextInfo);
          setStartInvoiceNum(nextInfo.nextNumber);
          form.setFieldValue('invoiceNo', nextInfo.previewInvoiceNo);
        } catch { /* Ignored */ }
      }

      if (isZip && exportSummary?.hasTwoFiles) {
        const secondNum = req.priority === ExportSequencePriority.GoFirst
          ? exportSummary.standardSequenceNumber
          : exportSummary.goSequenceNumber;
        const firstFileName = req.priority === ExportSequencePriority.GoFirst
          ? exportSummary.goFileName
          : exportSummary.standardFileName;
        const zipName = `${(firstFileName || 'shipment.xlsx').replace(/\.xlsx$/i, '')}-${secondNum}.zip`;
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
        const fallbackSeq = extractSequenceNumber(req.invoiceNo);
        const fileName = exportSummary?.singleFileName
          ?? (fallbackSeq ? formatPartnerFileName(selectedPartner?.fileNamePattern, fallbackSeq) : invoiceNoToFileName(req.invoiceNo));
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
      req.items.some((i) => normalizeProcessType(i.processType) === ProcessType.Standard) &&
      req.items.some((i) => normalizeProcessType(i.processType) === ProcessType.GoKhongMay);

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
        warehouseApi.markProcessed(activeWarehouseBatchId, savedId).catch(() => { });
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

  const handleUnlockClearedShipment = (order: SavedShipmentSummary) => {
    let reason = '';
    Modal.confirm({
      title: `Mở khóa hóa đơn ${order.invoiceNo}`,
      content: (
        <div className="mt-3 space-y-2">
          <p className="text-xs text-slate-600">Nhập lý do khai bổ sung sau thông quan (AMA). Thao tác sẽ được ghi vào lịch sử audit.</p>
          <Input.TextArea rows={3} placeholder="Lý do mở khóa (tối thiểu 10 ký tự)" onChange={(e) => { reason = e.target.value; }} />
        </div>
      ),
      okText: 'Mở khóa',
      okButtonProps: { danger: true },
      cancelText: 'Hủy',
      onOk: async () => {
        if (reason.trim().length < 10) throw new Error('Lý do mở khóa phải có ít nhất 10 ký tự.');
        await shipmentApi.unlockClearedShipment(order.id, reason.trim());
        message.success('Đã mở khóa đơn hàng và ghi lịch sử audit.');
        await loadShipmentsHistory();
      },
    });
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
    if (isKeToan) {
      message.warning('Tài khoản Kế toán chỉ có quyền tra cứu và tải Excel đối soát.');
      return;
    }
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
            processType: normalizeProcessType(i.processType),
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
          previewFileName: formatPartnerFileName(
            partnerFolders.find((p) => p.id === order.contractFolderId)?.fileNamePattern,
            seq,
          ),
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
    const normalizedOcrItems = ocrItems.map((item) => ({
      ...item,
      processType: normalizeProcessType(item.processType),
    }));
    let combined: CreateShipmentItem[] = [];
    if (mode === 'replace') {
      combined = normalizedOcrItems.length > 0 ? normalizedOcrItems : [
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
      message.success(`Đã thay thế toàn bộ bằng ${normalizedOcrItems.length} mặt hàng từ OCR.`);
    } else {
      setItems((prev) => {
        const validPrev = prev.filter((p) => p.styleCode.trim() || p.quantity > 0);
        combined = [...validPrev, ...normalizedOcrItems];
        return combined;
      });
      message.success(`Đã thêm nối tiếp ${normalizedOcrItems.length} mặt hàng từ OCR.`);
    }
    setIsDismissedMismatch(false);
    checkPartnerMismatch(combined.length > 0 ? combined : normalizedOcrItems);
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
      const isGo = normalizeProcessType(currentItem.processType) === ProcessType.GoKhongMay;

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

    const normalizedNewType = normalizeProcessType(newProcessType);
    if (p) {
      const isGo = normalizedNewType === ProcessType.GoKhongMay;
      next[index] = {
        ...item,
        processType: normalizedNewType,
        unitPriceCMT: (isGo && p.unitPriceCMT_Go && p.unitPriceCMT_Go > 0) ? p.unitPriceCMT_Go : p.unitPriceCMT,
        unitPriceDAP: (isGo && p.unitPriceDAP_Go && p.unitPriceDAP_Go > 0) ? p.unitPriceDAP_Go : p.unitPriceDAP,
      };
    } else {
      next[index] = { ...item, processType: normalizedNewType };
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
        // 1. Nhận diện chuẩn xác công đoạn Gò không may (chấp nhận cả number 2, string "GoKhongMay", hoặc đuôi .G)
        const rawProcess = String(item.processType || '');
        const rawCode = (item.styleCode || '').trim().toUpperCase();
        const isGo =
          (item.processType as unknown) === 2 ||
          rawProcess === 'GoKhongMay' ||
          rawProcess.toLowerCase().includes('go') ||
          rawCode.endsWith('.G');

        // 2. Tách mã gốc để tra cứu Master Data
        const baseCode = rawCode.endsWith('.G') ? rawCode.slice(0, -2).trim() : rawCode;

        // 3. Tra cứu sản phẩm trong Master Data
        const p = products.find((x) => {
          const pmCode = x.styleCode.trim().toUpperCase();
          return pmCode === baseCode || pmCode === rawCode;
        });

        // Safeguard kép: Nếu mã này trong Master Data CHỈ có đơn giá Gò không may (CMT==0 và CMT_Go>0),
        // tự động nhận diện chính xác là Gò không may để bảo toàn đơn giá, không bao giờ bị 0
        const isGoOnlyInMaster = !!(p && (!p.unitPriceCMT || p.unitPriceCMT === 0) && (p.unitPriceCMT_Go && p.unitPriceCMT_Go > 0));
        const effectiveIsGo = isGo || isGoOnlyInMaster;

        // 4. Lấy đúng đơn giá CMT/DAP theo công đoạn
        const cmt = p
          ? effectiveIsGo && p.unitPriceCMT_Go && p.unitPriceCMT_Go > 0
            ? p.unitPriceCMT_Go
            : p.unitPriceCMT
          : 0;

        const dap = p
          ? effectiveIsGo && p.unitPriceDAP_Go && p.unitPriceDAP_Go > 0
            ? p.unitPriceDAP_Go
            : p.unitPriceDAP
          : 0;

        return {
          styleCode: baseCode, // Giữ mã gốc sạch để hệ thống tự ghép hậu tố khi xuất
          description: p?.description || '',
          quantity: item.quantity,
          processType: effectiveIsGo ? ProcessType.GoKhongMay : ProcessType.Standard,
          unitPriceCMT: cmt,
          unitPriceDAP: dap,
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
      message.success(
        `Đã tiếp nhận thành công lô ${batch.batchName} (${mapped.length} mã, ${batch.totalQuantity.toLocaleString()} đôi) từ kho! Đã bảo toàn chính xác các mã Gò không may.`
      );
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
      if (!selectedPartner) {
        message.error('Vui lòng chọn đối tác trước khi cập nhật số thứ tự.');
        return;
      }
      const updated = await masterDataFolderApi.update(selectedPartner.id, {
        name: selectedPartner.name,
        parentId: selectedPartner.parentId ?? null,
        customerName: selectedPartner.customerName,
        deliveryAddress: selectedPartner.deliveryAddress,
        contractNo: selectedPartner.contractNo,
        poSuffix: selectedPartner.poSuffix,
        defaultPairsPerCarton: selectedPartner.defaultPairsPerCarton,
        defaultUnit: selectedPartner.defaultUnit,
        displayOrder: selectedPartner.displayOrder,
        invoiceNoPattern: selectedPartner.invoiceNoPattern,
        fileNamePattern: selectedPartner.fileNamePattern,
        currentSequenceNumber: sequenceEditValue,
      });
      const info = {
        nextNumber: updated.currentSequenceNumber,
        previewInvoiceNo: formatInvoiceNo(updated.invoiceNoPattern, updated.currentSequenceNumber),
        previewFileName: formatPartnerFileName(updated.fileNamePattern, updated.currentSequenceNumber),
      };
      setPartnerFolders((prev) => prev.map((p) => p.id === updated.id ? { ...p, ...updated } : p));
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

  // Tự động kích hoạt kiểm tra chéo khi bảng có từ 2 dòng báo đỏ "Mã chưa có trong Master Data"
  useEffect(() => {
    if (items.length === 0) return;
    if (missingMasterCodes.length >= 2 && !isDismissedMismatch && !suggestionBannerData) {
      const timer = setTimeout(() => {
        void checkPartnerMismatch(items);
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

  const standardItems = items.filter((i) => normalizeProcessType(i.processType) === ProcessType.Standard);
  const goItems = items.filter((i) => normalizeProcessType(i.processType) === ProcessType.GoKhongMay);
  const standardQty = standardItems.reduce((acc, i) => acc + (i.quantity || 0), 0);
  const goQty = goItems.reduce((acc, i) => acc + (i.quantity || 0), 0);

  const firstSeqNum = startInvoiceNum || 233;
  const secondSeqNum = firstSeqNum + 1;
  const isStandardFirst = exportPriority === ExportSequencePriority.StandardFirst;

  const previewFile1 = isStandardFirst
    ? {
      label: 'Hàng Thành hình',
      type: 'standard' as const,
      fileName: formatPartnerFileName(selectedPartner?.fileNamePattern, firstSeqNum),
      invoiceNo: formatInvoiceNo(selectedPartner?.invoiceNoPattern, firstSeqNum),
      qty: standardQty,
    }
    : {
      label: 'Hàng Gò không may',
      type: 'go' as const,
      fileName: formatPartnerFileName(selectedPartner?.fileNamePattern, firstSeqNum),
      invoiceNo: formatInvoiceNo(selectedPartner?.invoiceNoPattern, firstSeqNum),
      qty: goQty,
    };

  const previewFile2 = isStandardFirst
    ? {
      label: 'Hàng Gò không may',
      type: 'go' as const,
      fileName: formatPartnerFileName(selectedPartner?.fileNamePattern, secondSeqNum),
      invoiceNo: formatInvoiceNo(selectedPartner?.invoiceNoPattern, secondSeqNum),
      qty: goQty,
    }
    : {
      label: 'Hàng Thành hình',
      type: 'standard' as const,
      fileName: formatPartnerFileName(selectedPartner?.fileNamePattern, secondSeqNum),
      invoiceNo: formatInvoiceNo(selectedPartner?.invoiceNoPattern, secondSeqNum),
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
      title: 'Size Breakdown',
      key: 'sizeBreakdown',
      width: 155,
      align: 'center',
      render: (_, record, index) => {
        let total = 0;
        let hasJson = false;
        let isMatched = false;
        if (record.sizeBreakdownJson?.trim()) {
          try {
            const parsed = JSON.parse(record.sizeBreakdownJson) as Record<string, unknown>;
            const quantities = Object.values(parsed);
            if (!Array.isArray(parsed) && !quantities.some((v) => !Number.isInteger(v) || Number(v) < 0)) {
              total = quantities.reduce<number>((s, v) => s + Number(v), 0);
              hasJson = true;
              isMatched = total === record.quantity;
            }
          } catch {
            // invalid
          }
        }

        return (
          <div className="flex flex-col items-center gap-1">
            {hasJson ? (
              <Tooltip title="Bấm để xem và chỉnh sửa phân bổ Size Breakdown">
                <Tag
                  color={isMatched ? 'success' : 'error'}
                  className="cursor-pointer font-mono text-[11px] font-semibold m-0 px-2 py-0.5 rounded shadow-xs"
                  onClick={() => handleOpenSizeModal(index, record)}
                >
                  {isMatched ? (
                    <span>✅ Khớp: {total} đôi</span>
                  ) : (
                    <span className="font-bold">⚠️ Lệch: {total} ≠ {record.quantity}</span>
                  )}
                </Tag>
              </Tooltip>
            ) : (
              <Button
                size="small"
                type="dashed"
                className="text-[11px] h-6 px-2 text-slate-500 hover:text-blue-600 rounded"
                disabled={readOnly}
                onClick={() => handleOpenSizeModal(index, record)}
              >
                + Thêm Size
              </Button>
            )}
          </div>
        );
      },
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
          value={normalizeProcessType(val)}
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
            <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-[#F0FDF4] text-[#15803D] border border-[#BBF7D0]">
              <CheckCircleFilled className="mr-1 text-[10px] text-[#16A34A]" />
              Khớp dữ liệu
            </span>
          );
        }

        return (
          <div className="flex flex-col items-center gap-1.5 py-1">
            <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-medium bg-[#FEF2F2] text-[#B91C1C] border border-[#FCA5A5] leading-tight text-center">
              Chưa có trong Master Data
            </span>
            <Button
              type="primary"
              size="small"
              icon={<PlusOutlined className="text-[10px]" />}
              disabled={readOnly} onClick={() => handleOpenQuickAdd(index, record)}
              className="bg-[#B91C1C] hover:bg-[#991B1B] text-white text-[11px] h-6 px-2 font-medium shadow-none inline-flex items-center rounded"
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

  const filteredHistory = useMemo(() => {
    return savedShipments.filter((s) => {
      if (historySearchText.trim()) {
        const q = historySearchText.toLowerCase();
        const match =
          s.invoiceNo.toLowerCase().includes(q) ||
          s.customerName.toLowerCase().includes(q) ||
          s.contractNo.toLowerCase().includes(q) ||
          (s.declarationNo && s.declarationNo.toLowerCase().includes(q));
        if (!match) return false;
      }

      if (customsTreeFilter.partnerFolderId) {
        const pId = customsTreeFilter.partnerFolderId;
        const targetPartner = partnerFolders.find((p) => p.id === pId);
        const targetName = (targetPartner?.customerName || targetPartner?.name || customsTreeFilter.partnerName || '').toLowerCase();

        // Kiểm tra xem đơn hàng có thuộc folder này hoặc folder con, hoặc khớp tên khách hàng
        const getDescendantIds = (rootId: number): Set<number> => {
          const set = new Set<number>([rootId]);
          let added = true;
          while (added) {
            added = false;
            for (const f of partnerFolders) {
              if (f.parentId && set.has(f.parentId) && !set.has(f.id)) {
                set.add(f.id);
                added = true;
              }
            }
          }
          return set;
        };

        const descendantIds = getDescendantIds(pId);
        const matchFolder = s.contractFolderId ? descendantIds.has(s.contractFolderId) : false;
        const matchName = targetName && s.customerName ? s.customerName.toLowerCase().includes(targetName) : false;
        if (!matchFolder && !matchName) return false;
      } else if (customsTreeFilter.partnerName) {
        const pName = customsTreeFilter.partnerName.toLowerCase();
        if (!s.customerName || !s.customerName.toLowerCase().includes(pName)) {
          return false;
        }
      }

      if (customsTreeFilter.year) {
        const dateStr = s.clearanceDate || s.invoiceDate;
        const year = dayjs(dateStr).isValid() ? dayjs(dateStr).year() : dayjs().year();
        if (year !== customsTreeFilter.year) return false;
      }

      if (customsTreeFilter.contractNo) {
        const cFilter = customsTreeFilter.contractNo.trim().toLowerCase();
        const sContract = (s.contractNo || '').trim().toLowerCase();
        if (cFilter === 'không có hđ') {
          if (sContract && sContract !== 'không có hđ') return false;
        } else {
          if (sContract !== cFilter) return false;
        }
      }

      if (customsTreeFilter.customsStatus === 'Cleared') {
        if (s.status !== ShipmentStatus.Cleared) return false;
        if (customsTreeFilter.channel !== undefined && s.customsChannel !== customsTreeFilter.channel) {
          return false;
        }
      } else if (customsTreeFilter.pending || customsTreeFilter.customsStatus === 'Pending') {
        const isPending = !s.declarationNo || s.customsChannel == null || s.status === ShipmentStatus.Exported;
        if (!isPending) return false;
      } else if (customsTreeFilter.channel !== undefined) {
        if (s.customsChannel !== customsTreeFilter.channel) return false;
      }

      return true;
    });
  }, [savedShipments, historySearchText, customsTreeFilter, partnerFolders]);

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
      title: 'Khách hàng / Đối tác',
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
              <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded text-[11px] font-medium bg-[#F0FDF4] text-[#15803D] border border-[#BBF7D0]">
                <CheckCircleFilled className="text-[#16A34A] text-[10px]" />
                Luồng 1 - Xanh (Đã thông quan)
              </span>
            );
          }
          if (record.customsChannel === 2) {
            return (
              <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded text-[11px] font-medium bg-[#FFFBEB] text-[#B45309] border border-[#FDE68A]">
                <CheckCircleFilled className="text-[#D97706] text-[10px]" />
                Luồng 2 - Vàng (Đã thông quan)
              </span>
            );
          }
          if (record.customsChannel === 3) {
            return (
              <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded text-[11px] font-medium bg-[#FEF2F2] text-[#B91C1C] border border-[#FCA5A5]">
                <CheckCircleFilled className="text-[#DC2626] text-[10px]" />
                Luồng 3 - Đỏ (Đã thông quan)
              </span>
            );
          }
          return (
            <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded text-[11px] font-medium bg-[#F0FDF4] text-[#15803D] border border-[#BBF7D0]">
              <CheckCircleFilled className="text-[#16A34A] text-[10px]" />
              Đã thông quan
            </span>
          );
        }

        if (record.status === ShipmentStatus.Discrepancy) {
          return (
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-medium bg-[#FEF2F2] text-[#B91C1C] border border-[#FCA5A5]">
              <ExclamationCircleOutlined className="text-[#DC2626] text-[10px]" />
              Sai lệch số liệu
            </span>
          );
        }

        if (record.status === ShipmentStatus.Exported) {
          return (
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-medium bg-[#F3F4F6] text-[#4B5563] border border-[#E5E7EB]">
              Chờ thông quan
            </span>
          );
        }

        return (
          <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-[#F9FAFB] text-[#6B7280] border border-[#E5E7EB]">
            Bản nháp
          </span>
        );
      },
    },
    {
      title: 'Tờ khai Hải quan',
      key: 'customsDeclaration',
      width: 205,
      render: (_, record) => {
        if (!record.declarationNo) {
          return <span className="text-slate-400 italic text-xs">Chưa có tờ khai</span>;
        }

        const channelTag =
          record.customsChannel === 1 ? (
            <Tag color="success" className="m-0 text-[10px] px-1 py-0 font-medium">
              Luồng 1 (Xanh)
            </Tag>
          ) : record.customsChannel === 2 ? (
            <Tag color="warning" className="m-0 text-[10px] px-1 py-0 font-medium">
              Luồng 2 (Vàng)
            </Tag>
          ) : record.customsChannel === 3 ? (
            <Tag color="error" className="m-0 text-[10px] px-1 py-0 font-medium">
              Luồng 3 (Đỏ)
            </Tag>
          ) : null;

        return (
          <div className="flex items-start justify-between gap-1.5">
            <div>
              <div className="flex items-center gap-1.5 flex-wrap">
                <span className="font-mono font-semibold text-slate-900 text-xs">
                  {record.declarationNo}
                </span>
                {channelTag}
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
      width: 150,
      fixed: 'right',
      render: (_, record) => {
        if (isKeToan) {
          return (
            <Space size={4}>
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
        }

        const isLocked = Boolean(record.isLocked || record.status === ShipmentStatus.Cleared);

        const moreItems = [
          {
            key: 'open',
            icon: <FolderOpenOutlined />,
            label: isLocked ? 'Chỉnh sửa (đã khóa)' : 'Mở lại để chỉnh sửa',
            disabled: isLocked && !canUnlockCleared,
            onClick: () => isLocked ? handleUnlockClearedShipment(record) : handleLoadHistoricalOrder(record.id),
          },
          {
            key: 'download',
            icon: <DownloadOutlined />,
            label: 'Tải file Excel',
            onClick: () => handleDownloadHistorical(record.id, record.invoiceNo),
          },
          ...(record.customsAttachmentFileName ? [{
            key: 'declaration',
            icon: <FileExcelOutlined />,
            label: 'Tải tờ khai gốc',
            onClick: () => handleDownloadCustomsAttachment(record.id),
          }] : []),
          {
            key: 'audit',
            icon: <HistoryOutlined />,
            label: 'Xem nhật ký',
            onClick: () => {
              setAuditModalOrderId(record.id);
              setAuditModalInvoiceNo(record.invoiceNo);
              setAuditModalOpen(true);
            },
          },
          ...(!isLocked && !isKeToan ? [
            { type: 'divider' as const },
            {
              key: 'delete',
              danger: true,
              icon: <DeleteOutlined />,
              label: 'Xóa chứng từ',
              onClick: () => Modal.confirm({
                title: 'Xóa chứng từ?',
                content: `Bạn có chắc muốn xóa chứng từ "${record.invoiceNo}" không? Hành động này không thể hoàn tác.`,
                okText: 'Xóa',
                okType: 'danger',
                cancelText: 'Hủy',
                onOk: async () => {
                  try {
                    await shipmentApi.deleteShipment(record.id);
                    message.success(`Đã xóa đơn hàng ${record.invoiceNo}`);
                    await loadShipmentsHistory();
                  } catch {
                    message.error('Không thể xóa đơn hàng.');
                  }
                },
              }),
            },
          ] : []),
        ];

        return (
          <Space size={6}>
            <Tooltip title="Đối soát tờ khai hải quan VNACCS cho hóa đơn này">
              <Button
                size="small"
                icon={<AuditOutlined className="text-xs text-blue-600" />}
                className="text-xs border-blue-200 text-blue-700 bg-blue-50/50 hover:bg-blue-50 font-medium"
                onClick={() => handleOpenCustomsSync(record)}
              >
                Đối soát
              </Button>
            </Tooltip>
            <Dropdown menu={{ items: moreItems }} trigger={['click']} placement="bottomRight">
              <Button size="small" icon={<EllipsisOutlined />} aria-label="Thao tác khác" />
            </Dropdown>
          </Space>
        );
      },
    },
  ];

  return (
    <div className="enterprise-page">
      {activeNavTab === 'history' || activeTab === 'history' || isKeToan ? (
        /* ================= MÀN HÌNH LỊCH SỬ CHỨNG TỪ ================= */
        <div className="space-y-6">
          {/* Header */}
          <div className="enterprise-page-header">
            <div className="min-w-0 flex-1">
              <h1 className="enterprise-page-title">
                Lịch sử chứng từ xuất hàng
              </h1>
              <p className="enterprise-page-description">
                Danh sách hóa đơn Commercial Invoice & Packing List đã lập ({savedShipments.length} chứng từ)
              </p>
            </div>

            <div className="enterprise-actions">
              <Button
                icon={<ReloadOutlined />}
                onClick={loadShipmentsHistory}
                loading={loadingHistory}
                className="text-xs h-8 px-3 border-[#D1D5DB] text-[#374151] hover:text-[#111827] bg-white hover:bg-[#F9FAFB] font-medium shadow-xs"
              >
                Làm mới
              </Button>
              {!isKeToan && (
                <Button
                  type="primary"
                  icon={<PlusOutlined />}
                  onClick={() => {
                    setActiveTab('create');
                    onTabChange?.('shipment');
                  }}
                  className="bg-[#2563EB] hover:bg-[#1D4ED8] text-white text-xs h-8 px-3.5 font-medium shadow-xs"
                >
                  Lập hóa đơn mới
                </Button>
              )}
            </div>
          </div>

          {/* Main Layout: Customs Archive Tree on Left + Declarations List on Right */}
          <div className="bg-white border border-slate-200 rounded-lg shadow-xs overflow-hidden flex flex-col lg:flex-row min-h-[600px] min-w-0">
            <CustomsArchiveTreePanel
              shipments={savedShipments}
              folders={partnerFolders}
              selectedKey={customsTreeKey}
              onSelectNode={(key, filter) => {
                setCustomsTreeKey(key);
                setCustomsTreeFilter(filter);
              }}
            />

            <div className="flex-1 p-3 sm:p-4 space-y-3 overflow-hidden min-w-0">
              {selectedOrderIds.length > 0 ? (
                <div className="flex min-h-12 min-w-0 flex-wrap items-center gap-2 border border-blue-200 bg-blue-50/60 px-3 py-2">
                  <span className="mr-1 text-sm font-medium text-slate-700">
                    <span className="text-blue-700">{selectedOrderIds.length}</span> hồ sơ đã chọn
                    {currentFolderName ? <span className="hidden 2xl:inline text-slate-500"> · {currentFolderName}</span> : null}
                  </span>

                  {selectedOrderIds.length === 1 && (
                    <Tooltip title="Đối soát tờ khai hải quan">
                      <Button
                        icon={<FileSearchOutlined />}
                        onClick={() => {
                          const selectedOrder = savedShipments.find((s) => s.id === selectedOrderIds[0]);
                          if (selectedOrder) handleOpenCustomsSync(selectedOrder);
                        }}
                      >
                        Đối soát
                      </Button>
                    </Tooltip>
                  )}

                  <Dropdown.Button
                    type="primary"
                    icon={<DownOutlined />}
                    loading={batchPrinting}
                    onClick={() => handleBatchPrint('ALL')}
                    menu={{
                      items: [
                        { key: 'print-inv', label: 'In INV', onClick: () => handleBatchPrint('INV') },
                        { key: 'print-pkl', label: 'In PKL', onClick: () => handleBatchPrint('PKL') },
                      ],
                    }}
                  >
                    In INV + PKL
                  </Dropdown.Button>

                  <Button type="text" icon={<CloseOutlined />} onClick={handleClearSelection}>
                    Bỏ chọn
                  </Button>
                </div>
              ) : (
              <div className="grid grid-cols-1 xl:grid-cols-[minmax(240px,360px)_minmax(0,1fr)] items-center gap-3 min-w-0">
                <div className="w-full min-w-0">
                  <Input
                    placeholder="Tìm theo số Invoice, tên khách hàng hoặc hợp đồng..."
                    prefix={<SearchOutlined className="text-[#9CA3AF] text-xs mr-1" />}
                    value={historySearchText}
                    onChange={(e) => setHistorySearchText(e.target.value)}
                    allowClear
                    size="middle"
                    className="text-xs"
                  />
                </div>
                {customsTreeKey !== 'all' && (
                  <div className="flex min-w-0 items-center gap-2 xl:justify-end">
                    <Tag
                      color="blue"
                      closable
                      onClose={() => {
                        setCustomsTreeKey('all');
                        setCustomsTreeFilter({});
                      }}
                      className="text-xs py-1 px-2.5 font-medium flex min-w-0 max-w-full items-center gap-1 border-blue-200 text-blue-800 bg-blue-50 m-0"
                    >
                      <span className="min-w-0 truncate" title={customsTreeFilter.breadcrumb || customsTreeKey}>
                        Đang lọc: <strong>{customsTreeFilter.breadcrumb || customsTreeKey}</strong>
                      </span>
                    </Tag>
                  </div>
                )}
              </div>
              )}

              <div className="w-full overflow-x-auto min-w-0">
                <Table
                  dataSource={filteredHistory}
                  columns={historyColumns}
                  rowKey="id"
                  rowSelection={{
                    selectedRowKeys: selectedOrderIds,
                    onChange: (keys) => setSelectedOrderIds(keys as number[]),
                  }}
                  loading={loadingHistory}
                  pagination={{
                    pageSize: 15,
                    showSizeChanger: true,
                    pageSizeOptions: ['15', '30', '50', '100'],
                    showTotal: (total, range) => (
                      <span className="text-xs text-[#6B7280]">
                        {range[0]}-{range[1]} / {total} chứng từ
                      </span>
                    ),
                  }}
                  size="middle"
                  scroll={{ x: 1280 }}
                />
              </div>
            </div>
          </div>

        </div>
      ) : (
        /* ================= MÀN HÌNH LẬP HÓA ĐƠN & PACKING LIST ================= */
        <div className="space-y-6">
          {/* Header */}
          <div className="enterprise-page-header">
            <div className="min-w-0 flex-1">
              <h1 className="enterprise-page-title">
                Lập Invoice & Packing List (INV & PKL)
              </h1>
              <p className="enterprise-page-description">
                Nhập số liệu phiếu kho, đối soát tổng số đôi và xuất file Excel đa sheet chuẩn mẫu nhà máy
              </p>
            </div>

            <div className="enterprise-actions">
              <Button
                icon={<HistoryOutlined />}
                onClick={() => {
                  setActiveTab('history');
                  onTabChange?.('history');
                  loadShipmentsHistory();
                }}
                className="hidden xl:inline-flex text-xs h-8 px-3 border-[#D1D5DB] text-[#374151] hover:text-[#111827] bg-white hover:bg-[#F9FAFB] font-medium shadow-xs"
              >
                Lịch sử ({savedShipments.length})
              </Button>
              <Button
                icon={<EyeOutlined className="text-emerald-600" />}
                loading={loadingDocumentPreview}
                onClick={handlePreviewDocument}
                className="text-xs h-8 px-3 border-emerald-300 text-emerald-700 hover:text-emerald-800 bg-emerald-50/50 hover:bg-emerald-50 font-medium shadow-xs"
              >
                Xem trước Hóa đơn
              </Button>
              <Button
                icon={<EyeOutlined />}
                loading={loadingPreview}
                onClick={handlePreviewPkl}
                className="text-xs h-8 px-3 border-[#D1D5DB] text-[#374151] hover:text-[#111827] bg-white hover:bg-[#F9FAFB] font-medium shadow-xs"
              >
                Xem trước PKL
              </Button>
              <Button
                type={editingOrderId || readOnly ? 'primary' : 'default'}
                icon={<PlusOutlined />}
                onClick={handleResetToNewOrder}
                className={`hidden xl:inline-flex text-xs h-8 px-3 font-medium shadow-xs ${editingOrderId || readOnly
                  ? 'bg-[#2563EB] hover:bg-[#1D4ED8] text-white'
                  : 'border-[#D1D5DB] text-[#374151] hover:text-[#111827] bg-white hover:bg-[#F9FAFB]'
                  }`}
              >
                Tạo đơn mới
              </Button>
              <Button
                icon={<SaveOutlined />}
                loading={saving}
                disabled={readOnly || saving || exporting}
                onClick={handleSaveShipment}
                className="text-xs h-8 px-3 border-[#D1D5DB] text-[#374151] hover:text-[#111827] bg-white hover:bg-[#F9FAFB] font-medium shadow-xs"
              >
                {editingOrderId ? 'Cập nhật đơn' : 'Lưu đơn hàng'}
              </Button>

              {/* Overflow dropdown on compact/laptop screens */}
              <Dropdown
                menu={{
                  items: [
                    {
                      key: 'history-item',
                      icon: <HistoryOutlined />,
                      label: `Lịch sử chứng từ (${savedShipments.length})`,
                      onClick: () => {
                        setActiveTab('history');
                        onTabChange?.('history');
                        loadShipmentsHistory();
                      },
                    },
                    {
                      key: 'new-order-item',
                      icon: <PlusOutlined />,
                      label: 'Tạo đơn mới',
                      onClick: handleResetToNewOrder,
                    },
                  ],
                }}
                trigger={['click']}
              >
                <Button
                  icon={<EllipsisOutlined />}
                  className="xl:hidden text-xs h-8 px-2.5 border-[#D1D5DB] text-[#374151] hover:text-[#111827] bg-white hover:bg-[#F9FAFB] font-medium shadow-xs"
                >
                  Khác
                </Button>
              </Dropdown>

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
                          ? 'bg-[#E5E7EB] text-[#9CA3AF] border-[#E5E7EB] cursor-not-allowed text-xs h-8 px-3.5 font-medium'
                          : 'bg-[#2563EB] hover:bg-[#1D4ED8] text-white text-xs h-8 px-3.5 font-medium shadow-xs'
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
          <div id="tour-invoice-header" className="bg-white border border-[#E5E7EB] rounded-lg p-5 space-y-4 shadow-[0_1px_2px_rgba(0,0,0,0.05)]">
            <div className="flex items-center justify-between pb-3 border-b border-[#F3F4F6]">
              <div className="text-xs font-semibold text-[#111827] uppercase tracking-wider">
                1. Thông tin Chứng từ (Shipment Header)
              </div>
              <div className="flex items-center gap-2">


              </div>
            </div>

            {/* Bộ Chuyển Đổi Không Gian Làm Việc Theo Đối Tác (Partner Profile Presets) */}
            <div className="bg-[#F9FAFB] border border-[#E5E7EB] rounded-lg p-3.5 flex flex-col md:flex-row md:items-center justify-between gap-3 shadow-[0_1px_2px_rgba(0,0,0,0.05)]">
              <div className="flex items-center gap-3 min-w-0 flex-1">
                <div className="w-9 h-9 rounded-md bg-[#172033] text-white flex items-center justify-center text-sm shadow-xs shrink-0">
                  <ShopOutlined />
                </div>
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-xs font-semibold text-[#111827] uppercase tracking-wide truncate">
                      Hồ sơ Đối tác / Khách hàng (Partner Workspace)
                    </span>

                  </div>
                  <div className="text-[11px] text-[#6B7280] mt-0.5 truncate sm:whitespace-normal">
                    Tự động điền thông tin hợp đồng, địa chỉ giao hàng và áp dụng quy cách đóng thùng chuẩn của đối tác
                  </div>
                </div>
              </div>

              <div className="flex flex-col md:flex-row items-stretch md:items-center gap-3 w-full md:w-auto shrink-0">
                <div className="flex items-center gap-2 w-full md:w-72">
                  <span className="text-xs font-medium text-[#4B5563] whitespace-nowrap shrink-0">Đối tác:</span>
                  <Select
                    disabled={readOnly}
                    value={selectedPartnerId}
                    onChange={handlePartnerSelect}
                    className="w-full text-xs font-medium min-w-0"
                    size="middle"
                    placeholder="-- Chọn Đối tác --"
                    options={partnerFolders.map((pf) => ({
                      value: pf.id,
                      label: (
                        <div className="flex items-center justify-between">
                          <span className="font-semibold text-[#111827] truncate">{pf.name}</span>
                          <span className="text-[11px] text-[#9CA3AF] font-mono ml-2 shrink-0">
                            ({pf.defaultPairsPerCarton} đôi/thùng)
                          </span>
                        </div>
                      ),
                    }))}
                  />
                </div>

                <div className="flex items-center gap-1.5 w-full md:w-72">
                  <span className="text-xs font-medium text-[#4B5563] whitespace-nowrap shrink-0">Biểu mẫu:</span>
                  <Select
                    disabled={readOnly}
                    value={selectedTemplateId ?? undefined}
                    onChange={(val) => setSelectedTemplateId(val)}
                    className="w-full text-xs font-medium min-w-0"
                    size="middle"
                    placeholder="-- Chọn Biểu mẫu --"
                    options={templates.map((tpl) => ({
                      value: tpl.id,
                      label: (
                        <div className="flex items-center justify-between">
                          <span className="font-semibold text-[#111827] truncate">{tpl.name}</span>
                          {tpl.isDefault && (
                            <span className="text-[10px] text-emerald-700 bg-emerald-50 px-1 py-0 rounded border border-emerald-200 ml-1 shrink-0">
                              Mặc định
                            </span>
                          )}
                        </div>
                      ),
                    }))}
                  />
                  <Tooltip title="Quản lý & Tùy biến biểu mẫu xuất (BYOT)">
                    <Button
                      size="middle"
                      icon={<SettingOutlined />}
                      onClick={() => setTemplateModalVisible(true)}
                      className="shrink-0 text-slate-600 hover:text-blue-600"
                    />
                  </Tooltip>
                </div>
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
              <Row gutter={[16, 12]}>
                <Col xs={24} sm={12} md={12} lg={12} xl={6}>
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

                  </Form.Item>
                </Col>

                <Col xs={24} sm={12} md={12} lg={12} xl={6}>
                  <Form.Item
                    label={<span className="text-xs font-medium text-slate-700">Ngày Hóa đơn</span>}
                    name="invoiceDate"
                    rules={[{ required: true, message: 'Chọn ngày' }]}
                  >
                    <DatePicker format="DD/MM/YYYY" className="w-full text-xs" placeholder="Chọn ngày" />
                  </Form.Item>
                </Col>

                <Col xs={24} sm={12} md={12} lg={12} xl={6}>
                  <Form.Item
                    label={<span className="text-xs font-medium text-slate-700">Số Hợp đồng (Contract No)</span>}
                    name="contractNo"
                    rules={[{ required: true, message: 'Nhập số hợp đồng' }]}
                  >
                    <Input className="font-mono text-xs" placeholder="KM-HANEW/01-2025" />
                  </Form.Item>
                </Col>

                <Col xs={24} sm={12} md={12} lg={12} xl={6}>
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

          {activeWarehouseBatchId && totalQuantity >= 5000 && (
            <div className="rounded-lg border border-blue-200 bg-blue-50/60 px-4 py-3 flex flex-wrap items-center justify-between gap-3">
              <div>
                <div className="text-sm font-semibold text-blue-900">Đợt hàng có số lượng lớn ({totalQuantity.toLocaleString()} đôi)</div>
                <div className="text-xs text-blue-700 mt-0.5">Bạn có thể cân nhắc tách thành nhiều hóa đơn. Đây chỉ là gợi ý thao tác, không phải quy tắc bắt buộc.</div>
              </div>
              <Button type="primary" icon={<NodeIndexOutlined />} onClick={() => setSplitMatrixVisible(true)}>Tách ngay</Button>
            </div>
          )}

          {/* Section 2: Đối soát & Tổng hợp Số liệu (Reconciliation Summary) */}
          <div id="tour-reconciliation-bar" className="bg-white border border-[#E5E7EB] rounded-lg p-4 shadow-[0_1px_2px_rgba(0,0,0,0.05)]">
            <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4 xl:gap-0 xl:divide-x divide-[#E5E7EB]">
              <div className="xl:pr-5 min-w-0">
                <div className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
                  Tổng số đôi phiếu kho
                </div>
                <div className="mt-1 flex items-baseline flex-wrap gap-2">
                  <span className="text-2xl font-semibold text-[#111827] font-mono">
                    {totalQuantity.toLocaleString()}
                  </span>
                  <span className="text-xs text-[#6B7280]">đôi</span>
                  <Tooltip title="Số lượng thực tế các dòng hàng đã khớp chuẩn với tổng số đôi của phiếu kho.">
                    <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-[#F0FDF4] text-[#15803D] border border-[#BBF7D0] cursor-help">
                      Khớp phiếu kho
                    </span>
                  </Tooltip>
                </div>
                <div className="mt-1 text-xs text-[#6B7280]">
                  {items.length} dòng hàng • {uniqueStyleCodesCount} mã hình thể
                </div>
              </div>

              <div className="xl:px-5 min-w-0">
                <div className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
                  Số kiện ước tính
                </div>
                <div className="mt-1 flex items-baseline space-x-1.5">
                  <span className="text-2xl font-semibold text-[#111827] font-mono">
                    {totalCartons.toLocaleString()}
                  </span>
                  <span className="text-xs text-[#6B7280]">thùng</span>
                </div>
                <div className="mt-1 text-xs text-[#6B7280]">
                  Dựa trên quy cách đóng gói
                </div>
              </div>

              <div className="xl:px-5 min-w-0">
                <div className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
                  Tổng tiền gia công CMT
                </div>
                <div className="mt-1 flex items-baseline space-x-1.5">
                  <span className="text-2xl font-semibold text-[#111827] font-mono">
                    ${totalAmountCMT.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                </div>
                <div className="mt-1 text-xs text-[#6B7280]">
                  Gia công CMT xuất khẩu
                </div>
              </div>

              <div className="xl:pl-5 min-w-0">
                <div className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
                  Tổng trị giá DAP
                </div>
                <div className="mt-1 flex items-baseline space-x-1.5">
                  <span className="text-2xl font-semibold text-[#2563EB] font-mono">
                    ${totalAmountDAP.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                </div>
                <div className="mt-1 text-xs text-[#6B7280]">
                  Thành tiền Commercial Invoice
                </div>
              </div>
            </div>
          </div>

          {/* Section 3: Lưới Nhập liệu Hàng hóa (Editable Table) */}
          <div className="bg-white border border-[#E5E7EB] rounded-lg p-5 space-y-4 shadow-[0_1px_2px_rgba(0,0,0,0.05)]">
            <div id="tour-data-import" className="flex flex-col md:flex-row md:items-center md:justify-between gap-3 pb-3 border-b border-[#F3F4F6]">
              <div className="text-xs font-semibold text-[#111827] uppercase tracking-wider min-w-0">
                2. Danh sách Hàng hóa ({items.length} dòng)
              </div>
              <div className="flex items-center flex-wrap gap-2 min-w-0">
                <Button
                  icon={<InboxOutlined />}
                  disabled={readOnly}
                  onClick={() => {
                    loadPendingWarehouseBatches();
                    setReceiveBatchModalVisible(true);
                  }}
                  className="text-xs h-8 px-3 border-[#D1D5DB] text-[#374151] hover:text-[#111827] bg-white hover:bg-[#F9FAFB] font-medium shadow-xs"
                >
                  Tiếp nhận từ Kho
                </Button>
                <Tooltip title={activeWarehouseBatchId ? 'Phân bổ đợt đang tiếp nhận thành 2–10 hóa đơn' : 'Hãy tiếp nhận một đợt hàng từ Kho trước'}>
                  <Button icon={<NodeIndexOutlined />} disabled={readOnly || !activeWarehouseBatchId || items.length === 0} onClick={() => setSplitMatrixVisible(true)} className="text-xs h-8 px-3 border-[#D1D5DB] text-[#374151] bg-white font-medium">
                    Tách Hóa Đơn
                  </Button>
                </Tooltip>
                <Button icon={<ApartmentOutlined />} disabled={readOnly} onClick={() => setBatchOcrModalVisible(true)} className="text-xs h-8 px-3 border-[#D1D5DB] text-[#374151] bg-white font-medium">
                  OCR Batch / Gom đợt
                </Button>
                <Tooltip title="Hỗ trợ dán trực tiếp danh sách mã và số lượng copy từ bảng tính Excel.">
                  <Button
                    icon={<ThunderboltOutlined />}
                    disabled={readOnly} onClick={() => setQuickPasteVisible(true)}
                    className="text-xs h-8 px-3 border-[#D1D5DB] text-[#374151] hover:text-[#111827] bg-white hover:bg-[#F9FAFB] font-medium shadow-xs"
                  >
                    Dán nhanh (Clipboard)
                  </Button>
                </Tooltip>
                <Button
                  icon={<CameraOutlined />}
                  disabled={readOnly} onClick={() => setOcrModalVisible(true)}
                  className="hidden sm:inline-flex text-xs h-8 px-3 border-[#D1D5DB] text-[#374151] hover:text-[#111827] bg-white hover:bg-[#F9FAFB] font-medium shadow-xs"
                >
                  Quét OCR Phiếu kho
                </Button>

                {/* Dropdown "Thao tác khác" gom các chức năng phụ */}
                <Dropdown
                  menu={{
                    items: [
                      {
                        key: 'ocr-mobile',
                        icon: <CameraOutlined />,
                        label: 'Quét OCR Phiếu kho',
                        disabled: readOnly,
                        className: 'sm:hidden',
                        onClick: () => setOcrModalVisible(true),
                      },
                      {
                        key: 'batch-ocr',
                        icon: <RocketOutlined />,
                        label: 'Quét OCR hàng loạt (Batch)',
                        disabled: readOnly,
                        onClick: () => setBatchOcrModalVisible(true),
                      },
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
                    className="text-xs h-8 px-2.5 border-[#D1D5DB] text-[#374151] hover:text-[#111827] bg-white hover:bg-[#F9FAFB] font-medium shadow-xs"
                  >
                    Thao tác khác
                  </Button>
                </Dropdown>

                <Button
                  type="primary"
                  icon={<PlusOutlined />}
                  disabled={readOnly} onClick={handleAddItem}
                  className="bg-[#2563EB] hover:bg-[#1D4ED8] text-white text-xs h-8 px-3.5 font-medium shadow-xs"
                >
                  Thêm dòng
                </Button>
              </div>
            </div>

            {/* Thanh Cảnh báo Thông minh (Smart Suggestion Banner) */}
            {suggestionBannerData && suggestionBannerData.hasMismatch && suggestionBannerData.suggestedPartnerFolderId && (
              <div className="p-4 rounded-lg border border-[#FDE68A] bg-[#FFFBEB] text-[#92400E] text-xs shadow-[0_1px_2px_rgba(0,0,0,0.05)] transition-all">
                <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
                  <div className="flex items-start gap-3">
                    <span className="text-lg shrink-0 leading-none mt-0.5">💡</span>
                    <div className="space-y-1">
                      <div className="font-semibold text-[#92400E] text-xs uppercase tracking-wider flex items-center gap-1.5">
                        <span>Phát hiện có thể nhầm lẫn Đối tác:</span>
                        <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-mono bg-[#FEF3C7] text-[#B45309] border border-[#FDE68A]">
                          {suggestionBannerData.matchedCountInSuggested}/{suggestionBannerData.totalCodes} mã trùng khớp
                        </span>
                      </div>
                      <div className="text-[#92400E] leading-relaxed text-xs">
                        Hệ thống nhận thấy <strong>{suggestionBannerData.matchedCountInSuggested}/{suggestionBannerData.totalCodes}</strong> mã bạn vừa nhập thuộc danh mục của [<strong>{suggestionBannerData.suggestedPartnerName}</strong>].
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
                      className="bg-[#2563EB] hover:bg-[#1D4ED8] text-white font-medium text-xs h-8 px-3 shadow-xs"
                    >
                      Chuyển sang {suggestionBannerData.suggestedPartnerName} & Áp dụng mẫu
                    </Button>
                    <Button
                      onClick={handleKeepCurrentPartner}
                      className="text-xs h-8 px-3 text-[#374151] hover:text-[#111827] border-[#D1D5DB] bg-white hover:bg-[#F9FAFB] shadow-xs"
                    >
                      Giữ nguyên
                    </Button>
                  </div>
                </div>
              </div>
            )}

            {hasMissingMasterData && (
              <div className="flex items-center justify-between p-3 rounded-lg border border-[#FCA5A5] bg-[#FEF2F2] text-[#991B1B] text-xs shadow-[0_1px_2px_rgba(0,0,0,0.05)]">
                <div className="flex items-center gap-2.5">
                  <ExclamationCircleOutlined className="text-[#DC2626] text-base shrink-0" />
                  <span>
                    <strong>Chốt chặn nghiệp vụ (Business Rule Validation Guard):</strong> Phát hiện{' '}
                    <strong className="text-[#991B1B]">{missingMasterCodes.length}</strong> dòng hàng chứa mã chưa có
                    trong Master Data (<span className="font-mono font-semibold text-[#991B1B]">{missingMasterCodes.join(', ')}</span>
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
        partnerFolders={partnerFolders}
        onPartnerChange={(newPartnerId) => setSelectedPartnerId(newPartnerId)}
        onApply={handleApplyOcr}
        templateId={selectedTemplateId}
        poSuffix={form.getFieldValue('poSuffix') || ''}
        invoiceDate={dayjs(form.getFieldValue('invoiceDate') || undefined).format('YYYY-MM-DD')}
        onDispatchExported={() => { void loadShipmentsHistory(); void loadSequence(); }}
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
                  → <span className="font-mono text-blue-600">{formatInvoiceNo(selectedPartner?.invoiceNoPattern, sequenceEditValue)}</span>
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
              <span className="font-mono text-[11px] text-slate-500">
                ZIP: {previewFile1.fileName.replace(/\.xlsx$/i, '')}-{secondSeqNum}.zip
              </span>
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

      <SplitMatrixModal
        open={splitMatrixVisible}
        sourceBatchId={activeWarehouseBatchId}
        contractFolderId={selectedPartnerId}
        templateId={selectedTemplateId}
        poSuffix={form.getFieldValue('poSuffix') || ''}
        invoiceDate={dayjs(form.getFieldValue('invoiceDate') || undefined).format('YYYY-MM-DD')}
        onClose={() => setSplitMatrixVisible(false)}
        onExported={() => {
          setActiveWarehouseBatchId(null);
          setItems([]);
          void loadPendingWarehouseBatches();
          void loadShipmentsHistory();
        }}
      />

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

      {/* Modal Phân bổ Size (Size Breakdown Guard) */}
      <SizeBreakdownModal
        open={sizeModalOpen}
        onClose={() => setSizeModalOpen(false)}
        itemIndex={sizeModalIndex}
        itemStyleCode={sizeModalItem?.styleCode || ''}
        itemDescription={sizeModalItem?.description || ''}
        targetQuantity={sizeModalItem?.quantity || 0}
        initialJson={sizeModalItem?.sizeBreakdownJson}
        onSave={handleSaveSizeBreakdown}
      />

      {/* Modal Lịch sử mở khóa thông quan AMA/AMC (Unlock Audits) */}
      <UnlockAuditsModal
        open={auditModalOpen}
        onClose={() => setAuditModalOpen(false)}
        orderId={auditModalOrderId}
        invoiceNo={auditModalInvoiceNo}
      />

      {/* Modal Xem trước Hóa đơn & PKL (Live Preview & Web Direct Print) */}
      <DocumentPreviewModal
        visible={documentPreviewVisible}
        onClose={() => setDocumentPreviewVisible(false)}
        data={documentPreviewData}
        onExportExcel={handleExportExcel}
        exporting={exporting}
        disableExport={readOnly || exporting}
      />

      {/* Modal Quản lý Biểu mẫu & Tùy biến Tọa độ (BYOT Mapper) */}
      <TemplateConfigModal
        visible={templateModalVisible}
        onClose={() => setTemplateModalVisible(false)}
        onTemplateUpdated={(updatedList, newSelectedId) => {
          setTemplates(updatedList);
          if (newSelectedId) {
            setSelectedTemplateId(newSelectedId);
          } else {
            const def = updatedList.find((t) => t.isDefault) || updatedList[0];
            if (def) setSelectedTemplateId(def.id);
          }
        }}
        selectedTemplateId={selectedTemplateId}
      />
    </div>
  );
});

ShipmentPage.displayName = 'ShipmentPage';
