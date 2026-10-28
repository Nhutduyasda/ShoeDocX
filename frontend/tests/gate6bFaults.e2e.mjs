// Actual HTTPS Production API; no mocks or insecure certificate bypass.
import assert from 'node:assert/strict';
import https from 'node:https';
import {readFile,writeFile} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import {pathToFileURL} from 'node:url';
const {request,chromium}=await import(pathToFileURL(join(resolve(process.env.STUDIO_PLAYWRIGHT_MODULE),'index.mjs')).href);
const base=process.env.STUDIO_BASE_URL,folder=process.env.STUDIO_EVIDENCE_DIR,checks=[],measurements={};
const mode=process.env.GATE6B_FAULT_MODE??'normal';
const client=await request.newContext({baseURL:base,...(['wrong-dp','sql-outage','certificate-rollover'].includes(mode)?{storageState:join(folder,'old-protection-state.json')}:{})});
async function login(c,user='admin-a'){const r=await c.post('/api/auth/login',{data:{username:user,password:process.env.STUDIO_PASSWORD}});assert.equal(r.status(),200);return (await r.json()).token;}
async function boot(c){const r=await c.get('/api/universal-doc/bootstrap');assert.equal(r.status(),200);return (await r.json()).csrfToken;}
try {
 if(mode==='sql-outage') {
   const saved=JSON.parse(await readFile(join(folder,'old-protection-csrf.json'),'utf8')),draft=JSON.parse(await readFile(join(folder,'gate6b-draft.json'),'utf8'));
   const r=await client.post(`/api/universal-doc/drafts/${draft.id}/resume`,{headers:{'X-Studio-CSRF':saved.csrf},data:{expectedRevision:draft.revision},timeout:65000});
   assert.ok([503,504].includes(r.status()));assert.notEqual((await r.body()).subarray(0,2).toString(),'PK');assert.ok(!(await r.text()).includes('00121'));
   await writeFile(join(folder,'gate6b-faults-sql-outage.json'),JSON.stringify({status:'PASS',checks:['SQL_OUTAGE_ACTUAL_AUTHORIZED_RESUME_FAILS_CLOSED'],httpStatus:r.status()}));
 } else if(mode==='certificate-rollover') {
   const saved=JSON.parse(await readFile(join(folder,'old-protection-csrf.json'),'utf8'));
   const headers={'X-Studio-CSRF':saved.csrf};
   const r=await client.post('/api/universal-doc/sessions',{headers,data:{workflow:'example-template'}});
   assert.equal(r.status(),200);const session=await r.json();
   assert.equal((await client.delete(`/api/universal-doc/sessions/${session.id}`,{headers,data:{expectedVersion:session.version}})).status(),204);
   await writeFile(join(folder,'gate6b-faults-certificate-rollover.json'),JSON.stringify({status:'PASS',checks:['OLD_AUTH_AND_CSRF_ACCEPTED_AFTER_RESTART_WITH_NEW_ACTIVE_CERTIFICATE_AND_RETAINED_DECRYPTION_CERTIFICATE']}));
 } else if(mode==='wrong-dp') {
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
   const r=await client.get('/api/universal-doc/drafts');assert.equal(r.status(),200);assert.deepEqual(await r.json(),[]);const draft=JSON.parse(await readFile(join(folder,'gate6b-draft.json'),'utf8'));const expired=await client.post(`/api/universal-doc/drafts/${draft.id}/resume`,{headers,data:{expectedRevision:draft.revision}});assert.ok([404,410].includes(expired.status()));assert.notEqual((await expired.body()).subarray(0,2).toString(),'PK');checks.push('EXPIRED_RESTORED_DRAFT_NOT_DISCLOSED_OR_RESUMED');
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
   const oversizedCookie=(await client.storageState()).cookies.map(c=>c.name+'='+c.value).join('; ');
   const oversizedStatus=await new Promise((resolve,reject)=>{
     const outgoing=https.request(base+path+'/template',{method:'POST',family:4,agent:false,headers:{...headers,Cookie:oversizedCookie,'Content-Type':'multipart/form-data; boundary=gate6b','Content-Length':22*1024*1024,Expect:'100-continue'}},response=>{response.resume();response.once('end',()=>{outgoing.destroy();resolve(response.statusCode);});});
     outgoing.once('continue',()=>{outgoing.destroy();reject(new Error('OVERSIZED_TRANSFER_ACCEPTED'));});outgoing.on('error',reject);outgoing.setTimeout(10000,()=>{outgoing.destroy();reject(new Error('OVERSIZED_HEADER_TIMEOUT'));});outgoing.flushHeaders();
   });assert.equal(oversizedStatus,413);checks.push('OVERSIZED_DECLARED_UPLOAD_REJECTED_BEFORE_BODY_TRANSFER');
   const exportData={expectedVersion:current.session.version};const beforeDownload=await client.post(path+'/export',{headers,data:exportData});assert.equal(beforeDownload.status(),200);const original=await beforeDownload.body();const committedVersion=Number(beforeDownload.headers()['x-studio-version']);assert.ok(committedVersion>exportData.expectedVersion);exportData.expectedVersion=committedVersion;
   const browser=await chromium.launch({headless:true});let interrupted=false;
   try {
     const context=await browser.newContext({storageState:await client.storageState()});const page=await context.newPage();await page.goto(base+'/api/health');const channel=await context.newCDPSession(page);await channel.send('Network.enable');await channel.send('Network.emulateNetworkConditions',{offline:false,latency:0,downloadThroughput:1024,uploadThroughput:65536});
     const observed=await page.evaluate(async ({url,csrf,data})=>{
       const controller=new AbortController();const response=await fetch(url,{method:'POST',credentials:'same-origin',headers:{'Content-Type':'application/json','X-Studio-CSRF':csrf},body:JSON.stringify(data),signal:controller.signal});
       if(response.status!==200)return {status:response.status,chunkReceived:false};
       const first=await response.body.getReader().read();controller.abort();return {status:response.status,chunkReceived:!first.done&&first.value.length>0,chunkBytes:first.value?.length??0};
     },{url:base+path+'/export',csrf,data:exportData});
     measurements.interruptedDownload={receivedBytes:observed.chunkBytes,totalBytes:original.length,downloadBytesPerSecond:1024};assert.equal(observed.status,200);assert.ok(observed.chunkReceived);assert.ok(observed.chunkBytes<original.length);interrupted=true;await context.close();
   } finally {await browser.close();}
   assert.ok(interrupted);
   const refreshed=await client.get(path);assert.equal(refreshed.status(),200);const refreshedSession=await refreshed.json();assert.equal(refreshedSession.reviewState,'Approved');assert.equal(refreshedSession.version,committedVersion+1);exportData.expectedVersion=refreshedSession.version;
   const retry=await client.post(path+'/export',{headers,data:exportData});assert.equal(retry.status(),200);await writeFile(join(folder,'interrupted-original.xlsx'),original);await writeFile(join(folder,'interrupted-retry.xlsx'),await retry.body());assert.equal((await client.get('/api/health')).status(),200);checks.push('CLIENT_PARTIAL_DOWNLOAD_ABORTED_VERSION_REFRESHED_AUTHORIZED_RETRY');
   const logout=await client.post('/api/auth/logout',{data:{}});assert.equal(logout.status(),204);
   const revoked=await request.newContext({baseURL:base,extraHTTPHeaders:{Authorization:'Bearer '+token}});assert.equal((await revoked.get('/api/auth/me')).status(),401);await revoked.dispose();checks.push('LOGOUT_REVOKES_BEARER_JWT');
   await writeFile(join(folder,'gate6b-draft.json'),JSON.stringify(newest.draft));
   await login(client);const freshCsrf=await boot(client);await client.storageState({path:join(folder,'old-protection-state.json')});await writeFile(join(folder,'old-protection-csrf.json'),JSON.stringify({csrf:freshCsrf}));
 }
 await writeFile(join(folder,`gate6b-faults-${mode}.json`),JSON.stringify({status:'PASS',checks,measurements},null,2));
}
}catch(error){
 const failure={mode,status:'FAIL',checks,measurements,errorType:error.name,sourceLines:[...String(error.stack).matchAll(/gate6bFaults\.e2e\.mjs:(\d+):\d+/g)].map(m=>Number(m[1])),networkCodes:['EPIPE','ECONNRESET','ECONNREFUSED','CERTIFICATE_VERIFY_FAILED'].filter(code=>String(error.message).includes(code))};
 if(Number.isInteger(error.actual)&&error.actual>=100&&error.actual<=599)failure.actualHttpStatus=error.actual;
 if(Number.isInteger(error.expected)&&error.expected>=100&&error.expected<=599)failure.expectedHttpStatus=error.expected;
 await writeFile(join(folder,`gate6b-failure-${mode}.json`),JSON.stringify(failure));console.error('GATE6B_FAULT_FAILED',mode,error.name);process.exitCode=1;
}finally{await client.dispose();}
