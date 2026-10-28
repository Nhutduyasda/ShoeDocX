"""Real ENOSPC in bounded container tmpfs only. Never fill the runner filesystem."""
import json,os,zipfile
import gate5d_browser

def verify(private,env,cp,op,command,evidence,mark):
    fixture=private/'disk-fixture.xlsx'
    fixture.write_bytes((private/'fixtures/F-1-false-none.xlsx').read_bytes())
    with zipfile.ZipFile(fixture,'a') as workbook:workbook.comment=b'G'*65535
    env['GATE6B_DISK_FIXTURE']=str(fixture)
    op('fingerprint',private/'disk-before.json')
    result={'status':'FAIL','runnerRootFilled':False,'volumes':[],'phases':[]}
    def browser(phase):
        env['GATE6B_DISK_PHASE']=phase;command(['node','frontend/tests/gate6bDisk.e2e.mjs'])
        value=json.loads((private/'browser'/('disk-'+phase+'.json')).read_text());assert value['status']=='PASS'
        path=private/'browser'/('disk-'+phase+'.xlsx');op('reopen',path)
        value['independentWorkbook']=gate5d_browser.verify_workbook(path,'F',20);result['phases'].append(value)
    browser('baseline')
    for service,path,limit,phase in [('web','/var/cache/nginx',8,'ingress-full'),('backend','/tmp',64,'backend-full')]:
        filler=path+'/gate6b-fill'
        try:
            # Check the mount first; a writable overlay/root disk is never a target.
            info=cp('exec','-T','-u','0',service,'sh','-c',"awk '$2==\""+path+"\" && $3==\"tmpfs\" {print $4}' /proc/mounts").stdout.decode()
            assert 'size='+str(limit*1024)+'k' in info,'BOUNDED_TMPFS_REQUIRED'
            full=cp('exec','-T','-u','0',service,'sh','-c','dd if=/dev/zero of='+filler+' bs=1048576 count='+str(limit+1),expected=None)
            diagnostic={'service':service,'exit':full.returncode,'actualENOSPC':b'No space left on device' in full.stdout,
                'toolOutput':full.stdout.decode(errors='replace')[:1200]}
            (evidence/'gate6b-disk-attempt.json').write_text(json.dumps(diagnostic,indent=2));print(json.dumps({'diskAttempt':diagnostic}),flush=True)
            assert full.returncode!=0 and diagnostic['actualENOSPC'],'ENOSPC_NOT_OBSERVED'
            result['volumes'].append({'service':service,'limitMiB':limit,'actualENOSPC':True})
            browser(phase)
        finally:cp('exec','-T','-u','0',service,'rm','-f',filler,expected=None)
        browser(phase+'-recovered')
        if service=='web':
            remaining=cp('exec','-T','-u','0','web','sh','-c','find /var/cache/nginx -type f | wc -l').stdout.decode().strip()
            assert remaining=='0','PARTIAL_INGRESS_UPLOAD_LEFT_BEHIND';result['partialIngressFilesRemaining']=0
        op('fingerprint',private/'disk-after.json');assert json.loads((private/'disk-before.json').read_text())==json.loads((private/'disk-after.json').read_text()),'DISK_FAULT_CHANGED_DRAFT'
    result.update(status='PASS',encryptedDraftFingerprintUnchanged=True)
    (evidence/'gate6b-disk-full.json').write_text(json.dumps(result,indent=2));print(json.dumps({'diskFull':result},indent=2),flush=True)
    mark('actual_tmpfs_enospc_upload_atomicity_export_and_capacity_recovery')
    return result
