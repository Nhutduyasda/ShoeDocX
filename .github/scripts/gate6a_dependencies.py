"""Live official advisory scans and per-project resolved dependency/native graphs."""
import json
import os
from collections import deque
import gate4b1_dependencies as nuget

def graph(root):
    rows=[]
    for assets in sorted(root.rglob('obj/project.assets.json')):
        if 'node_modules' in assets.parts: continue
        data=json.loads(assets.read_text(encoding='utf-8'));project=assets.parent.parent.relative_to(root).as_posix()
        direct={p.lower() for f in data['project']['frameworks'].values() for p in f.get('dependencies',{})}
        for target, packages in data['targets'].items():
            for identity,entry in packages.items():
                if entry.get('type')!='package': continue
                name,version=identity.split('/')
                rows.append({'project':project,'target':target,'package':name.lower(),'version':version,'direct':name.lower() in direct,
                    'exposure':'production' if project.startswith('backend/') else 'test',
                    'dependencies':entry.get('dependencies',{}),
                    'native_assets':sorted(entry.get('native',{})),
                    'runtime_targets':sorted(entry.get('runtimeTargets',{}))})
    assert rows,'No package assets'
    return rows

def audit(ci):
    report={}
    for name in ('baseline','feature'):
        root=ci.ROOT/name
        inventory=graph(root);scan=nuget.audit(root)
        for finding in scan['findings']:
            finding['exposures']=sorted({r['exposure'] for r in inventory if r['package']==finding['package'] and r['version']==finding['version']})
            finding['projects']=sorted({r['project'] for r in inventory if r['package']==finding['package'] and r['version']==finding['version']})
        report[name]={'nuget':scan,'graph':inventory,'npm':{}}
        for label,extra in [('complete',[]),('production',['--omit=dev'])]:
            result=ci.command(['npm.cmd' if os.name=='nt' else 'npm','audit','--json',*extra],root/'frontend')
            assert result.returncode in (0,1),'npm audit unavailable'
            data=json.loads(result.stdout);assert 'error' not in data and 'vulnerabilities' in data,'npm advisory response invalid'
            report[name]['npm'][label]=data
        report[name]['npm_lock']=json.loads((root/'frontend/package-lock.json').read_text())['packages']
    (ci.EVIDENCE/'gate6a-dependencies.json').write_text(json.dumps(report,indent=2))
    high=[f for f in report['feature']['nuget']['findings'] if f['severity'] in ('high','critical')]
    assert not high,'Unresolved NuGet High/Critical finding'
    for label in ('production','complete'):
        counts=report['feature']['npm'][label]['metadata']['vulnerabilities']
        assert counts.get('high',0)==counts.get('critical',0)==0,'Unresolved npm High/Critical finding'
    old={(f['package'],f['url']) for f in report['baseline']['nuget']['findings']}
    assert all((f['package'],f['url']) in old for f in report['feature']['nuget']['findings']),'New NuGet advisory'
