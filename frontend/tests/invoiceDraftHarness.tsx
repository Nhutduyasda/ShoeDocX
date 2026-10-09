// Development-only fixture for exercising real hook effects and browser storage failures.
import React, { useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { useInvoiceDrafts } from '../src/hooks/useInvoiceDrafts';
import { InvoiceDraftStore, requestInvoiceDraftLeave, type InvoiceDraftData } from '../src/services/invoiceDraftStore';

export function Harness({ userId }: { userId: string }) {
  const [data, setData] = useState<InvoiceDraftData>({ fields: { invoiceNo: '' }, items: [],
    partnerId: null, templateId: null, warehouseBatchId: null, priority: 1, startNumber: 1 });
  const pending = useRef<(() => void) | undefined>(undefined);
  const draft = useInvoiceDrafts(userId, () => data, setData, true);
  const [leave, setLeave] = useState('');
  return <main style={{ padding: 24, fontFamily: 'sans-serif' }}>
    <h1>Kiểm thử INV đang soạn</h1><p>Tài khoản thử: {userId}. Không tạo hóa đơn trên máy chủ.</p>
    <label>Số INV <input disabled={draft.locked} value={String(data.fields.invoiceNo || '')}
      onChange={e => { draft.changed(); setData(prev => ({ ...prev, fields: { invoiceNo: e.target.value } })); }} /></label>
    <label>Số đôi <input disabled={draft.locked} type="number" value={data.items[0]?.quantity ?? 0}
      onChange={e => setData(prev => ({ ...prev, items: [{ styleCode: 'TEST', quantity: Number(e.target.value), processType: 1, unitPriceCMT: 0, unitPriceDAP: 0 }] }))} /></label>
    <p role="status">{draft.error || (draft.locked ? 'Chỉ xem' : draft.savedAt ? 'Đã tự lưu' : 'Chưa nhập')}</p>
    <p>ID máy chủ: {data.orderId ?? 'chưa có'}</p>
    <p>Bản hỏng: {draft.damaged}</p>
    <button onClick={async () => { if (await draft.fresh()) setData({ ...data, fields: { invoiceNo: '' }, items: [], orderId: undefined }); }}>Tạo bản mới</button>
    <button onClick={() => { setLeave('Đã giữ màn hình'); requestInvoiceDraftLeave(() => setLeave('Cho phép rời')); }}>Thử rời màn hình</button>
    <p>{leave}</p>
    <button onClick={() => { pending.current = () => {
      setData(prev => ({ ...prev, orderId: 123 }));
      void draft.recordResult({ orderId: 123, serverBaseline: 'test-server' }, String(data.fields.invoiceNo), 'REAL-123');
    }; }}>Bắt đầu lưu giả lập</button>
    <button onClick={() => pending.current?.()}>Trả kết quả lưu thành công</button>
    <button onClick={() => {
      const original = Storage.prototype.setItem;
      Storage.prototype.setItem = function(key: string, value: string) {
        if (key.startsWith('shoedocx:inv:') && key.includes(encodeURIComponent(userId))) throw new DOMException('Test quota', 'QuotaExceededError');
        original.call(this, key, value);
      };
    }}>Giả lập bộ nhớ đầy</button>
    <button onClick={() => {
      // A fresh same-origin realm also recovers after hot reload while the quota stub was active.
      const frame = document.createElement('iframe'); frame.hidden = true; document.body.append(frame);
      Storage.prototype.setItem = (frame.contentWindow as Window & typeof globalThis).Storage.prototype.setItem;
      frame.remove(); draft.changed();
    }}>Khôi phục bộ nhớ thử</button>
    <button onClick={() => {
      const store = new InvoiceDraftStore(localStorage, new URL('/api', location.origin).href, userId);
      localStorage.setItem(store.prefix + 'draft:broken-test', '{'); draft.refresh();
    }}>Tạo bản hỏng thử nghiệm</button>
    <button onClick={() => { draft.refresh(); }}>Đọc danh sách</button>
    <ul>{draft.drafts.map(d => <li key={d.id}>{String(d.data.fields.invoiceNo || 'Chưa có số')} — {d.data.items[0]?.quantity ?? 0} đôi <button onClick={() => draft.open(d)}>Tiếp tục {String(d.data.fields.invoiceNo)}</button></li>)}</ul>
  </main>;
}
export function App() {
  const [userId, setUserId] = useState('draft-test-user-a');
  return <><button onClick={() => setUserId(userId === 'draft-test-user-a' ? 'draft-test-user-b' : 'draft-test-user-a')}>Đổi tài khoản thử</button><Harness key={userId} userId={userId} /></>;
}
const root = createRoot(document.getElementById('root')!);
root.render(<React.StrictMode><App /></React.StrictMode>);
if (import.meta.hot) import.meta.hot.dispose(() => root.unmount());
