import assert from 'node:assert/strict';
import { mkdir, readFile, writeFile, access } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
const { chromium }=await import(pathToFileURL(join(resolve(process.env.STUDIO_PLAYWRIGHT_MODULE),'index.mjs')).href);
const base=process.env.STUDIO_BASE_URL,folder=resolve(process.env.STUDIO_EVIDENCE_DIR);await mkdir(folder,{recursive:true});
const browser=await chromium.launch({headless:true});let context=await browser.newContext({acceptDownloads:true});let page=await context.newPage();const errors=[],checks=[];
page.on('pageerror',e=>errors.push(e.name));
const click=name=>page.getByRole('button',{name,exact:true}).click();
async function mutate(name,suffix){const pending=page.waitForResponse(r=>r.url().endsWith(suffix)&&r.request().method()!=='GET');await click(name);const r=await pending;assert.equal(r.status(),200);return r.json();}
async function login(p,name){await p.goto(base+'/universal-doc-studio');await p.getByRole('textbox',{name:/Tên đăng nhập/}).fill(name);await p.getByLabel(/Mật khẩu/).fill('Studio@Test2026!');await p.getByRole('button',{name:/Đăng nhập/}).click();await p.getByRole('heading',{name:'Universal Document Studio',exact:true}).waitFor();}
try{
 await login(page,'admin-a');await page.getByRole('heading',{name:'My drafts',exact:true}).waitFor();checks.push('AUTHENTICATED_MY_DRAFTS');
 await page.getByLabel('Template mode').selectOption('example');await click('Create private session');
 const template=await(await context.request.get(base+'/api/__test/gate5c-template')).body(),example=await(await context.request.get(base+'/api/__test/gate5c-example')).body();
 await page.getByLabel('Excel template',{exact:true}).setInputFiles({name:'template.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:template});await mutate('Upload and analyze','/template');
 await page.getByLabel('Filled example',{exact:true}).setInputFiles({name:'example.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:example});let s=await mutate('Compare filled example','/example');
 for(let i=0;i<s.schemaDraft.fields.length;i++){const f=s.schemaDraft.fields[i];if(f.candidates[0].worksheet!=='Data1')await page.getByLabel(`Field omit ${i+1}`,{exact:true}).check();else{await page.getByLabel(`Field destination ${i+1}`,{exact:true}).selectOption('0');await page.getByLabel(`Field type ${i+1}`,{exact:true}).selectOption(f.label==='Item ID'?'Text':'Decimal');}}
 assert.ok(await page.getByRole('button',{name:'Save draft',exact:true}).isDisabled());s=await mutate('Apply schema corrections','/schema');
 const fields=s.schemaDraft.fields.filter(f=>!f.omit),key=label=>fields.find(f=>f.label===label).key;
 await click('Add data worksheet');await page.getByLabel('Mapped worksheet 2',{exact:true}).selectOption('Data2');await page.getByLabel('Mapped prototype 2',{exact:true}).fill('4');
 for(const f of fields)await page.getByLabel(`Map Data2 ${f.key}`,{exact:true}).fill(String(f.scope==='Header'?'B1':fields.filter(x=>x.scope==='LineItem').indexOf(f)+1));
 s=await mutate('Apply complete worksheet mapping','/multisheet-mapping');checks.push('TWO_SHEET_CONFIGURED');
 await click('Add validation rule');await page.getByLabel('Rule A 1',{exact:true}).selectOption(key('Quantity'));await page.getByLabel('Rule B 1',{exact:true}).selectOption(key('Price'));await page.getByLabel('Rule target 1',{exact:true}).selectOption(key('Amount'));
 await click('Add validation rule');await page.getByLabel('Rule type 2',{exact:true}).selectOption('CrossFoot');await page.getByLabel('Rule A 2',{exact:true}).selectOption(key('Amount'));await page.getByLabel('Rule target 2',{exact:true}).selectOption(key('Printed total'));await mutate('Apply validation rules','/validation-rules');
 await mutate('Confirm mapping as current user','/mapping/confirm');await click('3 Demo extraction');await page.getByLabel('Sample rows',{exact:true}).fill('2');
 const vals={'Item ID':['0001','0002'],Quantity:['2.5','2.5'],Price:['3','3'],Amount:['7.5','7.5'],'Printed total':['15']};
 for(const f of fields)for(let row=0;row<vals[f.label].length;row++)await page.getByLabel(`Sample ${f.key} ${row+1}`,{exact:true}).fill(vals[f.label][row]);
 assert.ok(await page.getByRole('button',{name:'Save draft',exact:true}).isDisabled());checks.push('UNAPPLIED_FORMS_CANNOT_BE_SAVED');
 s=await mutate('Validate manual sample','/manual-sample');await click('5 Human approval');await mutate('Complete human review','/review');await mutate('Approve document','/approve');
 const saved=await mutate('Save draft','/save-draft');const draft=saved.draft;await page.getByText('Draft saved durably.',{exact:true}).waitFor();checks.push('ACTUAL_SAVE_CONFIRMED');
 const update=await mutate('Save draft','/save-draft');assert.equal(update.draft.id,draft.id);assert.equal(update.draft.expiresAt,draft.expiresAt);assert.equal(update.draft.revision,draft.revision+1);Object.assign(draft,update.draft);
 assert.equal((await(await context.request.get(base+'/api/universal-doc/drafts')).json()).length,1);checks.push('REPEATED_SAVE_UPDATES_WITHOUT_DUPLICATION');
 await page.reload();await page.getByRole('button',{name:'Save draft',exact:true}).waitFor();
 const link=JSON.parse(await page.evaluate(()=>sessionStorage.getItem('universal-doc-draft-link')));assert.deepEqual(Object.keys(link).sort(),['draftId','revision','sessionId']);assert.equal(link.draftId,draft.id);checks.push('REFRESH_REAUTHORIZES_NON_AUTHORITATIVE_LINK');
 const csrf=await(await context.request.get(base+'/api/universal-doc/bootstrap')).json();
 const otherSave=await context.request.post(base+`/api/universal-doc/sessions/${update.session.id}/save-draft`,{headers:{'X-Studio-CSRF':csrf.csrfToken},data:{expectedVersion:update.session.version,draftId:draft.id,expectedRevision:draft.revision}});assert.equal(otherSave.status(),200);const concurrent=await otherSave.json();
 const stale=page.waitForResponse(r=>r.url().endsWith('/save-draft'));await click('Save draft');assert.equal((await stale).status(),409);
 await page.getByRole('alert').waitFor();await page.getByText('Current work is not saved as a durable draft. Apply form edits and save explicitly.',{exact:true}).waitFor();
 const reload=page.waitForResponse(r=>r.url().endsWith(`/sessions/${update.session.id}`)&&r.request().method()==='GET');await click('Reload server state');assert.equal((await reload).status(),200);
 await click('Refresh my drafts');await page.getByRole('button',{name:'Replace this draft on next save',exact:true}).waitFor();await click('Replace this draft on next save');
 const replaced=await mutate('Save draft','/save-draft');assert.equal(replaced.draft.revision,concurrent.draft.revision+1);Object.assign(draft,replaced.draft);checks.push('CONFLICT_REQUIRES_EXPLICIT_REPLACEMENT_NO_FAKE_SAVED');

 for(const user of ['other-a','admin-b']){const other=await browser.newContext(),p=await other.newPage();await login(p,user);const list=await other.request.get(base+'/api/universal-doc/drafts');assert.deepEqual(await list.json(),[]);const boot=await(await other.request.get(base+'/api/universal-doc/bootstrap')).json();const denied=await other.request.post(base+`/api/universal-doc/drafts/${draft.id}/resume`,{headers:{'X-Studio-CSRF':boot.csrfToken},data:{expectedRevision:draft.revision}});assert.equal(denied.status(),404);await other.close();}checks.push('REAL_CROSS_OWNER_TENANT_DENIED');
 await writeFile(join(folder,'restart-requested'),'restart');const until=Date.now()+120000;while(true){try{await access(join(folder,'restart-completed'));break;}catch{assert.ok(Date.now()<until);await new Promise(r=>setTimeout(r,200));}}
 await context.close();context=await browser.newContext({acceptDownloads:true});page=await context.newPage();page.on('pageerror',e=>errors.push(e.name));await login(page,'admin-a');
 await page.getByLabel(`Resume draft ${draft.id}`,{exact:true}).waitFor();checks.push('DRAFT_SURVIVED_PROCESS_RESTART_AND_RELOGIN');
 const response=page.waitForResponse(r=>r.url().endsWith(`/${draft.id}/resume`));await page.getByLabel(`Resume draft ${draft.id}`,{exact:true}).click();const rr=await response;assert.equal(rr.status(),200);s=await rr.json();assert.equal(s.mappings.length,2);assert.equal(s.validationRules.length,2);assert.equal(s.mappingState,'DraftSuggested');assert.notEqual(s.reviewState,'Approved');assert.ok(s.fields.some(f=>f.type==='Text'&&f.value==='0001'));checks.push('RESTORED_SERVER_CONFIGURATION_AND_TYPED_VALUES');
 await page.reload();await page.getByRole('heading',{name:'Universal Document Studio',exact:true}).waitFor();await page.getByText('DraftSuggested',{exact:true}).waitFor();checks.push('REFRESH_REAUTHORIZES_RUNTIME_SESSION');
 await click('6 Excel export');await click('Export Excel');await page.getByRole('alert').filter({hasText:'MAPPING_CONFIRMATION_REQUIRED'}).waitFor();checks.push('OLD_APPROVAL_NEVER_EXPORTS');
 await click('2 Mapping');await mutate('Confirm mapping as current user','/mapping/confirm');await click('6 Excel export');await click('Export Excel');await page.getByRole('alert').filter({hasText:'CURRENT_APPROVAL_REQUIRED'}).waitFor();
 await click('5 Human approval');await mutate('Complete human review','/review');await mutate('Approve document','/approve');checks.push('RENEWED_CONFIRMATION_REVIEW_APPROVAL');
 await click('6 Excel export');const dl=page.waitForEvent('download');await click('Export Excel');await(await dl).saveAs(join(folder,'synthetic-gate5c.xlsx'));checks.push('ACTUAL_XLSX_DOWNLOADED');
 await page.setViewportSize({width:390,height:844});assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));const resume=page.getByLabel(`Resume draft ${draft.id}`,{exact:true});await resume.scrollIntoViewIfNeeded();const box=await resume.boundingBox();assert.ok(box&&box.x>=0&&box.x+box.width<=391);checks.push('MOBILE_DRAFT_CONTROLS');
 const deleting=page.waitForResponse(r=>r.url().endsWith(`/drafts/${draft.id}`)&&r.request().method()==='DELETE');await page.getByLabel(`Delete draft ${draft.id}`,{exact:true}).click();assert.equal((await deleting).status(),204);await page.getByLabel(`Resume draft ${draft.id}`,{exact:true}).waitFor({state:'detached'});
 const boot=await(await context.request.get(base+'/api/universal-doc/bootstrap')).json();const denied=await context.request.post(base+`/api/universal-doc/drafts/${draft.id}/resume`,{headers:{'X-Studio-CSRF':boot.csrfToken},data:{expectedRevision:draft.revision}});assert.equal(denied.status(),404);checks.push('DELETE_PREVENTS_RESUME');
 assert.deepEqual(errors,[]);assert.equal(checks.length,16);await writeFile(join(folder,'gate5c-summary.json'),JSON.stringify({status:'PASS',checks,runtimeErrors:errors},null,2));
}catch(e){console.error(e);process.exitCode=1;}finally{await context.close();await browser.close();}
