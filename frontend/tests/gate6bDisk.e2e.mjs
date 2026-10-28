import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import {pathToFileURL} from 'node:url';
const {request}=await import(pathToFileURL(join(resolve(process.env.STUDIO_PLAYWRIGHT_MODULE),'index.mjs')).href);
const folder=process.env.STUDIO_EVIDENCE_DIR,phase=process.env.GATE6B_DISK_PHASE;
const client=await request.newContext({baseURL:process.env.STUDIO_BASE_URL,storageState:join(folder,'old-protection-state.json')});
const report={status:'FAIL',phase};
try {
 assert.equal((await client.get('/api/auth/me')).status(),200);
 const headers={'X-Studio-CSRF':(await (await client.get('/api/universal-doc/bootstrap')).json()).csrfToken};
 const created=await client.post('/api/universal-doc/sessions',{headers,data:{workflow:'example-template'}});assert.equal(created.status(),200);const uploadSession=await created.json();
 const uploaded=await client.post(`/api/universal-doc/sessions/${uploadSession.id}/template`,{headers,multipart:{expectedVersion:String(uploadSession.version),file:{name:'synthetic.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:await readFile(process.env.GATE6B_DISK_FIXTURE)}}});
 report.uploadStatus=uploaded.status();
 if(uploaded.status()===400){const body=await uploaded.text();report.csrfRejected=/CSRF_TOKEN_INVALID|CSRF_TOKEN_REQUIRED/.test(body);report.storageErrorReported=/No space left on device|Failed to read the request form/i.test(body);assert.equal(report.csrfRejected,false);}
 assert.equal(uploaded.status(),phase==='ingress-full'?500:phase==='backend-full'?400:200);
 const after=await (await client.get(`/api/universal-doc/sessions/${uploadSession.id}`)).json();
 if(phase==='ingress-full'||phase==='backend-full'){assert.equal(after.version,uploadSession.version);assert.equal(after.schemaDraft,null);report.failedUploadDidNotCommit=true;assert.notEqual((await uploaded.body()).subarray(0,2).toString(),'PK');}
 assert.equal((await client.delete(`/api/universal-doc/sessions/${uploadSession.id}`,{headers,data:{expectedVersion:after.version}})).status(),204);
 const original=JSON.parse(await readFile(join(folder,'gate6b-session.json'),'utf8'));
 const current=await (await client.get(`/api/universal-doc/sessions/${original.id}`)).json();assert.equal(current.reviewState,'Approved');
 const exported=await client.post(`/api/universal-doc/sessions/${current.id}/export`,{headers,data:{expectedVersion:current.version}});report.exportStatus=exported.status();
 if(phase==='backend-full') {
   assert.equal(exported.status(),409);const denied=await exported.json();
   assert.match(denied.code,/^[A-Z_]+$/);report.exportErrorCode=denied.code;
   assert.notEqual(denied.code,'SESSION_VERSION_CONFLICT');assert.notEqual(denied.code,'CURRENT_APPROVAL_REQUIRED');
   assert.notEqual((await exported.body()).subarray(0,2).toString(),'PK');
   const unchanged=await (await client.get(`/api/universal-doc/sessions/${current.id}`)).json();assert.equal(unchanged.version,current.version);assert.equal(unchanged.reviewState,'Approved');
   report.noWorkbookBytesReturned=true;report.approvedSessionUnchanged=true;
 } else {assert.equal(exported.status(),200);await writeFile(join(folder,`disk-${phase}.xlsx`),await exported.body());report.inMemoryExportCompleted=true;}
 const health=await client.get('/api/health');report.healthStatus=health.status();assert.equal(health.status(),phase==='backend-full'?503:200);
 report.status='PASS';
}catch(error){report.errorType=error.name;report.sourceLines=[...String(error.stack).matchAll(/gate6bDisk\.e2e\.mjs:(\d+):/g)].map(m=>Number(m[1]));}
finally{await client.dispose();await writeFile(join(folder,`disk-${phase}.json`),JSON.stringify(report));}
if(report.status!=='PASS')process.exitCode=1;
