// Exercise the installed artifact, including each of the three patched passes.
import assert from 'node:assert/strict';
import test from 'node:test';
import parser from 'postcss-selector-parser';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
test('both compatible build chains resolve the reviewed 6.1.5 artifact',()=>{
  for(const consumer of ['tailwindcss','postcss-nested']) {
    const resolveFrom=createRequire(require.resolve(consumer));
    assert.equal(resolveFrom('postcss-selector-parser/package.json').version,'6.1.5');
  }
});
for(const atom of ['.a','#a','#{a}']) {
  test(`backported flat selector complexity: ${atom}`,{timeout:30000},()=>{
    const fastest=input=>{
      let best=Infinity;
      for(let i=0;i<3;i++) {
        const start=performance.now();parser().astSync(input);best=Math.min(best,performance.now()-start);
      }
      return best;
    };
    // Upstream regression method and fixed threshold, set before execution.
    const hostile=fastest(atom.repeat(60000)),control=fastest(`${atom} `.repeat(60000));
    assert.ok(hostile/Math.max(control,1)<2,`hostile/control=${hostile/Math.max(control,1)}`);
  });
}
test('large selectors and leading-node deduplication retain semantics',()=>{
  const nodes=parser().astSync('.a'.repeat(20000)).first.nodes;
  assert.equal(nodes.length,20000);assert.ok(nodes.every(n=>n.type==='class'&&n.value==='a'));
  assert.deepEqual(parser().astSync('.a.b').first.nodes.map(n=>[n.type,n.value]),[['class','a'],['class','b']]);
  assert.deepEqual(parser().astSync('#x.y').first.nodes.map(n=>[n.type,n.value]),[['id','x'],['class','y']]);
  assert.equal(parser().astSync('.a').first.nodes.length,1);
});
