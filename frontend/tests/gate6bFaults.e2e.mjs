// Actual HTTPS Production API; no mocks or insecure certificate bypass.
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import {pathToFileURL} from 'node:url';
const {request}=await import(pathToFileURL(join(resolve(process.env.STUDIO_PLAYWRIGHT_MODULE),'index.mjs')).href);
const base=process.env.STUDIO_BASE_URL,folder=process.env.STUDIO_EVIDENCE_DIR,checks=[];
const client=await request.newContext({baseURL:base});
async function login(c,user='admin-a'){const r=await c.post('/api/auth/login',{data:{username:user,password:process.env.STUDIO_PASSWORD}});assert.equal(r.status(),200);return (await r.json()).token;}
async function boot(c){const r=await c.get('/api/universal-doc/bootstrap');assert.equal(r.status(),200);return (await r.json()).csrfToken;}
try {
 const token=await login(client),csrf=await boot(client),headers={'X-Studio-CSRF':csrf};
 const mode=process.env.GATE6B_FAULT_MODE??'normal';
 if(mode==='expired-draft') {
   const r=await client.get('/api/universal-doc/drafts');assert.equal(r.status(),200);assert.deepEqual(await r.json(),[]);checks.push('EXPIRED_RESTORED_DRAFT_NOT_DISCLOSED');
 } else {
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
   for(const action of ['review','approve','export']) {const r=await clerk.post(path+'/'+action,{headers:{'X-Studio-CSRF':clerkCsrf},data:{expectedVersion:current.session.version}});assert.equal(r.status(),403);}
   await clerk.dispose();checks.push('UNAUTHORIZED_REVIEW_APPROVAL_EXPORT_NO_BYTES');
   const data={expectedVersion:current.session.version,draftId:draft.id,expectedRevision:draft.revision};
   const concurrent=await Promise.all([client.post(path+'/save-draft',{headers,data}),client.post(path+'/save-draft',{headers,data})]);assert.deepEqual(concurrent.map(r=>r.status()).sort(),[200,409]);checks.push('CONCURRENT_DRAFT_UPDATE_ONE_WINNER');
   const newest=await concurrent.find(r=>r.status()===200).json();current=newest;
   const invalid=await client.post(path+'/template',{headers,multipart:{expectedVersion:String(current.session.version),file:{name:'bad.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:Buffer.from('invalid workbook')}}});assert.equal(invalid.status(),400);checks.push('INVALID_WORKBOOK_REJECTED');
   const oversized=await client.post(path+'/template',{headers,multipart:{expectedVersion:String(current.session.version),file:{name:'large.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:Buffer.alloc(22*1024*1024)}}});assert.equal(oversized.status(),413);checks.push('OVERSIZED_UPLOAD_PROXY_LIMIT');
   const logout=await client.post('/api/auth/logout',{data:{}});assert.equal(logout.status(),204);
   const revoked=await request.newContext({baseURL:base,extraHTTPHeaders:{Authorization:'Bearer '+token}});assert.equal((await revoked.get('/api/auth/me')).status(),401);await revoked.dispose();checks.push('LOGOUT_REVOKES_BEARER_JWT');
   await writeFile(join(folder,'gate6b-draft.json'),JSON.stringify(newest.draft));
 }
 await writeFile(join(folder,`gate6b-faults-${mode}.json`),JSON.stringify({status:'PASS',checks},null,2));
}finally{await client.dispose();}
