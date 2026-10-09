import type { CreateShipmentItem, ExportSequencePriority, SequenceInfo } from '../types';

export interface InvoiceDraftData {
  fields: Record<string, unknown>;
  items: CreateShipmentItem[];
  partnerId: number | null;
  templateId: number | null;
  warehouseBatchId: number | null;
  orderId?: number;
  serverBaseline?: string;
  priority: ExportSequencePriority;
  startNumber: number;
  sequence?: SequenceInfo | null;
  completed?: boolean;
  exportedOrders?: { id: number; invoiceNo: string }[];
}
export interface InvoiceDraft { version: 1; id: string; updatedAt: string; data: InvoiceDraftData }

export function requestInvoiceDraftLeave(proceed: () => void) {
  if (window.dispatchEvent(new CustomEvent('invoice-draft:before-leave', { cancelable: true, detail: { proceed } }))) proceed();
}
export class InvoiceDraftStore {
  readonly prefix: string;
  private storage: Storage;
  constructor(storage: Storage, backend: string, userId: string) {
    this.storage = storage;
    this.prefix = `shoedocx:inv:v1:${encodeURIComponent(backend)}:${encodeURIComponent(userId)}:`;
  }
  list(): { drafts: InvoiceDraft[]; damaged: number } {
    const drafts: InvoiceDraft[] = [];
    let damaged = 0;
    for (let i = 0; i < this.storage.length; i++) {
      const key = this.storage.key(i);
      if (!key?.startsWith(this.prefix + 'draft:')) continue;
      try {
        const draft = JSON.parse(this.storage.getItem(key)!);
        if (draft.version !== 1 || typeof draft.id !== 'string' || key !== this.prefix + 'draft:' + draft.id || !draft.data ||
            !Array.isArray(draft.data.items) || !draft.data.fields ||
            typeof draft.updatedAt !== 'string' || !Number.isFinite(Date.parse(draft.updatedAt)) ||
            typeof draft.data.fields !== 'object' || Array.isArray(draft.data.fields) ||
            draft.data.items.some((item: unknown) => !item || typeof item !== 'object' ||
              typeof (item as CreateShipmentItem).styleCode !== 'string' || typeof (item as CreateShipmentItem).quantity !== 'number')) throw new Error('Invalid draft');
        drafts.push(draft);
      } catch { damaged++; }
    }
    return { drafts: drafts.sort((a, b) => b.updatedAt.localeCompare(a.updatedAt)), damaged };
  }
  current(): InvoiceDraft | undefined {
    const { drafts } = this.list();
    const active = this.storage.getItem(this.prefix + 'active');
    return drafts.find(d => d.id === active) ?? drafts[0];
  }
  save(id: string, data: InvoiceDraftData, activate = true): InvoiceDraft {
    const draft: InvoiceDraft = { version: 1, id, updatedAt: new Date().toISOString(), data };
    this.storage.setItem(this.prefix + 'draft:' + id, JSON.stringify(draft));
    if (activate) this.storage.setItem(this.prefix + 'active', id);
    return draft;
  }
  remove(id: string) {
    this.storage.removeItem(this.prefix + 'draft:' + id);
    if (this.storage.getItem(this.prefix + 'active') === id) this.storage.removeItem(this.prefix + 'active');
  }
  activate(id: string) { this.storage.setItem(this.prefix + 'active', id); }
}

export function mergeDraftResult(data: InvoiceDraftData, patch: Partial<InvoiceDraftData>,
  requestedInvoiceNo: string, actualInvoiceNo?: string): InvoiceDraftData {
  return { ...data, ...patch, fields: { ...data.fields,
    ...(actualInvoiceNo && data.fields.invoiceNo === requestedInvoiceNo ? { invoiceNo: actualInvoiceNo } : {}) } };
}

export function copyDraftData(data: InvoiceDraftData): InvoiceDraftData {
  return { ...data, fields: { ...data.fields, invoiceNo: '' }, orderId: undefined,
    serverBaseline: undefined, warehouseBatchId: null, completed: false, exportedOrders: undefined };
}

export function canResumeServerDraft(data: InvoiceDraftData, order: { isLocked?: boolean }, cleared: boolean): boolean {
  return !cleared && !order.isLocked && Boolean(data.serverBaseline) && serverFingerprint(order) === data.serverBaseline;
}

// A stable snapshot of the server record detects changes without adding API fields.
export function serverFingerprint(order: unknown): string {
  const canonical = (value: unknown): unknown => {
    if (Array.isArray(value)) return value.map(canonical);
    if (value && typeof value === 'object') return Object.fromEntries(
      Object.entries(value).sort(([a], [b]) => a.localeCompare(b)).map(([k, v]) => [k, canonical(v)]));
    return value;
  };
  return JSON.stringify(canonical(order));
}
