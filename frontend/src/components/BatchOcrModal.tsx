import React, { useState, useEffect, useRef, useCallback } from 'react';
import {
  Modal,
  Button,
  Table,
  Tag,
  Input,
  InputNumber,
  Select,
  AutoComplete,
  Progress,
  message,
  Popconfirm,
  Card,
} from 'antd';
import {
  InboxOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  ExclamationCircleOutlined,
  DeleteOutlined,
  EditOutlined,
  FileZipOutlined,
  FileImageOutlined,
  ClearOutlined,
  RocketOutlined,
  SnippetsOutlined,
} from '@ant-design/icons';
import { ocrApi } from '../api/ocrApi';
import type {
  BatchOcrImageResult,
  OcrDetectedDocument,
  BatchOcrConfirmRequest,
  BatchScanItemExport,
  OcrItem,
  ProductMaster,
  MasterDataFolder,
  CreateShipmentItem,
} from '../types';
import { ProcessType, ExportSequencePriority, normalizeProcessType } from '../types';
import { normalizeOcrStyleCode } from '../utils/normalizeOcrStyleCode';

interface BatchOcrModalProps {
  contractFolderId?: number | null;
  profile?: MasterDataFolder | null;
  visible: boolean;
  onClose: () => void;
  products: ProductMaster[];
  onSuccess: () => void;
}

interface ImageQueueItem {
  id: string;
  file: File;
  previewUrl: string;
  status: 'pending' | 'processing' | 'done' | 'error';
  errorMessage?: string;
  result?: BatchOcrImageResult;
}

