"""Same-host exact-HEAD regression; staging acceptance is a separate mandatory job."""
import json, os, platform, subprocess, tempfile
from pathlib import Path
import verify_gate2 as ci
import gate4a_checks,gate4b1_checks,gate5a_browser,gate5b_browser,gate5c_browser,gate5d_browser
import gate6a_backport
from gate6a_dependencies import audit
from runtime_target import host_dll
BASELINE='2552252d0d916d4184d88482f3649c602468eff8'
def main():
    report={'head':os.environ['VERIFIED_HEAD'],'baseline':BASELINE,'os':platform.platform(),'status':'FAIL','runs':{}}
    try:
        for name,sha in [('baseline',BASELINE),('feature',report['head'])]:
            root=ci.ROOT/name
            assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()==sha
            assert subprocess.check_output(['dotnet','--version'],cwd=root,text=True).strip()=='10.0.402'
            for project in sorted(root.rglob('*.csproj')):
                if any(x in ('obj','node_modules') for x in project.parts):continue
                result=ci.command(['dotnet','build',str(project)],root)
                if result.returncode:print('\n'.join(x for x in result.stdout.splitlines() if 'error ' in x),flush=True)
                assert result.returncode==0,'BUILD_FAILED_'+project.name
        gate4a_checks.frontend(ci)
        npm='npm.cmd' if os.name=='nt' else 'npm'
        for name in ('baseline','feature'):
            assert ci.command(['node','--test','tests/officeModel.test.mjs','tests/selectorBackport.test.mjs'],ci.ROOT/name/'frontend').returncode==0
        gate6a_backport.verify(ci);audit(ci)
        runs={}
        with tempfile.TemporaryDirectory(prefix='gate6b-trx-') as temporary:
            for name in ('baseline','feature'):
                os.environ['GATE5C_PROBE']=str(host_dll(ci.ROOT/name,'Gate5CDemoHost'))
                for scope in ('engine','full'):
                    label=name+'-'+scope;runs[label]=ci.execute(label,ci.ROOT/name,scope=='engine',Path(temporary));report['runs'][label]={k:v for k,v in runs[label].items() if k!='tests'}
        for scope in ('engine','full'):
            old,new=runs['baseline-'+scope],runs['feature-'+scope]
            assert old['skipped']==new['skipped']==0
            for identity,prior in old['tests'].items():
                assert identity in new['tests'],'BASELINE_TEST_MISSING'
                if new['tests'][identity]['outcome']=='Failed':assert prior==new['tests'][identity],'NEW_OR_CHANGED_FAILURE'
            added=new['tests'].keys()-old['tests'].keys()
            assert added and all(new['tests'][k]['outcome']=='Passed' and new['tests'][k]['method'].startswith(ci.ENGINE+'Gate6B') for k in added)
            if scope=='engine':assert new['failed']==0
        gate4b1_checks.browser(ci);gate5a_browser.browser(ci);gate5b_browser.browser(ci);gate5c_browser.browser(ci);gate5d_browser.browser(ci)
        report['status']='PASS'
    except Exception as error:report['error']=str(error) if isinstance(error,AssertionError) else type(error).__name__
    finally:
        (ci.EVIDENCE/'gate6b-regression.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2),flush=True)
    return 0 if report['status']=='PASS' else 1
if __name__=='__main__':raise SystemExit(main())
