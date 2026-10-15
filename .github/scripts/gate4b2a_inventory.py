"""Sanitized resolved inventory and graph, without machine paths or package contents."""
import json
from pathlib import Path
import sys

PROJECTS = ('backend/ShoeExportInvoice.Api', 'tests/ShoeExportInvoice.Tests',
            'tests/Gate4ADemoHost', 'tests/Gate4B1DemoHost')


def inventory(root):
    projects = {}
    for relative in PROJECTS:
        assets = json.loads((root / relative / 'obj/project.assets.json').read_text(encoding='utf-8-sig'))
        projects[relative] = {
            'direct': {tfm: value.get('dependencies', {}) for tfm, value in assets['project']['frameworks'].items()},
            'targets': {tfm: {name: {k: value[k] for k in ('type', 'dependencies', 'compile', 'runtime', 'runtimeTargets') if k in value}
                              for name, value in target.items()} for tfm, target in assets['targets'].items()}}
    lock = json.loads((root / 'frontend/package-lock.json').read_text(encoding='utf-8'))
    return {'dotnet': projects, 'npm': {name: {k: value[k] for k in
            ('version', 'dev', 'optional', 'dependencies', 'devDependencies', 'peerDependencies', 'integrity') if k in value}
            for name, value in lock['packages'].items()}}


if __name__ == '__main__':
    Path(sys.argv[2]).write_text(json.dumps(inventory(Path(sys.argv[1])), indent=2), encoding='utf-8')
