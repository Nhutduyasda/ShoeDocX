"""Live official advisory scans and per-project resolved dependency/native graphs."""
import json
import os
import hashlib
import xml.etree.ElementTree as ET
from pathlib import Path
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
                    'runtime_targets':sorted(entry.get('runtimeTargets',{})),
                    'content_sha512':data['libraries'][identity].get('sha512'),
                    'native_provenance':native_provenance(data,identity,entry)})
    assert rows,'No package assets'
    return rows

def native_provenance(data, identity, entry):
    if not identity.lower().startswith('sqlitepclraw.lib.e_sqlite3/'):
        return []
    package=data['libraries'][identity]['path']
    rows=[]
    for folder in data['packageFolders']:
        root=Path(folder)/package
        if not root.exists(): continue
        for file in root.rglob('*'):
            if file.is_file() and file.suffix.lower() in ('.so','.dll','.dylib','.nuspec'):
                item={'path':file.relative_to(root).as_posix(),'sha256':hashlib.sha256(file.read_bytes()).hexdigest()}
                if file.suffix.lower()=='.nuspec':
                    metadata=ET.parse(file).getroot()
                    item['license']=[node.text for node in metadata.iter() if node.tag.split('}')[-1]=='license']
                    item['project_url']=[node.text for node in metadata.iter() if node.tag.split('}')[-1]=='projectUrl']
                rows.append(item)
    assert rows,'Native SQLite provenance missing'
    return rows

def dependency_paths(inventory, target):
    result=[]
    for project in sorted({r['project'] for r in inventory}):
        graph={r['package']:r for r in inventory if r['project']==project}
        queue=deque([[n] for n,r in graph.items() if r['direct']]);seen=set()
        while queue:
            path=queue.popleft();name=path[-1]
            if name in seen:continue
            seen.add(name)
            if name==target:
                result.append({'project':project,'path':[n+'/'+graph[n]['version'] if n in graph else n for n in path]});break
            queue.extend(path+[dep.lower()] for dep in graph.get(name,{}).get('dependencies',{}) if dep.lower() not in seen)
    assert result,'Advisory dependency path missing'
    return result

def audit(ci):
    report={}
    for name in ('baseline','feature'):
        root=ci.ROOT/name
        inventory=graph(root);scan=nuget.audit(root)
        for finding in scan['findings']:
            finding['exposures']=sorted({r['exposure'] for r in inventory if r['package']==finding['package'] and r['version']==finding['version']})
            finding['projects']=sorted({r['project'] for r in inventory if r['package']==finding['package'] and r['version']==finding['version']})
            finding['paths']=dependency_paths(inventory,finding['package'])
        report[name]={'nuget':scan,'graph':inventory,'npm':{}}
        for label,extra in [('complete',[]),('production',['--omit=dev'])]:
            result=ci.command(['npm.cmd' if os.name=='nt' else 'npm','audit','--json',*extra],root/'frontend')
            assert result.returncode in (0,1),'npm audit unavailable'
            data=json.loads(result.stdout);assert 'error' not in data and 'vulnerabilities' in data,'npm advisory response invalid'
            report[name]['npm'][label]=data
        report[name]['npm_lock']=json.loads((root/'frontend/package-lock.json').read_text())['packages']
    report['remaining_decisions']=[{
        'advisory':'GHSA-rj75-hqrm-r3gf','severity':'moderate','package':'postcss-selector-parser','resolved':'6.1.5',
        'verified_affected_range':'<7.1.6','classification':'build',
        'paths':['tailwindcss -> postcss-selector-parser','tailwindcss -> postcss-nested -> postcss-selector-parser'],
        'available_fix':'Verified compatible 6.1.5 upstream security backport installed; Tailwind 4 remains the registry-recommended major migration',
        'unresolved_reason':'Published backport contains all three linear-time passes, but npm/GitHub advisory still flags <7.1.6; retain Draft pending security review',
        'upstream_release':'https://github.com/postcss/postcss-selector-parser/releases/tag/6.1.5',
        'audit_suppressed':False,
        'actual_implementation':'patched backport verified independently from advisory range',
        'owner_risk_acceptance':False,
        'acceptance_criteria':['trusted version-controlled CSS/TSX only','no customer input in build sources','no public Vite service','production static dist only','isolated finite CI timeout','cross-platform browser regression','no new advisory']
    }]
    (ci.EVIDENCE/'gate6a-dependencies.json').write_text(json.dumps(report,indent=2))
    high=[f for f in report['feature']['nuget']['findings'] if f['severity'] in ('high','critical')]
    assert not high,'Unresolved NuGet High/Critical finding'
    for label in ('production','complete'):
        counts=report['feature']['npm'][label]['metadata']['vulnerabilities']
        assert counts.get('high',0)==counts.get('critical',0)==0,'Unresolved npm High/Critical finding'
    allowed={'tailwindcss','postcss-nested','postcss-selector-parser'}
    assert set(report['feature']['npm']['complete']['vulnerabilities'])<=allowed,'New npm advisory package'
    for value in report['feature']['npm']['complete']['vulnerabilities'].values():
        assert all(not isinstance(v,dict) or v['url'].endswith('/GHSA-rj75-hqrm-r3gf') for v in value['via']),'Unexpected residual npm advisory'
    old={(f['package'],f['url']) for f in report['baseline']['nuget']['findings']}
    assert all((f['package'],f['url']) in old for f in report['feature']['nuget']['findings']),'New NuGet advisory'
