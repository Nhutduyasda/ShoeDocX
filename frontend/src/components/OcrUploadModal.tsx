import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Alert, Button, Card, Checkbox, Input, InputNumber, message, Modal, Radio, Select, Space, Spin, Table, Tag } from 'antd';
import { DeleteOutlined, EyeOutlined, FileZipOutlined, InboxOutlined, NodeIndexOutlined, ReloadOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { ocrApi } from '../api/ocrApi';
import type { CreateShipmentItem, OcrDetectedDocument, OcrItem, ProductMaster } from '../types';
import { ProcessType, normalizeProcessType } from '../types';
import { normalizeOcrStyleCode } from '../utils/normalizeOcrStyleCode';
import { OriginalImagePreview } from './OriginalImagePreview';
import { SplitMatrixModal } from '../features/shipment-dispatch/components/SplitMatrixModal';
import type { ConsolidatedDispatchSource, DispatchSourceDocument, MergeShipmentPreviewResponse, OcrDispatchSourcePayload } from '../features/shipment-dispatch/types/shipmentDispatch';
import { shipmentDispatchApi, triggerDownload } from '../features/shipment-dispatch/api/shipmentDispatchApi';

interface Props {
  visible: boolean; onClose: () => void; products: ProductMaster[]; selectedPartnerId?: number | null;
  onApply: (items: CreateShipmentItem[], mode: 'replace' | 'append') => void;
  onDispatchExported?: () => void;
  templateId?: number | null;
  poSuffix?: string;
  invoiceDate?: string;
}

export const OcrUploadModal: React.FC<Props> = ({ visible, onClose, products, selectedPartnerId, onApply, onDispatchExported, templateId, poSuffix, invoiceDate }) => {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [documents, setDocuments] = useState<OcrDetectedDocument[]>([]);
  const [loading, setLoading] = useState(false);
  const [applyMode, setApplyMode] = useState<'replace' | 'append'>('replace');
  const [splitSource, setSplitSource] = useState<DispatchSourceDocument | null>(null);
  const [consolidatedSplitSource, setConsolidatedSplitSource] = useState<ConsolidatedDispatchSource | null>(null);
  const [processedDocumentIds, setProcessedDocumentIds] = useState<Set<string>>(new Set());
  const [selectedDocumentIds, setSelectedDocumentIds] = useState<string[]>([]);
  const [mergePreview, setMergePreview] = useState<MergeShipmentPreviewResponse | null>(null);
  const [isPreviewingMerge, setIsPreviewingMerge] = useState(false);
  const [isExportingMerge, setIsExportingMerge] = useState(false);
  const [isExportingSeparate, setIsExportingSeparate] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const productMap = useMemo(() => new Map(products.filter(p => !selectedPartnerId || !p.folderId || p.folderId === selectedPartnerId)
    .map(p => [p.styleCode.trim().toUpperCase(), p])), [products, selectedPartnerId]);

  useEffect(() => () => { if (preview?.startsWith('blob:')) URL.revokeObjectURL(preview); }, [preview]);

  const enrich = useCallback((item: OcrItem): OcrItem => {
    const styleCode = normalizeOcrStyleCode(item.styleCode);
    const key = styleCode.toUpperCase().replace(/\.G$/, '').trim();
    const pm = productMap.get(key);
    return { ...item, styleCode, processType: normalizeProcessType(item.processType), unitPriceCMT: pm?.unitPriceCMT ?? item.unitPriceCMT,
      unitPriceDAP: pm?.unitPriceDAP ?? item.unitPriceDAP, pairPerCarton: pm?.pairPerCarton || item.pairPerCarton || 12,
      description: pm?.description ?? item.description, unit: pm?.unit ?? item.unit ?? 'đôi', isMatched: Boolean(pm) || item.isMatched };
  }, [productMap]);

  const processImage = useCallback(async (image: File | Blob) => {
    setLoading(true);
    try {
      const response = await ocrApi.extractFromImage(image);
      const next = response.documents.map(d => ({ ...d, items: d.items.map(enrich), calculatedTotal: d.items.reduce((s, i) => s + i.quantity, 0) }));
      setDocuments(next);
      setSelectedDocumentIds(next.map(document => document.documentId));
      setMergePreview(null);
      message.success(`Đã phát hiện ${next.length} đợt hàng trong ảnh.`);
    } catch (e: unknown) {
      const text = (e as { response?: { data?: { message?: string } } })?.response?.data?.message || 'Không thể nhận diện ảnh phiếu kho.';
      message.error(text);
    } finally { setLoading(false); }
  }, [enrich]);

  const selectFile = (selected: File) => {
    if (!selected.type.startsWith('image/')) return message.error('Vui lòng chọn file ảnh hợp lệ.');
    if (preview?.startsWith('blob:')) URL.revokeObjectURL(preview);
    setFile(selected); setPreview(URL.createObjectURL(selected)); setDocuments([]); processImage(selected);
  };

  useEffect(() => {
    if (!visible) return;
    const paste = (e: ClipboardEvent) => { const blob = Array.from(e.clipboardData?.items || []).find(i => i.type.startsWith('image/'))?.getAsFile(); if (blob) selectFile(blob); };
    window.addEventListener('paste', paste); return () => window.removeEventListener('paste', paste);
  });

  const updateDocument = (id: string, fn: (d: OcrDetectedDocument) => OcrDetectedDocument) => { setDocuments(prev => prev.map(d => d.documentId === id ? fn(d) : d)); setMergePreview(null); };
  const columns = (doc: OcrDetectedDocument): ColumnsType<OcrItem> => [
    { title: 'Mã hình thể', dataIndex: 'styleCode', render: (v, _r, i) => <Input value={v} size="small" onChange={e => updateDocument(doc.documentId, d => ({ ...d, items: d.items.map((x, n) => n === i ? enrich({ ...x, styleCode: e.target.value }) : x) }))} /> },
    { title: 'Số lượng', dataIndex: 'quantity', width: 115, render: (v, _r, i) => <InputNumber min={1} value={v} size="small" onChange={q => updateDocument(doc.documentId, d => ({ ...d, items: d.items.map((x, n) => n === i ? { ...x, quantity: q || 0 } : x) }))} /> },
    { title: 'Công đoạn', dataIndex: 'processType', width: 145, render: (v, _r, i) => <Select size="small" value={normalizeProcessType(v)} options={[{ value: ProcessType.Standard, label: 'Thành hình' }, { value: ProcessType.GoKhongMay, label: 'Gò không may' }]} onChange={p => updateDocument(doc.documentId, d => ({ ...d, items: d.items.map((x, n) => n === i ? { ...x, processType: p } : x) }))} /> },
    { title: 'Master', width: 75, render: (_, r) => <Tag color={r.isMatched ? 'green' : 'default'}>{r.isMatched ? 'Khớp' : 'Mới'}</Tag> },
    { title: '', width: 40, render: (_, _r, i) => <Button danger type="text" icon={<DeleteOutlined />} onClick={() => updateDocument(doc.documentId, d => ({ ...d, items: d.items.filter((_x, n) => n !== i) }))} /> },
  ];

  const apply = (doc: OcrDetectedDocument) => {
    const valid = doc.items.filter(i => i.quantity > 0 && i.styleCode.trim());
    if (!valid.length) return message.warning('Đợt này không có mặt hàng hợp lệ.');
    onApply(valid.map(i => ({ styleCode: i.styleCode, quantity: i.quantity, processType: normalizeProcessType(i.processType), unitPriceCMT: i.unitPriceCMT, unitPriceDAP: i.unitPriceDAP, unit: i.unit, pairPerCarton: i.pairPerCarton, description: i.description })), applyMode);
    message.success(`Đã áp dụng đợt “${doc.title || 'Không tiêu đề'}” vào hóa đơn.`); onClose();
  };

  const toDispatchSource = (doc: OcrDetectedDocument): DispatchSourceDocument => ({
    sourceType: 'ocr-document', documentId: doc.documentId, title: doc.title,
    items: doc.items.map(i => ({ styleCode: i.styleCode, quantity: i.quantity, processType: normalizeProcessType(i.processType), unitPriceCMT: i.unitPriceCMT, unitPriceDAP: i.unitPriceDAP, unit: i.unit, pairPerCarton: i.pairPerCarton, description: i.description })),
    calculatedTotal: doc.items.reduce((sum, item) => sum + item.quantity, 0), reportedTotal: doc.reportedTotal,
    sourceFileName: file?.name,
  });

  const selectedDocuments = documents.filter(document => selectedDocumentIds.includes(document.documentId) && !processedDocumentIds.has(document.documentId));
  const selectedTotal = selectedDocuments.reduce((sum, document) => sum + document.items.reduce((itemSum, item) => itemSum + item.quantity, 0), 0);
  const sourcePayloads = (): OcrDispatchSourcePayload[] => selectedDocuments.map(document => ({ documentId: document.documentId, title: document.title, sourceFileName: file?.name, items: toDispatchSource(document).items }));
  const mergeRequest = () => ({ sourceDocuments: sourcePayloads(), contractFolderId: selectedPartnerId ?? undefined, templateId: templateId ?? undefined, poSuffix, invoiceDate: invoiceDate || new Date().toISOString().slice(0, 10) });
  const toggleSelection = (id: string, checked: boolean) => { setSelectedDocumentIds(current => checked ? [...new Set([...current, id])] : current.filter(value => value !== id)); setMergePreview(null); };
  const previewMerge = async () => { if (selectedDocuments.length < 2) return; setIsPreviewingMerge(true); try { setMergePreview(await shipmentDispatchApi.previewMerge(mergeRequest())); } catch (error: any) { message.error(error.response?.data?.message || 'Không thể xem trước kết quả gom.'); } finally { setIsPreviewingMerge(false); } };
  const consolidatedSource = (): ConsolidatedDispatchSource | null => mergePreview ? ({ sourceType: 'merged-ocr-documents', sourceDocumentIds: selectedDocuments.map(d => d.documentId), sourceTitles: selectedDocuments.map(d => d.title), sourceDocuments: sourcePayloads(), title: selectedDocuments.map(d => d.title).join(' + '), items: mergePreview.mergedItems, totalQuantity: mergePreview.totalQuantity, totalCartons: mergePreview.totalCartons }) : null;
  const exportMerge = async () => { if (!mergePreview || isExportingMerge) return; setIsExportingMerge(true); try { const result = await shipmentDispatchApi.exportMerge(mergeRequest()); triggerDownload(result); setProcessedDocumentIds(current => new Set([...current, ...selectedDocuments.map(d => d.documentId)])); message.success('Đã xuất 1 Invoice từ nguồn gom.'); onDispatchExported?.(); } catch { message.error('Không thể xuất Invoice gom. Kết quả OCR vẫn được giữ nguyên.'); } finally { setIsExportingMerge(false); } };
  const exportSeparate = async () => { if (!selectedDocuments.length) return; setIsExportingSeparate(true); try { const batches = selectedDocuments.map(document => ({ batchId: document.documentId, title: document.title, items: toDispatchSource(document).items })); const blob = await ocrApi.batchExportZip({ contractFolderId: selectedPartnerId, poSuffix, invoiceDate, batches }); const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = `OCR-Separate-${invoiceDate || new Date().toISOString().slice(0, 10)}.zip`; anchor.click(); URL.revokeObjectURL(url); setProcessedDocumentIds(current => new Set([...current, ...selectedDocuments.map(d => d.documentId)])); message.success(`Đã xuất riêng ${batches.length} đợt.`); onDispatchExported?.(); } catch { message.error('Không thể xuất riêng các đợt đã chọn.'); } finally { setIsExportingSeparate(false); } };

  return <Modal title="OCR phiếu kho — phát hiện nhiều đợt" open={visible} onCancel={onClose} width={1080} footer={<Button onClick={onClose}>Đóng</Button>}>
    <input ref={inputRef} hidden type="file" accept="image/*" onChange={e => e.target.files?.[0] && selectFile(e.target.files[0])} />
    <div className="space-y-4">
      <div onDrop={e => { e.preventDefault(); if (e.dataTransfer.files[0]) selectFile(e.dataTransfer.files[0]); }} onDragOver={e => e.preventDefault()}>
        {preview ? <>
          <OriginalImagePreview src={preview} alt="Ảnh phiếu kho gốc" />
          <Space className="mt-2">
            <Button onClick={() => inputRef.current?.click()}>Chọn ảnh khác</Button>
            {file && <Button type="link" icon={<ReloadOutlined />} onClick={() => processImage(file)}>Quét lại ảnh gốc</Button>}
          </Space>
        </> : <div onClick={() => inputRef.current?.click()} className="border-2 border-dashed rounded-lg min-h-64 flex items-center justify-center cursor-pointer bg-slate-50">
          <Space direction="vertical" align="center"><InboxOutlined className="text-3xl" /><span>Chọn, kéo thả hoặc dán ảnh</span></Space>
        </div>}
      </div>
      <div><Radio.Group value={applyMode} onChange={e => setApplyMode(e.target.value)}><Radio value="replace">Thay thế</Radio><Radio value="append">Nối thêm</Radio></Radio.Group></div>
      <div className="max-h-[620px] overflow-y-auto space-y-3">
        {loading ? <div className="py-24 text-center"><Spin tip="Đang phát hiện các bảng..." /></div> : documents.length ? <>
          <Alert type="info" showIcon message={`Đã phát hiện ${documents.length} đợt hàng`} />
          <div className="rounded-lg border border-violet-200 bg-violet-50/40 p-3 space-y-3">
            <div className="flex justify-between"><div><strong>Đã chọn: {selectedDocuments.length} đợt</strong><div className="text-xs text-slate-500">Tổng đã chọn: {selectedTotal.toLocaleString()} đôi</div></div><Space><Button size="small" onClick={() => { setSelectedDocumentIds(documents.filter(d => !processedDocumentIds.has(d.documentId)).map(d => d.documentId)); setMergePreview(null); }}>Chọn tất cả</Button><Button size="small" onClick={() => { setSelectedDocumentIds([]); setMergePreview(null); }}>Bỏ chọn</Button></Space></div>
            <Space wrap>
              <Button disabled={selectedDocuments.length !== 1} onClick={() => selectedDocuments[0] && apply(selectedDocuments[0])}>Áp dụng 1 INV</Button>
              <Button icon={<NodeIndexOutlined />} disabled={selectedDocuments.length !== 1} onClick={() => selectedDocuments[0] && setSplitSource(toDispatchSource(selectedDocuments[0]))}>Tách document đã chọn</Button>
              <Button icon={<FileZipOutlined />} loading={isExportingSeparate} disabled={!selectedDocuments.length} onClick={exportSeparate}>Xuất riêng từng đợt</Button>
              <Button type="primary" icon={<EyeOutlined />} loading={isPreviewingMerge} disabled={selectedDocuments.length < 2} onClick={previewMerge}>Gom {selectedDocuments.length} đợt đã chọn</Button>
            </Space>
            {selectedDocuments.length < 2 && <div className="text-xs text-slate-500">Cần chọn ít nhất 2 đợt để gom.</div>}
          </div>
          {documents.map((doc, index) => { const total = doc.items.reduce((s, i) => s + (i.quantity || 0), 0); const diff = doc.reportedTotal == null ? null : total - doc.reportedTotal; const processed = processedDocumentIds.has(doc.documentId); return <Card key={doc.documentId} title={<Space><Checkbox checked={selectedDocumentIds.includes(doc.documentId)} disabled={processed} onChange={event => toggleSelection(doc.documentId, event.target.checked)} /><Input value={doc.title} onChange={e => updateDocument(doc.documentId, d => ({ ...d, title: e.target.value }))} /></Space>}>
            <div className="mb-2 flex gap-3 text-sm"><b>Đợt {index + 1}</b><span>{doc.items.length} mã</span><span>{total.toLocaleString()} đôi</span>{processed && <Tag color="green">Đã xử lý</Tag>}</div>
            {doc.reportedTotal == null ? <Alert type="warning" showIcon message="Chưa thể đối chiếu tổng" description={`Không nhận diện được tổng trên phiếu. Tổng dòng hàng: ${total.toLocaleString()} đôi. Vui lòng kiểm tra các dòng hàng.`} /> : diff === 0 ? <Alert type="success" showIcon message={`Khớp — ${total.toLocaleString()} đôi`} /> : <Alert type="warning" showIcon message={`Lệch ${(diff ?? 0) > 0 ? '+' : ''}${(diff ?? 0).toLocaleString()} đôi`} description={`Tổng trên phiếu: ${doc.reportedTotal.toLocaleString()} · Tổng nhận diện: ${total.toLocaleString()}`} />}
            <Table className="mt-3" size="small" pagination={false} rowKey={(_r, i) => `${doc.documentId}-${i}`} dataSource={doc.items} columns={columns(doc)} />
          </Card>; })}
          {mergePreview && <Card title="KẾT QUẢ SAU KHI GOM" className="border-emerald-200">
            <Alert type="success" showIcon message={`${selectedDocuments.map(d => d.title).join(' + ')} · ${mergePreview.mergedItems.length} mã · ${mergePreview.totalQuantity.toLocaleString()} đôi`} description="Số liệu do backend tính. Thay đổi lựa chọn hoặc chỉnh document sẽ hủy preview." />
            {selectedTotal !== mergePreview.totalQuantity && <Alert className="mt-2" type="error" showIcon message={`Tổng local ${selectedTotal.toLocaleString()} khác backend ${mergePreview.totalQuantity.toLocaleString()}. Export đang bị khóa.`} />}
            <Table className="mt-3" size="small" pagination={false} rowKey={row => `${row.styleCode}-${row.processType}`} dataSource={mergePreview.mergedItems} columns={[{ title: 'Mã hình thể', dataIndex: 'styleCode' }, { title: 'Công đoạn', dataIndex: 'processType', render: value => value === ProcessType.GoKhongMay ? 'Gò không may' : 'Thành hình' }, { title: 'SL sau gom', dataIndex: 'quantity', align: 'right', render: value => value.toLocaleString() }, { title: 'Carton', dataIndex: 'pairPerCarton', align: 'right' }, { title: 'CMT', dataIndex: 'unitPriceCMT', align: 'right' }, { title: 'DAP', dataIndex: 'unitPriceDAP', align: 'right' }]} />
            <Space className="mt-3" wrap><Button onClick={() => setMergePreview(null)}>Quay lại chọn đợt</Button><Button type="primary" loading={isExportingMerge} disabled={selectedTotal !== mergePreview.totalQuantity} onClick={exportMerge}>Xuất 1 INV</Button><Button icon={<NodeIndexOutlined />} disabled={selectedTotal !== mergePreview.totalQuantity} onClick={() => { const source = consolidatedSource(); if (source) setConsolidatedSplitSource(source); }}>Tách thành nhiều INV</Button></Space>
          </Card>}
        </> : <Alert message="Chưa có kết quả OCR" description="Chọn một ảnh để bắt đầu." />}
      </div>
    </div>
    <SplitMatrixModal open={Boolean(splitSource)} sourceDocument={splitSource} contractFolderId={selectedPartnerId} templateId={templateId} poSuffix={poSuffix} invoiceDate={invoiceDate || new Date().toISOString().slice(0, 10)} onClose={() => setSplitSource(null)} onExported={() => { if (splitSource) setProcessedDocumentIds(current => new Set(current).add(splitSource.documentId)); onDispatchExported?.(); }} />
    <SplitMatrixModal open={Boolean(consolidatedSplitSource)} consolidatedSource={consolidatedSplitSource} contractFolderId={selectedPartnerId} templateId={templateId} poSuffix={poSuffix} invoiceDate={invoiceDate || new Date().toISOString().slice(0, 10)} onClose={() => setConsolidatedSplitSource(null)} onExported={() => { if (consolidatedSplitSource) setProcessedDocumentIds(current => new Set([...current, ...consolidatedSplitSource.sourceDocumentIds])); onDispatchExported?.(); }} />
  </Modal>;
};
