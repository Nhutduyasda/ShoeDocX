// Real, authorized A -> B -> A exercise. No decrypted SQL payload or approval injection.
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import {pathToFileURL} from 'node:url';
const {request}=await import(pathToFileURL(join(resolve(process.env.STUDIO_PLAYWRIGHT_MODULE),'index.mjs')).href);
const folder=process.env.STUDIO_EVIDENCE_DIR,phase=process.env.GATE6B_RELEASE_PHASE;
const client=await request.newContext({baseURL:process.env.STUDIO_BASE_URL});
const report={phase,status:'FAIL',stages:{},oldApprovalRestored:false};
try {
 async function expect(response,status,key){report.stages[key]=response.status();assert.equal(response.status(),status);return status===204?null:response.json();}
 await expect(await client.post('/api/auth/login',{data:{username:'admin-a',password:process.env.STUDIO_PASSWORD}}),200,'login');
 const headers={'X-Studio-CSRF':(await expect(await client.get('/api/universal-doc/bootstrap'),200,'bootstrap')).csrfToken};
 let draft=JSON.parse(await readFile(join(folder,'gate6b-draft.json'),'utf8')),session;
 async function mutation(endpoint,data,method='post') {
   session=await expect(await client[method](`/api/universal-doc/sessions/${session.id}/${endpoint}`,{headers,data:{expectedVersion:session.version,...data}}),200,endpoint);
 }
 if(phase==='A-create') {
   const seed=JSON.parse(await readFile(join(folder,'gate6b-session.json'),'utf8'));
   session=await expect(await client.post('/api/universal-doc/sessions',{headers,data:{workflow:'example-template'}}),200,'create');
   for(const [endpoint,file] of [['template','F-1-false-none.xlsx'],['example','F-3-true-none.xlsx']]) {
     session=await expect(await client.post(`/api/universal-doc/sessions/${session.id}/${endpoint}`,{headers,multipart:{expectedVersion:String(session.version),file:{name:file,mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:await readFile(join(process.env.GATE6B_FIXTURES,file))}}}),200,endpoint);
   }
   const fields=seed.schemaDraft.fields.map(f=>{const c=f.omit?null:f.candidates[f.candidate];return {key:f.key,label:f.label,scope:f.scope,type:f.type,required:f.required,omit:f.omit,candidate:null,worksheet:c?.worksheet,cell:c?.cell,column:c?.column,prototypeRow:c?.prototypeRow};});
   await mutation('schema',{fields},'patch');await mutation('multisheet-mapping',{sheets:seed.mappings},'patch');await mutation('validation-rules',{rules:seed.validationRules},'patch');await mutation('mapping/confirm',{});
   const included=new Set(fields.filter(f=>!f.omit).map(f=>f.scope+':'+f.key));
   const values=seed.fields.filter(f=>included.has(f.scope+':'+f.key));
   const header=Object.fromEntries(values.filter(f=>f.scope==='Header').map(f=>[f.key,f.value??'']));
   const rows=Array.from({length:20},(_,i)=>Object.fromEntries(values.filter(f=>f.scope==='LineItem'&&f.row===i).map(f=>[f.key,f.value??''])));
   await mutation('manual-sample',{header,rows});
   const saved=await expect(await client.post(`/api/universal-doc/sessions/${session.id}/save-draft`,{headers,data:{expectedVersion:session.version,draftId:draft.id,expectedRevision:draft.revision}}),200,'save-by-A');
   session=saved.session;draft=saved.draft;await writeFile(join(folder,'gate6b-draft.json'),JSON.stringify(draft));
 } else {
   session=await expect(await client.post(`/api/universal-doc/drafts/${draft.id}/resume`,{headers,data:{expectedRevision:draft.revision}}),200,'resume');
   assert.notEqual(session.reviewState,'Approved');assert.equal(session.mappingState,'DraftSuggested');
   const denied=await client.post(`/api/universal-doc/sessions/${session.id}/export`,{headers,data:{expectedVersion:session.version}});
   assert.ok([400,409].includes(denied.status()));assert.notEqual((await denied.body()).subarray(0,2).toString(),'PK');
   await mutation('mapping/confirm',{});
 }
 for(const user of ['other-a','admin-b']) {
   const other=await request.newContext({baseURL:process.env.STUDIO_BASE_URL});
   try {assert.equal((await other.post('/api/auth/login',{data:{username:user,password:process.env.STUDIO_PASSWORD}})).status(),200);const csrf=(await (await other.get('/api/universal-doc/bootstrap')).json()).csrfToken;
     assert.equal((await other.post(`/api/universal-doc/drafts/${draft.id}/resume`,{headers:{'X-Studio-CSRF':csrf},data:{expectedRevision:draft.revision}})).status(),404);
   } finally {await other.dispose();}
 }
 const missing=await client.post(`/api/universal-doc/sessions/${session.id}/review`,{data:{expectedVersion:session.version}});assert.equal(missing.status(),400);
 await mutation('review',{});await mutation('approve',{});
 const exported=await client.post(`/api/universal-doc/sessions/${session.id}/export`,{headers,data:{expectedVersion:session.version}});report.stages.export=exported.status();assert.equal(exported.status(),200);
 await writeFile(join(folder,`release-${phase}.xlsx`),await exported.body());
 const version=Number(exported.headers()['x-studio-version']);assert.ok(version>session.version);
 await expect(await client.delete(`/api/universal-doc/sessions/${session.id}`,{headers,data:{expectedVersion:version}}),204,'remove-session');
 report.status='PASS';report.authorizedExport=true;report.crossOwnerAndTenantDenied=true;report.freshApproval=true;
}catch(error){report.errorType=error.name;report.sourceLines=[...String(error.stack).matchAll(/gate6bRelease\.e2e\.mjs:(\d+):/g)].map(m=>Number(m[1]));}
finally{await client.dispose();await writeFile(join(folder,`release-${phase}.json`),JSON.stringify(report));}
if(report.status!=='PASS')process.exitCode=1;
