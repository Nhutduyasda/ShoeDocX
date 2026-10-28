import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import ts from 'typescript';
const source=readFileSync(new URL('../src/features/universal-doc-engine/officeModel.ts',import.meta.url),'utf8');
const module={exports:{}};vm.runInNewContext(ts.transpileModule(source,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText,{module,exports:module.exports,require:()=>{throw Error('Unexpected runtime dependency');}});
const {officeError,OfficeRequestError,validEntry,colName,ruleText}=module.exports;
test('Server Boolean display values remain editable after validation and recovery without rewriting other types',()=>{const {entryValue}=module.exports;assert.equal(entryValue('Boolean','True'),'true');assert.equal(entryValue('Boolean','False'),'false');assert.equal(entryValue('Boolean',null),'');assert.equal(entryValue('Text','True'),'True');assert.equal(entryValue('Text','00123'),'00123');assert.equal(entryValue('Decimal','7.50'),'7.50');assert.equal(entryValue('Boolean','Có'),'Có');});
for(const code of ['SESSION_EXPIRED','SESSION_VERSION_CONFLICT','INVALID_MULTISHEET_MAPPING','DOCUMENT_VALIDATION_REQUIRED','DRAFT_REVISION_CONFLICT','DRAFT_STORAGE_UNAVAILABLE'])test('Localized corrective guidance '+code,()=>{const text=officeError(code);assert.ok(text.length>30);assert.ok(!text.includes(code));});
test('Unknown backend content and prototype property names never become messages',()=>{for(const x of ['<script>document.cookie</script>','__proto__','constructor',{message:'secret workbook'},null])assert.equal(officeError(x),officeError('REQUEST_FAILED'));assert.equal(new OfficeRequestError('private source bytes').message,officeError('REQUEST_FAILED'));});
test('Text identifiers and exact decimal strings are validated without rewriting',()=>{assert.equal(validEntry({type:'Text'},'00123'),true);const value='9007199254740993.123456789';assert.equal(validEntry({type:'Decimal'},value),true);assert.equal(value,'9007199254740993.123456789');});
for(const x of ['1,234','1.234,56','2e3','NaN','Infinity',' 2.50 '])test('Ambiguous decimal is not guessed '+x,()=>assert.equal(validEntry({type:'Decimal'},x),false));
test('Dates and booleans have unambiguous wire formats',()=>{assert.ok(validEntry({type:'Date'},'2026-10-24'));assert.ok(!validEntry({type:'Date'},'24/10/2026'));assert.ok(validEntry({type:'Boolean'},'true'));assert.ok(validEntry({type:'Boolean'},'false'));assert.ok(!validEntry({type:'Boolean'},'Có'));});
test('Empty is submitted for server requiredness evaluation, never declared PASS locally',()=>assert.ok(validEntry({type:'Decimal',required:true},'')));
test('Coordinates stay bounded to accepted analyzed columns',()=>{assert.equal(colName(1),'A');assert.equal(colName(26),'Z');assert.equal(colName(27),'AA');assert.equal(colName(32),'AF');for(const n of [0,33,16384,NaN])assert.equal(colName(n),'?');});
test('Readable rule labels use exact schema identities and omitted fields are not described as active',()=>{const s={schemaDraft:{fields:[{key:'a',label:'Số lượng',scope:'LineItem',omit:false},{key:'b',label:'Đơn giá',scope:'LineItem',omit:false},{key:'t',label:'Thành tiền',scope:'LineItem',omit:false}]}};assert.equal(ruleText({kind:'Arithmetic',fieldA:'a',fieldB:'b',target:'t',operator:'Multiply'},s),'Số lượng × Đơn giá = Thành tiền');});


test('Server check IDs resolve to exact field identities and bounded rows for focus',()=>{
  const header={key:'id',scope:'Header',omit:false},item={key:'amount',scope:'LineItem',omit:false};
  const session={schemaDraft:{fields:[header,item]},validationRules:[{id:'multiply',kind:'Arithmetic',target:'amount'},{id:'sum',kind:'CrossFoot',target:'id'}]};
  const locate=field=>module.exports.issueLocation({field,rowPosition:null},session);
  assert.equal(locate('field:Header:id').field,header);assert.equal(locate('field:LineItem:amount:19').row,19);
  assert.equal(locate('multiply:2').field,item);assert.equal(locate('multiply:2').row,2);assert.equal(locate('sum').field,header);
  assert.equal(locate('field:LineItem:amount:20'),null);assert.equal(locate('multiply:invalid'),null);assert.equal(locate('unknown'),null);
});
