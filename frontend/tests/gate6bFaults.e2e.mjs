// Actual HTTPS Production API; no mocks or insecure certificate bypass.
import assert from 'node:assert/strict';
import https from 'node:https';
import {readFile,writeFile} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import {pathToFileURL} from 'node:url';
const {request}=await import(pathToFileURL(join(resolve(process.env.STUDIO_PLAYWRIGHT_MODULE),'index.mjs')).href);
const base=process.env.STUDIO_BASE_URL,folder=process.env.STUDIO_EVIDENCE_DIR,checks=[];
const mode=process.env.GATE6B_FAULT_MODE??'normal';
const client=await request.newContext({baseURL:base,...(mode==='wrong-dp'?{storageState:join(folder,'old-protection-state.json')}:{})});
async function login(c,user='admin-a'){const r=await c.post('/api/auth/login',{data:{username:user,password:process.env.STUDIO_PASSWORD}});assert.equal(r.status(),200);return (await r.json()).token;}
async function boot(c){const r=await c.get('/api/universal-doc/bootstrap');assert.equal(r.status(),200);return (await r.json()).csrfToken;}
try {
 if(mode==='wrong-dp') {
   const saved=JSON.parse(await readFile(join(folder,'old-protection-csrf.json'),'utf8'));
   const r=await client.post('/api/universal-doc/sessions',{headers:{'X-Studio-CSRF':saved.csrf},data:{workflow:'example-template'}});
   assert.equal(r.status(),400);assert.equal((await r.json()).code,'CSRF_TOKEN_INVALID');
   await writeFile(join(folder,'gate6b-faults-wrong-dp.json'),JSON.stringify({status:'PASS',checks:['WRONG_DATAPROTECTION_CERTIFICATE_REJECTS_OLD_CSRF_NO_OUTPUT']}));
 } else {
 const token=await login(client),csrf=await boot(client),headers={'X-Studio-CSRF':csrf};
 const cookies=(await client.storageState()).cookies;
 assert.ok(cookies.filter(c=>['shoedocx_access','shoedocx_studio_csrf'].includes(c.name)).length===2);
 assert.ok(cookies.every(c=>c.secure&&c.httpOnly&&c.sameSite==='Strict'));checks.push('SECURE_HTTPONLY_STRICT_AUTH_AND_CSRF_COOKIES');
 if(mode==='workflow-expiry') {
   const create=await client.post('/api/universal-doc/sessions',{headers,data:{workflow:'example-template'}});assert.equal(create.status(),200);const session=await create.json();
   const wait=Date.parse(session.expiresAt)-Date.now();assert.ok(wait>1190000&&wait<=1201000);
   while(Date.now()<Date.parse(session.expiresAt)+1500)await new Promise(r=>setTimeout(r,Math.min(30000,Date.parse(session.expiresAt)+1500-Date.now())));
   const expired=await client.get(`/api/universal-doc/sessions/${session.id}`);assert.ok([404,410].includes(expired.status()));assert.ok(!(await expired.text()).includes('00121'));checks.push('REAL_TWENTY_MINUTE_WORKFLOW_EXPIRY_NO_CLOCK_OVERRIDE');
 } else if(mode==='db-conflict') {
   const draft=JSON.parse(await readFile(join(folder,'gate6b-draft.json'),'utf8'));
   const r=await client.post(`/api/universal-doc/drafts/${draft.id}/resume`,{headers,data:{expectedRevision:draft.revision}});assert.equal(r.status(),503);assert.equal((await r.json()).code,'DRAFT_STORAGE_UNAVAILABLE');checks.push('REAL_SQL_TRANSACTION_LOCK_CONFLICT_FAILS_CLOSED');
 } else if(mode==='wrong-aes') {
   const draft=JSON.parse(await readFile(join(folder,'gate6b-draft.json'),'utf8'));
   const r=await client.post(`/api/universal-doc/drafts/${draft.id}/resume`,{headers,data:{expectedRevision:draft.revision}});
   assert.equal(r.status(),409);assert.equal((await r.json()).code,'DRAFT_PAYLOAD_INVALID');checks.push('INCORRECT_AES_KEY_RESUME_REJECTED_NO_BYTES');
 } else if(mode==='expired-draft') {
   const r=await client.get('/api/universal-doc/drafts');assert.equal(r.status(),200);assert.deepEqual(await r.json(),[]);checks.push('EXPIRED_RESTORED_DRAFT_NOT_DISCLOSED');
 } else {
   const expired=await readFile(process.env.GATE6B_EXPIRED_TOKEN,'utf8');
   const expiredClient=await request.newContext({baseURL:base,extraHTTPHeaders:{Authorization:'Bearer '+expired}});
   assert.equal((await expiredClient.get('/api/auth/me')).status(),401);await expiredClient.dispose();checks.push('EXPIRED_SIGNED_JWT_REJECTED');
   const session=JSON.parse(await readFile(join(folder,'gate6b-session.json'),'utf8'));
   const path=`/api/universal-doc/sessions/${session.id}`;
   for(const value of [undefined,'invalid']) {
     const r=await client.post(path+'/save-draft',{headers:value?{'X-Studio-CSRF':value}:{},data:{expectedVersion:session.version,expectedRevision:0}});assert.equal(r.status(),400);
   }checks.push('MISSING_AND_INVALID_CSRF_REJECTED');
   const saved=await client.post(path+'/save-draft',{headers,data:{expectedVersion:session.version,expectedRevision:0}});assert.equal(saved.status(),200);let current=await saved.json();const draft=current.draft;
   assert.equal((Date.parse(draft.expiresAt)-Date.parse(draft.createdAt))/86400000,7);checks.push('FIXED_SEVEN_DAY_RETENTION');
   for(const user of ['other-a','admin-b']) {
     const c=await request.newContext({baseURL:base});await login(c,user);const csrf2=await boot(c);
     const list=await c.get('/api/universal-doc/drafts');assert.deepEqual(await list.json(),[]);
     const r=await c.post(`/api/universal-doc/drafts/${draft.id}/resume`,{headers:{'X-Studio-CSRF':csrf2},data:{expectedRevision:draft.revision}});assert.equal(r.status(),404);assert.ok(!(await r.text()).includes('00121'));await c.dispose();
   }checks.push('CROSS_OWNER_AND_TENANT_DRAFT_RESUME_DENIED');
   const clerk=await request.newContext({baseURL:base});await login(clerk,'clerk-a');const clerkCsrf=await boot(clerk);
   for(const action of ['review','approve','export']) {const r=await clerk.post(path+'/'+action,{headers:{'X-Studio-CSRF':clerkCsrf},data:{expectedVersion:current.session.version}});assert.equal(r.status(),403);assert.notEqual((await r.body()).subarray(0,2).toString(),'PK');}
   await clerk.dispose();checks.push('UNAUTHORIZED_REVIEW_APPROVAL_EXPORT_NO_BYTES');
   const data={expectedVersion:current.session.version,draftId:draft.id,expectedRevision:draft.revision};
   const concurrent=await Promise.all([client.post(path+'/save-draft',{headers,data}),client.post(path+'/save-draft',{headers,data})]);assert.deepEqual(concurrent.map(r=>r.status()).sort(),[200,409]);checks.push('CONCURRENT_DRAFT_UPDATE_ONE_WINNER');
   const newest=await concurrent.find(r=>r.status()===200).json();current=newest;
   const invalid=await client.post(path+'/template',{headers,multipart:{expectedVersion:String(current.session.version),file:{name:'bad.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:Buffer.from('invalid workbook')}}});assert.equal(invalid.status(),400);checks.push('INVALID_WORKBOOK_REJECTED');
   for(const [endpoint,file] of [['template','H-1-false-far.xlsx'],['example','F-3-true-reference.xlsx']]) {
     const rejected=await client.post(path+'/'+endpoint,{headers,multipart:{expectedVersion:String(current.session.version),file:{name:file,mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:await readFile(join(process.env.GATE6B_FIXTURES,file))}}});assert.equal(rejected.status(),400);assert.notEqual((await rejected.body()).subarray(0,2).toString(),'PK');
   }checks.push('UNSUPPORTED_STRUCTURE_AND_CHANGED_REFERENCE_EXAMPLE_REJECTED');
   const oversized=await client.post(path+'/template',{headers,multipart:{expectedVersion:String(current.session.version),file:{name:'large.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:Buffer.alloc(22*1024*1024)}}});assert.equal(oversized.status(),413);checks.push('OVERSIZED_UPLOAD_PROXY_LIMIT');
   const exportData={expectedVersion:current.session.version};const beforeDownload=await client.post(path+'/export',{headers,data:exportData});assert.equal(beforeDownload.status(),200);const original=await beforeDownload.body();
   const body=JSON.stringify(exportData),cookie=(await client.storageState()).cookies.map(c=>c.name+'='+c.value).join('; ');let interrupted=false;
   await new Promise((resolve,reject)=>{
     const outgoing=https.request(base+path+'/export',{method:'POST',headers:{...headers,Cookie:cookie,'Content-Type':'application/json','Content-Length':Buffer.byteLength(body)}},response=>{
       if(response.statusCode!==200){reject(new Error('INTERRUPTED_EXPORT_STATUS'));response.destroy();return;}
       response.on('error',()=>{});response.once('data',()=>{interrupted=true;response.destroy();outgoing.destroy();resolve();});response.once('end',()=>{if(!interrupted)reject(new Error('NO_DOWNLOAD_CHUNK'));});
     });outgoing.on('error',error=>{if(!interrupted)reject(error);});outgoing.setTimeout(10000,()=>{outgoing.destroy();if(!interrupted)reject(new Error('INTERRUPTED_EXPORT_TIMEOUT'));});outgoing.end(body);
   });assert.ok(interrupted);
   const retry=await client.post(path+'/export',{headers,data:exportData});assert.equal(retry.status(),200);assert.deepEqual(await retry.body(),original);assert.equal((await client.get('/api/health')).status(),200);checks.push('CLIENT_DOWNLOAD_INTERRUPTED_AUTHORIZED_RETRY_IDENTICAL_NO_STATE_LOSS');
   const logout=await client.post('/api/auth/logout',{data:{}});assert.equal(logout.status(),204);
   const revoked=await request.newContext({baseURL:base,extraHTTPHeaders:{Authorization:'Bearer '+token}});assert.equal((await revoked.get('/api/auth/me')).status(),401);await revoked.dispose();checks.push('LOGOUT_REVOKES_BEARER_JWT');
   await writeFile(join(folder,'gate6b-draft.json'),JSON.stringify(newest.draft));
   await login(client);const freshCsrf=await boot(client);await client.storageState({path:join(folder,'old-protection-state.json')});await writeFile(join(folder,'old-protection-csrf.json'),JSON.stringify({csrf:freshCsrf}));
 }
 await writeFile(join(folder,`gate6b-faults-${mode}.json`),JSON.stringify({status:'PASS',checks},null,2));
}
}finally{await client.dispose();}
