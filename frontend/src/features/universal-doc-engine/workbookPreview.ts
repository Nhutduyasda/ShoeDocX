// Informational, memory-only OOXML reader. Never evaluates formulas or follows links.
export interface PreviewCell { value:string; formula:boolean }
export interface PreviewSheet { name:string; cells:Record<string,PreviewCell>; merges:string[]; rows:number; columns:number }
export interface WorkbookEvidence { sha256:string; sheets:PreviewSheet[] }
const limit=8*1024*1024;
const fail=()=>new Error('Không thể xem tệp trong giới hạn an toàn. Chỉ hỗ trợ XLSX không mã hóa, tối đa 2 MiB, 4 sheet, 2.000 ô, dòng 1.000 và cột AF.');
export function coordinate(cell:string):{row:number;column:number}|null {
  const match=/^([A-Z]{1,2})([1-9]\d{0,3})$/.exec(cell);if(!match)return null;
  const column=[...match[1]].reduce((n,c)=>n*26+c.charCodeAt(0)-64,0),row=Number(match[2]);
  return column<=32&&row<=1000?{row,column}:null;
}
export async function readWorkbook(file:File):Promise<WorkbookEvidence> {
  if(file.size>2*1024*1024||file.size<22)throw fail();
  const bytes=new Uint8Array(await file.arrayBuffer()),view=new DataView(bytes.buffer);
  let end=bytes.length-22;while(end>=Math.max(0,bytes.length-65557)&&view.getUint32(end,true)!==0x06054b50)end--;
  if(end<0||end<bytes.length-65557||view.getUint16(end+4,true)||view.getUint16(end+6,true))throw fail();
  const count=view.getUint16(end+10,true);if(count>128)throw fail();
  let offset=view.getUint32(end+16,true),total=0;const parts=new Map<string,string>();const names=new Set<string>();
  const decoder=new TextDecoder('utf-8',{fatal:true});
  for(let i=0;i<count;i++) {
    if(offset+46>end||view.getUint32(offset,true)!==0x02014b50)throw fail();
    const flags=view.getUint16(offset+8,true),method=view.getUint16(offset+10,true),compressed=view.getUint32(offset+20,true),size=view.getUint32(offset+24,true);
    const length=view.getUint16(offset+28,true),extra=view.getUint16(offset+30,true),comment=view.getUint16(offset+32,true),local=view.getUint32(offset+42,true);
    if(offset+46+length+extra+comment>end)throw fail();
    const name=decoder.decode(bytes.subarray(offset+46,offset+46+length));offset+=46+length+extra+comment;
    total+=size;if(total>limit||size>2*1024*1024||flags&1||![0,8].includes(method)||names.has(name)||name.includes('..')||name.startsWith('/'))throw fail();names.add(name);
    if(/vbaProject|externalLinks|embeddings|connections/i.test(name))throw fail();
    if(!/^(?:xl\/workbook.xml|xl\/_rels\/workbook.xml.rels|xl\/sharedStrings.xml|xl\/worksheets\/[^/]+\.xml)$/.test(name))continue;
    if(local+30>bytes.length||view.getUint32(local,true)!==0x04034b50)throw fail();
    const start=local+30+view.getUint16(local+26,true)+view.getUint16(local+28,true);if(start+compressed>end)throw fail();
    const source=bytes.slice(start,start+compressed);
    let raw:Uint8Array;
    if(method===0)raw=source;else {
      const stream=new Blob([source]).stream().pipeThrough(new DecompressionStream('deflate-raw'));
      const reader=stream.getReader(),chunks:Uint8Array[]=[];let actual=0;
      try{for(;;){const result=await reader.read();if(result.done)break;actual+=result.value.length;if(actual>size||actual>2*1024*1024)throw fail();chunks.push(result.value);}}catch(e){await reader.cancel();throw e;}
      raw=new Uint8Array(actual);let at=0;for(const chunk of chunks){raw.set(chunk,at);at+=chunk.length;}
    }
    if(raw.length!==size)throw fail();parts.set(name,decoder.decode(raw));
  }
  const xml=(name:string)=>{const text=parts.get(name);if(!text||/<!DOCTYPE|<!ENTITY/i.test(text))throw fail();const doc=new DOMParser().parseFromString(text,'application/xml');if(doc.querySelector('parsererror'))throw fail();return doc;};
  const nodes=(root:Document|Element,tag:string)=>Array.from(root.getElementsByTagNameNS('*',tag));
  const relationships=nodes(xml('xl/_rels/workbook.xml.rels'),'Relationship');
  if(relationships.some(r=>r.getAttribute('TargetMode')==='External'))throw fail();
  const strings=parts.has('xl/sharedStrings.xml')?nodes(xml('xl/sharedStrings.xml'),'si').map(si=>nodes(si,'t').map(t=>t.textContent??'').join('')):[];
  const sheets:PreviewSheet[]=[];let cellCount=0;
  for(const sheet of nodes(xml('xl/workbook.xml'),'sheet')) {
    if(sheets.length>=4)throw fail();const id=sheet.getAttributeNS('http://schemas.openxmlformats.org/officeDocument/2006/relationships','id');
    const target=relationships.find(r=>r.getAttribute('Id')===id)?.getAttribute('Target');if(!target)throw fail();
    const path=target.startsWith('/xl/')?target.slice(1):`xl/${target}`;if(!/^xl\/worksheets\/[^/]+\.xml$/.test(path))throw fail();
    const doc=xml(path),cells:Record<string,PreviewCell>={};let rows=1,columns=1;
    for(const cell of nodes(doc,'c')) {
      const ref=cell.getAttribute('r')??'',pos=coordinate(ref);if(!pos||++cellCount>2000||Object.hasOwn(cells,ref))throw fail();
      const type=cell.getAttribute('t'),raw=nodes(cell,'v')[0]?.textContent??'';
      const value=type==='s'?strings[Number(raw)]:type==='inlineStr'?nodes(cell,'t').map(t=>t.textContent??'').join(''):type==='b'?(raw==='1'?'TRUE':'FALSE'):raw;
      if(value===undefined||value.length>2000)throw fail();cells[ref]={value,formula:nodes(cell,'f').length>0};rows=Math.max(rows,pos.row);columns=Math.max(columns,pos.column);
    }
    const merges=nodes(doc,'mergeCell').map(m=>m.getAttribute('ref')??'');for(const range of merges){const ends=range.split(':').map(coordinate);if(ends.length!==2||ends.some(p=>!p))throw fail();rows=Math.max(rows,ends[1]!.row);columns=Math.max(columns,ends[1]!.column);}
    sheets.push({name:sheet.getAttribute('name')??'',cells,merges,rows,columns});
  }
  if(!sheets.length)throw fail();
  const hash=await crypto.subtle.digest('SHA-256',bytes);
  return {sha256:Array.from(new Uint8Array(hash),n=>n.toString(16).padStart(2,'0')).join(''),sheets};
}
