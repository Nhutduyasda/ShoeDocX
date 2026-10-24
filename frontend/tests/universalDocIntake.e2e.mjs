// Real authenticated UI and production intake endpoints; synthetic bytes, no response mocks.
import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { pathToFileURL } from 'node:url';
const moduleUrl = process.env.STUDIO_PLAYWRIGHT_MODULE
  ? pathToFileURL(join(resolve(process.env.STUDIO_PLAYWRIGHT_MODULE), 'index.mjs')).href : 'playwright';
const { chromium } = await import(moduleUrl);
const base = process.env.STUDIO_BASE_URL || 'http://127.0.0.1:5176';
const evidence = resolve(process.env.STUDIO_EVIDENCE_DIR || join(tmpdir(), 'shoedocx-gate4b1-browser'));
await mkdir(evidence, { recursive: true });
const browser = await chromium.launch({ headless: true, ...(process.env.STUDIO_CHROMIUM_EXECUTABLE ? { executablePath: process.env.STUDIO_CHROMIUM_EXECUTABLE } : {}) });
const context = await browser.newContext({ viewport: { width: 1360, height: 1000 } });
const page = await context.newPage(); const checks = []; const runtimeErrors = []; const externalRequests = [];
page.on('pageerror', error => runtimeErrors.push(error.name));
page.on('request', request => { if (!request.url().startsWith(base) && !request.url().startsWith('blob:') && !request.url().startsWith('data:')) externalRequests.push(new URL(request.url()).origin); });
const click = name => page.getByRole('button', { name, exact: true }).click();
const state = text => page.getByText(text, { exact: true }).waitFor();
const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4//8/AAX+Av4N70a4AAAAAElFTkSuQmCC', 'base64');
let sessionId;
async function current() { const response = await context.request.get(`${base}/api/universal-doc/sessions/${sessionId}`); assert.equal(response.status(), 200); return response.json(); }
async function cycle() {
  await click('3 Demo extraction'); await click('Run Demo Extraction'); await state('Draft');
  await click('5 Human approval'); await click('Complete human review'); await state('ReadyForApproval');
  await click('Approve document'); await state('Approved');
}
async function exportDenied() {
  await click('6 Excel export'); await click('Export Excel'); await page.getByRole('alert').filter({ hasText: 'EXTRACTION_REQUIRED' }).waitFor();
}
try {
  await page.goto(base + '/universal-doc-studio?mode=demo');
  await page.getByRole('textbox', { name: /Tên đăng nhập/ }).fill('admin-a'); await page.getByLabel(/Mật khẩu/).fill('Studio@Test2026!');
  await page.getByRole('button', { name: /Đăng nhập/ }).click(); await page.getByRole('heading', { name: 'Universal Document Studio', exact: true }).waitFor();
  const created = page.waitForResponse(r => r.url().endsWith('/api/universal-doc/sessions') && r.request().method() === 'POST');
  await click('Create private session'); sessionId = (await (await created).json()).id;
  const template = await context.request.get(base + '/api/universal-doc/templates/invoice'); assert.equal(template.status(), 200);
  await page.getByLabel('Excel template', { exact: true }).setInputFiles({ name: 'synthetic.xlsx', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', buffer: await template.body() });
  await click('Upload and analyze'); await click('Confirm mapping as current user'); await state('UserConfirmed'); await cycle();
  checks.push('EXISTING_CONFIRMED_DEMO_REVIEW_APPROVAL');
  await click('3 Demo extraction'); await page.getByLabel('Source document', { exact: true }).setInputFiles({ name: 'synthetic.png', mimeType: 'image/png', buffer: png });
  const uploaded = page.waitForResponse(r => r.url().endsWith('/sources') && r.request().method() === 'POST'); await click('Upload source');
  const response = await uploaded; assert.equal(response.status(), 200); const snapshot = await response.json(); const source = snapshot.sources[0];
  assert.equal(source.byteLength, png.length); assert.equal(source.mediaType, 'image/png'); assert.equal(source.width, 1); assert.equal(source.height, 1); assert.equal(source.pageCount, null);
  const metadata = page.getByLabel('Server source metadata'); await metadata.getByText(source.sourceId, { exact: true }).waitFor();
  await metadata.getByText(source.sha256, { exact: true }).waitFor(); await metadata.getByText('image/png · 69 bytes', { exact: true }).waitFor();
  await metadata.getByText('Admitted — not recognized', { exact: true }).waitFor(); await state('NoDocument');
  assert.equal(snapshot.fields.length, 0); checks.push('REAL_SOURCE_UPLOAD_VERIFIED_METADATA', 'NO_OCR_CLAIM_OR_FAKE_FIELDS', 'UPLOAD_INVALIDATES_APPROVAL');
  await metadata.screenshot({ path: join(evidence, 'source-metadata-desktop.png'), style: '.uds-sources dd:first-of-type { visibility: hidden; }' });
  await page.setViewportSize({ width: 390, height: 844 });
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1));
  await metadata.scrollIntoViewIfNeeded();
  await page.screenshot({ path: join(evidence, 'source-metadata-phone.png'), style: '.uds-sources dd:first-of-type { visibility: hidden; }' });
  checks.push('PHONE_LAYOUT_NO_HORIZONTAL_OVERFLOW'); await page.setViewportSize({ width: 1360, height: 1000 });
  await exportDenied(); checks.push('EXPORT_DENIED_AFTER_UPLOAD');
  // A complex content PDF is explicitly unsupported, never accepted as recognized.
  await click('3 Demo extraction'); await page.getByLabel('Source document', { exact: true }).setInputFiles({ name: 'complex.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4\n/JavaScript /URI (https://example.invalid)\n%%EOF\n') });
  await click('Upload source'); await page.getByRole('alert').filter({ hasText: 'SOURCE_PDF_SUBSET_UNSUPPORTED' }).waitFor();
  checks.push('COMPLEX_PDF_EXPLICIT_UNSUPPORTED');
  const other = await browser.newContext();
  assert.equal((await other.request.post(base + '/api/auth/login', { data: { username: 'admin-b', password: 'Studio@Test2026!' } })).status(), 200);
  const otherBootstrap = await (await other.request.get(base + '/api/universal-doc/bootstrap')).json();
  const headers = { 'X-Tenant-Id': 'tenant-a', 'X-Studio-CSRF': otherBootstrap.csrfToken };
  assert.equal((await other.request.get(`${base}/api/universal-doc/sessions/${sessionId}/sources`, { headers })).status(), 404);
  assert.equal((await other.request.delete(`${base}/api/universal-doc/sessions/${sessionId}/sources/${source.sourceId}`, { headers, data: { expectedVersion: snapshot.version } })).status(), 404);
  assert.equal((await other.request.post(`${base}/api/universal-doc/sessions/${sessionId}/sources`, { headers, multipart: { expectedVersion: String(snapshot.version), file: { name: 'synthetic.png', mimeType: 'image/png', buffer: png } } })).status(), 404);
  await other.close(); checks.push('CROSS_TENANT_READ_UPLOAD_DELETE_DENIED');
  await cycle(); await click('3 Demo extraction'); await click('Remove source'); await state('NoDocument');
  assert.equal((await current()).sources.length, 0); await exportDenied(); checks.push('DELETE_INVALIDATES_NEW_APPROVAL_AND_EXPORT');
  await click('3 Demo extraction'); await page.getByLabel('Source document', { exact: true }).setInputFiles({ name: 'synthetic.png', mimeType: 'image/png', buffer: png });
  await click('Upload source'); await metadata.getByText('Admitted — not recognized', { exact: true }).waitFor();
  const bootstrap = await (await context.request.get(base + '/api/universal-doc/bootstrap')).json();
  assert.equal((await context.request.post(base + '/api/__test/expire-sessions', { headers: { 'X-Studio-CSRF': bootstrap.csrfToken } })).status(), 204);
  const expired = await context.request.get(`${base}/api/universal-doc/sessions/${sessionId}/sources`); assert.ok([404, 410].includes(expired.status()));
  assert.ok(!(await expired.text()).includes(source.sha256)); checks.push('DETERMINISTIC_EXPIRED_SOURCE_METADATA_DENIED');
  assert.deepEqual(externalRequests, []); assert.deepEqual(runtimeErrors, []); checks.push('NO_EXTERNAL_REQUESTS_OR_RAW_DOCUMENT_PREVIEW');
  await writeFile(join(evidence, 'intake-summary.json'), JSON.stringify({ status: 'PASS', checks, runtimeErrors, externalRequests }, null, 2));
  console.log(JSON.stringify({ status: 'PASS', checks: checks.length, runtimeErrors: runtimeErrors.length }));
} catch (error) {
  await page.screenshot({ path: join(evidence, 'synthetic-intake-failure.png'), fullPage: true, style: '.uds-sources dd:first-of-type { visibility: hidden; }' }).catch(() => {});
  console.error(error); process.exitCode = 1;
} finally { await context.close(); await browser.close(); }
