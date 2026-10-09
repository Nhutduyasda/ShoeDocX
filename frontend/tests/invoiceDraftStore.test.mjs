import { test } from 'node:test';
import assert from 'node:assert/strict';
import { InvoiceDraftStore, mergeDraftResult, copyDraftData, serverFingerprint, canResumeServerDraft, requestInvoiceDraftLeave } from '../src/services/invoiceDraftStore.ts';

class MemoryStorage {
  values = new Map();
  get length() { return this.values.size; }
  key(i) { return [...this.values.keys()][i] ?? null; }
  getItem(k) { return this.values.get(k) ?? null; }
  setItem(k, v) { this.values.set(k, v); }
  removeItem(k) { this.values.delete(k); }
}
const data = () => ({ fields: { invoiceNo: '', invoiceDate: '2026-10-09', customerName: 'Test' },
  items: [{ styleCode: '', quantity: 17, processType: 0, sizeBreakdownJson: '{"38":17}', pairPerCarton: 24 }],
  partnerId: 1, templateId: 2, warehouseBatchId: 3, priority: 0, startNumber: 79 });

test('Incomplete fields, size distribution and source metadata survive recreation', () => {
  const storage = new MemoryStorage();
  const store = new InvoiceDraftStore(storage, 'http://localhost/api', 'xnk');
  store.save('one', data());
  assert.deepEqual(new InvoiceDraftStore(storage, 'http://localhost/api', 'xnk').current().data, data());
});
test('Three drafts coexist and active draft is selected independently of invoice number', () => {
  const store = new InvoiceDraftStore(new MemoryStorage(), 'api', 'xnk');
  for (const id of ['a', 'b', 'c']) store.save(id, data());
  assert.equal(store.list().drafts.length, 3);
  assert.equal(store.current().id, 'c');
  store.save('a', data()); assert.equal(store.current().id, 'a');
});
test('Users and backend destinations are isolated', () => {
  const storage = new MemoryStorage();
  new InvoiceDraftStore(storage, 'api-a', 'xnk').save('a', data());
  assert.equal(new InvoiceDraftStore(storage, 'api-a', 'other').current(), undefined);
  assert.equal(new InvoiceDraftStore(storage, 'api-b', 'xnk').current(), undefined);
});
test('Malformed and unsupported records remain untouched without hiding readable drafts', () => {
  const storage = new MemoryStorage(); const store = new InvoiceDraftStore(storage, 'api', 'xnk');
  store.save('good', data());
  storage.setItem(store.prefix + 'draft:broken', '{');
  storage.setItem(store.prefix + 'draft:future', JSON.stringify({ version: 2, id: 'future', data: data() }));
  assert.equal(store.list().damaged, 2);
  assert.equal(store.current().id, 'good');
  assert.equal(storage.getItem(store.prefix + 'draft:broken'), '{');
});
test('Quota failure reports failure and preserves the last successful snapshot', () => {
  const storage = new MemoryStorage(); const store = new InvoiceDraftStore(storage, 'api', 'xnk');
  store.save('a', data());
  storage.setItem = () => { throw new Error('QuotaExceededError'); };
  assert.throws(() => store.save('a', { ...data(), items: [] }));
  assert.equal(store.current().data.items.length, 1);
});
test('Removal only removes the requested local draft', () => {
  const store = new InvoiceDraftStore(new MemoryStorage(), 'api', 'xnk');
  store.save('a', data()); store.save('b', data()); store.remove('b');
  assert.equal(store.current().id, 'a'); assert.equal(store.list().drafts.length, 1);
});
test('Server baseline ignores property order and detects changed quantities or lock state', () => {
  assert.equal(serverFingerprint({ id: 1, items: [{ quantity: 17 }], isLocked: false }),
    serverFingerprint({ isLocked: false, items: [{ quantity: 17 }], id: 1 }));
  assert.notEqual(serverFingerprint({ items: [{ quantity: 17 }] }), serverFingerprint({ items: [{ quantity: 18 }] }));
  assert.notEqual(serverFingerprint({ isLocked: false }), serverFingerprint({ isLocked: true }));
});

