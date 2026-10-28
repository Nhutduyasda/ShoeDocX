"""Two immutable API versions on the same SQL database, mounts and trusted ingress."""
import hashlib,json,subprocess
from pathlib import Path
import gate5d_browser
FROZEN='b0604b9472a79905d1a9b5c7afe13e681f23a232'

def verify(feature,private,env,command,cp,op,ready,request,evidence,mark):
    root=feature.parent/'hardened'
    assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()==FROZEN,'FROZEN_RELEASE_MISMATCH'
    command(['docker','build','-f',str(root/'backend/ShoeExportInvoice.Api/Dockerfile'),'-t','gate6b-frozen-api',str(root)])
    context=private/'frozen-trust';context.mkdir();(context/'ca.crt').write_bytes((private/'ca.crt').read_bytes())
    (context/'Dockerfile').write_text('FROM gate6b-frozen-api\nUSER root\nCOPY ca.crt /usr/local/share/ca-certificates/gate6b.crt\nRUN update-ca-certificates\nUSER app\n')
    command(['docker','build','-t','gate6b-frozen-trusted',str(context)])
    image=lambda name:command(['docker','image','inspect',name,'--format','{{.Id}}']).stdout.decode().strip()
    a,b=image('gate6b-frozen-trusted'),image('gate6b-api-trusted');assert a!=b,'RELEASE_IMAGES_NOT_DISTINCT'
    result={'status':'FAIL','candidateA':FROZEN,'candidateB':env['VERIFIED_HEAD'],'imageA':a,'imageB':b,'productionRollbackApproved':False,'phases':[]}
    base=['docker','compose','-p','gate6b-isolated','-f',str(feature/'deploy/gate6b/compose.yml')]
    def switch(identity,phase):
        key_before=cp('exec','-T','backend','sh','-c','sha256sum /app/data/protection-keys/key-*.xml').stdout
        cp('stop','backend');op('state',private/'release-state-before.json')
        path=private/'release-image.json';path.write_text(json.dumps({'services':{'backend':{'image':identity}}}));path.chmod(0o600)
        command(base+['-f',str(path),'up','-d','--no-deps','--force-recreate','backend']);ready()
        op('state',private/'release-state-after.json')
        assert json.loads((private/'release-state-before.json').read_text())==json.loads((private/'release-state-after.json').read_text()),'STARTUP_CHANGED_DATABASE_'+phase
        key_after=cp('exec','-T','backend','sh','-c','sha256sum /app/data/protection-keys/key-*.xml').stdout
        assert key_before==key_after,'STARTUP_CHANGED_KEYRING_'+phase
        headers={k.lower():v for k,v in request('/api/health')[2].items()}
        assert headers.get('x-shoedocx-storage-health')==('temp-write-v1' if identity==b else None),'WRONG_RELEASE_BEHAVIOR_'+phase
        meta=json.loads(command(['docker','inspect',cp('ps','-q','backend').stdout.decode().strip()]).stdout)[0]
        assert meta['Image']==identity and meta['Config']['User']=='app' and meta['HostConfig']['ReadonlyRootfs']
        result['phases'].append({'phase':phase,'startupDatabaseUnchanged':True,'keyringUnchanged':True,'actualImage':identity})
    try:
        switch(a,'A-create');env['GATE6B_RELEASE_PHASE']='A-create';command(['node','frontend/tests/gate6bRelease.e2e.mjs'])
        op('fingerprint',private/'release-draft-before.json')
        for identity,phase in [(b,'B-upgrade'),(a,'A-rollback')]:
            switch(identity,phase);env['GATE6B_RELEASE_PHASE']=phase;command(['node','frontend/tests/gate6bRelease.e2e.mjs'])
            op('fingerprint',private/'release-draft-after.json');assert json.loads((private/'release-draft-before.json').read_text())==json.loads((private/'release-draft-after.json').read_text()),'RELEASE_CHANGED_ENCRYPTED_DRAFT'
            if phase=='B-upgrade':
                # Real session expiry also lets bounded SQL resume leases expire naturally.
                # No lease deletion, fake clock, SQL expiry rewrite or capacity exemption.
                env['GATE6B_FAULT_MODE']='workflow-expiry';command(['node','frontend/tests/gate6bFaults.e2e.mjs'],timeout=1300)
                (evidence/'gate6b-workflow-expiry.json').write_bytes((private/'browser/gate6b-faults-workflow-expiry.json').read_bytes())
                mark('real_twenty_minute_session_expiry_denied')
        workbooks=[]
        for phase in ['A-create','B-upgrade','A-rollback']:
            path=private/'browser'/('release-'+phase+'.xlsx');op('reopen',path);workbooks.append(gate5d_browser.verify_workbook(path,'F',20))
            result['phases'][['A-create','B-upgrade','A-rollback'].index(phase)]['authorization']=json.loads((private/'browser'/('release-'+phase+'.json')).read_text())
        result.update(status='PASS',encryptedDraftFingerprintUnchanged=True,workbooks=workbooks)
        mark('hardened_two_version_upgrade_and_rollback_authorized_xlsx')
    finally:
        switch(b,'B-return')
        (evidence/'gate6b-hardened-rollback.json').write_text(json.dumps(result,indent=2));print(json.dumps({'hardenedRollback':result},indent=2),flush=True)
    return result
