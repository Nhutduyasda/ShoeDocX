import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { message, Modal } from 'antd';
import { apiClient } from '../api/client';
import { InvoiceDraftStore, mergeDraftResult, copyDraftData, type InvoiceDraft, type InvoiceDraftData } from '../services/invoiceDraftStore';

const restoreNotices = new Set<string>();
const confirmUnsaved = () => new Promise<boolean>(resolve => Modal.confirm({
  title: 'INV chưa được tự lưu', content: 'Rời bản hiện tại có thể mất dữ liệu vừa nhập. Bạn muốn tiếp tục rời đi?',
  okText: 'Vẫn rời đi', cancelText: 'Ở lại', onOk: () => resolve(true), onCancel: () => resolve(false),
}));

export function useInvoiceDrafts(userId: string | undefined, capture: () => InvoiceDraftData,
  restore: (data: InvoiceDraftData) => void, enabled: boolean) {
  const [store] = useState(() => {
    try { return userId ? new InvoiceDraftStore(localStorage,
      new URL(apiClient.defaults.baseURL || '/api', location.origin).href, userId) : null; }
    catch { return null; }
  });
  const [id, setId] = useState<string>(() => crypto.randomUUID());
  const [ready, setReady] = useState(false);
  const [locked, setLocked] = useState(true);
  const [error, setError] = useState(!store && enabled ? 'Trình duyệt đang chặn bộ nhớ. INV chưa được tự lưu trên máy.' : '');
  const [savedAt, setSavedAt] = useState('');
  const [drafts, setDrafts] = useState<InvoiceDraft[]>([]);
  const [damaged, setDamaged] = useState(0);
  const current = useRef({ capture, restore, id, enabled, ready, locked });
  const last = useRef('');
  const skip = useRef(false);
  const hasInput = useRef(false);
  const resultLink = useRef<{ patch: Partial<InvoiceDraftData>; requested: string; actual?: string } | undefined>(undefined);
  const mounted = useRef(true);
  const ownedId = useRef<string | undefined>(undefined);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  useLayoutEffect(() => { current.current = { capture, restore, id, enabled, ready, locked }; });
  const refresh = useCallback(() => {
    try {
      const result = store?.list();
      setDrafts(result?.drafts ?? []); setDamaged(result?.damaged ?? 0);
    } catch { setError('Không đọc được bản đang soạn trên máy.'); }
  }, [store]);
  const persist = useCallback((force = false) => {
    const state = current.current;
    if (!state.enabled) return true;
    if (!store) return false;
    if (!state.ready || state.locked || skip.current) return true;
    const captured = state.capture();
    const link = resultLink.current;
    const data = link ? mergeDraftResult(captured, link.patch, link.requested, link.actual) : captured;
    const text = JSON.stringify(data);
    if (!force && text === last.current) return true;
    if (!hasInput.current && !data.items.length) { last.current = text; return true; }
    try {
      const draft = store.save(state.id, data);
      last.current = text; setSavedAt(draft.updatedAt); setError('');
      return true;
    } catch {
      setError('Chưa tự lưu được trên máy. Bộ nhớ có thể đã đầy hoặc bị chặn. Hãy giữ màn hình này và lưu đơn hàng khi có thể.');
      return false;
    }
  }, [store]);
  const changed = () => { hasInput.current = true; persist(true); };
  const open = async (draft: InvoiceDraft, copy = false) => {
    if (!persist() && !await confirmUnsaved()) return false;
    skip.current = true;
    resultLink.current = undefined;
    if (!copy) {
      try { store?.activate(draft.id); }
      catch { setError('Không cập nhật được bản đang soạn gần nhất trên máy.'); }
    }
    const data = copy ? copyDraftData(draft.data) : draft.data;
    if (copy) message.info('Đã sao chép dữ liệu. Hãy nhập số INV mới trước khi lưu đơn hàng.');
    setId(copy ? crypto.randomUUID() : draft.id);
    setLocked(true);
    hasInput.current = true; last.current = copy ? '' : JSON.stringify(data);
    current.current.restore(data);
    setSavedAt(copy ? '' : draft.updatedAt);
    queueMicrotask(() => { skip.current = false; });
    return true;
  };
  const fresh = async (remember = false) => {
    if (!persist() && !await confirmUnsaved()) return false;
    skip.current = true;
    resultLink.current = undefined;
    setId(crypto.randomUUID()); setLocked(true); setSavedAt('');
    hasInput.current = remember; last.current = '';
    queueMicrotask(() => { skip.current = false; });
    return true;
  };
  const copyCurrent = () => open({ version: 1, id, updatedAt: new Date().toISOString(), data: capture() }, true);
  // Attach the server response to the initiating draft, including edits made while waiting.
  const recordResult = async (patch: Partial<InvoiceDraftData>, requestedInvoiceNo: string, actualInvoiceNo?: string) => {
    if (!store) return;
    const owns = mounted.current && current.current.id === id && ownedId.current === id;
    if (owns) resultLink.current = { patch, requested: requestedInvoiceNo, actual: actualInvoiceNo };
    const write = () => {
      try {
        const data = owns ? current.current.capture() : store.list().drafts.find(d => d.id === id)?.data;
        if (!data) return;
        const merged = mergeDraftResult(data, patch, requestedInvoiceNo, actualInvoiceNo);
        const saved = store.save(id, merged, owns);
        if (owns) { last.current = JSON.stringify(merged); setSavedAt(saved.updatedAt); setError(''); }
      } catch { if (mounted.current) setError('Hóa đơn đã lưu trên hệ thống nhưng chưa cập nhật được bản trên máy. Hãy giữ màn hình này.'); }
    };
    if (owns) write();
    else if (navigator.locks) await navigator.locks.request(store.prefix + 'lock:' + id, { ifAvailable: true }, lock => { if (lock) write(); });
  };
  useEffect(() => {
    if (!store || !enabled) { setReady(true); setLocked(false); return; }
    try {
      const draft = store.current();
      if (draft) {
        setId(draft.id); hasInput.current = true; last.current = JSON.stringify(draft.data);
        current.current.restore(draft.data); setSavedAt(draft.updatedAt);
        const noticeKey = store.prefix + draft.id;
        if (!restoreNotices.has(noticeKey)) {
          restoreNotices.add(noticeKey);
          message.info({ key: 'inv-draft-restored', content: 'Đã khôi phục INV đang soạn trên máy.' });
        }
      }
      refresh();
    } catch { setError('Không đọc được bản đang soạn. Dữ liệu cũ vẫn được giữ trên máy.'); }
    setReady(true);
  }, [store, enabled, refresh]);
  useEffect(() => {
    if (!ready || !store || !enabled) return;
    let release: (() => void) | undefined;
    let disposed = false;
    const controller = new AbortController();
    const name = store.prefix + 'lock:' + id;
    // Browser Web Locks provides atomic ownership across tabs; never guess ownership.
    if (!navigator.locks) { setError('Trình duyệt chưa hỗ trợ khóa bản đang soạn. Hãy dùng Chrome hoặc Edge mới để nhập an toàn.'); return; }
    void navigator.locks.request(name, { signal: controller.signal }, async lock => {
      if (!lock || disposed) return;
      ownedId.current = id;
      try {
        // The previous owner may have edited while this tab waited. Reload before allowing writes.
        const latest = store.list().drafts.find(d => d.id === id);
        if (latest && JSON.stringify(latest.data) !== last.current) {
          resultLink.current = undefined;
          last.current = JSON.stringify(latest.data); hasInput.current = true;
          current.current.restore(latest.data); setSavedAt(latest.updatedAt);
        }
        setLocked(false);
      } catch { setError('Không đọc được phiên bản mới nhất. Bản này tạm thời chỉ xem để tránh ghi đè.'); }
      await new Promise<void>(resolve => { release = resolve; if (disposed) resolve(); });
    }).catch(() => { if (!disposed) setError('Không lấy được quyền sửa bản đang soạn.'); });
    return () => {
      disposed = true; controller.abort();
      if (ownedId.current === id) ownedId.current = undefined;
      release?.();
    };
  }, [id, ready, store, enabled]);
  // Run after React commits every row/state change; header changes also persist in onValuesChange.
  useLayoutEffect(() => { if (ready && !locked) persist(); });
  useEffect(() => {
    const flush = () => { persist(); };
    const guard = (event: Event) => {
      if (persist()) return;
      event.preventDefault();
      const detail = (event as CustomEvent<{ proceed?: () => void; blocked?: boolean }>).detail;
      queueMicrotask(() => {
        if (detail?.blocked) return;
        void confirmUnsaved().then(leave => { if (leave && !detail?.blocked) detail?.proceed?.(); });
      });
    };
    const unload = (event: BeforeUnloadEvent) => { if (!persist()) { event.preventDefault(); event.returnValue = ''; } };
    const storage = (event: StorageEvent) => { if (event.key?.startsWith(store?.prefix || 'shoedocx:inv:')) refresh(); };
    window.addEventListener('invoice-draft:before-leave', guard);
    window.addEventListener('beforeunload', unload);
    window.addEventListener('pagehide', flush);
    document.addEventListener('visibilitychange', flush);
    window.addEventListener('storage', storage);
    return () => {
      flush(); window.removeEventListener('invoice-draft:before-leave', guard);
      window.removeEventListener('beforeunload', unload); window.removeEventListener('pagehide', flush);
      document.removeEventListener('visibilitychange', flush); window.removeEventListener('storage', storage);
    };
  }, [store, ready, locked, persist, refresh]);
  return { id, ready, locked, error, savedAt, drafts, damaged, changed, persist, open, fresh, copyCurrent, refresh, recordResult,
    findOrder: (orderId: number) => {
      try { return store?.list().drafts.find(d => d.data.orderId === orderId); }
      catch { setError('Không đọc được bản đang soạn trên máy.'); return undefined; }
    },
    remove: async (draftId: string) => {
      if (!store || !navigator.locks) return;
      if (ownedId.current === draftId) {
        try { store.remove(draftId); refresh(); }
        catch { setError('Không xóa được bản đang soạn trên máy.'); }
        return;
      }
      await navigator.locks.request(store.prefix + 'lock:' + draftId, { ifAvailable: true }, lock => {
        if (!lock) { message.warning('Bản này đang được mở ở tab khác. Hãy đóng bản đó trước khi xóa.'); return; }
        try { store.remove(draftId); refresh(); }
        catch { setError('Không xóa được bản đang soạn trên máy.'); }
      });
    } };
}
