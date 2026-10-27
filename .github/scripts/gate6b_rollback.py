"""Probe the immutable previous build without changing the current key ring or saved draft."""
import hashlib,json,os,subprocess,time,urllib.error,urllib.request
from pathlib import Path
import gate5d_browser
BASELINE='2552252d0d916d4184d88482f3649c602468eff8'

def verify(feature,private,config,env,command,op,evidence):
    before=feature.parent/'baseline'
    assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=before,text=True).strip()==BASELINE,'ROLLBACK_CHECKOUT_MISMATCH'
    result={'build':BASELINE,'status':'NO-GO','claimed_supported':False}
    data=private/'rollback-data';proxy=private/'rollback-nginx.conf'
    command(['sudo','cp','-a',str(private/'data'),str(data)])
    proxy.write_text((feature/'deploy/gate6b/nginx.conf').read_text().replace('http://backend:5270','http://172.30.86.5:5270'))
    command(['docker','build','-f',str(before/'backend/ShoeExportInvoice.Api/Dockerfile'),'-t','gate6b-api-previous',str(before)])
    trust=private/'previous-public-trust';trust.mkdir();(trust/'ca.crt').write_bytes((private/'ca.crt').read_bytes())
    (trust/'Dockerfile').write_text('FROM gate6b-api-previous\nUSER root\nCOPY ca.crt /usr/local/share/ca-certificates/gate6b.crt\nRUN update-ca-certificates\nUSER app\n')
    command(['docker','build','-t','gate6b-previous-trusted',str(trust)])
    environment={'ASPNETCORE_ENVIRONMENT':'Production','ASPNETCORE_URLS':'http://+:5270'}
    def flatten(value,prefix=''):
        if isinstance(value,dict):
            for k,v in value.items():flatten(v,prefix+'__'+k if prefix else k)
        elif isinstance(value,list):
            for i,v in enumerate(value):flatten(v,prefix+'__'+str(i))
        else:environment[prefix]=str(value).lower() if isinstance(value,bool) else str(value)
    flatten(config)
    for i in range(3):environment['Xnk__AllowedOrigins__'+str(i)]='https://localhost:8444'
    spec={'services':{
        'previous-api':{'image':'gate6b-previous-trusted','environment':environment,'read_only':True,'tmpfs':['/tmp:uid=1654,gid=1654,mode=0700,size=64m'],'cap_drop':['ALL'],'security_opt':['no-new-privileges:true'],
            'volumes':[str(data)+':/app/data',str(private/'templates')+':/app/Templates:ro'],'mem_limit':'1g','pids_limit':256,'networks':{'internal':{'ipv4_address':'172.30.86.5'}}},
        'previous-web':{'image':'gate6b-web','ports':['127.0.0.1:8444:8443'],'volumes':[str(proxy)+':/etc/nginx/conf.d/default.conf:ro',str(private/'web.crt')+':/run/tls/web.crt:ro',str(private/'web.key')+':/run/tls/web.key:ro'],
            'networks':{'internal':{'ipv4_address':'172.30.86.6'},'ingress':{'gw_priority':1}}}},
        'networks':{'internal':{'external':True,'name':'gate6b-isolated_isolated'},'ingress':{'external':True,'name':'gate6b-isolated_ingress'}}}
    path=private/'previous-compose.json';path.write_text(json.dumps(spec));path.chmod(0o600)
    compose=['docker','compose','-p','gate6b-previous','-f',str(path)]
    op('fingerprint',private/'rollback-before.json')
    original_base=env['STUDIO_BASE_URL']
    try:
        command(compose+['up','-d'])
        import ssl
        ready=False
        for _ in range(120):
            try:
                urllib.request.urlopen('https://localhost:8444/api/auth/me',context=ssl.create_default_context(cafile=str(private/'ca.crt')),timeout=3)
            except urllib.error.HTTPError as error:
                if error.code==401:ready=True;break
            except (urllib.error.URLError,TimeoutError):pass
            time.sleep(.5)
        result['actual_previous_production_started']=ready
        if ready:
            env['STUDIO_BASE_URL']='https://localhost:8444';command(['node','frontend/tests/gate6bRollback.e2e.mjs'])
            result['functional']=json.loads((private/'browser/rollback-functional.json').read_text())
            if result['functional']['status']=='FUNCTIONAL_PASS':
                op('reopen',private/'browser/rollback.xlsx')
                result['independent_workbook']=gate5d_browser.verify_workbook(private/'browser/rollback.xlsx','F',20)
    finally:
        env['STUDIO_BASE_URL']=original_base
        command(compose+['down','--remove-orphans'],expected=None)
        command(['sudo','chown','-R',str(os.getuid())+':'+str(os.getgid()),str(data)])
    keys=list((data/'protection-keys').glob('key-*.xml'))
    result['all_previous_runtime_key_files_encrypted']=bool(keys) and all('encryptedSecret' in p.read_text() for p in keys)
    op('fingerprint',private/'rollback-after.json')
    result['saved_draft_ciphertext_unchanged']=json.loads((private/'rollback-before.json').read_text())==json.loads((private/'rollback-after.json').read_text())
    assert result['saved_draft_ciphertext_unchanged'],'ROLLBACK_CHANGED_SAVED_DRAFT'
    result['reason']='Accepted Gate 6A build has no configured certificate protector for new DataProtection keys and retains startup initialization writes; hardened staging rollback is not approved'
    (evidence/'gate6b-previous-build-rollback.json').write_text(json.dumps(result,indent=2))
    return result
