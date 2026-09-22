import { ThunderboltOutlined } from '@ant-design/icons';
import { Alert, Button, InputNumber, message, Modal, Skeleton, Tag } from 'antd';
import { useEffect, useMemo, useState } from 'react';
import { warehouseApi } from '../../../api/warehouseApi';
import type { WarehouseBatch, WarehouseBatchItem } from '../../../types/warehouse';
import type { ProcessType } from '../../../types';
import { shipmentDispatchApi, readBlobValidation, triggerDownload } from '../api/shipmentDispatchApi';
import type { ConsolidatedDispatchSource, DispatchSourceDocument, SubInvoiceAllocation, ValidateSplitResult } from '../types/shipmentDispatch';
import { balanceQuantity, itemKey, normalizeStyle } from '../utils/splitAllocation';
import { DispatchSafetyBar } from './DispatchSafetyBar';
import { DispatchValidationPanel } from './DispatchValidationPanel';
import { SplitMatrixTable } from './SplitMatrixTable';

interface Props { open: boolean; sourceBatchId?: number | null; sourceDocument?: DispatchSourceDocument | null; consolidatedSource?: ConsolidatedDispatchSource | null; contractFolderId?: number | null; templateId?: number | null; poSuffix?: string; invoiceDate: string; onClose: () => void; onExported: () => void }

