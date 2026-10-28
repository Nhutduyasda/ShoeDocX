import { useCallback, useEffect, useRef, useState } from 'react';
import { FileDrop } from '../../components/ui/file-drop';
import { ConfirmAction } from '../../components/ui/confirm-action';
import { useAuth } from '../../contexts/AuthContext';
import { StudioApi, type OfficePreview, type StudioBootstrap, type StudioDraft, type StudioSession } from './studioApi';
import { OfficeData, OfficeFields, OfficeMapping, OfficeRules, type OfficeSection } from './OfficeEditors';
import { OfficeRequestError, checkLabel, issueText, officeError, ruleText, typeLabel } from './officeModel';
const steps=['Chọn mẫu Excel','Xem cấu trúc','Chọn vị trí dữ liệu','Nhập và kiểm tra','Rà soát và phê duyệt','Xuất Excel'];
const SESSION='universal-doc-office-session',LINK='universal-doc-office-draft-link';
type Binding={sessionId:string;draft:StudioDraft};
function download(blob:Blob) {const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download='chung-tu.xlsx';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
export default function OfficeWizard({onDemo}:{onDemo:()=>void}) {
  const {user,logout}=useAuth();const [api]=useState(()=>new StudioApi(true));const [boot,setBoot]=useState<StudioBootstrap|null>(null);
  const [session,setSession]=useState<StudioSession|null>(null);const [preview,setPreview]=useState<OfficePreview|null>(null);
  const [step,setStep]=useState(0);const [busy,setBusy]=useState(true);const [error,setError]=useState('');const [notice,setNotice]=useState('');
  const workspaceRef=useRef<HTMLElement>(null);
  useEffect(()=>{workspaceRef.current?.scrollTo({top:0});},[step]);
  const templateInput=useRef<HTMLInputElement>(null), exampleInput=useRef<HTMLInputElement>(null);
  const [template,setTemplate]=useState<File|null>(null);const [example,setExample]=useState<File|null>(null);const [loaded,setLoaded]=useState<{name:string;size:number}|null>(null);
  const [drafts,setDrafts]=useState<StudioDraft[]>([]);const [binding,setBinding]=useState<Binding|null>(null);const [bindingLoaded,setBindingLoaded]=useState(false);const [savedVersion,setSavedVersion]=useState<number|null>(null);
  const [dirty,setDirty]=useState<Partial<Record<OfficeSection,boolean>>>({});const formsDirty=Object.values(dirty).some(Boolean);const unsaved=formsDirty||!!template||!!example;
  const report=useCallback((section:OfficeSection,value:boolean)=>{setDirty(old=>old[section]===value?old:{...old,[section]:value});if(value)setNotice('');},[]);
  const refresh=async(s:StudioSession)=>{setSession(s);setPreview(null);const p=await api.officePreview(s);if(p.version!==s.version||p.sessionId!==s.id)throw new OfficeRequestError('SESSION_VERSION_CONFLICT',409);setPreview(p);};
  useEffect(()=>{let stopped=false;const abort=new AbortController();void (async()=>{
    try {const b=await api.bootstrap(abort.signal);if(stopped)return;setBoot(b);
      const id=sessionStorage.getItem(SESSION);const results=await Promise.allSettled([b.draftsEnabled?api.drafts():Promise.resolve([]),id?api.read(id):Promise.resolve(null)]);if(stopped)return;
      const list=results[0].status==='fulfilled'?results[0].value:[];setDrafts(list);
      if(results[0].status==='rejected')setError(results[0].reason instanceof OfficeRequestError?results[0].reason.message:officeError('REQUEST_FAILED'));
      if(results[1].status==='fulfilled'&&results[1].value){const s=results[1].value;await refresh(s);if(stopped)return;
        try {const link=JSON.parse(sessionStorage.getItem(LINK)||'null');const d=list.find(d=>d.id===link?.draftId);if(d&&link.sessionId===s.id&&Number.isSafeInteger(link.revision)&&link.revision>0)setBinding({sessionId:s.id,draft:{...d,revision:link.revision}});}catch{/* Opaque convenience metadata has no authority. */}
      }else if(id){sessionStorage.removeItem(SESSION);}
    }catch(e){if(!stopped)setError(e instanceof OfficeRequestError?e.message:officeError('REQUEST_FAILED'));}finally{if(!stopped){setBindingLoaded(true);setBusy(false);}}
  })();return()=>{stopped=true;abort.abort();};},[api]);
  useEffect(()=>{if(session)sessionStorage.setItem(SESSION,session.id);},[session]);
  useEffect(()=>{if(!bindingLoaded)return;if(binding)sessionStorage.setItem(LINK,JSON.stringify({sessionId:binding.sessionId,draftId:binding.draft.id,revision:binding.draft.revision}));else sessionStorage.removeItem(LINK);},[binding,bindingLoaded]);
  useEffect(()=>{if(!unsaved)return;const guard=(e:BeforeUnloadEvent)=>{e.preventDefault();e.returnValue='';};window.addEventListener('beforeunload',guard);return()=>window.removeEventListener('beforeunload',guard);},[unsaved]);
  async function run(operation:()=>Promise<void>) {setBusy(true);setError('');setNotice('');try{await operation();}catch(e){setSavedVersion(null);setPreview(null);setError(e instanceof OfficeRequestError?e.message:officeError('REQUEST_FAILED'));}finally{setBusy(false);}}
  function navigate(next:number){if(busy)return;if(unsaved&&next!==step){setError(officeError('UNSAVED_CHANGES'));return;}setStep(next);setError('');}
  async function apply(action:string,body:Record<string,unknown>,section:OfficeSection,method='POST') {
    if(!session)return;
    if(template||example||Object.entries(dirty).some(([name,value])=>value&&name!==section)){setError(officeError('UNSAVED_CHANGES'));return;}
    await run(async()=>{await refresh(await api.act(session,action,body,method));setNotice('Đã áp dụng lên máy chủ. Kiểm tra trạng thái trước khi tiếp tục.');});
  }
  function action(name:string){if(!session)return;if(unsaved){setError(officeError('UNSAVED_CHANGES'));return;}void run(async()=>{await refresh(await api.act(session,name));setNotice(name==='mapping/confirm'?'Đã xác nhận vị trí Excel hiện tại.':name==='review'?'Đã hoàn tất rà soát dữ liệu hiện tại.':'Đã phê duyệt dữ liệu hiện tại.');});}
  const current=preview&&session&&preview.sessionId===session.id&&preview.version===session.version?preview:null;
  const editorKey=session?`${session.id}:${session.profileVersion}:${session.schemaDraft?.revision}:${session.dataRevision}`:'';
  const editorProps=session?{session,busy,apply,report}:null;
  const saved=!!session&&savedVersion===session.version&&!unsaved;
  const issues=session?.issues??[];
  return <main ref={workspaceRef} className="uds office-wizard" lang="vi">
    <header className="uds-top"><div><p className="uds-eyebrow">SHOEDOCX · CÔNG CỤ CHỨNG TỪ</p><h1>Trợ lý Excel văn phòng</h1><p>Chọn mẫu, kiểm tra vị trí, nhập dữ liệu và xuất sau phê duyệt.</p></div><div><span>{user?.fullName}</span><a href="/">Không gian làm việc</a><button disabled={busy||unsaved} onClick={onDemo}>Synthetic Demo / kỹ thuật</button><button onClick={logout}>Đăng xuất</button></div></header>
    <p className="uds-banner">Chưa có OCR hoặc AI nhận dạng. Ví dụ Excel chỉ cung cấp gợi ý cấu trúc; dữ liệu nhập tay cần được kiểm tra. Phiên hết hạn sau 20 phút. Bản nháp đã lưu được giữ tối đa 7 ngày.</p>
    <div className="office-workspace"><nav className="uds-steps" aria-label="Các bước xử lý Excel">{steps.map((label,i)=><button disabled={busy} key={label} aria-current={step===i?'step':undefined} onClick={()=>navigate(i)}><span>{i+1}</span>{label}</button>)}</nav>
    <div className="office-editing">{error&&<div role="alert" className="uds-error">{error}</div>}
    {notice&&<p role="status" className="uds-success">{notice}</p>}
    {unsaved&&<p role="status" className="uds-error">Có nội dung chưa áp dụng. Đây chưa phải dữ liệu đã lưu hoặc được xác nhận.</p>}
    {session&&<aside className="uds-state"><strong>Trạng thái máy chủ:</strong> vị trí {session.mappingState==='UserConfirmed'?'đã xác nhận':'chưa xác nhận'}; dữ liệu {session.reviewState==='Approved'?'đã phê duyệt':session.reviewState==='ReadyForApproval'?'đã rà soát, chờ phê duyệt':'chưa hoàn tất rà soát/phê duyệt'}.
      <button disabled={busy||unsaved} onClick={()=>void run(async()=>{await refresh(await api.read(session.id));if(boot?.draftsEnabled)setDrafts(await api.drafts());setSavedVersion(null);setNotice('Đã tải trạng thái mới nhất từ máy chủ.');})}>Tải lại trạng thái</button>
      <button disabled={busy||unsaved} onClick={()=>void run(async()=>{await api.remove(session);setSession(null);setPreview(null);setBinding(null);setSavedVersion(null);setLoaded(null);sessionStorage.removeItem(SESSION);setStep(0);})}>Đóng phiên hiện tại</button>
    </aside>}
    <section className="uds-panel" aria-busy={busy}><h2>{step+1}. {steps[step]}</h2>
    {step===0&&<>
      <p><strong>Mẫu đầu ra:</strong> tệp .xlsx có bố cục muốn giữ. <strong>Ví dụ đã điền:</strong> tệp cùng cấu trúc, có 1–5 dòng dữ liệu minh họa, không phải kết quả đọc ảnh/PDF.</p>
      <p>Giới hạn: 4 sheet tổng, 3 sheet dữ liệu, một dòng mẫu mỗi sheet, 20 dòng đầu ra, 8 quy tắc, 2.000 ô đã phân tích; không quá dòng 1.000/cột AF. Tổng mẫu, ví dụ và nguồn tối đa 2 MiB. Không hỗ trợ macro, liên kết ngoài hoặc công thức ngoài ngữ pháp được chấp nhận.</p>
      <form className="uds-inline" onSubmit={e=>{e.preventDefault();if(template)void run(async()=>{const s=session??await api.create('example-template');setSession(s);const next=await api.upload(s,template);setLoaded({name:template.name,size:template.size});setTemplate(null);if(templateInput.current)templateInput.current.value='';await refresh(next);setNotice('Đã phân tích mẫu. Xem danh sách sheet và gợi ý ở bước 2.');});}}><FileDrop file={template} disabled={busy||formsDirty} inputRef={templateInput} onChange={setTemplate} onError={setError}/><button disabled={busy||formsDirty||!template}>Tải và phân tích mẫu</button></form>
      {template&&<p>{template.name} · {template.size} byte <button disabled={busy} onClick={()=>{setTemplate(null);if(templateInput.current)templateInput.current.value='';}}>Bỏ tệp mẫu đã chọn</button></p>}
      {loaded&&<p>Mẫu vừa áp dụng: {loaded.name} · {loaded.size} byte</p>}
      {session&&<form className="uds-inline" onSubmit={e=>{e.preventDefault();if(example)void run(async()=>{await refresh(await api.upload(session,example,'example'));setExample(null);if(exampleInput.current)exampleInput.current.value='';setNotice('Đã đối chiếu ví dụ. Cần kiểm tra lại các mục và vị trí Excel.');});}}><label>Ví dụ đã điền (tùy chọn)<input disabled={busy||formsDirty} ref={exampleInput} aria-label="Ví dụ Excel đã điền" type="file" accept=".xlsx" onChange={e=>setExample(e.target.files?.[0]??null)}/></label><button disabled={busy||formsDirty||!example||!!template}>Đối chiếu ví dụ</button></form>}
      {example&&<p>{example.name} · {example.size} byte <button disabled={busy} onClick={()=>{setExample(null);if(exampleInput.current)exampleInput.current.value='';}}>Bỏ ví dụ đã chọn</button></p>}
      {session&&<p>Sheet từ máy chủ: {session.sheets.join(', ')||'Chưa có mẫu hợp lệ'}</p>}
    </>}
    {step>0&&!session&&<p>Hãy chọn và tải mẫu ở bước 1, hoặc mở bản nháp đã lưu.</p>}
    {step===1&&session&&<>
      <div className="uds-scroll" role="region" tabIndex={0} aria-label="Cấu trúc Excel đã phân tích"><table><thead><tr><th>Sheet</th><th>Ô đã phân tích</th><th>Bảng gợi ý / dòng mẫu</th><th>Ô gộp</th></tr></thead><tbody>{session.sheetSummaries.map(s=><tr key={s.name}><th>{s.name}</th><td>{s.usedCells}</td><td>{s.tables} / {s.prototypeRows.join(', ')||'Chưa thấy'}</td><td>{s.mergedRanges.join(', ')||'Không có'}</td></tr>)}</tbody></table></div>
      {editorProps&&<OfficeFields key={editorKey} {...editorProps}/>}
      {session.schemaDraft?.correspondence&&<p>Ví dụ đã được đối chiếu cấu trúc: {session.schemaDraft.correspondence.sheets.map(s=>`${s.worksheet}: ${s.exampleRowCount} dòng (${s.firstExampleRow}–${s.lastExampleRow})`).join('; ')}. Chưa xác nhận ánh xạ nghiệp vụ.</p>}
    </>}
    {step===2&&session&&<>{session.mapping&&editorProps?<OfficeMapping key={editorKey} {...editorProps}/>:<p>Áp dụng lựa chọn các mục dữ liệu ở bước 2 trước.</p>}
      {editorProps&&session.mapping&&<OfficeRules key={`rules:${editorKey}`} {...editorProps}/>}
      <button disabled={busy||unsaved||session.mappingState!=='DraftSuggested'||session.schemaDraft?.state!=='ReadyForConfirmation'} onClick={()=>action('mapping/confirm')}>Xác nhận vị trí Excel</button>
      <p>Xác nhận chỉ áp dụng cho cấu hình hiện tại. Đổi sheet, mục, quy tắc hoặc ví dụ cần xác nhận lại.</p></>}
    {step===3&&session&&editorProps&&<><h3>Quy tắc hiện tại</h3><ul>{session.validationRules.map(r=><li key={r.id}>{ruleText(r,session)}</li>)}</ul><OfficeData key={`data:${editorKey}`} {...editorProps}/></>}
    {step>=3&&session&&<>
      <h3>Kết quả kiểm tra từ máy chủ</h3><p className="office-check-summary" data-passed={session.checks.length>0&&session.checks.every(c=>c.outcome==='Passed')}>{session.checks.filter(c=>c.outcome==='Passed').length} đạt · {session.checks.filter(c=>c.outcome==='NotEvaluated').length} chưa đánh giá · {session.checks.filter(c=>c.outcome!=='Passed'&&c.outcome!=='NotEvaluated').length} chưa đạt.</p><details className="office-check-details"><summary>Chi tiết {session.checks.length} kiểm tra máy chủ</summary><ul aria-label="Kết quả kiểm tra">{session.checks.map(c=><li key={c.id}>{checkLabel(c.id,session)}: {c.outcome==='Passed'?'Đạt':c.outcome==='NotEvaluated'?'Chưa đánh giá':'Chưa đạt'} {c.required?'· Bắt buộc':''}</li>)}</ul></details>
      {!session.checks.length&&<p>Chưa có kết quả; hãy gửi dữ liệu để máy chủ kiểm tra.</p>}
      <ul aria-label="Các lỗi và cảnh báo">{issues.map((i,n)=>{return <li key={n}>{i.severity==='Warning'?'Cảnh báo':'Cần xử lý'}: {i.field?checkLabel(i.field,session):'Giá trị hoặc cấu hình'} — {issueText[i.code]??'Máy chủ phát hiện dữ liệu chưa phù hợp. Kiểm tra các mục đang dùng và quy tắc.'}</li>;})}</ul>
      <details className="office-preview-details" open={step===3}><summary>Xem dữ liệu và vị trí dự kiến</summary><p>Chỉ là thông tin từ phiên được cấp quyền, chưa tạo tệp Excel. Không dùng làm chứng từ đã phê duyệt.</p>
      {current?<div className="uds-scroll" role="region" tabIndex={0} aria-label="Xem dữ liệu theo vị trí Excel"><table><thead><tr><th>Mục / dòng</th><th>Kiểu</th><th>Giá trị hiện tại</th><th>Vị trí dự kiến</th></tr></thead><tbody>{current.fields.map(f=><tr key={`${f.scope}:${f.row}:${f.key}`}><th>{f.label}{f.row!==null?` · dòng ${f.row+1}`:''}</th><td>{typeLabel[f.type]}</td><td>{f.missing?(f.required?'Thiếu giá trị bắt buộc':'Chưa nhập'):f.value}</td><td>{f.destinations.map(d=>`${d.sheet}!${d.cell}`).join('; ')||'Chưa có vị trí'}</td></tr>)}</tbody></table></div>:<p>Cần tải lại trạng thái để xem thông tin hiện tại.</p>}</details>
    </>}
    {step===4&&session&&<><h3>Tóm tắt trước phê duyệt</h3><p>Sheet: {session.sheets.join(', ')}. Sheet nhận dữ liệu: {session.mappings.map(s=>s.sheet).join(', ')||'Chưa chọn'}.</p>
      <p>Vị trí: {current?.readiness.mappingConfirmed?'Đã xác nhận trên máy chủ':'Cần xác nhận'}. Kiểm tra dữ liệu: {current?.readiness.dataValid?'Đạt điều kiện dữ liệu trên máy chủ':'Chưa đạt/chưa kiểm tra'}.</p>
      {!session.canReview&&<p>Tài khoản này không có quyền hoàn tất rà soát.</p>}{!session.canApprove&&<p>Tài khoản này không có quyền phê duyệt hoặc xuất tài liệu.</p>}
      <button disabled={busy||unsaved||!current?.readiness.canReviewNow} onClick={()=>action('review')}>Hoàn tất rà soát</button><button disabled={busy||unsaved||!current?.readiness.canApproveNow} onClick={()=>action('approve')}>Phê duyệt dữ liệu</button>
    </>}
    {step>=4&&session&&<ul aria-label="Điều kiện còn thiếu">{current?.readiness.blockers.map(code=><li key={code}>{officeError(code)}</li>)}</ul>}
    {step===5&&session&&<><p role="status">{current?.readiness.canExport&&!unsaved?'Máy chủ xác nhận đủ điều kiện xuất ở phiên bản hiện tại.':'Chưa đủ điều kiện xuất Excel. Xử lý các điều kiện còn thiếu và thay đổi chưa áp dụng.'}</p>
      <button disabled={busy||unsaved||!current?.readiness.canExport} onClick={()=>void run(async()=>{const bytes=await api.export(session);if(bytes.size===0)throw new OfficeRequestError('REQUEST_FAILED');download(bytes);await refresh(await api.read(session.id));setNotice('Đã nhận tệp Excel từ máy chủ và bắt đầu tải xuống.');})}>Tải Excel đã phê duyệt</button>
      <p>Máy chủ kiểm tra lại quyền, phiên bản, thời hạn và phê duyệt ngay trước khi trả bytes. Thông tin xem trước không thay thế bước này.</p></>}
    <footer className="uds-inline"><button disabled={busy||step===0} onClick={()=>navigate(step-1)}>Bước trước</button><button disabled={busy||step===5} onClick={()=>navigate(step+1)}>Bước tiếp theo</button></footer>
    </section>
    {boot?.draftsEnabled&&<section className="uds-panel office-drafts" aria-label="Bản nháp riêng tư"><h2>Bản nháp của tôi</h2><p>Tối đa 2 bản riêng tư, 7 ngày tính từ lúc tạo. Lưu bản nháp không đồng nghĩa phê duyệt. Mở lại sau restart cần đăng nhập và xác nhận/rà soát/phê duyệt mới.</p>
      <p role="status">{saved?'Đã lưu bản nháp của trạng thái hiện tại.':'Chưa xác nhận lưu trạng thái hiện tại thành bản nháp.'}</p>
      <button disabled={busy||unsaved} onClick={()=>void run(async()=>{setDrafts(await api.drafts());})}>Tải danh sách bản nháp</button>
      {session&&<><button disabled={busy||unsaved||!session.schemaDraft} onClick={()=>void run(async()=>{const result=await api.saveDraft(session,binding?.sessionId===session.id?binding.draft:undefined);await refresh(result.session);setBinding({sessionId:result.session.id,draft:result.draft});setDrafts(await api.drafts());setSavedVersion(result.session.version);setNotice('Máy chủ đã xác nhận lưu bản nháp.');})}>Lưu bản nháp</button>
      {binding?.sessionId===session.id&&<button disabled={busy||unsaved} onClick={()=>void run(async()=>{const r=await api.saveDraft(session);await refresh(r.session);setBinding({sessionId:r.session.id,draft:r.draft});setDrafts(await api.drafts());setSavedVersion(r.session.version);setNotice('Đã lưu thành một bản nháp mới, riêng biệt.');})}>Lưu thành bản mới</button>}</>}
      {drafts.map((d,i)=><article className="uds-state" key={d.id}><h3>Bản nháp Excel {i+1}</h3><p>Cập nhật {new Date(d.updatedAt).toLocaleString('vi-VN')} · hết hạn {new Date(d.expiresAt).toLocaleString('vi-VN')}</p>
      {session&&binding?.sessionId===session.id&&binding.draft.id===d.id&&binding.draft.revision!==d.revision&&<><p>Bản nháp đã được yêu cầu khác cập nhật. Mở phiên bản mới hoặc chọn rõ thay thế ở lần lưu sau.</p><button disabled={busy||unsaved} onClick={()=>{setBinding({sessionId:session.id,draft:d});setSavedVersion(null);}}>Chọn thay thế bản nháp {i+1} ở lần lưu sau</button></>}
      <button disabled={busy||unsaved} aria-label={`Mở bản nháp ${i+1}`} onClick={()=>void run(async()=>{const s=await api.resumeDraft(d);await refresh(s);setBinding({sessionId:s.id,draft:d});setSavedVersion(null);setLoaded(null);setStep(1);setNotice('Đã mở bản nháp vào phiên mới. Cần xác nhận vị trí và lặp lại rà soát, phê duyệt.');})}>Mở bản nháp {i+1}</button>
      <ConfirmAction disabled={busy||unsaved} title={`Xóa bản nháp ${i+1}?`} description="Bản nháp đã lưu sẽ bị xóa. Phiên đang mở vẫn giữ thời hạn và quyền hiện tại. Thao tác này không thể hoàn tác." onConfirm={()=>run(async()=>{await api.deleteDraft(d);setDrafts(await api.drafts());if(binding?.draft.id===d.id)setBinding(null);setSavedVersion(null);setNotice('Đã xóa bản nháp. Phiên đang mở vẫn tuân theo thời hạn và quyền riêng của nó.');})}>Xóa bản nháp {i+1}</ConfirmAction>
      </article>)}
    </section>}
    </div><aside className="office-context" aria-label="Tiến độ và trạng thái lưu"><span className="section-kicker">PHIÊN LÀM VIỆC</span><h2>{steps[step]}</h2><p>Bước {step+1} / 6</p><dl><dt>Thay đổi cục bộ</dt><dd>{unsaved?'Chưa áp dụng':'Không có thay đổi chờ áp dụng'}</dd><dt>Bản nháp</dt><dd>{saved?'Đã được máy chủ xác nhận lưu':'Chưa xác nhận lưu trạng thái hiện tại'}</dd><dt>Vị trí Excel</dt><dd>{session?.mappingState==='UserConfirmed'?'Đã xác nhận':'Chưa xác nhận'}</dd><dt>Kiểm tra dữ liệu</dt><dd>{current?.readiness.dataValid?'Máy chủ xác nhận hợp lệ':'Chưa hợp lệ hoặc chưa kiểm tra'}</dd><dt>Phê duyệt</dt><dd>{session?.reviewState==='Approved'?'Đã phê duyệt':session?.reviewState==='ReadyForApproval'?'Chờ phê duyệt':'Chưa hoàn tất'}</dd></dl><p className="office-session-note">Phiên có thời hạn 20 phút. Bản nháp riêng tư được giữ tối đa 7 ngày. Khi mở lại cần kiểm tra trạng thái mới từ máy chủ.</p><p>Thông tin xem trước chỉ mô tả dữ liệu và vị trí, không phải bản in Excel chính xác.</p></aside></div>
  </main>;
}
