import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Alert, Button, Card, Input, InputNumber, message, Modal, Radio, Select, Space, Spin, Table, Tag } from 'antd';
import { DeleteOutlined, InboxOutlined, NodeIndexOutlined, ReloadOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { ocrApi } from '../api/ocrApi';
import type { CreateShipmentItem, OcrDetectedDocument, OcrItem, ProductMaster } from '../types';
import { ProcessType, normalizeProcessType } from '../types';
import { normalizeOcrStyleCode } from '../utils/normalizeOcrStyleCode';
import { OriginalImagePreview } from './OriginalImagePreview';
import { SplitMatrixModal } from '../features/shipment-dispatch/components/SplitMatrixModal';
import type { DispatchSourceDocument } from '../features/shipment-dispatch/types/shipmentDispatch';

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
  const [processedDocumentIds, setProcessedDocumentIds] = useState<Set<string>>(new Set());
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

  const updateDocument = (id: string, fn: (d: OcrDetectedDocument) => OcrDetectedDocument) => setDocuments(prev => prev.map(d => d.documentId === id ? fn(d) : d));
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
          {documents.map((doc, index) => { const total = doc.items.reduce((s, i) => s + (i.quantity || 0), 0); const diff = doc.reportedTotal == null ? null : total - doc.reportedTotal; const processed = processedDocumentIds.has(doc.documentId); return <Card key={doc.documentId} title={<Input value={doc.title} onChange={e => updateDocument(doc.documentId, d => ({ ...d, title: e.target.value }))} />} extra={<Space><Button disabled={processed} onClick={() => apply(doc)}>Áp dụng 1 INV</Button><Button type="primary" icon={<NodeIndexOutlined />} disabled={processed || loading || !doc.items.some(i => i.quantity > 0)} onClick={() => setSplitSource(toDispatchSource(doc))}>Tách nhiều INV</Button></Space>}>
            <div className="mb-2 flex gap-3 text-sm"><b>Đợt {index + 1}</b><span>{doc.items.length} mã</span><span>{total.toLocaleString()} đôi</span>{processed && <Tag color="green">Đã xử lý</Tag>}</div>
            {doc.reportedTotal == null ? <Alert type="warning" showIcon message="Chưa thể đối chiếu tổng" description={`Không nhận diện được tổng trên phiếu. Tổng dòng hàng: ${total.toLocaleString()} đôi. Vui lòng kiểm tra các dòng hàng.`} /> : diff === 0 ? <Alert type="success" showIcon message={`Khớp — ${total.toLocaleString()} đôi`} /> : <Alert type="warning" showIcon message={`Lệch ${(diff ?? 0) > 0 ? '+' : ''}${(diff ?? 0).toLocaleString()} đôi`} description={`Tổng trên phiếu: ${doc.reportedTotal.toLocaleString()} · Tổng nhận diện: ${total.toLocaleString()}`} />}
            <Table className="mt-3" size="small" pagination={false} rowKey={(_r, i) => `${doc.documentId}-${i}`} dataSource={doc.items} columns={columns(doc)} />
          </Card>; })}
        </> : <Alert message="Chưa có kết quả OCR" description="Chọn một ảnh để bắt đầu." />}
      </div>
    </div>
    <SplitMatrixModal open={Boolean(splitSource)} sourceDocument={splitSource} contractFolderId={selectedPartnerId} templateId={templateId} poSuffix={poSuffix} invoiceDate={invoiceDate || new Date().toISOString().slice(0, 10)} onClose={() => setSplitSource(null)} onExported={() => { if (splitSource) setProcessedDocumentIds(current => new Set(current).add(splitSource.documentId)); onDispatchExported?.(); }} />
  </Modal>;
};
