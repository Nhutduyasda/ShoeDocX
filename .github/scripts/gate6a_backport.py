"""Verify the registry artifact, upstream fix and exact compiled frontend parity.

This produces technical evidence, never an advisory waiver or a clean-audit claim.
"""
import base64
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import tarfile
import urllib.request

START = '1df7badbf3635fda03687cb8604dde8858cd9bb5'
UPSTREAM = 'd90b6e38a0c79d07b02bf0d602bc242fe94be6b9'
FIX = '70b0779c843462197380d25daa02d1528bf7eae5'
PARSER_SHA256 = 'b00cfcba4229ca9db927fa401d1d2b40ecaec14401316bfce8283aa8cc2b46fc'
SOURCE_SHA256 = 'bf2ebf51b3ca0085f5e7e242df84bc08ee8c17e24b45ec1974d98b44c18b0430'
INTEGRITY = 'sha512-TEHFzb5KRDKaOgJ4TYE+0jyJtFbjLvkdVOu0fZ2b6MTWzoqZd4Le+61aEppWkvTlYePgOX4ATL1wsAIPMLTw6Q=='

def fetch(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent':'ShoeDocX-Gate6A-verifier'}),timeout=30) as r:
        return r.read()

def sha(data):
    return hashlib.sha256(data).hexdigest()

def verify(ci):
    feature=ci.ROOT/'feature'; before=ci.ROOT/'dependency-baseline'
    assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=before,text=True).strip()==START
    registry=json.loads(fetch('https://registry.npmjs.org/postcss-selector-parser/6.1.5'))
    assert registry['version']=='6.1.5' and registry['gitHead']==UPSTREAM
    assert registry['dist']['integrity']==INTEGRITY
    assert registry['dist']['tarball']=='https://registry.npmjs.org/postcss-selector-parser/-/postcss-selector-parser-6.1.5.tgz'
    archive=fetch(registry['dist']['tarball'])
    assert 'sha512-'+base64.b64encode(hashlib.sha512(archive).digest()).decode()==INTEGRITY
    with tarfile.open(fileobj=io.BytesIO(archive),mode='r:gz') as tar:
        published=tar.extractfile('package/dist/parser.js').read()
    assert sha(published)==PARSER_SHA256
    installed=feature/'frontend/node_modules/postcss-selector-parser/dist/parser.js'
    assert installed.read_bytes()==published,'Installed code differs from reviewed published artifact'
    source=fetch(f'https://raw.githubusercontent.com/postcss/postcss-selector-parser/{UPSTREAM}/src/parser.js')
    assert sha(source)==SOURCE_SHA256
    for term in (b'new Set(interpolations)',b'new Set(hasClass)',b'new Set(hasId)',b'Array.from(new Set(Array.prototype.concat.apply([], arguments)))',b'classIndexes.has(ind)',b'idIndexes.has(ind)',b'interpolationIndexes.has(hashIndex)'):
        assert term in source and term in published,'Backport membership pass missing'
    # The release is a version/changelog-only child of the security-fix commit.
    commit=json.loads(fetch(f'https://api.github.com/repos/postcss/postcss-selector-parser/commits/{UPSTREAM}'))
    assert commit['parents'][0]['sha']==FIX and commit['commit']['verification']['verified']
    # npm registry signatures bind package name/version/integrity, independently
    # of the publisher's descriptive gitHead metadata. No SLSA attestation claimed.
    keys=json.loads(fetch('https://registry.npmjs.org/-/npm/v1/keys'))
    code="""const c=require('node:crypto'),p=JSON.parse(process.argv[1]),keys=JSON.parse(process.argv[2]);
const ok=p.dist.signatures.some(s=>{const k=keys.keys.find(k=>k.keyid===s.keyid);return k&&c.verify('sha256',Buffer.from(`${p.name}@${p.version}:${p.dist.integrity}`),c.createPublicKey({key:Buffer.from(k.key,'base64'),format:'der',type:'spki'}),Buffer.from(s.sig,'base64'));});if(!ok)process.exit(1);"""
    assert subprocess.run(['node','-e',code,json.dumps(registry),json.dumps(keys)],check=False).returncode==0,'Registry signature invalid'
    npm='npm.cmd' if os.name=='nt' else 'npm'
    for args in ([npm,'ci'],[npm,'run','build']):
        assert ci.command(args,before/'frontend').returncode==0,'Pre-remediation clean build failed'
    old=json.loads((before/'frontend/package-lock.json').read_text())['packages']
    new=json.loads((feature/'frontend/package-lock.json').read_text())['packages']
    assert old.keys()==new.keys()
    changed=[k for k in old if old[k]!=new[k]]
    assert changed==['node_modules/postcss-selector-parser'],'Unrelated dependency changes'
    assert old[changed[0]]['version']=='6.1.4' and new[changed[0]]['version']=='6.1.5'
    assert new[changed[0]]['integrity']==INTEGRITY
    for consumer,constraint in [('tailwindcss','^6.1.2'),('postcss-nested','^6.1.1')]:
        assert new['node_modules/'+consumer]['dependencies']['postcss-selector-parser']==constraint
    files=lambda root:{p.relative_to(root).as_posix():sha(p.read_bytes()) for p in root.rglob('*') if p.is_file()}
    old_files=files(before/'frontend/dist');new_files=files(feature/'frontend/dist')
    assert old_files and old_files==new_files,'Compiled frontend changed; visual migration review required'
    result=subprocess.run([npm,'ls','postcss-selector-parser','--json'],cwd=feature/'frontend',
                          stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,timeout=60)
    assert result.returncode==0
    tests=ci.command(['node','--test','tests/selectorBackport.test.mjs'],feature/'frontend')
    assert tests.returncode==0,'Backport execution regression'
    report={'status':'PASS','baseline_sha':START,'feature_sha':os.environ['VERIFIED_HEAD'],
        'release_sha':UPSTREAM,'fix_parent_sha':FIX,'release_git_signature_verified':True,
        'registry_signature_verified':True,'npm_slsa_attestation':'not provided; not claimed',
        'tarball_integrity':INTEGRITY,'published_parser_sha256':PARSER_SHA256,'upstream_source_sha256':SOURCE_SHA256,
        'reviewed_three_linear_passes_present':True,'changed_lock_entries':changed,
        'before_lock':old,'after_lock':new,'installed_dependency_graph':json.loads(result.stdout),
        'compiled_files':new_files,'compiled_frontend_byte_identical':True,'backport_tests':5,
        'visual_screenshot_comparison':'separate gate6a-visual.json; compiled parity is not a screenshot claim',
        'security_acceptance':'separate from technical verification; preserve current audit result'}
    (ci.EVIDENCE/'gate6a-backport.json').write_text(json.dumps(report,indent=2))
