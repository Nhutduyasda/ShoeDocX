// Real UI + existing AuthController + production Studio controllers. No route/response mocks.
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
const moduleUrl = process.env.STUDIO_PLAYWRIGHT_MODULE
  ? pathToFileURL(join(resolve(process.env.STUDIO_PLAYWRIGHT_MODULE), 'index.mjs')).href : 'playwright';
const { chromium } = await import(moduleUrl);
const base = process.env.STUDIO_BASE_URL || 'http://127.0.0.1:5176';
const evidence = resolve(process.env.STUDIO_EVIDENCE_DIR || join(tmpdir(), 'shoedocx-gate4a-browser'));
await mkdir(evidence, { recursive: true });
const downloads = await mkdtemp(join(tmpdir(), 'studio-synthetic-downloads-'));
const browser = await chromium.launch({ headless: true, ...(process.env.STUDIO_CHROMIUM_EXECUTABLE ? { executablePath: process.env.STUDIO_CHROMIUM_EXECUTABLE } : {}) });
const context = await browser.newContext({ acceptDownloads: true, viewport: { width: 1360, height: 1000 } });
const page = await context.newPage();
const errors = []; const responses = []; const checks = [];
let createdSessionId;
page.on('pageerror', error => errors.push(error.name));
page.on('response', response => {
  const url = new URL(response.url());
  if (url.pathname.startsWith('/api/universal-doc')) responses.push({ path: url.pathname.replace(/[A-F0-9]{64}/g, '<session>'), status: response.status() });
});
const click = name => page.getByRole('button', { name, exact: true }).click();
const state = text => page.getByText(text, { exact: true }).waitFor({ state: 'visible' });
async function denied(code) { await page.getByRole('alert').filter({ hasText: code }).waitFor({ state: 'visible' }); checks.push(code); }
async function review() {
  await click('5 Human approval'); await click('Complete human review'); await state('ReadyForApproval');
  await click('Approve document'); await state('Approved');
}
async function exported(name) {
  await click('6 Excel export'); const promise = page.waitForEvent('download'); await click('Export Excel');
  const download = await promise; const path = join(downloads, name); await download.saveAs(path);
  const bytes = await readFile(path); assert.equal(bytes.subarray(0, 2).toString(), 'PK'); assert.ok(bytes.length > 1000);
  await page.getByText('Excel workbook exported.', { exact: true }).waitFor(); checks.push('REAL_XLSX_DOWNLOAD');
}
try {
  await page.goto(base + '/universal-doc-studio?mode=demo');
  await page.getByRole('textbox', { name: /Tên đăng nhập/ }).fill('admin-a');
  await page.getByLabel(/Mật khẩu/).fill('Studio@Test2026!');
  await page.getByRole('button', { name: /Đăng nhập/ }).click();
  await page.getByRole('heading', { name: 'Universal Document Studio', exact: true }).waitFor();
  // Direct pathname refresh retains authenticated routing, without replacing tab navigation.
  await page.reload(); await page.getByRole('heading', { name: 'Universal Document Studio', exact: true }).waitFor();
  checks.push('AUTHENTICATED_DIRECT_ROUTE_REFRESH');
  const createdResponse = page.waitForResponse(r => r.url().endsWith('/api/universal-doc/sessions') && r.request().method() === 'POST');
  await click('Create private session'); const created = await (await createdResponse).json(); createdSessionId = created.id;
  const samplePromise = page.waitForEvent('download'); await click('Download synthetic template');
  const sample = await samplePromise; const template = join(downloads, 'synthetic-template.xlsx'); await sample.saveAs(template);
  await page.getByLabel('Excel template', { exact: true }).setInputFiles(template); await click('Upload and analyze');
  await page.getByRole('heading', { name: '2. Mapping', exact: true }).waitFor();
  await page.getByText('Detected worksheets:', { exact: false }).waitFor(); checks.push('ACTUAL_TEMPLATE_ANALYSIS');
  await click('6 Excel export'); await click('Export Excel'); await denied('MAPPING_CONFIRMATION_REQUIRED');
  await click('2 Mapping'); await click('Confirm mapping as current user'); await state('UserConfirmed');
  await click('3 Demo extraction'); await click('Run Demo Extraction'); await state('Draft');
  await click('4 Data review'); assert.equal(await page.getByRole('textbox', { name: 'Header number', exact: true }).inputValue(), 'DEMO-0001');
  assert.equal(await page.getByRole('textbox', { name: 'LineItem row 1 description', exact: true }).inputValue(), 'Vải tổng hợp');
  checks.push('DEMO_DATA_AND_SOURCE_EVIDENCE_RENDERED');
  await page.getByRole('textbox', { name: 'Header number', exact: true }).fill('DEMO-BROWSER-EDIT'); await click('Save Header number'); await state('NeedsReview');
  await review(); await exported('first.xlsx');
  await click('4 Data review'); await page.getByRole('textbox', { name: 'Header number', exact: true }).fill('DEMO-AFTER-APPROVAL'); await click('Save Header number'); await state('NeedsReview');
  await click('6 Excel export'); await click('Export Excel'); await denied('CURRENT_APPROVAL_REQUIRED');
  await review(); await exported('second.xlsx');
  await click('2 Mapping'); await page.getByRole('textbox', { name: 'Mapping number', exact: true }).fill('C1'); await click('Apply mapping correction');
  await state('DraftSuggested'); await state('NeedsReview'); checks.push('MAPPING_EDIT_INVALIDATES_APPROVAL');
  await click('6 Excel export'); await click('Export Excel'); await denied('MAPPING_CONFIRMATION_REQUIRED');
  await page.screenshot({ path: join(evidence, 'mapping-edit-denied.png'), fullPage: true });
  await click('2 Mapping'); await click('Confirm mapping as current user'); await state('UserConfirmed');
  await click('6 Excel export'); await click('Export Excel'); await denied('CURRENT_APPROVAL_REQUIRED');
  // Actual authenticated HTTP attack against the same backend; no client-state substitutions.
  const other = await browser.newContext();
  const login = await other.request.post(base + '/api/auth/login', { data: { username: 'admin-b', password: 'Studio@Test2026!' } }); assert.equal(login.status(), 200);
  const attack = await other.request.get(`${base}/api/universal-doc/sessions/${created.id}`, { headers: { 'X-Tenant-Id': 'tenant-a' } }); assert.equal(attack.status(), 404);
  const bootstrap = await (await other.request.get(base + '/api/universal-doc/bootstrap')).json();
  const forgedBody = await other.request.post(base + '/api/universal-doc/sessions', { headers: { 'X-Studio-CSRF': bootstrap.csrfToken }, data: { workflow: 'invoice', tenantId: 'tenant-a' } });
  assert.equal(forgedBody.status(), 400); checks.push('CROSS_TENANT_HEADER_ATTACK_404', 'FORGED_TENANT_BODY_400'); await other.close();
  await review(); await exported('third.xlsx');
  await page.screenshot({ path: join(evidence, 'studio-export-success.png'), fullPage: true });
  assert.deepEqual(errors, []);
  await writeFile(join(evidence, 'browser-summary.json'), JSON.stringify({ status: 'PASS', checks, runtimeErrors: errors, responses }, null, 2));
  console.log(JSON.stringify({ status: 'PASS', checks: checks.length, runtimeErrors: errors.length }));
} catch (error) {
  await page.screenshot({ path: join(evidence, 'studio-failure.png'), fullPage: true }).catch(() => {});
  console.error(error); process.exitCode = 1;
} finally {
  if (createdSessionId) {
    const response = await context.request.get(`${base}/api/universal-doc/sessions/${createdSessionId}`).catch(() => null);
    if (response?.ok()) {
      const snapshot = await response.json(); const bootstrap = await (await context.request.get(base + '/api/universal-doc/bootstrap')).json();
      await context.request.delete(`${base}/api/universal-doc/sessions/${createdSessionId}`, {
        headers: { 'X-Studio-CSRF': bootstrap.csrfToken }, data: { expectedVersion: snapshot.version },
      });
    }
  }
  await context.close(); await browser.close();
}
