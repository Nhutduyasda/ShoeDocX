"""Actual legacy API + SQL draft host, immutable builds and private paired captures."""
import http.cookiejar
import json
import os
import re
from pathlib import Path
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid
import openpyxl
from runtime_target import host_dll

BASELINE='1df7badbf3635fda03687cb8604dde8858cd9bb5'

def verify(ci):
    feature=ci.ROOT/'feature';before=ci.ROOT/'dependency-baseline'
    assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=before,text=True).strip()==BASELINE
    assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=feature,text=True).strip()==os.environ['VERIFIED_HEAD']
    # Browser application and all business code remain immutable between A/B.
    changed=subprocess.check_output(['git','diff','--name-only',BASELINE,'HEAD','--','frontend/src','frontend/package.json','frontend/tailwind.config.js','frontend/postcss.config.js','backend'],cwd=feature,text=True)
    assert not changed.strip(),'Visual baseline application delta requires a new behavior review'
    report={'status':'FAIL','baseline':BASELINE,'feature':os.environ['VERIFIED_HEAD']}
    processes=[];logs=[]
    # Connections exist only in the private process environment and temporary logs.
    # Both DBs are disposable; helpers validate prefixes before cleanup.
    raw=os.environ['GATE5C_SQL']
    assert 'database=' not in raw.lower() and 'initial catalog=' not in raw.lower()
    legacy=raw+';Database=gate6a_visual_'+uuid.uuid4().hex
    draft=raw+';Database=gate5c_visual_'+uuid.uuid4().hex
    environment=os.environ.copy()
    environment.update(GATE5C_DATABASE=draft,GATE6A_VISUAL_LEGACY=legacy,
        STUDIO_DEMO_PORT='5309',ASPNETCORE_ENVIRONMENT='Development',ASPNETCORE_URLS='http://127.0.0.1:5308',
        ConnectionStrings__DefaultConnection=legacy,Jwt__Key='Gate6A_Visual_Synthetic_Only_1234567890123456789',
        Jwt__Issuer='gate6a-visual',Jwt__Audience='gate6a-visual',DevelopmentUsers__Enabled='true',UniversalDocDrafts__Enabled='false',
        GATE6A_VISUAL_BASELINE=BASELINE)
    host=str(host_dll(feature,'Gate5DDemoHost'))
    def maintenance(args):
        return subprocess.run(['dotnet',host,*args],cwd=feature,env=environment,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=90).returncode
    with tempfile.TemporaryDirectory(prefix='gate6a-visual-private-') as temporary:
        private=Path(temporary);environment['GATE6A_VISUAL_PRIVATE']=str(private)
        def launch(args,cwd):
            log=open(private/(str(len(logs))+'.log'),'wb');logs.append(log)
            p=subprocess.Popen(args,cwd=cwd,env=environment,stdout=log,stderr=subprocess.STDOUT);p.visual_label=('office-host' if args[0]=='dotnet' and 'Gate5DDemoHost' in args[1] else 'legacy-api' if args[0]=='dotnet' else 'static-'+args[-2]);p.visual_log=Path(log.name);processes.append(p);return p
        def ready(url):
            until=time.monotonic()+120
            while True:
                failed=[p for p in processes if p.poll() is not None]
                if failed:
                    report['process_failures']=[{'label':getattr(p,'visual_label','browser'),'exit_code':p.returncode,'error_types':sorted(set(re.findall(r'\b(?:EADDRINUSE|ECONNRESET|EPIPE|ERR_[A-Z_]+|[A-Za-z.]+Exception)\b',getattr(p,'visual_log',private/'browser.log').read_text(encoding='utf-8',errors='replace'))))} for p in failed]
                    raise AssertionError('Visual host exited: '+','.join(row['label'] for row in report['process_failures']))
                try:
                    with urllib.request.urlopen(url,timeout=2):return
                except urllib.error.HTTPError as e:
                    if e.code==401:return
                    raise AssertionError('Unexpected visual readiness status') from None
                except (urllib.error.URLError,TimeoutError):
                    assert time.monotonic()<until,'Visual host timeout';time.sleep(.2)
        try:
            assert maintenance(['--migrate'])==0,'Visual SQL draft migration failed'
            # Draft environment flags are overridden by the isolated host's real SQL configuration.
            office_host=launch(['dotnet',host],feature)
            api=feature/'backend/ShoeExportInvoice.Api'
            launch(['dotnet',str(api/'bin/Debug/net10.0/ShoeExportInvoice.Api.dll')],api)
            report['stage']='initial-api-readiness'
            ready('http://127.0.0.1:5308/api/auth/me');ready('http://127.0.0.1:5309/api/universal-doc/bootstrap')
            client=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
            def post(path,body):
                request=urllib.request.Request('http://127.0.0.1:5308/api/'+path,data=json.dumps(body).encode(),headers={'Content-Type':'application/json'})
                with client.open(request,timeout=30) as r:return json.load(r)
            post('auth/login',{'username':'admin','password':'@Admin1234'})
            for i in range(1,17):
                post('product-masters',{'styleCode':f'SYNTH-VIS-{i:03}','description':f'Hàng tổng hợp kiểm thử {i:02}',
                    'unitPriceCMT':2.5,'unitPriceDAP':3,'hsCode':'64041990','unit':'đôi','pairPerCarton':12})
            server=str(feature/'.github/scripts/gate6a_visual_server.mjs')
            for directory,port,target in [(before,5310,5308),(feature,5311,5308),(before,5312,5309),(feature,5313,5309)]:
                launch(['node',server,str(directory/'frontend/dist'),str(port),f'http://127.0.0.1:{target}'],feature)
                report['stage']='static-'+str(port)+'-readiness'
                ready(f'http://127.0.0.1:{port}/')
            browser_log=open(private/'browser.log','wb');logs.append(browser_log)
            browser=subprocess.Popen(['node','frontend/tests/gate6aVisual.e2e.mjs'],cwd=feature,env=environment,stdout=browser_log,stderr=subprocess.STDOUT);processes.append(browser)
            report['stage']='paired-browser-capture'
            until=time.monotonic()+600;reset=False
            while browser.poll() is None:
                assert time.monotonic()<until,'Visual browser timed out'
                if not reset and (private/'reset-office-requested').exists():
                    report['stage']='office-reset'
                    office_host.terminate();office_host.wait(timeout=15);processes.remove(office_host)
                    assert maintenance(['--drop'])==0 and maintenance(['--migrate'])==0,'Clean visual SQL reset failed'
                    office_host=launch(['dotnet',host],feature);ready('http://127.0.0.1:5309/api/universal-doc/bootstrap')
                    (private/'reset-office-completed').write_text('Real host and disposable SQL reset; quotas unchanged')
                    reset=True
                time.sleep(.2)
            browser_log.flush()
            result=type('Result',(),{'returncode':browser.returncode,'stdout':(private/'browser.log').read_text(encoding='utf-8')})()
            summary=private/'visual-summary.json'
            if summary.exists():report=json.loads(summary.read_text(encoding='utf-8'))
            if result.returncode:
                # Synthetic selector diagnostics only; do not publish raw process logs.
                print('\n'.join(l for l in result.stdout.splitlines() if any(t in l for t in ('Error:', 'locator.', 'waiting for','Expected:','Actual:')))[-3500:],flush=True)
            assert result.returncode==0 and report['status']=='PASS' and reset,'Paired authenticated visual acceptance failed'
            assert len(report['pairs'])==28 and report['captured']=={'baseline':28,'feature':28}
            assert len({p['view'] for p in report['pairs']})==14 and all(p['status']=='PASS' for p in report['pairs'])
            # Independently open both actual authorized downloads; these bytes remain private.
            for name in ('baseline','feature'):
                wb=openpyxl.load_workbook(private/(name+'-authorized.xlsx'))
                assert wb['HoaDon']['A6'].value=='00121' and wb['HoaDon']['D6'].value==7.5
            report['independent_authorized_xlsx_reopens']=2
            report['screenshots_published']=False
            print('Gate 6A: 28 paired real UI screenshot/DOM comparisons + two authorized XLSX reopens PASS',flush=True)
        except Exception as error:
            summary=private/'visual-summary.json'
            if summary.exists():report.update(json.loads(summary.read_text(encoding='utf-8')))
            report['status']='FAIL'
            report['orchestration_blocker']=str(error) if isinstance(error,AssertionError) else type(error).__name__
            raise
        finally:
            for p in processes:
                if p.poll() is None:p.terminate()
            for p in processes:
                try:p.wait(timeout=10)
                except subprocess.TimeoutExpired:p.kill();p.wait()
            for log in logs:log.close()
            (ci.EVIDENCE/'gate6a-visual.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
            assert maintenance(['--drop'])==0,'Visual SQL draft cleanup failed'
            assert maintenance(['--drop-visual-legacy'])==0,'Visual legacy SQL cleanup failed'
