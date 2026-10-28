// Read-only-to-draft compatibility probe against the immutable accepted previous API.
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import {pathToFileURL} from 'node:url';
const {request}=await import(pathToFileURL(join(resolve(process.env.STUDIO_PLAYWRIGHT_MODULE),'index.mjs')).href);
const folder=process.env.STUDIO_EVIDENCE_DIR;
const client=await request.newContext({baseURL:process.env.STUDIO_BASE_URL});
const report={status:'FUNCTIONAL_BLOCKED',stages:{},oldApprovalRestored:false,exportAuthorized:false};
try {
 const login=await client.post('/api/auth/login',{data:{username:'admin-a',password:process.env.STUDIO_PASSWORD}});report.stages.login=login.status();assert.equal(login.status(),200);
 const boot=await client.get('/api/universal-doc/bootstrap');report.stages.bootstrap=boot.status();assert.equal(boot.status(),200);const headers={'X-Studio-CSRF':(await boot.json()).csrfToken};
 const draft=JSON.parse(await readFile(join(folder,'gate6b-draft.json'),'utf8'));
 const resumed=await client.post(`/api/universal-doc/drafts/${draft.id}/resume`,{headers,data:{expectedRevision:draft.revision}});report.stages.resume=resumed.status();assert.equal(resumed.status(),200);let session=await resumed.json();
 assert.notEqual(session.reviewState,'Approved');assert.equal(session.mappingState,'DraftSuggested');
 for(const step of ['mapping/confirm','review','approve']) {
   const response=await client.post(`/api/universal-doc/sessions/${session.id}/${step}`,{headers,data:{expectedVersion:session.version}});report.stages[step]=response.status();assert.equal(response.status(),200);session=await response.json();
 }
 const exported=await client.post(`/api/universal-doc/sessions/${session.id}/export`,{headers,data:{expectedVersion:session.version}});report.stages.export=exported.status();assert.equal(exported.status(),200);
 const workbook=await exported.body();assert.equal(workbook.subarray(0,2).toString(),'PK');await writeFile(join(folder,'rollback.xlsx'),workbook);report.exportAuthorized=true;
 const exportedVersion=Number(exported.headers()['x-studio-version']);assert.ok(exportedVersion>session.version);const removed=await client.delete(`/api/universal-doc/sessions/${session.id}`,{headers,data:{expectedVersion:exportedVersion}});assert.equal(removed.status(),204);
 report.status='FUNCTIONAL_PASS';
} catch(error) {report.errorType=error.name;}
finally {await client.dispose();await writeFile(join(folder,'rollback-functional.json'),JSON.stringify(report,null,2));}