export const BatchOcrModal: React.FC<BatchOcrModalProps> = ({
  contractFolderId,
  profile,
  visible,
  onClose,
  products,
  onSuccess,
}) => {
  // Image queue
  const [imageQueue, setImageQueue] = useState<ImageQueueItem[]>([]);
  const [isProcessing, setIsProcessing] = useState<boolean>(false);
  const [isExporting, setIsExporting] = useState<boolean>(false);

  // Edit Card Modal state
  const [editingCard, setEditingCard] = useState<ImageQueueItem | null>(null);
  const [editingDocumentId, setEditingDocumentId] = useState<string | null>(null);
  const [editTitle, setEditTitle] = useState<string>('');
  const [editItems, setEditItems] = useState<OcrItem[]>([]);
  const [editReportedTotal, setEditReportedTotal] = useState<number | null>(null);

  // General export settings
  const [contractNo, setContractNo] = useState<string>('KM-HANEW/01-2025');
  const [poSuffix, setPoSuffix] = useState<string>('(KM3.PO5.26)');
  const [customerName, setCustomerName] = useState<string>(
    'CÔNG TY TNHH KINGMAKER III (VIỆT NAM) FOOTWEAR'
  );
  const [batchPriority, setBatchPriority] = useState<ExportSequencePriority>(
    ExportSequencePriority.StandardFirst
  );

  const fileInputRef = useRef<HTMLInputElement>(null);

  // AutoComplete options from master products
  const productOptions = React.useMemo(() => {
    return (products || []).filter(p => p.folderId === contractFolderId).map((p) => ({
      value: p.styleCode,
      label: `${p.styleCode} - ${p.description}`,
    }));
  }, [products, contractFolderId]);

  // Clean up object URLs on unmount
  useEffect(() => {
    return () => {
      imageQueue.forEach((item) => {
        if (item.previewUrl && item.previewUrl.startsWith('blob:')) {
          URL.revokeObjectURL(item.previewUrl);
        }
      });
    };
  }, [imageQueue]);

  // Add files to queue helper
  const addFilesToQueue = useCallback((newFiles: File[]) => {
    const validFiles = newFiles.filter((f) => {
      const ext = f.name.toLowerCase();
      return (
        f.type.startsWith('image/') ||
        ext.endsWith('.png') ||
        ext.endsWith('.jpg') ||
        ext.endsWith('.jpeg') ||
        ext.endsWith('.webp')
      );
    });

    if (validFiles.length === 0) {
      message.warning('Vui lòng chọn các file ảnh định dạng PNG, JPG, WEBP.');
      return;
    }

    const newQueueItems: ImageQueueItem[] = validFiles.map((file) => ({
      id: globalThis.crypto?.randomUUID?.() || `${Date.now()}_${Math.random().toString(36).substring(2, 9)}`,
      file,
      previewUrl: URL.createObjectURL(file),
      status: 'pending',
    }));

    setImageQueue((prev) => [...prev, ...newQueueItems]);
    message.success(`Đã nạp thêm ${newQueueItems.length} ảnh vào hàng đợi.`);
  }, []);

  // Global Paste (Ctrl+V) listener for pasting screenshots from Zalo / Clipboard
  useEffect(() => {
    if (!visible) return;

    const handlePaste = (e: ClipboardEvent) => {
      const items = e.clipboardData?.items;
      if (!items) return;

      const pastedFiles: File[] = [];
      for (let i = 0; i < items.length; i++) {
        const item = items[i];
        if (item.type.indexOf('image') !== -1) {
          const blob = item.getAsFile();
          if (blob) {
            const fileName = `Zalo_Screenshot_${Date.now()}_${i + 1}.png`;
            const file = new File([blob], fileName, { type: blob.type });
            pastedFiles.push(file);
          }
        }
      }

      if (pastedFiles.length > 0) {
        addFilesToQueue(pastedFiles);
      }
    };

    window.addEventListener('paste', handlePaste);
    return () => {
      window.removeEventListener('paste', handlePaste);
    };
  }, [visible, addFilesToQueue]);

  // File input change handler
  const handleFileInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      addFilesToQueue(Array.from(e.target.files));
      e.target.value = '';
    }
  };

  // Drag & drop handlers
  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      addFilesToQueue(Array.from(e.dataTransfer.files));
    }
  };

  const handleDragOver = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
  };

  // Remove single image from queue
  const handleRemoveItem = (id: string) => {
    setImageQueue((prev) => {
      const target = prev.find((item) => item.id === id);
      if (target && target.previewUrl.startsWith('blob:')) {
        URL.revokeObjectURL(target.previewUrl);
      }
      return prev.filter((item) => item.id !== id);
    });
  };

  // Clear all images
  const handleClearAll = () => {
    imageQueue.forEach((item) => {
      if (item.previewUrl.startsWith('blob:')) {
        URL.revokeObjectURL(item.previewUrl);
      }
    });
    setImageQueue([]);
  };

  // Start Batch Extraction
  const handleStartBatchExtraction = async () => {
    const pendingItems = imageQueue.filter((i) => i.status === 'pending' || i.status === 'error');
    if (pendingItems.length === 0) {
      message.info('Không có ảnh nào trong hàng đợi cần bóc tách.');
      return;
    }

    setIsProcessing(true);

    // Mark pending as processing
    setImageQueue((prev) =>
      prev.map((item) =>
        item.status === 'pending' || item.status === 'error'
          ? { ...item, status: 'processing' }
          : item
      )
    );

    try {
      const scanResults = await ocrApi.batchExtract(pendingItems.map(item => ({ clientFileId: item.id, file: item.file })));

      // Match each result back to imageQueue item by fileName or position
      setImageQueue((prev) =>
        prev.map((item) => {
          if (item.status !== 'processing') return item;

          const matchedRes = scanResults.find((r) => r.clientFileId === item.id);

          if (matchedRes && matchedRes.isSuccess) {
            return {
              ...item,
              status: 'done',
              result: {
                ...matchedRes,
                documents: matchedRes.documents.map(d => ({ ...d, items: d.items.map(it => ({ ...it, processType: normalizeProcessType(it.processType) })) })),
              },
            };
          } else {
            return {
              ...item,
              status: 'error',
              errorMessage: matchedRes?.errorMessage || 'Không tìm thấy kết quả OCR tương ứng với ảnh nguồn.',
              result: matchedRes,
            };
          }
        })
      );

      message.success('Đã hoàn thành bóc tách hàng loạt ảnh phiếu kho.');
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Có lỗi xảy ra trong quá trình bóc tách ảnh.');
      setImageQueue((prev) =>
        prev.map((item) =>
          item.status === 'processing'
            ? { ...item, status: 'error', errorMessage: 'Lỗi bóc tách.' }
            : item
        )
      );
    } finally {
      setIsProcessing(false);
    }
  };

  // Open Edit Card Modal
  const handleOpenEditCard = (item: ImageQueueItem, document: OcrDetectedDocument) => {
    setEditingCard(item);
    setEditingDocumentId(document.documentId);
    setEditTitle(document.title);
    setEditReportedTotal(document.reportedTotal);
    setEditItems([...document.items]);
  };

  // Save changes from Edit Card Modal
  const handleSaveEditCard = () => {
    if (!editingCard || !editingCard.result) return;

    const cleanedItems = editItems.map((i) => ({
      ...i,
      processType: normalizeProcessType(i.processType),
      styleCode: normalizeOcrStyleCode(i.styleCode),
    }));
    const newCalculatedTotal = cleanedItems.reduce((acc, i) => acc + (i.quantity || 0), 0);
    const updatedDocument: OcrDetectedDocument = {
      ...editingCard.result.documents.find(d => d.documentId === editingDocumentId)!,
      title: editTitle.trim() || 'Đợt hàng',
      reportedTotal: editReportedTotal,
      calculatedTotal: newCalculatedTotal,
      hasReportedTotal: editReportedTotal !== null,
      isTotalMatched: editReportedTotal !== null && editReportedTotal === newCalculatedTotal,
      discrepancy: editReportedTotal === null ? null : newCalculatedTotal - editReportedTotal,
      items: cleanedItems,
      hasStandardItems: cleanedItems.some((i) => normalizeProcessType(i.processType) === ProcessType.Standard),
      hasGoItems: cleanedItems.some((i) => normalizeProcessType(i.processType) === ProcessType.GoKhongMay),
    };

    setImageQueue((prev) =>
      prev.map((item) => item.id === editingCard.id && item.result ? { ...item, result: { ...item.result, documents: item.result.documents.map(d => d.documentId === editingDocumentId ? updatedDocument : d) } } : item)
    );

    setEditingCard(null);
    message.success('Đã cập nhật chi tiết đợt thành công.');
  };

  // Handle export all completed batches to single ZIP
  const handleExportZip = async () => {
    const doneItems = imageQueue.filter((item) => item.status === 'done' && item.result && item.result.documents.some(d => d.items.length > 0));

    if (doneItems.length === 0) {
      message.warning('Chưa có đợt nào hoàn thành bóc tách hợp lệ để xuất Excel.');
      return;
    }

    try {
      setIsExporting(true);

      const batchesPayload: BatchScanItemExport[] = doneItems.flatMap((dItem) => dItem.result!.documents.filter(d => d.items.length).map(doc => {
        const shipmentItems: CreateShipmentItem[] = doc.items.map((i) => ({
          styleCode: i.styleCode,
          description: i.description,
          quantity: i.quantity,
          processType: normalizeProcessType(i.processType),
          unitPriceCMT: i.unitPriceCMT,
          unitPriceDAP: i.unitPriceDAP,
          unit: i.unit || 'đôi',
          pairPerCarton: i.pairPerCarton || 12,
        }));

        return {
          batchId: doc.documentId,
          title: doc.title,
          items: shipmentItems,
        };
      }));

      const requestPayload: BatchOcrConfirmRequest = {
        contractFolderId,
        contractNo: profile?.contractNo || contractNo.trim(),
        address: profile?.deliveryAddress || "",
        poSuffix: profile?.poSuffix || poSuffix.trim(),
        customerName: profile?.customerName || customerName.trim(),
        priority: batchPriority,
        batches: batchesPayload,
      };

      const zipBlob = await ocrApi.batchExportZip(requestPayload);
      const url = window.URL.createObjectURL(zipBlob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `KM3-Batch-OCR-Export-${new Date().toISOString().slice(0, 10)}.zip`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);

      message.success(`Đã xuất thành công gói ZIP chứa ${batchesPayload.length} đợt giao hàng.`);
      onSuccess();
      onClose();
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Không thể xuất gói file ZIP.');
    } finally {
      setIsExporting(false);
    }
  };

  // Calculation metrics
  const totalQueued = imageQueue.length;
  const totalDone = imageQueue.filter((i) => i.status === 'done').length;
  const totalError = imageQueue.filter((i) => i.status === 'error').length;
  const totalPending = imageQueue.filter((i) => i.status === 'pending').length;

  const totalPairs = imageQueue.reduce((acc, item) => {
    return acc + (item.result ? item.result.documents.reduce((sum, d) => sum + d.items.reduce((s, i) => s + i.quantity, 0), 0) : 0);
  }, 0);
  const totalDocuments = imageQueue.reduce((acc, item) => acc + (item.result?.documents.length || 0), 0);

  return (
    <Modal
      title={
        <div className="flex items-center gap-2">
          <RocketOutlined className="text-blue-600 text-lg" />
          <span className="text-base font-semibold text-slate-900">
            Quét ảnh phiếu kho hàng loạt theo lô (Batch Upload / Multi-Scan OCR)
          </span>
          <Tag className="bg-blue-50 text-blue-700 border-blue-200 text-xs font-normal">
            Vision AI Batch
          </Tag>
        </div>
      }
      open={visible}
      onCancel={onClose}
      width={1150}
      footer={null}
      destroyOnClose
    >
      <div className="space-y-4 my-2">
        {/* Dropzone & Paste Area */}
        <div
          onDrop={handleDrop}
          onDragOver={handleDragOver}
          onClick={() => fileInputRef.current?.click()}
          className="border-2 border-dashed border-slate-300 hover:border-blue-500 rounded-xl p-6 text-center cursor-pointer transition-colors bg-slate-50/60 hover:bg-blue-50/30"
        >
          <input
            ref={fileInputRef}
            type="file"
            multiple
            accept="image/png,image/jpeg,image/webp"
            className="hidden"
            onChange={handleFileInputChange}
          />
          <InboxOutlined className="text-4xl text-blue-500 mb-2" />
          <h3 className="text-sm font-semibold text-slate-800 m-0">
            Kéo thả hoặc Bấm để chọn nhiều ảnh phiếu kho cùng lúc
          </h3>
          <p className="text-xs text-slate-500 mt-1 mb-2">
            Hỗ trợ chọn từ 3 đến 10 ảnh (PNG, JPG, WEBP) hoặc{' '}
            <strong className="text-blue-600 font-medium">bấm chụp màn hình Zalo rồi dán Ctrl+V liên tiếp</strong>{' '}
            vào màn hình này
          </p>
          <div className="inline-flex items-center gap-2 px-3 py-1 bg-white border border-slate-200 rounded-full text-xs text-slate-600 shadow-sm">
            <SnippetsOutlined className="text-slate-400" />
            <span>Đã nạp: <strong className="text-slate-900">{totalQueued} ảnh</strong></span>
          </div>
        </div>

        {/* Toolbar & Progress */}
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 bg-white p-3 border border-slate-200 rounded-lg">
          <div className="flex items-center gap-3">
            <Button
              type="primary"
              icon={<RocketOutlined />}
              onClick={handleStartBatchExtraction}
              loading={isProcessing}
              disabled={totalQueued === 0 || totalPending === 0}
              className="bg-blue-600 hover:bg-blue-700 text-xs h-8 px-4"
            >
              Bắt đầu bóc tách ({totalPending} ảnh chờ)
            </Button>

            {totalQueued > 0 && (
              <Popconfirm
                title="Xác nhận xóa tất cả ảnh trong hàng đợi?"
                onConfirm={handleClearAll}
                okText="Xóa hết"
                cancelText="Hủy"
              >
                <Button
                  danger
                  icon={<ClearOutlined />}
                  size="small"
                  className="text-xs"
                  disabled={isProcessing}
                >
                  Xóa tất cả
                </Button>
              </Popconfirm>
            )}
          </div>

          <div className="flex items-center gap-4 text-xs">
            <div className="flex items-center gap-1.5">
              <span className="text-slate-500">Đã bóc tách:</span>
              <span className="font-mono font-semibold text-emerald-600">{totalDone}</span>
            </div>
            {totalError > 0 && (
              <div className="flex items-center gap-1.5">
                <span className="text-slate-500">Lỗi:</span>
                <span className="font-mono font-semibold text-rose-600">{totalError}</span>
              </div>
            )}
            <div className="flex items-center gap-1.5">
              <span className="text-slate-500">Tổng sản lượng:</span>
              <span className="font-mono font-semibold text-blue-600">
                {totalPairs.toLocaleString()} đôi
              </span>
            </div>
            <div className="flex items-center gap-1.5">
              <span className="text-slate-500">Đợt hàng:</span>
              <span className="font-mono font-semibold text-violet-600">{totalDocuments}</span>
            </div>
          </div>
        </div>

        {/* Progress Bar when processing */}
        {isProcessing && (
          <div className="bg-blue-50/50 p-3 rounded-lg border border-blue-100">
            <div className="flex justify-between text-xs text-blue-800 mb-1">
              <span>Đang nhận diện AI qua Concurrency Limit (tối đa 3 luồng song song)...</span>
              <span className="font-mono font-semibold">
                {totalDone} / {totalQueued} ảnh
              </span>
            </div>
            <Progress
              percent={Math.round((totalDone / (totalQueued || 1)) * 100)}
              status="active"
              strokeColor="#2563EB"
              size="small"
            />
          </div>
        )}

        {/* Cards Grid */}
        {imageQueue.length > 0 && (
          <div className="max-h-[420px] overflow-y-auto pr-1">
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
              {imageQueue.map((item, idx) => {
                const res = item.result;
                const isMatched = Boolean(res?.documents.length) && res!.documents.every(d => d.isTotalMatched);

                return (
                  <Card
                    key={item.id}
                    size="small"
                    className={`border transition-all ${
                      item.status === 'done'
                        ? isMatched
                          ? 'border-emerald-200 bg-white shadow-sm'
                          : 'border-amber-200 bg-amber-50/20 shadow-sm'
                        : item.status === 'error'
                        ? 'border-rose-200 bg-rose-50/30'
                        : 'border-slate-200 bg-slate-50/40'
                    }`}
                    title={
                      <div className="flex items-center justify-between text-xs py-1">
                        <span className="font-semibold text-slate-800 truncate max-w-[200px]">
                          #{idx + 1}. {item.file.name}
                        </span>
                        <Button
                          type="text"
                          danger
                          size="small"
                          icon={<DeleteOutlined className="text-xs" />}
                          onClick={() => handleRemoveItem(item.id)}
                          className="h-6 w-6 p-0 flex items-center justify-center text-slate-400 hover:text-rose-600"
                        />
                      </div>
                    }
                  >
                    <div className="flex gap-3">
                      {/* Image Thumbnail */}
                      <div className="w-16 h-16 rounded border border-slate-200 overflow-hidden shrink-0 bg-slate-100 flex items-center justify-center relative">
                        {item.previewUrl ? (
                          <img
                            src={item.previewUrl}
                            alt="preview"
                            className="w-full h-full object-cover"
                          />
                        ) : (
                          <FileImageOutlined className="text-xl text-slate-400" />
                        )}
                        {item.status === 'processing' && (
                          <div className="absolute inset-0 bg-black/40 flex items-center justify-center">
                            <span className="text-white text-[10px] animate-pulse">Quét...</span>
                          </div>
                        )}
                      </div>

                      {/* Content details */}
                      <div className="flex-1 min-w-0 flex flex-col justify-between">
                        {item.status === 'done' && res && (
                          <div className="space-y-1">
                            <div className="font-semibold text-xs text-violet-700">Đã phát hiện {res.documents.length} đợt</div>
                            {res.documents.map(doc => <div key={doc.documentId} className="border-t border-slate-100 pt-1 mt-1">
                              <div className="flex justify-between gap-2"><span className="truncate text-xs">{doc.title || 'Không tiêu đề'}</span><span className="font-mono text-xs">{doc.items.reduce((s, i) => s + i.quantity, 0).toLocaleString()} đôi</span></div>
                              <div className="flex justify-between items-center">
                                {doc.reportedTotal == null ? <Tag color="warning" className="text-[10px] m-0"><ExclamationCircleOutlined /> Chưa thể đối chiếu</Tag> : doc.isTotalMatched ? <Tag color="success" className="text-[10px] m-0"><CheckCircleOutlined /> Khớp</Tag> : <Tag color="error" className="text-[10px] m-0">Lệch {doc.discrepancy}</Tag>}
                                <Button size="small" type="link" icon={<EditOutlined />} onClick={() => handleOpenEditCard(item, doc)} className="text-xs p-0 h-auto">Sửa</Button>
                              </div>
                            </div>)}
                          </div>
                        )}

                        {item.status === 'pending' && (
                          <div className="text-xs text-slate-400 italic py-2">
                            Chờ bắt đầu bóc tách...
                          </div>
                        )}

                        {item.status === 'processing' && (
                          <div className="text-xs text-blue-600 font-medium py-2">
                            Đang xử lý qua Vision AI...
                          </div>
                        )}

                        {item.status === 'error' && (
                          <div className="text-xs text-rose-600 py-1">
                            <CloseCircleOutlined className="mr-1" />
                            {item.errorMessage || 'Lỗi nhận diện'}
                          </div>
                        )}

                      </div>
                    </div>
                  </Card>
                );
              })}
            </div>
          </div>
        )}

        {/* Global Export Config & Final Action Button */}
        <div className="border-t border-slate-200 pt-3 bg-slate-50 p-4 rounded-lg flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 flex-1 text-xs">
            <div>
              <span className="text-slate-500 block mb-0.5">Số Hợp đồng:</span>
              <Input
                size="small"
                value={contractNo}
                onChange={(e) => setContractNo(e.target.value)}
                className="text-xs"
              />
            </div>
            <div>
              <span className="text-slate-500 block mb-0.5">Đuôi PO:</span>
              <Input
                size="small"
                value={poSuffix}
                onChange={(e) => setPoSuffix(e.target.value)}
                className="text-xs"
              />
            </div>
            <div>
              <span className="text-slate-500 block mb-0.5">Doanh nghiệp:</span>
              <Input
                size="small"
                value={customerName}
                onChange={(e) => setCustomerName(e.target.value)}
                className="text-xs"
              />
            </div>
            <div>
              <span className="text-slate-500 block mb-0.5">Thứ tự xuất (nếu tách 2 file):</span>
              <Select
                size="small"
                value={batchPriority}
                onChange={setBatchPriority}
                className="w-full text-xs"
                options={[
                  { value: ExportSequencePriority.StandardFirst, label: 'Thành hình trước, Gò sau' },
                  { value: ExportSequencePriority.GoFirst, label: 'Gò trước, Thành hình sau' },
                ]}
              />
            </div>
          </div>

          <div className="flex items-center gap-2 self-end sm:self-center">
            <Button onClick={onClose} className="text-xs">
              Đóng
            </Button>
            <Button
              type="primary"
              size="middle"
              icon={<FileZipOutlined />}
              onClick={handleExportZip}
              loading={isExporting}
              disabled={totalDone === 0}
              className="bg-emerald-600 hover:bg-emerald-700 border-emerald-600 font-semibold text-xs px-5 shadow-sm"
            >
              Xác nhận & Xuất toàn bộ gói Excel (.zip)
            </Button>
          </div>
        </div>
      </div>

      {/* Edit Card Detail Modal */}
      <Modal
        title={`Chỉnh sửa đợt: ${editTitle}`}
        open={Boolean(editingCard)}
        onOk={handleSaveEditCard}
        onCancel={() => setEditingCard(null)}
        width={800}
        okText="Lưu thay đổi"
        cancelText="Hủy"
      >
        <div className="space-y-3 mt-3">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <span className="text-xs font-medium text-slate-600 block mb-1">Tên đợt (Title):</span>
              <Input
                value={editTitle}
                onChange={(e) => setEditTitle(e.target.value)}
                className="text-xs"
              />
            </div>
            <div>
              <span className="text-xs font-medium text-slate-600 block mb-1">
                Số ghi tại ô TỔNG CỘNG trên ảnh:
              </span>
              <InputNumber
                value={editReportedTotal}
                placeholder="Không nhận diện được"
                onChange={(v) => setEditReportedTotal(v)}
                className="w-full text-xs font-mono"
              />
            </div>
          </div>

          <div className="border border-slate-200 rounded overflow-hidden">
            <Table
              dataSource={editItems}
              rowKey={(r, idx) => `${r.styleCode}_${idx}`}
              size="small"
              pagination={false}
              columns={[
                {
                  title: 'STT',
                  key: 'idx',
                  width: 50,
                  align: 'center',
                  render: (_, __, i) => <span className="text-xs text-slate-500">{i + 1}</span>,
                },
                {
                  title: 'Mã hình thể',
                  dataIndex: 'styleCode',
                  key: 'styleCode',
                  width: 150,
                  render: (val: string, _, idx) => (
                    <AutoComplete
                      size="small"
                      value={val}
                      options={productOptions}
                      onChange={(newCode) => {
                        setEditItems((prev) =>
                          prev.map((item, i) => (i === idx ? { ...item, styleCode: newCode } : item))
                        );
                      }}
                      filterOption={(inputValue, option) =>
                        (option?.value?.toString() ?? '')
                          .toUpperCase()
                          .includes(inputValue.toUpperCase()) ||
                        (option?.label?.toString() ?? '')
                          .toUpperCase()
                          .includes(inputValue.toUpperCase())
                      }
                      className="w-full text-xs font-mono font-semibold"
                    />
                  ),
                },
                {
                  title: 'Loại hàng',
                  dataIndex: 'processType',
                  key: 'processType',
                  width: 130,
                  render: (val: ProcessType, _, idx) => (
                    <Select
                      size="small"
                      value={normalizeProcessType(val)}
                      onChange={(newType) => {
                        setEditItems((prev) =>
                          prev.map((item, i) => (i === idx ? { ...item, processType: newType } : item))
                        );
                      }}
                      className="w-full text-xs"
                      options={[
                        { value: ProcessType.Standard, label: 'Thành hình' },
                        { value: ProcessType.GoKhongMay, label: 'Gò không may' },
                      ]}
                    />
                  ),
                },
                {
                  title: 'Số lượng (đôi)',
                  dataIndex: 'quantity',
                  key: 'quantity',
                  width: 120,
                  align: 'right',
                  render: (val: number, _, idx) => (
                    <InputNumber
                      size="small"
                      min={1}
                      value={val}
                      onChange={(newQty) => {
                        setEditItems((prev) =>
                          prev.map((item, i) =>
                            i === idx ? { ...item, quantity: newQty || 0 } : item
                          )
                        );
                      }}
                      className="w-full text-xs font-mono text-right"
                    />
                  ),
                },
                {
                  title: 'Ghi chú',
                  dataIndex: 'note',
                  key: 'note',
                  render: (val: string, _, idx) => (
                    <Input
                      size="small"
                      value={val}
                      onChange={(e) => {
                        const newNote = e.target.value;
                        setEditItems((prev) =>
                          prev.map((item, i) => (i === idx ? { ...item, note: newNote } : item))
                        );
                      }}
                      className="text-xs"
                    />
                  ),
                },
                {
                  title: '',
                  key: 'del',
                  width: 50,
                  align: 'center',
                  render: (_, __, idx) => (
                    <Button
                      type="text"
                      danger
                      size="small"
                      icon={<DeleteOutlined />}
                      onClick={() => {
                        setEditItems((prev) => prev.filter((_, i) => i !== idx));
                      }}
                    />
                  ),
                },
              ]}
              summary={() => {
                const totalCalculated = editItems.reduce((acc, i) => acc + (i.quantity || 0), 0);
                const diff = editReportedTotal === null ? null : totalCalculated - editReportedTotal;
                return (
                  <Table.Summary.Row className="bg-slate-50 font-semibold text-xs">
                    <Table.Summary.Cell index={0} colSpan={3} align="center">
                      <span>TỔNG CỘNG CHI TIẾT</span>
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={3} align="right">
                      <span className="font-mono text-blue-600">{totalCalculated} đôi</span>
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={4} colSpan={2}>
                      <span className={diff === 0 ? 'text-emerald-600' : 'text-amber-600'}>
                        {diff === null ? '⚠ Chưa thể đối chiếu tổng' : diff === 0 ? '✓ Khớp tổng phiếu kho' : `⚠ Lệch ${diff > 0 ? `+${diff}` : diff} đôi`}
                      </span>
                    </Table.Summary.Cell>
                  </Table.Summary.Row>
                );
              }}
            />
          </div>
        </div>
      </Modal>
    </Modal>
  );
};
