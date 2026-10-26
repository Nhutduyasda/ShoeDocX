// Deterministic paired captures of real legacy and Office UI. No API route mocks.
import assert from 'node:assert/strict';
import {mkdir,writeFile,readFile,access} from 'node:fs/promises';
import {resolve,join} from 'node:path';
import {pathToFileURL} from 'node:url';
import {createHash} from 'node:crypto';
const tools=resolve(process.env.STUDIO_PLAYWRIGHT_MODULE);
const {chromium}=await import(pathToFileURL(join(tools,'index.mjs')).href);
const {PNG}=await import(pathToFileURL(join(tools,'../pngjs/lib/png.js')).href).then(m=>m.default??m);
const folder=resolve(process.env.GATE6A_VISUAL_PRIVATE);await mkdir(folder,{recursive:true});
// Fixed before any captures: ignore <=16 channel noise; <=0.1% remaining pixels.
// Structural tolerance <=0.5 CSS px, exact visible text and selected CSS styles.
const policy={channelNoise:16,maxDifferentPixelRatio:.001,maxBoundsDelta:.5,pointer:'neutral (0,0) before each capture; keyboard focus preserved',masking:'SQL draft timestamp paragraph only (.office-drafts article.uds-state > p); controls and document content unmasked',animations:'disabled',caret:'hide',deviceScaleFactor:1,locale:'vi-VN',timezoneId:'Asia/Ho_Chi_Minh',fixedDate:'2026-10-24T10:00:00Z'};
await writeFile(join(folder,'comparison-policy.json'),JSON.stringify(policy,null,2));
const browser=await chromium.launch({headless:true});
const captures={baseline:[],feature:[]},errors=[],printProbes={};
const viewports=[{width:1360,height:1000},{width:390,height:844}];
const hash=b=>createHash('sha256').update(b).digest('hex');
async function settle(page){await page.waitForLoadState('networkidle');await page.locator('.ant-spin-spinning').first().waitFor({state:'hidden'});await page.evaluate(()=>document.fonts.ready);await page.waitForTimeout(350);}
async function capture(page,revision,id){
  for(const viewport of viewports){
    await page.setViewportSize(viewport);await page.mouse.move(0,0);await settle(page);
    await page.evaluate(()=>{window.scrollTo(0,0);for(const e of document.querySelectorAll('main,.uds-shell,.ant-modal-body'))e.scrollTop=0;});
    if(id.startsWith('09-')||id.startsWith('10-')||id.startsWith('11-')||id.startsWith('12-')||id.startsWith('14-'))await page.locator('.office-wizard > section').first().evaluate(e=>e.scrollIntoView({block:'start'}));
    if(id.startsWith('13-'))await page.getByRole('heading',{name:'Bản nháp của tôi',exact:true}).evaluate(e=>e.scrollIntoView({block:'start'}));
    if(id==='06-product-modal'){const combo=page.getByRole('dialog').getByRole('combobox').first();await combo.focus();if(await combo.getAttribute('aria-expanded')!=='true')await combo.press('ArrowDown');await page.locator('.ant-select-dropdown').first().waitFor();assert.ok(await combo.evaluate(e=>e===document.activeElement),'Modal select focus must match');await settle(page);}
    const dimensions=await page.evaluate(()=>({rootWidth:document.documentElement.scrollWidth,viewport:innerWidth,bodyWidth:document.body.clientWidth}));
    const dom=await page.evaluate(()=>{
      const selectors='h1,h2,h3,main,nav,button,input,select,textarea,label,table,th,.ant-modal,.ant-modal-mask,.ant-select-selector,.ant-select-dropdown,.ant-pagination,.ant-table-content,.uds-panel,[role="alert"],[role="status"],[role="region"]';
      return [...document.querySelectorAll(selectors)].filter(e=>{const r=e.getBoundingClientRect();const s=getComputedStyle(e);return r.width>0&&r.height>0&&s.visibility!=='hidden'&&s.display!=='none';}).map(e=>{
        const r=e.getBoundingClientRect(),s=getComputedStyle(e);
        return {tag:e.tagName,role:e.getAttribute('role'),text:(e.matches('input,textarea,main,nav,table,.ant-modal,.ant-pagination,.ant-table-content,.uds-panel,[role="region"]')?'':e.innerText??'').replace(/\s+/g,' ').trim(),
          bounds:[r.x,r.y,r.width,r.height].map(n=>Math.round(n*100)/100),
          css:Object.fromEntries(['display','position','color','backgroundColor','borderTopWidth','borderTopColor','borderRadius','fontFamily','fontSize','fontWeight','lineHeight','padding','gap','overflowX','overflowY','zIndex','outlineWidth','borderColor','borderWidth','boxShadow','opacity','transform'].map(k=>[k,s[k]]))};
      });
    });
    const name=`${revision}-${id}-${viewport.width}.png`,bytes=await page.screenshot({path:join(folder,name),animations:'disabled',caret:'hide',mask:[page.locator('.office-drafts article.uds-state > p')],maskColor:'#808080'});
    captures[revision].push({id,viewport,name,sha256:hash(bytes),dom,dimensions});
  }
  await page.setViewportSize(viewports[0]);await settle(page);
}
async function context(){
  const c=await browser.newContext({viewport:viewports[0],deviceScaleFactor:1,locale:policy.locale,timezoneId:policy.timezoneId,reducedMotion:'reduce',acceptDownloads:true});
  await c.addInitScript(()=>localStorage.setItem('has_completed_tour_v1','true'));
  const p=await c.newPage();await p.clock.setFixedTime(new Date(policy.fixedDate));
  p.on('pageerror',e=>errors.push(e.name));p.on('response',r=>{if(r.url().includes('/api/')&&r.status()>=400&&!(r.status()===401&&new URL(r.url()).pathname==='/api/auth/me'))errors.push(`HTTP_${r.status()}_${new URL(r.url()).pathname}`);});
  return {c,p};
}
async function login(p,base,user,password,office=false){
  await p.goto(base+(office?'/universal-doc-studio':'/'));await p.getByRole('textbox',{name:/Tên đăng nhập/}).fill(user);await p.getByLabel(/Mật khẩu/).fill(password);await p.getByRole('button',{name:/Đăng nhập/}).click();
  await p.getByRole('heading',{name:office?'Trợ lý Excel văn phòng':/Tổng quan/,exact:office}).first().waitFor();await settle(p);
}
async function legacy(revision,base){
  const {c,p}=await context();try{
    await p.goto(base);await p.getByRole('textbox',{name:/Tên đăng nhập/}).waitFor();await capture(p,revision,'01-login');
    await login(p,base,'admin','@Admin1234');await capture(p,revision,'02-overview');
    await p.getByRole('button',{name:/Giám sát xuất kho/}).click();await p.getByRole('heading',{name:/Lưới xuất kho thành phẩm/}).waitFor();await capture(p,revision,'03-warehouse');
    await p.getByRole('button',{name:'Lập Invoice / Packing List'}).click();await p.locator('#tour-invoice-header').waitFor();await capture(p,revision,'04-shipment');await p.emulateMedia({media:'print'});printProbes[revision]=await p.locator('main').evaluate(e=>{const s=getComputedStyle(e),r=e.getBoundingClientRect();return {display:s.display,overflow:s.overflow,fontSize:s.fontSize,width:r.width};});await p.emulateMedia({media:'screen'});
    await p.getByRole('button',{name:'Master Data & Cấu hình'}).click();await p.getByRole('heading',{name:'Danh mục Hàng hóa (Master Data)',exact:true}).waitFor();await p.getByText('SYNTH-VIS-016',{exact:true}).first().waitFor();await capture(p,revision,'05-products');
    await p.getByRole('button',{name:/Thêm mã|Thêm mới/}).first().click();await p.getByRole('dialog').waitFor();await settle(p);await p.getByPlaceholder('42072-030').fill('SYNTH-MODAL');await p.getByPlaceholder('42072-030').focus();await p.getByRole('dialog').getByRole('combobox').first().click();await p.locator('.ant-select-dropdown').first().waitFor();await capture(p,revision,'06-product-modal');
    await p.getByRole('dialog').getByRole('button',{name:'Hủy',exact:true}).click();await p.getByRole('dialog').waitFor({state:'hidden'});
    await p.locator('.ant-pagination-item-2').click();await p.getByText('SYNTH-VIS-001',{exact:true}).first().waitFor();assert.equal(await p.locator('.ant-pagination-item-active').getAttribute('title'),'2');await capture(p,revision,'07-table-pagination');
  }finally{await c.close();}
}
async function office(revision,base){
  const {c,p}=await context();
  const click=name=>p.getByRole('button',{name,exact:true}).click();
  const nav=async n=>{await p.getByRole('navigation',{name:'Các bước xử lý Excel'}).getByRole('button').nth(n).click();await p.getByRole('heading',{name:`${n+1}. `,exact:false}).waitFor();};
  const mutate=async(name,suffix)=>{const pending=p.waitForResponse(r=>r.url().includes(suffix)&&r.request().method()!=='GET');await click(name);const r=await pending;assert.ok(r.ok(),`Mutation failed ${suffix} ${r.status()}`);return r.status()===204?null:r.json();};
  try{
    await login(p,base,'admin-a','Studio@Test2026!',true);await capture(p,revision,'08-office-template');
    const data=await(await c.request.get(base+'/api/__test/gate5d/A')).body();await p.getByLabel('Mẫu Excel đầu ra',{exact:true}).setInputFiles({name:'mau-tong-hop.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:data});let s=await mutate('Tải và phân tích mẫu','/template');await nav(1);
    for(let i=0;i<s.schemaDraft.fields.length;i++){const f=s.schemaDraft.fields[i];if(f.candidates[0].worksheet!=='HoaDon')await p.getByLabel(`Bỏ qua mục ${i+1}`,{exact:true}).check();else{await p.getByLabel(`Vị trí mục ${i+1}`,{exact:true}).selectOption('0');if(f.scope==='LineItem'&&['Số lượng','Đơn giá','Thành tiền'].includes(f.label))await p.getByLabel(`Kiểu mục ${i+1}`,{exact:true}).selectOption('Decimal');}}
    s=await mutate('Áp dụng các mục dữ liệu','/schema');await nav(2);await click('Thêm quy tắc');await p.getByLabel('Mục A quy tắc 1',{exact:true}).selectOption({label:'Số lượng'});await p.getByLabel('Mục B quy tắc 1',{exact:true}).selectOption({label:'Đơn giá'});await p.getByLabel('Kết quả quy tắc 1',{exact:true}).selectOption({label:'Thành tiền'});s=await mutate('Áp dụng quy tắc','/validation-rules');await capture(p,revision,'09-office-mapping');s=await mutate('Xác nhận vị trí Excel','/mapping/confirm');await nav(3);
    await p.getByLabel('Thông tin Số phiếu',{exact:true}).fill('CT-00123');await p.getByLabel('Thông tin Ngày chứng từ',{exact:true}).fill('2026-10-24');await p.getByLabel('Thông tin Đã đối chiếu',{exact:true}).selectOption('true');
    for(const [label,value]of Object.entries({'Mã hàng':'00121','Số lượng':'2.50','Đơn giá':'3','Thành tiền':'8','Đơn vị':'cái'}))await p.getByLabel(`Dòng 1 ${label}`,{exact:true}).fill(value);
    await p.getByLabel('Dòng 1 Mã hàng',{exact:true}).focus();await p.keyboard.press('Tab');assert.equal(await p.evaluate(()=>document.activeElement.getAttribute('aria-label')),'Dòng 1 Số lượng');assert.ok(await p.evaluate(()=>parseFloat(getComputedStyle(document.activeElement).outlineWidth)>=2));await capture(p,revision,'10-office-entry');
    s=await mutate('Gửi dữ liệu để kiểm tra','/manual-sample');await nav(4);await p.getByLabel('Các lỗi và cảnh báo').filter({hasText:'Số lượng'}).waitFor();assert.ok(await p.getByRole('button',{name:'Hoàn tất rà soát',exact:true}).isDisabled());await capture(p,revision,'11-office-validation');
    await nav(3);await p.getByLabel('Dòng 1 Thành tiền',{exact:true}).fill('7.50');s=await mutate('Gửi dữ liệu để kiểm tra','/manual-sample');await nav(4);await mutate('Hoàn tất rà soát','/review');await mutate('Phê duyệt dữ liệu','/approve');await capture(p,revision,'12-office-approval');
    await mutate('Lưu bản nháp','/save-draft');await p.reload();await p.getByRole('button',{name:'Mở bản nháp 1',exact:true}).waitFor();await mutate('Mở bản nháp 1','/resume');await capture(p,revision,'13-office-draft-resume');
    await nav(2);await mutate('Xác nhận vị trí Excel','/mapping/confirm');await nav(4);await mutate('Hoàn tất rà soát','/review');await mutate('Phê duyệt dữ liệu','/approve');await nav(5);const download=p.waitForEvent('download');await click('Tải Excel đã phê duyệt');const d=await download;await d.saveAs(join(folder,`${revision}-authorized.xlsx`));await p.getByRole('status').filter({hasText:'Đã nhận tệp Excel'}).waitFor();await capture(p,revision,'14-office-export');
    await mutate('Xóa bản nháp 1','/drafts/');await click('Đóng phiên hiện tại');
  }finally{await c.close();}
}
let report={status:'FAIL',policy,baseline:process.env.GATE6A_VISUAL_BASELINE,feature:process.env.VERIFIED_HEAD,browserVersion:browser.version(),os:process.platform,pairs:[]};
try{
  for(const [revision,legacyBase,officeBase]of [['baseline','http://127.0.0.1:5310','http://127.0.0.1:5312'],['feature','http://127.0.0.1:5311','http://127.0.0.1:5313']]){
    await legacy(revision,legacyBase);await office(revision,officeBase);
    if(revision==='baseline'){
      await writeFile(join(folder,'reset-office-requested'),'reset');const until=Date.now()+120000;
      while(true){try{await access(join(folder,'reset-office-completed'));break;}catch{assert.ok(Date.now()<until,'Clean Office host reset timed out');await new Promise(r=>setTimeout(r,200));}}
    }
  }
  report.backendResetBetweenRevisions=true;
  assert.deepEqual(printProbes.baseline,printProbes.feature);report.printMediaComputedComparison={status:'PASS',baseline:printProbes.baseline,feature:printProbes.feature};
  assert.equal(captures.baseline.length,28);assert.equal(captures.feature.length,28);assert.deepEqual(errors,[]);
  for(let i=0;i<28;i++){
    const a=captures.baseline[i],b=captures.feature[i];assert.equal(a.id,b.id);assert.deepEqual(a.viewport,b.viewport);
    const x=PNG.sync.read(await readFile(join(folder,a.name))),y=PNG.sync.read(await readFile(join(folder,b.name)));assert.equal(x.width,y.width);assert.equal(x.height,y.height);
    let different=0,minX=x.width,minY=x.height,maxX=-1,maxY=-1;for(let p=0;p<x.data.length;p+=4)if([0,1,2,3].some(c=>Math.abs(x.data[p+c]-y.data[p+c])>policy.channelNoise)){different++;const px=(p/4)%x.width,py=Math.floor((p/4)/x.width);minX=Math.min(minX,px);minY=Math.min(minY,py);maxX=Math.max(maxX,px);maxY=Math.max(maxY,py);}
    const structural=[];if(a.dom.length!==b.dom.length)structural.push('visible-element-count');
    for(let j=0;j<Math.min(a.dom.length,b.dom.length);j++){const l=a.dom[j],r=b.dom[j];if(l.tag!==r.tag||l.role!==r.role||l.text!==r.text||JSON.stringify(l.css)!==JSON.stringify(r.css)||l.bounds.some((n,k)=>Math.abs(n-r.bounds[k])>policy.maxBoundsDelta))structural.push(`element-${j}-${l.tag}`);}
    if(JSON.stringify(a.dimensions)!==JSON.stringify(b.dimensions))structural.push('page-dimensions');
    const score=different/(x.width*x.height);report.pairs.push({view:a.id,viewport:a.viewport,baselineCommit:report.baseline,featureCommit:report.feature,browserVersion:report.browserVersion,baselineSha256:a.sha256,featureSha256:b.sha256,differentPixels:different,differenceBounds:different?[minX,minY,maxX,maxY]:null,differenceScore:score,unexpectedStructuralDifferences:structural.slice(0,15),visibleElementsCompared:a.dom.length,status:score<=policy.maxDifferentPixelRatio&&!structural.length?'PASS':'FAIL'});
  }
  assert.ok(report.pairs.every(p=>p.status==='PASS'),'Visual comparison differences');report.status='PASS';
}catch(e){report.blocker=e.message;console.error(e);}finally{report.runtimeErrors=errors;report.captured={baseline:captures.baseline.length,feature:captures.feature.length};await writeFile(join(folder,'visual-summary.json'),JSON.stringify(report,null,2));await browser.close();}
assert.equal(report.status,'PASS',report.blocker);