export function SplitMatrixModal({ open, sourceBatchId, sourceDocument, consolidatedSource, contractFolderId, templateId, poSuffix, invoiceDate, onClose, onExported }: Props) {
  const [batch, setBatch] = useState<WarehouseBatch | null>(null);
  const [loading, setLoading] = useState(false);
  const [invoiceCount, setInvoiceCount] = useState(4);
  const [allocations, setAllocations] = useState<Record<string, number[]>>({});
  const [serverValidation, setServerValidation] = useState<ValidateSplitResult | null>(null);
  const [dirty, setDirty] = useState(true);
  const [validating, setValidating] = useState(false);
  const [exporting, setExporting] = useState(false);

  const sourceItems = useMemo(() => {
    const grouped = new Map<string, WarehouseBatchItem>();
    const items = consolidatedSource?.items ?? sourceDocument?.items ?? batch?.items ?? [];
    items.forEach((item) => { const key = itemKey(item.styleCode, item.processType); const current = grouped.get(key); grouped.set(key, current ? { ...current, quantity: current.quantity + item.quantity } : { id: 'id' in item ? item.id : 0, styleCode: normalizeStyle(item.styleCode), quantity: item.quantity, processType: item.processType }); });
    return [...grouped.values()];
  }, [batch, sourceDocument, consolidatedSource]);

  const allocationsFor = (items: WarehouseBatchItem[], count: number) => {
    const grouped = new Map<string, WarehouseBatchItem>();
    items.forEach((item) => { const key = itemKey(item.styleCode, item.processType); const current = grouped.get(key); grouped.set(key, current ? { ...current, quantity: current.quantity + item.quantity } : item); });
    return Object.fromEntries([...grouped.values()].map((item) => [itemKey(item.styleCode, item.processType), balanceQuantity(item.quantity, count)]));
  };

  const invalidate = () => { setServerValidation(null); setDirty(true); };
  const rebalance = (count = invoiceCount) => {
    const next: Record<string, number[]> = {};
    sourceItems.forEach((item) => { next[itemKey(item.styleCode, item.processType)] = balanceQuantity(item.quantity, count); });
    setAllocations(next); invalidate();
  };

  useEffect(() => {
    if (!open) return;
    const directItems = consolidatedSource?.items ?? sourceDocument?.items;
    if (directItems) {
      queueMicrotask(() => { setBatch(null); setServerValidation(null); setDirty(true); setInvoiceCount(4); setAllocations(allocationsFor(directItems as WarehouseBatchItem[], 4)); });
      return;
    }
    if (!sourceBatchId) return;
    queueMicrotask(() => {
      setLoading(true); setBatch(null); setServerValidation(null); setDirty(true); setInvoiceCount(4);
      warehouseApi.getBatchById(sourceBatchId).then((value) => { setBatch(value); setAllocations(allocationsFor(value.items, 4)); }).catch(() => message.error('Không thể tải đợt nguồn để tách hóa đơn.')).finally(() => setLoading(false));
    });
  }, [open, sourceBatchId, sourceDocument, consolidatedSource]);
  useEffect(() => { if (open) queueMicrotask(invalidate); }, [open, contractFolderId, templateId, poSuffix, invoiceDate]);

  const subInvoices = useMemo<SubInvoiceAllocation[]>(() => Array.from({ length: invoiceCount }, (_, index) => ({
    invoiceSuffixTitle: `INV ${index + 1}`,
    items: sourceItems.flatMap((item) => { const quantity = allocations[itemKey(item.styleCode, item.processType)]?.[index] ?? 0; return quantity > 0 ? [{ styleCode: normalizeStyle(item.styleCode), processType: item.processType as ProcessType, quantity }] : []; }),
  })), [allocations, invoiceCount, sourceItems]);
  const originalTotal = sourceItems.reduce((sum, item) => sum + item.quantity, 0);
  const allocatedTotal = Object.values(allocations).flat().reduce((sum, quantity) => sum + quantity, 0);
  const rowsValid = sourceItems.every((item) => (allocations[itemKey(item.styleCode, item.processType)] ?? []).reduce((a, b) => a + b, 0) === item.quantity);
  const noEmptyInvoice = subInvoices.every((invoice) => invoice.items.length > 0);
  const clientValid = originalTotal > 0 && originalTotal === allocatedTotal && rowsValid && noEmptyInvoice;

  const changeCount = (count: number | null) => { const safe = Math.min(10, Math.max(2, Number(count ?? 2))); setInvoiceCount(safe); const next: Record<string, number[]> = {}; sourceItems.forEach((item) => { next[itemKey(item.styleCode, item.processType)] = balanceQuantity(item.quantity, safe); }); setAllocations(next); invalidate(); };
  const changeCell = (key: string, index: number, value: number) => { setAllocations((current) => ({ ...current, [key]: current[key].map((entry, i) => i === index ? Math.max(0, Math.trunc(value)) : entry) })); invalidate(); };
  const sourcePayload = sourceDocument ? { documentId: sourceDocument.documentId, title: sourceDocument.title, items: sourceDocument.items, sourceFileName: sourceDocument.sourceFileName, clientFileId: sourceDocument.clientFileId } : undefined;
  const hasSource = Boolean(batch || sourceDocument || consolidatedSource);
  const validate = async () => { if (!hasSource || !clientValid) return; setValidating(true); try { const result = await shipmentDispatchApi.validateSplit({ sourceBatchId: batch?.id, sourceDocument: sourcePayload, sourceDocuments: consolidatedSource?.sourceDocuments, subInvoices }); setServerValidation(result); setDirty(false); if (result.isValid) message.success('Backend đã xác nhận phân bổ hợp lệ.'); } finally { setValidating(false); } };
  const exportZip = async () => { if (!hasSource || dirty || !serverValidation?.isValid) return; setExporting(true); try {
    const result = await shipmentDispatchApi.exportSplitZip({ sourceBatchId: batch?.id, sourceDocument: sourcePayload, sourceDocuments: consolidatedSource?.sourceDocuments, contractFolderId: contractFolderId ?? batch?.contractFolderId ?? undefined, templateId: templateId ?? undefined, poSuffix, invoiceDate, subInvoices });
    triggerDownload(result); message.success('Đã tải bộ chứng từ tách hóa đơn thành công.'); onExported(); onClose();
  } catch (error) { const validation = await readBlobValidation(error); if (validation) { setServerValidation(validation); setDirty(false); } message.error(validation ? 'Dữ liệu nguồn đã thay đổi hoặc không còn hợp lệ. Vui lòng kiểm tra lại.' : 'Không thể tạo bộ chứng từ. Phân bổ của bạn vẫn được giữ nguyên.'); } finally { setExporting(false); } };

  return <Modal title="Tách đợt hàng thành nhiều hóa đơn" open={open} onCancel={exporting ? undefined : onClose} footer={null} width="94vw" style={{ maxWidth: 1500, top: 24 }} destroyOnHidden maskClosable={!exporting}>
    {loading || !hasSource ? <Skeleton active /> : <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-slate-200 bg-slate-50 p-3">
        <div><div className="font-semibold text-slate-900">Nguồn: {consolidatedSource?.title || sourceDocument?.title || batch?.batchName}</div><div className="text-xs text-slate-500">{sourceItems.length} mã · {originalTotal.toLocaleString()} đôi{sourceDocument?.sourceFileName ? ` · Ảnh nguồn: ${sourceDocument.sourceFileName}` : batch?.contractNote ? ` · ${batch.contractNote}` : ''}</div></div>
        <div className="flex items-center gap-2"><span className="text-xs text-slate-600">Số Invoice logic:</span><InputNumber min={2} max={10} precision={0} value={invoiceCount} disabled={exporting} onChange={changeCount} /><Button icon={<ThunderboltOutlined />} disabled={exporting} onClick={() => rebalance()}>Tự động chia đều <span className="text-slate-400">· ưu tiên thùng 12</span></Button></div>
      </div>
      {!noEmptyInvoice && <Alert type="error" showIcon message="Có hóa đơn con chưa được phân bổ mặt hàng." />}
      <SplitMatrixTable items={sourceItems} allocations={allocations} invoiceCount={invoiceCount} disabled={exporting} onChange={changeCell} />
      <DispatchValidationPanel result={serverValidation} />
      {serverValidation?.isValid && serverValidation.warnings.length > 0 && <Tag color="orange">Cảnh báo không khóa thao tác xuất</Tag>}
      <DispatchSafetyBar original={originalTotal} allocated={allocatedTotal} serverValid={serverValidation?.isValid} isDirty={dirty} validating={validating} exporting={exporting} canValidate={clientValid && !validating} canExport={clientValid && !dirty && !!serverValidation?.isValid && serverValidation.errors.length === 0 && !exporting} onValidate={validate} onExport={exportZip} />
    </div>}
  </Modal>;
}
