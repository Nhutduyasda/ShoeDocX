import { useEffect, useState } from 'react';
import type { LearnedField, StudioMapping, StudioRule, StudioSession } from './studioApi';
import { colName, entryValue, ruleText, scopeLabel, typeLabel, validEntry } from './officeModel';
export type OfficeSection = 'schema'|'mapping'|'rules'|'data';
export type OfficeApply = (action:string, body:Record<string,unknown>, section:OfficeSection, method?:string)=>Promise<void>;
type Props = { session:StudioSession; busy:boolean; apply:OfficeApply; report:(section:OfficeSection,dirty:boolean)=>void };
function useDirty(section:OfficeSection,dirty:boolean,report:Props['report']) {
  useEffect(()=>{report(section,dirty);return()=>report(section,false);},[section,dirty,report]);
}
function Pending({dirty,discard,busy}:{dirty:boolean;discard:()=>void;busy:boolean}) {return <button type="button" disabled={busy||!dirty} onClick={discard}>Hoàn tác thay đổi</button>;}
export function OfficeFields({session,busy,apply,report}:Props) {
  const initial=session.schemaDraft?.fields??[];const [fields,setFields]=useState(initial);const dirty=JSON.stringify(fields)!==JSON.stringify(initial);useDirty('schema',dirty,report);
  const update=(i:number,patch:Partial<LearnedField>)=>setFields(old=>old.map((f,n)=>i===n?{...f,...patch}:f));
  return <form onSubmit={e=>{e.preventDefault();void apply('schema',{fields:fields.map(({key,label,scope,type,required,omit,candidate,worksheet,cell,column,prototypeRow})=>({key,label,scope,type,required,omit,candidate,worksheet,cell,column,prototypeRow}))},'schema','PATCH');}}>
    <h3>Các mục tìm thấy trong mẫu</h3><p>Đây là gợi ý từ cấu trúc và kiểu ô, chưa xác nhận ý nghĩa nghiệp vụ. Mục cùng tên ở hai sheet vẫn là hai mục riêng cho đến khi bạn chọn dùng chung ở bước 3.</p>
    <Pending dirty={dirty} busy={busy} discard={()=>setFields(initial)}/>
    {!fields.length&&<p role="alert">Chưa có vùng được hỗ trợ. Mẫu cần tiêu đề cột in đậm và một dòng mẫu có sẵn.</p>}
    {fields.map((f,i)=><fieldset key={`${f.scope}:${f.key}`}><legend>{i+1}. {f.label}</legend>
      <p>{scopeLabel[f.scope]} · {f.candidates.length===1?'Một vị trí gợi ý, cần người kiểm tra':`${f.candidates.length} vị trí gợi ý — cần chọn rõ`}</p>
      <label>Tên mục<input disabled={busy} aria-label={`Tên mục ${i+1}`} value={f.label} maxLength={256} onChange={e=>update(i,{label:e.target.value})}/></label>
      <div className="uds-grid"><label>Phạm vi<select disabled={busy} aria-label={`Phạm vi mục ${i+1}`} value={f.scope} onChange={e=>update(i,{scope:e.target.value})}>{Object.entries(scopeLabel).map(([value,label])=><option key={value} value={value}>{label}</option>)}</select></label>
      <label>Kiểu dữ liệu<select disabled={busy} aria-label={`Kiểu mục ${i+1}`} value={f.type} onChange={e=>update(i,{type:e.target.value})}>{Object.entries(typeLabel).map(([value,label])=><option key={value} value={value}>{label}</option>)}</select></label>
      <label>Vị trí gợi ý<select disabled={busy||f.omit} aria-label={`Vị trí mục ${i+1}`} value={f.candidate??''} onChange={e=>update(i,{candidate:e.target.value===''?null:Number(e.target.value),worksheet:undefined})}><option value="">Chưa chọn</option>{f.candidates.map((c,n)=><option key={n} value={n}>{c.worksheet} — {c.cell?`ô ${c.cell}`:`cột ${colName(c.column!)}, dòng ${c.prototypeRow}`} (nhãn {c.labelCell})</option>)}</select></label></div>
      <label><input disabled={busy} type="checkbox" aria-label={`Bắt buộc mục ${i+1}`} checked={f.required} onChange={e=>update(i,{required:e.target.checked,omit:e.target.checked?false:f.omit})}/>Bắt buộc nhập</label>
      <label><input disabled={busy} type="checkbox" aria-label={`Bỏ qua mục ${i+1}`} checked={f.omit} onChange={e=>update(i,{omit:e.target.checked,required:e.target.checked?false:f.required})}/>Bỏ qua mục tùy chọn</label>
      <details><summary>Chọn vị trí khác có sẵn trong mẫu</summary><label>Sheet<select disabled={busy||f.omit} aria-label={`Sheet khác mục ${i+1}`} value={f.worksheet??''} onChange={e=>update(i,{worksheet:e.target.value||undefined})}><option value="">Dùng gợi ý ở trên</option>{session.sheets.map(s=><option key={s}>{s}</option>)}</select></label>
      {f.scope==='Header'?<label>Ô nhận thông tin<input disabled={busy||f.omit} aria-label={`Ô khác mục ${i+1}`} value={f.cell??''} maxLength={12} onChange={e=>update(i,{cell:e.target.value})}/></label>:<><label>Số cột (A=1, AF=32)<input disabled={busy||f.omit} aria-label={`Cột khác mục ${i+1}`} type="number" min={1} max={32} value={f.column??''} onChange={e=>update(i,{column:Number(e.target.value)})}/></label><label>Dòng mẫu có sẵn<input disabled={busy||f.omit} aria-label={`Dòng khác mục ${i+1}`} type="number" min={1} max={1000} value={f.prototypeRow??''} onChange={e=>update(i,{prototypeRow:Number(e.target.value)})}/></label></>}
      </details>
    </fieldset>)}
    <button id="office-apply-schema" disabled={busy||!fields.length}>Áp dụng các mục dữ liệu</button>
  </form>;
}
export function OfficeMapping({session,busy,apply,report}:Props) {
  const initial=session.mappings;const [sheets,setSheets]=useState(initial);const [selected,setSelected]=useState(0);const dirty=JSON.stringify(sheets)!==JSON.stringify(initial);useDirty('mapping',dirty,report);
  const fields=session.schemaDraft?.fields??[];const active=fields.filter(f=>!f.omit);const sheet=sheets[selected];
  const update=(patch:Partial<StudioMapping>)=>setSheets(old=>old.map((s,i)=>i===selected?{...s,...patch}:s));
  return <form onSubmit={e=>{e.preventDefault();void apply('multisheet-mapping',{sheets:sheets.map(s=>({sheet:s.sheet,prototypeRow:s.startRow,headers:s.headers,columns:s.columns}))},'mapping','PATCH');}}>
    <h3>Mục dữ liệu → Sheet → Ô hoặc cột</h3><p>Tối đa 3 sheet dùng cùng tập dòng theo cùng thứ tự. Sheet không được chọn giữ nguyên. Dùng cùng một mục trên nhiều sheet là lựa chọn chia sẻ rõ ràng; không gộp mục chỉ vì tên giống nhau.</p>
    <p>Không ghi vào ô công thức hoặc ô gộp. Chữ ký gộp ở sau bảng được giữ theo phạm vi hỗ trợ. Xác nhận vị trí là thao tác riêng trên máy chủ.</p>
    <Pending busy={busy} dirty={dirty} discard={()=>{setSheets(initial);setSelected(0);}}/>
    <div role="group" aria-label="Các sheet dữ liệu">{sheets.map((s,i)=><button disabled={busy} type="button" key={i} aria-pressed={selected===i} onClick={()=>setSelected(i)}>{s.sheet}</button>)}</div>
    {sheet&&<fieldset><legend>Sheet dữ liệu {selected+1}</legend><div className="uds-grid"><label>Tên sheet<select disabled={busy} aria-label="Sheet đang ánh xạ" value={sheet.sheet} onChange={e=>update({sheet:e.target.value})}>{session.sheets.map(n=><option key={n}>{n}</option>)}</select></label>
      <label>Dòng đầu tiên nhận dữ liệu<input disabled={busy} aria-label="Dòng dữ liệu đầu tiên" type="number" min={1} max={1000} value={sheet.startRow} onChange={e=>update({startRow:Number(e.target.value)})}/></label></div>
      <div className="uds-scroll" tabIndex={0} role="region" aria-label="Bảng ánh xạ Excel"><table><thead><tr><th>Mục dữ liệu</th><th>Kiểu / trạng thái</th><th>Vị trí trong {sheet.sheet}</th><th>Dùng chung</th></tr></thead><tbody>{fields.map(f=>{
        const header=f.scope==='Header';const dest=header?sheet.headers:sheet.columns;const shared=sheets.filter(s=>(header?s.headers:s.columns)[f.key]!==undefined).length;
        return <tr key={`${f.scope}:${f.key}`}><th scope="row">{f.label}</th><td>{typeLabel[f.type]} · {f.omit?'Bỏ qua':f.required?'Bắt buộc':'Tùy chọn'}</td><td>{f.omit?'Không sử dụng':<input disabled={busy} aria-label={`Vị trí ${sheet.sheet} ${f.label}`} placeholder={header?'Ví dụ B1':'Số cột: A=1'} type={header?'text':'number'} min={header?undefined:1} max={header?undefined:32} maxLength={12} value={dest[f.key]??''} onChange={e=>{
          const next={...dest};if(e.target.value==='')delete next[f.key];else next[f.key]=header?e.target.value:Number(e.target.value);update(header?{headers:next as Record<string,string>}:{columns:next as Record<string,number>});
        }}/>}</td><td>{f.omit?'—':shared>1?`Cùng mục trên ${shared} sheet`:'Mục riêng / một sheet'}</td></tr>;
      })}</tbody></table></div><button disabled={busy} type="button" onClick={()=>{setSheets(old=>old.filter((_,i)=>i!==selected));setSelected(0);}}>Bỏ sheet khỏi ánh xạ</button>
    </fieldset>}
    <button disabled={busy||sheets.length>=3} type="button" onClick={()=>{const name=session.sheets.find(n=>!sheets.some(s=>s.sheet===n));if(name){setSheets(old=>[...old,{sheet:name,startRow:session.sheetSummaries.find(s=>s.name===name)?.prototypeRows[0]??1,headers:{},columns:{}}]);setSelected(sheets.length);}}}>Thêm sheet dữ liệu</button>
    <button id="office-apply-mapping" disabled={busy||!sheets.length||!active.length}>Áp dụng vị trí Excel</button>
    <p>Trạng thái máy chủ: {session.mappingState==='UserConfirmed'?'Đã xác nhận':'Chưa xác nhận'}. Bảng đang sửa chưa thay đổi trạng thái này.</p>
  </form>;
}
export function OfficeRules({session,busy,apply,report}:Props) {
  const initial=session.validationRules;const [rules,setRules]=useState(initial);const dirty=JSON.stringify(rules)!==JSON.stringify(initial);useDirty('rules',dirty,report);
  const decimals=session.schemaDraft?.fields.filter(f=>!f.omit&&f.type==='Decimal')??[];
  const options=(scope:string)=><><option value="">Chọn mục Số</option>{decimals.filter(f=>f.scope===scope).map(f=><option key={f.key} value={f.key}>{f.label}</option>)}</>;
  const update=(i:number,p:Partial<StudioRule>)=>setRules(old=>old.map((r,n)=>i===n?{...r,...p}:r));
  return <form onSubmit={e=>{e.preventDefault();void apply('validation-rules',{rules},'rules','PATCH');}}><h3>Quy tắc kiểm tra đã nhập</h3><p>Tối đa 8 quy tắc. Chỉ so sánh số đã nhập, không tự thay thành tiền hoặc suy ra độ đúng của chứng từ nguồn. Đổi quy tắc cần xác nhận, rà soát và phê duyệt lại.</p>
    <Pending busy={busy} dirty={dirty} discard={()=>setRules(initial)}/>
    {rules.map((r,i)=><fieldset key={r.id}><legend>Quy tắc {i+1}: {ruleText(r,session)}</legend>
      <label>Cách kiểm tra<select disabled={busy} aria-label={`Cách kiểm tra ${i+1}`} value={r.kind} onChange={e=>update(i,{kind:e.target.value,target:'',operator:e.target.value==='Arithmetic'?'Multiply':null,fieldB:e.target.value==='Arithmetic'?'':null})}><option value="Arithmetic">Phép tính từng dòng</option><option value="CrossFoot">Cộng cả cột</option></select></label>
      <label>Mục thứ nhất<select disabled={busy} aria-label={`Mục A quy tắc ${i+1}`} value={r.fieldA} onChange={e=>update(i,{fieldA:e.target.value})}>{options('LineItem')}</select></label>
      {r.kind==='Arithmetic'&&<><label>Phép tính<select disabled={busy} aria-label={`Phép tính ${i+1}`} value={r.operator??'Multiply'} onChange={e=>update(i,{operator:e.target.value})}>{Object.entries({Add:'Cộng',Subtract:'Trừ',Multiply:'Nhân',Divide:'Chia'}).map(([v,l])=><option key={v} value={v}>{l}</option>)}</select></label><label>Mục thứ hai<select disabled={busy} aria-label={`Mục B quy tắc ${i+1}`} value={r.fieldB??''} onChange={e=>update(i,{fieldB:e.target.value})}>{options('LineItem')}</select></label></>}
      <label>Kết quả đã nhập<select disabled={busy} aria-label={`Kết quả quy tắc ${i+1}`} value={r.target} onChange={e=>update(i,{target:e.target.value})}>{options(r.kind==='Arithmetic'?'LineItem':'Header')}</select></label>
      <label>Độ lệch cho phép (0–1)<input disabled={busy} aria-label={`Độ lệch ${i+1}`} type="number" min={0} max={1} step="any" value={r.tolerance} onChange={e=>update(i,{tolerance:Number(e.target.value)})}/></label>
      <label><input disabled={busy} aria-label={`Bắt buộc kiểm tra ${i+1}`} type="checkbox" checked={r.required} onChange={e=>update(i,{required:e.target.checked})}/>Bắt buộc đánh giá</label>
      <label>Mức độ<select disabled={busy} aria-label={`Mức độ ${i+1}`} value={r.severity} onChange={e=>update(i,{severity:e.target.value})}><option value="BlockingError">Lỗi chặn</option><option value="Warning">Cảnh báo cần xử lý</option></select></label>
      <button disabled={busy} type="button" onClick={()=>setRules(old=>old.filter((_,n)=>i!==n))}>Xóa quy tắc {i+1}</button><button disabled={busy||i===0} type="button" onClick={()=>setRules(old=>{const n=[...old];[n[i-1],n[i]]=[n[i],n[i-1]];return n;})}>Đưa quy tắc {i+1} lên</button>
    </fieldset>)}
    <button disabled={busy||rules.length>=8} type="button" onClick={()=>setRules(old=>[...old,{id:Array.from({length:9},(_,n)=>`rule${n+1}`).find(id=>!old.some(r=>r.id===id))!,kind:'Arithmetic',fieldA:'',operator:'Multiply',fieldB:'',target:'',tolerance:0,required:true,severity:'BlockingError'}])}>Thêm quy tắc</button>
    <button id="office-apply-rules" disabled={busy||!dirty}>Áp dụng quy tắc</button>
  </form>;
}
function ValueInput({field,value,set,busy,label}:{field:LearnedField;value:string;set:(v:string)=>void;busy:boolean;label:string}) {
  return field.type==='Boolean'?<select disabled={busy} aria-label={label} value={value} onChange={e=>set(e.target.value)}><option value="">Chưa nhập</option><option value="true">Có</option><option value="false">Không</option></select>:<input disabled={busy} aria-label={label} type={field.type==='Date'?'date':'text'} inputMode={field.type==='Decimal'?'decimal':undefined} value={value} maxLength={2000} onChange={e=>set(e.target.value)} aria-invalid={!validEntry(field,value)}/>;
}
export function OfficeData({session,busy,apply,report}:Props) {
  const fields=session.schemaDraft?.fields.filter(f=>!f.omit)??[];const headers=fields.filter(f=>f.scope==='Header');const columns=fields.filter(f=>f.scope==='LineItem');
  const [values,setValues]=useState<Record<string,string>>(()=>Object.fromEntries(session.fields.map(f=>[`${f.scope}:${f.row??0}:${f.key}`,entryValue(f.type,f.value)])));const [initial]=useState(values);
  const [rows,setRows]=useState(()=>Math.max(1,...session.fields.filter(f=>f.row!==null).map(f=>f.row!+1)));const [initialRows]=useState(rows);
  const key=(f:LearnedField,row:number)=>`${f.scope}:${row}:${f.key}`;
  const dirty=JSON.stringify(values)!==JSON.stringify(initial)||rows!==initialRows;useDirty('data',dirty,report);
  const invalid=fields.some(f=>Array.from({length:f.scope==='Header'?1:rows},(_,row)=>!validEntry(f,values[key(f,row)]??'')).some(Boolean));
  return <form onSubmit={e=>{e.preventDefault();if(!invalid)void apply('manual-sample',{header:Object.fromEntries(headers.map(f=>[f.key,values[key(f,0)]??''])),rows:Array.from({length:rows},(_,row)=>Object.fromEntries(columns.map(f=>[f.key,values[key(f,row)]??''])))},'data');}}>
    <h3>Nhập dữ liệu chứng từ</h3><p>Nhập tay, chưa có OCR/AI. Số dùng dấu chấm, không phân nhóm hàng nghìn (ví dụ 2.50). Giữ mã như 00123 ở kiểu Văn bản. Ngày được gửi theo năm-tháng-ngày. Máy chủ quyết định kết quả kiểm tra.</p>
    <Pending busy={busy} dirty={dirty} discard={()=>{setValues(initial);setRows(initialRows);}}/>
    <div className="uds-grid">{headers.map(f=><label key={f.key}>{f.label} · {typeLabel[f.type]} {f.required?'(bắt buộc)':''}<ValueInput busy={busy} field={f} label={`Thông tin ${f.label}`} value={values[key(f,0)]??''} set={v=>setValues(old=>({...old,[key(f,0)]:v}))}/></label>)}</div>
    <div className="uds-scroll" role="region" tabIndex={0} aria-label="Bảng nhập chi tiết"><table><thead><tr><th>Dòng</th>{columns.map(f=><th key={f.key} scope="col">{f.label}<small>{typeLabel[f.type]} {f.required?'· Bắt buộc':''}</small></th>)}</tr></thead><tbody>{Array.from({length:rows},(_,row)=><tr key={row}><th scope="row">{row+1}</th>{columns.map(f=><td key={f.key}><ValueInput field={f} busy={busy} label={`Dòng ${row+1} ${f.label}`} value={values[key(f,row)]??''} set={v=>setValues(old=>({...old,[key(f,row)]:v}))}/></td>)}</tr>)}</tbody></table></div>
    <div className="uds-inline"><span role="status">{rows}/20 dòng</span><button type="button" disabled={busy||rows>=20} onClick={()=>setRows(n=>n+1)}>Thêm dòng</button><button type="button" disabled={busy||rows<=1} onClick={()=>{setValues(old=>Object.fromEntries(Object.entries(old).filter(([k])=>!k.startsWith(`LineItem:${rows-1}:`))));setRows(n=>n-1);}}>Xóa dòng cuối</button></div>
    {invalid&&<p role="alert">Định dạng số/ngày chưa rõ. Không dùng dấu phẩy hay ký hiệu phân nhóm trong số; hệ thống không tự đoán giá trị.</p>}
    <button id="office-apply-data" disabled={busy||invalid||session.mappingState!=='UserConfirmed'}>Gửi dữ liệu để kiểm tra</button>
    {session.mappingState!=='UserConfirmed'&&<p>Xác nhận vị trí Excel ở bước 3 trước khi gửi dữ liệu.</p>}
  </form>;
}