test('Selecting an unchanged draft restores it next time without creating another draft', () => {
  const storage = new MemoryStorage(); const store = new InvoiceDraftStore(storage, 'api', 'xnk');
  store.save('a', data()); store.save('b', data()); store.activate('a');
  assert.equal(new InvoiceDraftStore(storage, 'api', 'xnk').current().id, 'a');
  assert.equal(store.list().drafts.length, 2);
});

test('A save response links its real ID and number while keeping edits made during the request', () => {
  const latest = data(); latest.fields.invoiceNo = 'requested'; latest.items[0].quantity = 27;
  const linked = mergeDraftResult(latest, { orderId: 123, serverBaseline: 'baseline' }, 'requested', 'actual');
  assert.equal(linked.orderId, 123); assert.equal(linked.fields.invoiceNo, 'actual');
  assert.equal(linked.items[0].quantity, 27); assert.equal(linked.warehouseBatchId, 3);
  latest.fields.invoiceNo = 'user-edited';
  assert.equal(mergeDraftResult(latest, { orderId: 123 }, 'requested', 'actual').fields.invoiceNo, 'user-edited');
});

test('A delayed response does not switch away from a newer working draft', () => {
  const store = new InvoiceDraftStore(new MemoryStorage(), 'api', 'xnk');
  store.save('old', data()); store.save('new', data());
  store.save('old', { ...data(), orderId: 123 }, false);
  assert.equal(store.current().id, 'new');
  assert.equal(store.list().drafts.find(d => d.id === 'old').data.orderId, 123);
});

test('Copying a saved or locked invoice keeps rows and sizes without reusing its ID or warehouse processing link', () => {
  const original = { ...data(), orderId: 123, serverBaseline: 'locked', completed: true };
  original.fields.invoiceNo = 'INV-123';
  const copy = copyDraftData(original);
  assert.equal(copy.orderId, undefined); assert.equal(copy.serverBaseline, undefined);
  assert.equal(copy.fields.invoiceNo, ''); assert.equal(copy.warehouseBatchId, null);
  assert.deepEqual(copy.items, original.items); assert.equal(copy.completed, false);
  assert.equal(original.fields.invoiceNo, 'INV-123'); assert.equal(original.orderId, 123);
});

test('Restoring an edit requires the unchanged, unlocked server record', () => {
  const order = { id: 123, isLocked: false, items: [{ quantity: 17 }] };
  const draft = { ...data(), orderId: 123, serverBaseline: serverFingerprint(order) };
  assert.equal(canResumeServerDraft(draft, order, false), true);
  assert.equal(canResumeServerDraft(draft, { ...order, items: [{ quantity: 18 }] }, false), false);
  assert.equal(canResumeServerDraft(draft, order, true), false);
  const locked = { ...order, isLocked: true };
  assert.equal(canResumeServerDraft({ ...draft, serverBaseline: serverFingerprint(locked) }, locked, false), false);
  assert.equal(canResumeServerDraft({ ...draft, serverBaseline: undefined }, order, false), false);
});

test('Navigation waits for the unsaved-data guard before invoking its destination', () => {
  const previous = globalThis.window; const target = new EventTarget(); globalThis.window = target;
  try {
    let moved = 0;
    requestInvoiceDraftLeave(() => moved++); assert.equal(moved, 1);
    let resume;
    target.addEventListener('invoice-draft:before-leave', event => { event.preventDefault(); resume = event.detail.proceed; });
    requestInvoiceDraftLeave(() => moved++); assert.equal(moved, 1);
    resume(); assert.equal(moved, 2);
  } finally { if (previous === undefined) delete globalThis.window; else globalThis.window = previous; }
});
