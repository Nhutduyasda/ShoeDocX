import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import ts from 'typescript';
function load(path,requires={}){const module={exports:{}};vm.runInNewContext(ts.transpileModule(readFileSync(new URL(path,import.meta.url),'utf8'),{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText,{module,exports:module.exports,require:name=>{if(name in requires)return requires[name];throw Error(name);}});return module.exports;}
const auth=load('../src/types/auth.ts');const nav=load('../src/layouts/navigation.ts',{'../types/auth':auth});
for(const department of ['Admin','Xnk','XnkManager','Kho','KeToan',0,1,2,3,4])test(`navigation exactly preserves allowed destinations: ${department}`,()=>{
 const user={department,id:'synthetic',username:'synthetic',fullName:'Synthetic',departmentName:''};
 assert.deepEqual([...nav.navigationFor(user).map(item=>item.key)].sort(),[...auth.getAllowedTabsForUser(user)].sort());
 assert.ok(nav.navigationFor(user).some(item=>item.key===auth.getDefaultTabForUser(user)));
});
test('navigation identities stay unique and have working business descriptions',()=>{assert.equal(new Set(nav.destinations.map(item=>item.key)).size,nav.destinations.length);for(const item of nav.destinations)assert.ok(item.description.length>15);});
