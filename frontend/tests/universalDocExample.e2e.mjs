// Real Identity/MVC/ClosedXML host and real browser. Only synthetic workbook fixtures.
import assert from 'node:assert/strict';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
const moduleUrl = process.env.STUDIO_PLAYWRIGHT_MODULE ? pathToFileURL(join(resolve(process.env.STUDIO_PLAYWRIGHT_MODULE), 'index.mjs')).href : 'playwright';
const { chromium } = await import(moduleUrl);
const base = process.env.STUDIO_BASE_URL || 'http://127.0.0.1:5176';
const folder = resolve(process.env.STUDIO_EVIDENCE_DIR); await mkdir(folder, { recursive: true });
const browser = await chromium.launch({ headless: true }); const context = await browser.newContext({ acceptDownloads: true }); const page = await context.newPage();
const errors = []; const checks = []; page.on('pageerror', e => errors.push(e.name));
const click = name => page.getByRole('button', { name, exact: true }).click();
const state = name => page.getByText(name, { exact: true }).waitFor();
async function review() { await click('5 Human approval'); await click('Complete human review'); await state('ReadyForApproval'); await click('Approve document'); await state('Approved'); }
async function denied(code) { await page.getByRole('alert').filter({ hasText: code }).waitFor(); checks.push(code); }
try {
  await page.goto(base + '/universal-doc-studio'); await page.getByRole('textbox', { name: /Tên đăng nhập/ }).fill('admin-a'); await page.getByLabel(/Mật khẩu/).fill('Studio@Test2026!'); await page.getByRole('button', { name: /Đăng nhập/ }).click();
  await page.getByRole('heading', { name: 'Universal Document Studio', exact: true }).waitFor();
  await page.getByLabel('Template mode').selectOption('example'); await click('Create private session');
  const template = await (await context.request.get(base + '/api/__test/gate5a-template')).body();
  await page.getByLabel('Excel template', { exact: true }).setInputFiles({ name: 'existing.xlsx', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', buffer: template }); await click('Upload and analyze');
  await page.getByRole('heading', { name: '2. Mapping', exact: true }).waitFor();
  const destinations = page.locator('select[aria-label^="Field destination"]'); assert.equal(await destinations.count(), 6);
  let ambiguous = 0;
  for (let i = 0; i < await destinations.count(); i++) { if (await destinations.nth(i).inputValue() === '') ambiguous++; await destinations.nth(i).selectOption('0'); }
  assert.ok(ambiguous > 0); checks.push('AMBIGUOUS_MAPPING_CORRECTED');
  // Inference order: table columns, followed by header fields. Explicitly choose numeric quantity and printed value.
  await page.getByLabel('Field type 3', { exact: true }).selectOption('Decimal'); await page.getByLabel('Field type 4', { exact: true }).selectOption('Decimal'); await page.getByLabel('Field type 6', { exact: true }).selectOption('Decimal');
  await click('Apply schema corrections'); await click('Confirm mapping as current user'); await state('UserConfirmed');
  checks.push('SCHEMA_AND_MAPPING_EXPLICITLY_CONFIRMED');
  await click('3 Demo extraction'); await page.getByLabel('Sample rows', { exact: true }).fill('2');
  const values = [['00042', '00043'], ['Vải tổng hợp', 'sample'], ['2.5', '3'], ['9.5', '10'], ['00123'], ['19.50']];
  for (let f = 0; f < values.length; f++) for (let row = 0; row < values[f].length; row++) await page.getByLabel(`Sample field${f + 1} ${row + 1}`, { exact: true }).fill(values[f][row]);
  await click('Validate manual sample'); await state('Draft'); await review();
  await click('6 Excel export'); const downloading = page.waitForEvent('download'); await click('Export Excel'); const download = await downloading;
  const path = join(folder, 'synthetic-gate5a.xlsx'); await download.saveAs(path);
  const bootstrap = await (await context.request.get(base + '/api/universal-doc/bootstrap')).json();
  const verified = await context.request.post(base + '/api/__test/gate5a-verify', { headers: { 'X-Studio-CSRF': bootstrap.csrfToken, 'Content-Type': 'application/octet-stream' }, data: await readFile(path) });
  assert.equal(verified.status(), 200); assert.equal((await verified.json()).status, 'PASS'); checks.push('REOPEN_VALUES_FORMULAS_FORMATS_MERGES_REFERENCE_PASS');
  await page.getByText('Excel workbook exported.', { exact: true }).waitFor();
  await click('2 Mapping'); await page.getByLabel('Field label 1', { exact: true }).fill('Reviewed product code'); await click('Apply schema corrections'); await state('NeedsReview');
  await click('6 Excel export'); await click('Export Excel'); await denied('MAPPING_CONFIRMATION_REQUIRED');
  await click('2 Mapping'); await click('Confirm mapping as current user'); await state('UserConfirmed');
  await click('6 Excel export'); await click('Export Excel'); await denied('CURRENT_APPROVAL_REQUIRED');
  await review(); await click('6 Excel export'); const again = page.waitForEvent('download'); await click('Export Excel'); await again; checks.push('REVIEW_AFTER_INVALIDATION_REQUIRED');
  assert.deepEqual(errors, []); await page.screenshot({ path: join(folder, 'example-workflow.png'), fullPage: true });
  await writeFile(join(folder, 'example-summary.json'), JSON.stringify({ status: 'PASS', checks, runtimeErrors: errors }, null, 2));
} catch (error) { await page.screenshot({ path: join(folder, 'example-failure.png'), fullPage: true }).catch(() => {}); console.error(error); process.exitCode = 1; }
finally { await context.close(); await browser.close(); }
