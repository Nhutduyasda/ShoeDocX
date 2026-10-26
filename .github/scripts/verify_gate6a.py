"""Exact-HEAD .NET 8 baseline vs .NET 10 feature, no historical failure allow-list."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import platform
import re
import xml.etree.ElementTree as ET
import verify_gate2 as ci
import gate4a_checks
import gate4b1_checks
import gate5a_browser
import gate5b_browser
import gate5c_browser
import gate5d_browser
from gate6a_dependencies import audit
from runtime_target import host_dll
import gate6a_backport
import gate6a_visual
BASELINE = '3d07a4f1db7e95cccb1f36e7de23569e5f57c2c5'

def main():
    report = {'gate':'6A', 'baseline_sha':BASELINE, 'feature_sha':os.environ['VERIFIED_HEAD'], 'os':platform.platform(), 'status':'FAIL', 'runs':{}, 'builds':{}}
    try:
        for name, sha, sdk in [('baseline',BASELINE,'8.0.426'),('feature',report['feature_sha'],'10.0.402')]:
            root = ci.ROOT/name
            assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()==sha, 'Wrong checkout'
            if name=='baseline': (root/'global.json').write_text(json.dumps({'sdk':{'version':sdk,'rollForward':'disable','allowPrerelease':False}}))
            assert subprocess.check_output(['dotnet','--version'],cwd=root,text=True).strip()==sdk, 'Wrong SDK'
            for project in sorted(root.rglob('*.csproj')):
                if any(p in ('obj','node_modules') for p in project.parts): continue
                result=ci.command(['dotnet','build',str(project)],root)
                key=name+':'+project.relative_to(root).as_posix();report['builds'][key]=result.returncode
                if result.returncode: print('\n'.join(l for l in result.stdout.splitlines() if 'error ' in l),flush=True)
                assert result.returncode==0,'Project build failed: '+key
        gate4a_checks.frontend(ci)
        result=ci.command(['node','--test','tests/officeModel.test.mjs'],ci.ROOT/'feature/frontend')
        assert result.returncode==0,'Office model tests failed'
        gate6a_backport.verify(ci)
        audit(ci)
        gate6a_visual.verify(ci)
        runs={}
        with tempfile.TemporaryDirectory(prefix='gate6a-trx-') as temporary:
            for name in ('baseline','feature'):
                os.environ['GATE5C_PROBE']=str(host_dll(ci.ROOT/name,'Gate5CDemoHost'))
                for scope in ('engine','full'):
                    label=name+'-'+scope
                    runs[label]=ci.execute(label,ci.ROOT/name,scope=='engine',Path(temporary))
                    report['runs'][label]={k:v for k,v in runs[label].items() if k!='tests'}
                    if label=='feature-engine':
                        tree=ET.parse(Path(temporary)/label/'results.trx')
                        versions=[]
                        for node in tree.findall('.//t:UnitTestResult',ci.NS):
                            versions.extend(re.findall(r'native_sqlite_version=([0-9.]+)',node.findtext('t:Output/t:StdOut','',ci.NS)))
                        assert len(versions)==1,'Native SQLite execution version evidence missing'
                        report['native_sqlite_version']=versions[0]
        fixed=[]
        for scope in ('engine','full'):
            old,new=runs['baseline-'+scope],runs['feature-'+scope]
            assert old['total']>0 and old['skipped']==new['skipped']==0
            for identity,prior in old['tests'].items():
                assert identity in new['tests'],'Missing baseline identity: '+identity
                current=new['tests'][identity]
                if current['outcome']=='Failed': assert current==prior,'New/changed failure: '+identity
                elif prior['outcome']=='Failed': fixed.append(identity)
            added=new['tests'].keys()-old['tests'].keys()
            assert added and all(new['tests'][k]['outcome']=='Passed' and new['tests'][k]['method'].startswith(ci.ENGINE+'Gate6A') for k in added), 'New test failure or unexplained test inventory'
            report['new_'+scope+'_tests']=len(added)
        for name in ('baseline','feature'):
            engine=runs[name+'-engine'];full=runs[name+'-full']
            assert engine['failed']==0 and engine['tests']=={k:v for k,v in full['tests'].items() if v['method'].startswith(ci.ENGINE)},'Engine mismatch'
        report['resolved_baseline_failures']=sorted(set(fixed))
        gate4b1_checks.browser(ci)
        gate5a_browser.browser(ci)
        gate5b_browser.browser(ci)
        gate5c_browser.browser(ci)
        gate5d_browser.browser(ci)
        report['status']='PASS'
        report['delivery_verdict']='TECHNICAL ACCEPTANCE PASS — OWNER SECURITY DECISION REQUIRED'
        report['merge_recommendation']='NOT READY'
        report['delivery_blockers']=['Explicit owner security decision for the verified backport/current advisory discrepancy is not yet granted']
    except Exception as error:
        report['blocker']=str(error) if isinstance(error,AssertionError) else type(error).__name__
    finally:
        (ci.EVIDENCE/'gate6a-summary.json').write_text(json.dumps(report,indent=2))
        print(json.dumps(report,indent=2),flush=True)
    return 0 if report['status']=='PASS' else 1
if __name__=='__main__': raise SystemExit(main())
