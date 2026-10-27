"""Disposable Linux production-entrypoint acceptance. Never writes raw logs to artifacts."""
import hashlib, json, os, shutil, ssl, subprocess, tarfile, tempfile, time, urllib.request, urllib.error, uuid
from pathlib import Path
import gate5d_browser

ROOT=Path.cwd(); FEATURE=ROOT/'feature'; EVIDENCE=ROOT/'verification-evidence'; EVIDENCE.mkdir(exist_ok=True)

def verify():
    report={'head':os.environ['VERIFIED_HEAD'],'status':'FAIL','checks':{},'internal_pilot':'NOT EXECUTED'}
    processes=[]
    with tempfile.TemporaryDirectory(prefix='gate6b-private-') as temp:
        private=Path(temp); private.chmod(0o700); env=os.environ.copy(); env['GATE6B_PRIVATE']=str(private)
        database='gate6b_'+uuid.uuid4().hex
        recovery_names=('runtime.json','maintenance.json','protection.pfx','data','templates','backup','sql.crt','sql.key','web.crt','web.key','mssql.conf','ca.crt','operator.json')
        password=lambda: 'G6b!'+uuid.uuid4().hex+'aA1'
        sa,migration,runtime,user,pfx=password(),password(),password(),password(),password()
        env['GATE6B_SQL_PASSWORD']=sa
        compose=['docker','compose','-p','gate6b-isolated','-f',str(FEATURE/'deploy/gate6b/compose.yml')]
        probe=['dotnet',str(FEATURE/'tests/Gate6BOperations/bin/Debug/net10.0/Gate6BOperations.dll')]
        def command(args,expected=0,timeout=600):
            result=subprocess.run(args,cwd=FEATURE,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=timeout)
            if expected is not None and result.returncode!=expected:
                import re
                text=result.stdout.decode(errors='replace')
                report['command_failure']={'tool':Path(args[0]).name,'exit':result.returncode,'exception_types':sorted(set(re.findall(r'\b[A-Za-z.]+Exception\b',text))),
                    'sql_numbers':sorted(set(re.findall(r'Error Number:(\d+)',text))),
                    'classifiers':{k:bool(re.search(v,text,re.I)) for k,v in {'sql_login':r'login failed','sql_certificate':r'certificate chain|certificate name|certificate validation','permissions':r'permission denied|access is denied','database_missing':r'cannot open database','read_only':r'read.only file system','object_missing':r'invalid object name','missing_file':r'no such file|could not find'}.items()}}
                raise AssertionError('COMMAND_FAILED_'+Path(args[0]).name)
            return result
        def cp(*args,**kwargs):return command(compose+list(args),**kwargs)
        def op(action,*args,**kwargs):return command(probe+[action,*map(str,args)],**kwargs)
        def mark(key):report['checks'][key]='PASS';print('Gate6B '+key+' PASS',flush=True)
        def openssl(*args):command(['openssl',*args])
        def write(name,content):
            p=private/name;p.write_text(content);p.chmod(0o600);return p
        def request(path,body=None,headers=None):
            req=urllib.request.Request('https://localhost:8443'+path,data=None if body is None else json.dumps(body).encode(),headers=headers or {})
            try:
                with urllib.request.urlopen(req,context=ssl.create_default_context(cafile=str(private/'ca.crt')),timeout=15) as r:return r.status,r.read(),dict(r.headers)
            except urllib.error.HTTPError as e:return e.code,e.read(),dict(e.headers)
        def ready():
            observations={}
            for _ in range(120):
                try:
                    status=request('/api/health')[0]
                    if status==200:return
                    key='http_'+str(status);observations[key]=observations.get(key,0)+1
                except (urllib.error.URLError,TimeoutError) as error:
                    key=type(getattr(error,'reason',error)).__name__;observations[key]=observations.get(key,0)+1
                time.sleep(.5)
            report['readiness_observations']=observations
            raise AssertionError('PRODUCTION_READINESS_TIMEOUT')
        try:
            openssl('req','-x509','-newkey','rsa:2048','-nodes','-keyout',str(private/'ca.key'),'-out',str(private/'ca.crt'),'-days','2','-subj','/CN=Gate6B Synthetic CA')
            for name,host in [('sql','sql'),('web','localhost')]:
                openssl('req','-newkey','rsa:2048','-nodes','-keyout',str(private/(name+'.key')),'-out',str(private/(name+'.csr')),'-subj','/CN='+host)
                ext=write(name+'.ext',f'subjectAltName=DNS:{host}\nkeyUsage=digitalSignature,keyEncipherment\nextendedKeyUsage=serverAuth\n')
                openssl('x509','-req','-in',str(private/(name+'.csr')),'-CA',str(private/'ca.crt'),'-CAkey',str(private/'ca.key'),'-CAcreateserial','-out',str(private/(name+'.crt')),'-days','2','-extfile',str(ext))
            env['GATE6B_PFX_PASSWORD']=pfx
            openssl('pkcs12','-export','-in',str(private/'web.crt'),'-inkey',str(private/'web.key'),'-out',str(private/'protection.pfx'),'-passout','env:GATE6B_PFX_PASSWORD')
            write('mssql.conf','[network]\ntlscert = /run/tls/sql.crt\ntlskey = /run/tls/sql.key\ntlsprotocols = 1.2\nforceencryption = 1\n').chmod(0o644)
            for name in ('data','templates','backup'):(private/name).mkdir(mode=0o700)
            (private/'data/protection-keys').mkdir(mode=0o700);(private/'data/customs').mkdir(mode=0o700)
            # CI host and API image trust exactly this ephemeral CA; negative trust control below.
            command(['sudo','cp',str(private/'ca.crt'),'/usr/local/share/ca-certificates/gate6b.crt'])
            command(['sudo','update-ca-certificates'])
            sqlbase='Server=172.30.86.2;Database=master;User ID=sa;Password='+sa+';Encrypt=True;TrustServerCertificate=False;HostNameInCertificate=sql;Connect Timeout=5'
            operator={'adminConnection':sqlbase,'database':database,'migrationPassword':migration,'runtimePassword':runtime,'userPassword':user}
            operatorpath=write('operator.json',json.dumps(operator));env['GATE6B_OPERATOR']=str(operatorpath)
            def configuration(login,secret):return {
                'ConnectionStrings':{'DefaultConnection':f'Server=sql;Database={database};User ID={login};Password={secret};Encrypt=True;TrustServerCertificate=False;Connect Timeout=5'},
                'Jwt':{'Key':password(),'Issuer':'gate6b','Audience':'gate6b'},'AllowedHosts':'localhost',
                'Xnk':{'AllowedOrigins':['https://localhost:8443'],'CustomsStoragePath':'/app/data/customs','ShipmentTemplatePath':'/app/Templates/synthetic.xlsx'},
                'Ingress':{'KnownProxies':['172.30.86.4']},
                'DataProtection':{'KeyPath':'/app/data/protection-keys','CertificatePath':'/run/secrets/protection.pfx','CertificatePassword':pfx},
                'UniversalDocDrafts':{'Enabled':True,'ActiveKeyId':'synthetic-v1','Keys':{'synthetic-v1':__import__('base64').b64encode(os.urandom(32)).decode()}},
                'UniversalDocGate4A':{'Users':{'admin-a':'synthetic-a','other-a':'synthetic-a','clerk-a':'synthetic-a','admin-b':'synthetic-b'}},
                'Database':{'ApplyMigrationsOnStartup':False},'BootstrapAdmin':{'Enabled':False},'DevelopmentUsers':{'Enabled':False},
                'Logging':{'LogLevel':{'Default':'Warning','Microsoft.EntityFrameworkCore':'Warning'}}}
            fixture=private/'fixtures';op('fixtures',fixture)
            shutil.copy2(fixture/'A-1-false-none.xlsx',private/'templates/synthetic.xlsx')
            write('data/customs/synthetic-attachment.txt','SYNTHETIC RECOVERY SENTINEL')
            storage_fingerprints={name:hashlib.sha256((private/name).read_bytes()).hexdigest() for name in ('templates/synthetic.xlsx','data/customs/synthetic-attachment.txt')}
            config=configuration('gate6b_runtime',runtime);write('runtime.json',json.dumps(config));operator['jwtKey']=config['Jwt']['Key'];operatorpath.write_text(json.dumps(operator));op('expired-jwt',private/'expired.jwt');env['GATE6B_EXPIRED_TOKEN']=str(private/'expired.jwt')
            maintenance_config=json.loads(json.dumps(config));maintenance_config['ConnectionStrings']['DefaultConnection']=config['ConnectionStrings']['DefaultConnection'].replace('gate6b_runtime','gate6b_migration').replace(runtime,migration);maintenance_config['Database']['MaintenanceAuthorized']=True
            write('maintenance.json',json.dumps(maintenance_config))
            # Bind directories explicitly owned by actual image user; secrets 0400.
            command(['sudo','chown','-R','1654:1654',str(private/'data'),str(private/'templates')])
            for name in ('runtime.json','maintenance.json','protection.pfx'):
                (private/name).chmod(0o400);command(['sudo','chown','1654:1654',str(private/name)])
            (private/'sql.key').chmod(0o400);command(['sudo','chown','10001:0',str(private/'sql.key'),str(private/'backup')])
            command(['docker','build','-f','backend/ShoeExportInvoice.Api/Dockerfile','-t','gate6b-api','.'])
            command(['docker','build','-t','gate6b-web','frontend'])
            trust=private/'public-trust-context';trust.mkdir();shutil.copy2(private/'ca.crt',trust/'ca.crt')
            (trust/'Dockerfile').write_text('FROM gate6b-api\nUSER root\nCOPY ca.crt /usr/local/share/ca-certificates/gate6b.crt\nRUN update-ca-certificates\nUSER app\n')
            command(['docker','build','-t','gate6b-api-trusted',str(trust)])
            cp('run','--rm','--no-deps','--user','0','--entrypoint','/bin/bash','sql','-c','chown 10001:0 /var/opt/mssql/data && chmod 700 /var/opt/mssql/data')
            cp('up','-d','sql')
            for _ in range(90):
                result=cp('exec','-T','sql','/opt/mssql-tools18/bin/sqlcmd','-S','localhost','-U','sa','-P',sa,'-C','-Q','SELECT 1',expected=None)
                if result.returncode==0:break
                time.sleep(1)
            else:raise AssertionError('SQL_STARTUP_TIMEOUT')
            # Readiness sqlcmd uses bootstrap trust only. All application/probe SQL is certificate validated.
            op('provision');mark('sql_validated_tls_and_separate_accounts')
            for action in ('--migrate-all','--initialize-staging'):
                cp('run','--rm','-v',str(private/'maintenance.json')+':/run/secrets/runtime.json:ro','backend',action)
            op('users');op('permissions');op('tls-negative');mark('both_migrations_explicit_initialization_and_runtime_no_ddl')
            cp('up','-d','backend','web');ready();mark('actual_production_images_https_proxy_readiness')
            assert request('/')[0]==200 and request('/api/auth/me')[0]==401
            assert request('/swagger/v1/swagger.json')[0]==404
            assert request('/api/auth/me',headers={'X-Forwarded-Proto':'http','X-Forwarded-Host':'evil.invalid'})[0]==401
            mark('ingress_replaces_spoofed_headers_no_public_swagger')
            for headers in ({},{'X-Forwarded-Proto':'https','X-Forwarded-For':'127.0.0.1'}):
                try:urllib.request.urlopen(urllib.request.Request('http://172.30.86.3:5270/api/auth/me',headers=headers),timeout=5)
                except urllib.error.HTTPError as e:assert e.code==400,'UNTRUSTED_PROXY_ACCEPTED'
                else:raise AssertionError('DIRECT_HTTP_ACCEPTED')
            mark('direct_http_and_untrusted_forwarding_rejected')
            # Verify wrong CA fails HTTPS (not a browser ignore-certificate workaround).
            try:urllib.request.urlopen('https://localhost:8443/api/health',context=ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT),timeout=5)
            except urllib.error.URLError:mark('ingress_untrusted_certificate_rejected')
            else:raise AssertionError('UNTRUSTED_HTTPS_ACCEPTED')
            meta=json.loads(command(['docker','inspect',cp('ps','-q','backend').stdout.decode().strip()]).stdout)[0]
            assert meta['Config']['User']=='app' and meta['HostConfig']['ReadonlyRootfs']
            assert not meta['HostConfig']['PortBindings'];mark('nonroot_readonly_backend_no_published_port')
            images=json.loads(command(['docker','image','inspect','gate6b-api','gate6b-api-trusted','gate6b-web','mcr.microsoft.com/mssql/server:2022-latest']).stdout)
            report['images']=[{'id':v['Id'],'repo_digests':v.get('RepoDigests',[]),'user':v['Config'].get('User'),'architecture':v['Architecture']} for v in images]
            cp('exec','-T','web','sh','-c','test ! -e /usr/local/bin/node && test ! -d /app/node_modules');mark('frontend_runtime_has_no_node_or_vite')
            env.update(STUDIO_BASE_URL='https://localhost:8443',STUDIO_EVIDENCE_DIR=str(private/'browser'),GATE6B_FIXTURES=str(fixture),STUDIO_PASSWORD=user,NODE_EXTRA_CA_CERTS=str(private/'ca.crt'))
            # Trust only the test CA for Chromium, never set ignoreHTTPSErrors.
            command(['mkdir','-p',str(Path.home()/'.pki/nssdb')])
            command(['certutil','-N','-d','sql:'+str(Path.home()/'.pki/nssdb'),'--empty-password'],expected=None)
            command(['certutil','-A','-d','sql:'+str(Path.home()/'.pki/nssdb'),'-n','Gate6B','-t','C,,','-i',str(private/'ca.crt')])
            out=open(private/'browser.log','wb')
            browser=subprocess.Popen(['node','frontend/tests/universalDocGate5D.e2e.mjs'],cwd=FEATURE,env=env,stdout=out,stderr=subprocess.STDOUT);processes.append(browser)
            until=time.monotonic()+720;recovered=False
            while browser.poll() is None:
                assert time.monotonic()<until,'BROWSER_TIMEOUT'
                if not recovered and (private/'browser/restart-requested').exists():
                    cp('stop','backend');op('fingerprint',private/'before.json');op('backup')
                    assert json.loads((private/'before.json').read_text())['rows']==1
                    # Encrypted recovery bundle covers SQL, protected keys, config and file storage.
                    command(['sudo','chown','-R',str(os.getuid())+':'+str(os.getgid()),str(private/'data'),str(private/'templates'),str(private/'backup')])
                    for name in ('runtime.json','maintenance.json','protection.pfx','sql.key'):command(['sudo','chown',str(os.getuid())+':'+str(os.getgid()),str(private/name)])
                    bundle=private/'recovery.tar'
                    with tarfile.open(bundle,'w') as archive:
                        for name in recovery_names:archive.add(private/name,arcname=name)
                    (private/'backup.key').write_bytes(os.urandom(32));(private/'backup.key').chmod(0o600);op('seal',bundle,private/'recovery.aesgcm',private/'backup.key');bundle.unlink()
                    (private/'incorrect.key').write_bytes(os.urandom(32));(private/'incorrect.key').chmod(0o600)
                    assert op('unseal',private/'recovery.aesgcm',private/'must-not-exist.tar',private/'incorrect.key',expected=None).returncode!=0
                    assert not (private/'must-not-exist.tar').exists()
                    corrupt=bytearray((private/'recovery.aesgcm').read_bytes());corrupt[-1]^=1;(private/'corrupt.aesgcm').write_bytes(corrupt)
                    assert op('unseal',private/'corrupt.aesgcm',private/'must-not-exist.tar',private/'backup.key',expected=None).returncode!=0
                    assert not (private/'must-not-exist.tar').exists()
                    mark('backup_wrong_key_and_authenticated_corruption_rejected')
                    sql_backup=private/'backup/staging.bak';saved_backup=sql_backup.read_bytes();corrupt_backup=bytearray(saved_backup);corrupt_backup[:4096]=b'X'*4096;sql_backup.write_bytes(corrupt_backup)
                    command(['sudo','chown','10001:0',str(sql_backup)])
                    assert op('restore',expected=None).returncode!=0,'CORRUPT_SQL_BACKUP_ACCEPTED'
                    command(['sudo','chown',str(os.getuid())+':'+str(os.getgid()),str(sql_backup)]);sql_backup.write_bytes(saved_backup);command(['sudo','chown','10001:0',str(sql_backup)])
                    mark('corrupt_sql_backup_rejected_before_restore')
                    cp('down','-v')
                    for name in recovery_names:
                        p=private/name;shutil.rmtree(p) if p.is_dir() else p.unlink()
                    started=time.monotonic();op('unseal',private/'recovery.aesgcm',bundle,private/'backup.key')
                    with tarfile.open(bundle) as archive:archive.extractall(private,filter='data')
                    bundle.unlink()
                    command(['sudo','chown','-R','1654:1654',str(private/'data'),str(private/'templates')])
                    for name in ('runtime.json','maintenance.json','protection.pfx'):command(['sudo','chown','1654:1654',str(private/name)])
                    command(['sudo','chown','-R','10001:0',str(private/'backup')]);(private/'sql.key').chmod(0o400);command(['sudo','chown','10001:0',str(private/'sql.key')])
                    cp('run','--rm','--no-deps','--user','0','--entrypoint','/bin/bash','sql','-c','chown 10001:0 /var/opt/mssql/data && chmod 700 /var/opt/mssql/data')
                    cp('up','-d','sql')
                    for _ in range(90):
                        if cp('exec','-T','sql','/opt/mssql-tools18/bin/sqlcmd','-S','localhost','-U','sa','-P',sa,'-C','-Q','SELECT 1',expected=None).returncode==0:break
                        time.sleep(1)
                    op('provision');op('restore');op('permissions')
                    # New SQL logins have new SIDs; explicitly remap recovered database users.
                    # Driver restore performs this before application start.
                    op('fingerprint',private/'after.json')
                    assert json.loads((private/'before.json').read_text())==json.loads((private/'after.json').read_text())
                    assert all(hashlib.sha256((private/name).read_bytes()).hexdigest()==value for name,value in storage_fingerprints.items())
                    cp('up','-d','backend','web');ready();report['observed_rebuild_restore_seconds']=round(time.monotonic()-started,2)
                    (private/'browser/restart-completed').write_text('actual complete instance recovery')
                    mark('encrypted_backup_complete_instance_loss_restore_ciphertext_fingerprint');recovered=True
                time.sleep(.2)
            out.close()
            assert browser.returncode==0 and recovered,'PRODUCTION_OFFICE_BROWSER_FAILED'
            summary=json.loads((private/'browser/office-summary.json').read_text());assert summary['status']=='PASS'
            op('reopen',private/'browser')
            downloads=json.loads((private/'browser/office-downloads.json').read_text())
            qa=[gate5d_browser.verify_workbook(private/'browser'/d['name'],d['scenario'],d['rows']) for d in downloads]
            (EVIDENCE/'gate6b-office.json').write_text(json.dumps({'browser':summary,'independent_xlsx':qa},indent=2));mark('office_multisheet_mobile_reapproval_real_export_dual_reopen')
            command(['node','frontend/tests/gate6bFaults.e2e.mjs']);mark('production_csrf_roles_revocation_retention_concurrency_upload_limits')
            shutil.copy2(private/'browser/gate6b-faults-normal.json',EVIDENCE/'gate6b-production-faults.json')
            cp('stop','sql')
            assert request('/api/health')[0]==503,'SQL_OUTAGE_DID_NOT_FAIL_CLOSED'
            cp('start','sql');ready();mark('sql_outage_controlled_health_and_recovery')
            cp('stop','backend')
            def reject_configuration(name,mutate):
                value=json.loads(json.dumps(config));mutate(value)
                path=write('fault-'+name+'.json',json.dumps(value));path.chmod(0o400);command(['sudo','chown','1654:1654',str(path)])
                result=cp('run','--rm','-v',str(path)+':/run/secrets/runtime.json:ro','backend',expected=None,timeout=150)
                assert result.returncode!=0,'FAULT_CONFIGURATION_ACCEPTED_'+name
                mark('startup_rejects_'+name)
            reject_configuration('sql-auth',lambda v:v['ConnectionStrings'].update(DefaultConnection=v['ConnectionStrings']['DefaultConnection'].replace(runtime,password())))
            reject_configuration('sql-certificate',lambda v:v['ConnectionStrings'].update(DefaultConnection=v['ConnectionStrings']['DefaultConnection']+';HostNameInCertificate=wrong.invalid'))
            reject_configuration('missing-aes',lambda v:v['UniversalDocDrafts'].update(Keys={}))
            reject_configuration('wrong-key-id',lambda v:v['UniversalDocDrafts'].update(ActiveKeyId='missing-id'))
            reject_configuration('wrong-dataprotection-password',lambda v:v['DataProtection'].update(CertificatePassword='incorrect'))
            reject_configuration('missing-dataprotection-certificate',lambda v:v['DataProtection'].update(CertificatePath='/run/secrets/missing.pfx'))
            reject_configuration('incomplete-config',lambda v:v['Jwt'].update(Key=''))
            reject_configuration('missing-template',lambda v:v['Xnk'].update(ShipmentTemplatePath='/app/Templates/missing.xlsx'))
            noaccess=private/'data/denied';command(['sudo','mkdir',str(noaccess)]);command(['sudo','chmod','000',str(noaccess)])
            reject_configuration('storage-permission',lambda v:v['Xnk'].update(CustomsStoragePath='/app/data/denied'))
            command(['sudo','rmdir',str(noaccess)])
            def replace_runtime(value):
                path=private/'runtime.json';command(['sudo','chown',str(os.getuid())+':'+str(os.getgid()),str(path)]);path.chmod(0o600);path.write_text(json.dumps(value));path.chmod(0o400);command(['sudo','chown','1654:1654',str(path)])
            wrong_aes=json.loads(json.dumps(config));wrong_aes['UniversalDocDrafts']['Keys']['synthetic-v1']=__import__('base64').b64encode(os.urandom(32)).decode();replace_runtime(wrong_aes)
            cp('start','backend');ready();env['GATE6B_FAULT_MODE']='wrong-aes';command(['node','frontend/tests/gate6bFaults.e2e.mjs'])
            shutil.copy2(private/'browser/gate6b-faults-wrong-aes.json',EVIDENCE/'gate6b-wrong-aes.json');mark('incorrect_aes_key_runtime_resume_denied')
            cp('stop','backend');replace_runtime(config);cp('start','backend');ready()
            command(['sudo','chown','-R',str(os.getuid())+':'+str(os.getgid()),str(private/'data/protection-keys')])
            keyfiles=list((private/'data/protection-keys').glob('key-*.xml'));assert keyfiles and all('encryptedSecret' in p.read_text() for p in keyfiles)
            command(['sudo','chown','-R','1654:1654',str(private/'data/protection-keys')]);mark('dataprotection_persisted_keys_encrypted_at_rest')
            op('expire');env['GATE6B_FAULT_MODE']='expired-draft';command(['node','frontend/tests/gate6bFaults.e2e.mjs'])
            shutil.copy2(private/'browser/gate6b-faults-expired-draft.json',EVIDENCE/'gate6b-expiration.json');mark('expired_draft_after_recovery_denied')
            report['status']='PARTIAL';report['remaining']=['Full fault-injection inventory not yet verified','Windows SQL TLS production parity not yet verified','Previous-build rollback not yet verified','Actual internal pilot not executed']
        except Exception as e:
            report['failure_stage']=list(report['checks'])[-1] if report['checks'] else 'bootstrap'
            report['error']=str(e) if isinstance(e,AssertionError) else type(e).__name__
            # Only exception type identifiers from private runtime diagnostics.
            import re
            logs=cp('logs','--no-color',expected=None).stdout.decode(errors='replace')
            tcp=cp('exec','-T','backend','cat','/proc/net/tcp','/proc/net/tcp6',expected=None).stdout.decode(errors='replace')
            report['backend_listening_ports']=sorted({int(line.split()[1].split(':')[-1],16) for line in tcp.splitlines() if len(line.split())>3 and line.split()[3]=='0A'})
            report['exception_types']=sorted(set(re.findall(r'\b[A-Za-z.]+Exception\b',logs)))
            report['sql_error_numbers']=sorted(set(re.findall(r'(?i)error[: ]+(\d{3,6})',logs)))
            report['startup_classifiers']={key:bool(re.search(pattern,logs,re.I)) for key,pattern in {
                'certificate_load_failure':r'(?:certificate|private key).{0,100}(?:fail|error|unable|not found)|(?:fail|error|unable).{0,100}(?:certificate|private key)',
                'permission_denied':r'permission denied|access is denied',
                'master_database_failure':r'(?:master.mdf|master database).{0,100}(?:fail|error|unable)',
                'out_of_memory':r'insufficient memory|out of memory',
                'password_policy':r'password.{0,50}(?:policy|complexity)',
                'directory_creation_failure':r'(?:directory|file).{0,100}(?:cannot|unable|could not|failed)'}.items()}
            ids=cp('ps','-aq',expected=None).stdout.decode().split()
            if ids:
                states=json.loads(command(['docker','inspect',*ids],expected=None).stdout)
                report['container_states']=[{'service':v.get('Config',{}).get('Labels',{}).get('com.docker.compose.service'),'status':v['State']['Status'],'exit':v['State']['ExitCode'],'oom':v['State']['OOMKilled']} for v in states]
        finally:
            for p in processes:
                if p.poll() is None:p.terminate();p.wait(timeout=15)
            cp('down','-v','--remove-orphans',expected=None)
            command(['sudo','chown','-R',str(os.getuid())+':'+str(os.getgid()),str(private)],expected=None)
            (EVIDENCE/'gate6b-staging.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2),flush=True)
    return report
if __name__=='__main__':raise SystemExit(0 if verify()['status']=='PASS' else 1)
