import { test, expect } from '@playwright/test';
import { readFile } from 'node:fs/promises';
test('offline reader faithfully reads real synthetic OOXML values, coordinates, sheets, merges and cached formulas',async({page})=>{
  await page.goto('/tests/ui-v2/components.html');
  const bytes=await readFile('tests/fixtures/visual-preview.xlsx');
  const result=await page.evaluate(async(base64)=>{
    // Vite serves the same production reader; this is a parser test, not API success evidence.
    const {readWorkbook}=await import('/src/features/universal-doc-engine/workbookPreview.ts');
    const bytes=Uint8Array.from(atob(base64),c=>c.charCodeAt(0));return readWorkbook(new File([bytes],'fixture.xlsx'));
  },bytes.toString('base64'));
  expect(result.sheets.map((s:{name:string})=>s.name)).toEqual(['HoaDon','Kho']);
  expect(result.sheets[0].cells.B1.value).toBe('00123');expect(result.sheets[0].cells.A3.value).toBe('00001');
  expect(result.sheets[0].cells.C3.formula).toBe(true);expect(result.sheets[0].cells.C3.value).toBe('');
  expect(result.sheets[0].merges).toEqual(['A5:B5']);expect(result.sheets[0].rows).toBe(80);expect(result.sheets[0].columns).toBe(32);
  expect(result.sheets[1].cells.A1.value).toBe('<img src=x onerror=alert(1)>');
});
test('reader rejects oversized, malformed and encrypted archive evidence',async({page})=>{
  await page.goto('/tests/ui-v2/components.html');const bytes=await readFile('tests/fixtures/visual-preview.xlsx');
  const errors=await page.evaluate(async(base64)=>{const {readWorkbook}=await import('/src/features/universal-doc-engine/workbookPreview.ts');
    const original=Uint8Array.from(atob(base64),c=>c.charCodeAt(0)),encrypted=original.slice();
    for(let i=0;i<encrypted.length-46;i++)if(new DataView(encrypted.buffer).getUint32(i,true)===0x02014b50){encrypted[i+8]|=1;break;}
    return Promise.all([new Uint8Array(2*1024*1024+1),new Uint8Array(22),encrypted].map(async b=>{try{await readWorkbook(new File([b],'unsafe.xlsx'));return false;}catch{return true;}}));
  },bytes.toString('base64'));expect(errors).toEqual([true,true,true]);
});
