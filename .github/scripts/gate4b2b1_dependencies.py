"""Fail-closed NuGet advisory scan plus CycloneDX package/native inventory for the isolated POC."""
import hashlib
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
import gate4b1_dependencies as advisory


def inspect(root, output):
    output.mkdir(exist_ok=True, parents=True)
    assets = json.loads((root / 'tools/UniversalDocDecoder.Worker/obj/project.assets.json').read_text(encoding='utf-8-sig'))
    components, native = [], []
    folders = [Path(p) for p in assets['packageFolders']]
    for identity, library in sorted(assets['libraries'].items()):
        if library['type'] != 'package':
            continue
        name, version = identity.split('/')
        package = next(p / library['path'] for p in folders if (p / library['path']).exists())
        nuspec = next(package.glob('*.nuspec'))
        metadata = ET.parse(nuspec).getroot()
        license_node = metadata.find('.//{*}license')
        repository = metadata.find('.//{*}repository')
        component = {'type': 'library', 'name': name, 'version': version, 'bom-ref': identity,
                     'purl': f'pkg:nuget/{name}@{version}', 'properties': [
                         {'name': 'nuget.contentHash', 'value': library['sha512']},
                         {'name': 'repository', 'value': json.dumps(repository.attrib if repository is not None else {})}]}
        if license_node is not None:
            component['properties'].append({'name': 'nuget.license', 'value': license_node.text or ''})
        components.append(component)
        for relative in library.get('files', []):
            if relative.endswith(('.dll', '.so', '.dylib')) and ('native/' in relative or '/nativeassets/' in relative):
                file = package / relative
                native.append({'package': identity, 'file': relative, 'sha256': hashlib.sha256(file.read_bytes()).hexdigest()})
    index = advisory.fetch('https://api.nuget.org/v3/vulnerabilities/index.json')
    assert index and all(x.get('@updated') for x in index), 'Missing official advisory metadata'
    records = {}
    for item in index:
        for name, entries in advisory.fetch(item['@id']).items(): records.setdefault(name, []).extend(entries)
    findings = []
    for component in components:
        for entry in records.get(component['name'].lower(), []):
            if advisory.affected(component['version'], entry['versions']):
                findings.append({'package': component['name'], 'version': component['version'], **entry})
    dependencies = []
    target = next(iter(assets['targets'].values()))
    component_ids = {c['bom-ref'] for c in components}
    identities = {k.split('/')[0]: k for k in target if k in component_ids}
    for identity, value in target.items():
        if identity not in component_ids: continue
        dependencies.append({'ref': identity, 'dependsOn': [identities[n] for n in value.get('dependencies', {}) if n in identities]})
    sbom = {'bomFormat': 'CycloneDX', 'specVersion': '1.5', 'version': 1, 'components': components, 'dependencies': dependencies, 'metadata': {'properties': [
        {'name': 'shoedocx:native-review', 'value': 'INCOMPLETE'},
        {'name': 'shoedocx:native-review-evidence', 'value': 'docs/gate4b2b1/recovery-native-review.json'}]}}
    (output / 'decoder-sbom.cdx.json').write_text(json.dumps(sbom, indent=2), encoding='utf-8')
    report = {'official_feed': index, 'findings': findings, 'native_files': native,
              'native_nested_component_advisory_coverage': 'INCOMPLETE: package audit is not a native codec CVE proof',
              'package_components': components, 'runtime_framework': assets.get('project', {}).get('frameworks', {})}
    # Strip framework paths: only target requirements, not machine metadata, belong in artifacts.
    report.pop('runtime_framework')
    (output / 'decoder-dependencies.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    assert not [f for f in findings if f['severity'] >= 2], 'New High/Critical decoder package advisory'
    return report


if __name__ == '__main__':
    result = inspect(Path(sys.argv[1]).resolve(), Path(sys.argv[2]).resolve())
    print(json.dumps({'packages': len(result['package_components']), 'nativeFiles': len(result['native_files']), 'findings': result['findings']}))
