// Real authenticated browser, real API mutations and actual downloaded Excel bytes.
import assert from 'node:assert/strict';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
const moduleUrl = process.env.STUDIO_PLAYWRIGHT_MODULE ? pathToFileURL(join(resolve(process.env.STUDIO_PLAYWRIGHT_MODULE), 'index.mjs')).href : 'playwright';
const { chromium } = await import(moduleUrl);
const base = process.env.STUDIO_BASE_URL || 'http://127.0.0.1:5176';
const folder = resolve(process.env.STUDIO_EVIDENCE_DIR); await mkdir(folder, { recursive: true });
const browser = await chromium.launch({ headless: true }); const context = await browser.newContext({ acceptDownloads: true }); const page = await context.newPage();
const errors = []; const checks = []; page.on('pageerror', error => errors.push(error.name));
const click = name => page.getByRole('button', { name, exact: true }).click();
const state = name => page.getByText(name, { exact: true }).waitFor();
async function mutate(button, suffix) {
  const response = page.waitForResponse(r => r.url().endsWith(suffix) && r.request().method() !== 'GET');
  await click(button); const result = await response; assert.equal(result.status(), 200, await result.text()); return result.json();
}
async function deny(code) {
  await click('6 Excel export'); await click('Export Excel'); await page.getByRole('alert').filter({ hasText: code }).waitFor();
}
try {
  await page.goto(base + '/universal-doc-studio'); await page.getByRole('textbox', { name: /Tên đăng nhập/ }).fill('admin-a'); await page.getByLabel(/Mật khẩu/).fill('Studio@Test2026!'); await page.getByRole('button', { name: /Đăng nhập/ }).click();
  await page.getByRole('heading', { name: 'Universal Document Studio', exact: true }).waitFor();
  await page.getByLabel('Template mode').selectOption('example'); await click('Create private session');
  const template = await (await context.request.get(base + '/api/__test/gate5b-template')).body();
  const example = await (await context.request.get(base + '/api/__test/gate5b-example')).body();
  await page.getByLabel('Excel template', { exact: true }).setInputFiles({ name: 'synthetic-template.xlsx', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', buffer: template }); await mutate('Upload and analyze', '/template');
  await page.getByLabel('Filled example', { exact: true }).setInputFiles({ name: 'synthetic-example.xlsx', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', buffer: example });
  let session = await mutate('Compare filled example', '/example');
  await page.getByLabel('Example correspondence', { exact: true }).waitFor();
  assert.equal(session.schemaDraft.correspondence.sheets.length, 2); assert.ok(session.schemaDraft.correspondence.sheets.every(s => s.exampleRowCount === 2)); checks.push('MULTIROW_CORRESPONDENCE');
  let ambiguous = false;
  for (let index = 0; index < session.schemaDraft.fields.length; index++) {
    const field = session.schemaDraft.fields[index];
    if (field.candidates[0].worksheet !== 'Data1') await page.getByLabel(`Field omit ${index + 1}`, { exact: true }).check();
    else {
      if (field.candidate === null) ambiguous = true;
      await page.getByLabel(`Field destination ${index + 1}`, { exact: true }).selectOption('0');
      await page.getByLabel(`Field type ${index + 1}`, { exact: true }).selectOption(field.label === 'Item ID' ? 'Text' : 'Decimal');
    }
  }
  assert.ok(ambiguous); session = await mutate('Apply schema corrections', '/schema'); checks.push('AMBIGUOUS_FIELDS_EXPLICITLY_CORRECTED');
  const fields = session.schemaDraft.fields.filter(f => !f.omit); const key = label => fields.find(f => f.label === label).key;
  await click('Add data worksheet'); await page.getByLabel('Mapped worksheet 2', { exact: true }).selectOption('Data2'); await page.getByLabel('Mapped prototype 2', { exact: true }).fill('4');
  for (const field of fields) await page.getByLabel(`Map Data2 ${field.key}`, { exact: true }).fill(String(field.scope === 'Header' ? 'B1' : fields.filter(f => f.scope === 'LineItem').indexOf(field) + 1));
  session = await mutate('Apply complete worksheet mapping', '/multisheet-mapping'); assert.equal(session.mappings.length, 2); checks.push('SHARED_FIELDS_ACROSS_TWO_SHEETS');
  await click('Add validation rule'); await page.getByLabel('Rule A 1', { exact: true }).selectOption(key('Quantity')); await page.getByLabel('Rule B 1', { exact: true }).selectOption(key('Price')); await page.getByLabel('Rule target 1', { exact: true }).selectOption(key('Amount'));
  await click('Add validation rule'); await page.getByLabel('Rule type 2', { exact: true }).selectOption('CrossFoot'); await page.getByLabel('Rule A 2', { exact: true }).selectOption(key('Amount')); await page.getByLabel('Rule target 2', { exact: true }).selectOption(key('Printed total'));
  await mutate('Apply validation rules', '/validation-rules'); await mutate('Confirm mapping as current user', '/mapping/confirm'); await state('UserConfirmed'); checks.push('NO_CODE_RULES_AND_MAPPING_CONFIRMED');
  await click('3 Demo extraction'); await page.getByLabel('Sample rows', { exact: true }).fill('2');
  const values = { 'Item ID': ['0001', '0002'], Quantity: ['2.5', '2.5'], Price: ['3', '3'], Amount: ['8', '7.5'], 'Printed total': ['15'] };
  for (const field of fields) for (let row = 0; row < values[field.label].length; row++) await page.getByLabel(`Sample ${field.key} ${row + 1}`, { exact: true }).fill(values[field.label][row]);
  session = await mutate('Validate manual sample', '/manual-sample'); assert.ok(session.checks.some(c => c.outcome === 'Failed')); await deny('DOCUMENT_VALIDATION_REQUIRED'); checks.push('INVALID_AMOUNT_BLOCKS_EXPORT');
  await click('3 Demo extraction'); await page.getByLabel(`Sample ${key('Amount')} 1`, { exact: true }).fill('7.5'); session = await mutate('Validate manual sample', '/manual-sample'); assert.ok(session.checks.filter(c => c.required).every(c => c.outcome === 'Passed'));
  await click('4 Data review'); await page.getByLabel('Validation results', { exact: true }).waitFor();
  await click('5 Human approval'); await mutate('Complete human review', '/review'); await state('ReadyForApproval'); await mutate('Approve document', '/approve'); await state('Approved'); checks.push('CORRECTED_DATA_REVIEWED_AND_APPROVED');
  await click('6 Excel export'); const downloading = page.waitForEvent('download'); await click('Export Excel'); const download = await downloading; const path = join(folder, 'synthetic-gate5b.xlsx'); await download.saveAs(path);
  await page.getByText('Excel workbook exported.', { exact: true }).waitFor();
  const bootstrap = await (await context.request.get(base + '/api/universal-doc/bootstrap')).json();
  const verified = await context.request.post(base + '/api/__test/gate5b-verify', { headers: { 'X-Studio-CSRF': bootstrap.csrfToken, 'Content-Type': 'application/octet-stream' }, data: await readFile(path) });
  assert.equal(verified.status(), 200); assert.equal((await verified.json()).status, 'PASS'); checks.push('DOWNLOADED_WORKBOOK_REOPENED');
  await page.reload(); await page.getByRole('heading', { name: 'Universal Document Studio', exact: true }).waitFor(); await state('Approved'); checks.push('BROWSER_REFRESH_RESTORES_SERVER_STATE');
  await click('2 Mapping'); await page.getByLabel('Rule tolerance 1', { exact: true }).fill('0.01'); session = await mutate('Apply validation rules', '/validation-rules'); assert.equal(session.reviewState, 'NeedsReview');
  await deny('MAPPING_CONFIRMATION_REQUIRED'); await click('2 Mapping'); await mutate('Confirm mapping as current user', '/mapping/confirm'); await deny('CURRENT_APPROVAL_REQUIRED'); checks.push('RULE_CHANGE_REQUIRES_RENEWED_CONFIRMATION_REVIEW_APPROVAL');
  await page.setViewportSize({ width: 390, height: 844 }); await click('2 Mapping');
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1));
  await page.screenshot({ path: join(folder, 'gate5b-mobile-top.png'), fullPage: true });
  const mobileMapping = page.getByLabel('Mapped worksheet 1', { exact: true }); await mobileMapping.scrollIntoViewIfNeeded();
  const mappingBox = await mobileMapping.boundingBox(); assert.ok(mappingBox && mappingBox.x >= 0 && mappingBox.x + mappingBox.width <= 391);
  assert.equal(await mobileMapping.inputValue(), 'Data1');
  const mobileRule = page.getByLabel('Rule A 1', { exact: true }); await mobileRule.scrollIntoViewIfNeeded();
  const ruleBox = await mobileRule.boundingBox(); assert.ok(ruleBox && ruleBox.x >= 0 && ruleBox.x + ruleBox.width <= 391);
  assert.equal(await mobileRule.inputValue(), key('Quantity'));
  await page.screenshot({ path: join(folder, 'gate5b-mobile.png'), fullPage: true }); checks.push('MOBILE_LAYOUT');
  assert.deepEqual(errors, []); assert.equal(checks.length, 10);
  await writeFile(join(folder, 'gate5b-summary.json'), JSON.stringify({ status: 'PASS', checks, runtimeErrors: errors }, null, 2));
} catch (error) { await page.screenshot({ path: join(folder, 'gate5b-failure.png'), fullPage: true }).catch(() => {}); console.error(error); process.exitCode = 1; }
finally { await context.close(); await browser.close(); }
