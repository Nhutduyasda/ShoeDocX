"""Finite trusted-probe feasibility investigation. Never loads a document/decoder."""
import errno
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import signal
import subprocess
import sys
import tempfile
import time

ROOT = Path(__file__).resolve().parents[2]
CHECKS = ('nonroot', 'direct_user_mapping', 'direct_pid', 'direct_net', 'direct_mount',
          'bwrap_mapping', 'confined_bootstrap', 'namespace_identity', 'nnp_seccomp',
          'network', 'read', 'write', 'spawn', 'tmp', 'parent_env_fd', 'memory', 'cpu_primitive',
          'pids', 'cancellation', 'parent_death', 'pdf_budget', 'pdf_image_decode')


def classify(stderr, trace=''):
    # Raw strings/traces remain private temp data; only fixed operations/errno are exported.
    denied = re.findall(r'\b(unshare|mount|mount_setattr|clone3?|execve|openat|write)\([^\n]*?= -1 (EACCES|EPERM|ENOENT|EROFS|EINVAL|ENOSPC)', trace)
    pairs = sorted({(op, code) for op, code in denied})
    if any(op == 'unshare' and code in ('EACCES', 'EPERM') for op, code in pairs):
        category = 'SECURITY_POLICY_OR_HOST_DENIAL'
    elif any(op in ('mount', 'mount_setattr') for op, _ in pairs):
        category = 'MOUNT_CONFIGURATION_OR_POLICY_DENIAL'
    elif 'uid map' in stderr or 'gid map' in stderr:
        category = 'ID_MAPPING_FAILURE'
    elif 'Permission denied' in stderr or 'Operation not permitted' in stderr:
        category = 'PERMISSION_DENIED_UNKNOWN_OPERATION'
    elif 'No such file' in stderr:
        category = 'CONFIGURATION_OR_RUNTIME_PATH'
    else:
        category = 'UNKNOWN'
    flags=sorted({flag for line in trace.splitlines() if 'unshare(' in line and re.search(r'= -1 (EACCES|EPERM)',line) for flag in re.findall(r'CLONE_NEW[A-Z]+',line)})
    return {'category': category, 'denied_namespace_flags':flags, 'denied_syscalls': [{'operation': op, 'errno': code} for op, code in pairs],
            'stderr_sha256': hashlib.sha256(stderr.encode()).hexdigest()}


def command(args, env=None, timeout=8, preexec=None, on_timeout=None):
    started = time.monotonic()
    p = subprocess.Popen(args, stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=env,
                         close_fds=True, start_new_session=True, preexec_fn=preexec)
    timed_out = False
    try:
        out, err = p.communicate(timeout=timeout)
    except subprocess.TimeoutExpired:
        timed_out = True
        if on_timeout: on_timeout()
        try: os.killpg(p.pid, signal.SIGKILL)
        except ProcessLookupError: pass
        out, err = p.communicate(timeout=2)
    # Native trusted probes have fixed small output, but reject excess rather than archive it.
    if len(out) > 8192 or len(err) > 8192:
        raise ValueError('TRUSTED_PROBE_OUTPUT_LIMIT')
    return {'exit_code': p.returncode, 'timeout': timed_out, 'wall_ms': round((time.monotonic()-started)*1000, 2)}, out.decode(errors='replace'), err.decode(errors='replace')


def value(path, fallback='UNAVAILABLE'):
    try: return Path(path).read_text().strip()
    except OSError: return fallback


def counters(group):
    def fields(name):
        return {k: int(v) for k, v in (line.split() for line in value(group/name, '').splitlines())}
    return {'cpu': fields('cpu.stat'), 'memory': fields('memory.events'), 'pids': fields('pids.events'),
            'peak_memory_bytes': int(value(group/'memory.peak', '0'))}


def populated(group):
    return 'populated 1' in value(group/'cgroup.events', '')


def killgroup(group):
    try:
        fd=os.open(group/'cgroup.kill',os.O_WRONLY)
        try: os.write(fd,b'1\n')
        finally: os.close(fd)
    except OSError: pass


def makegroup(parent):
    group=parent/('probe-'+os.urandom(8).hex());group.mkdir()
    try:
        for name, data in {'memory.max':str(256*1024*1024),'memory.swap.max':'0','pids.max':'64','cpu.max':'100000 100000'}.items():
            fd=os.open(group/name,os.O_WRONLY)
            try: os.write(fd,(data+'\n').encode())
            finally: os.close(fd)
    except OSError:
        killgroup(group)
        try:group.rmdir()
        except OSError:pass
        raise
    return group


def attach(group):
    # Runs only in the trusted single-threaded Popen bootstrap, before exec and source input.
    fd=os.open(group/'cgroup.procs',os.O_WRONLY)
    try: os.write(fd,(str(os.getpid())+'\n').encode())
    finally: os.close(fd)


def bwrap(runtime, op, full=True):
    args=['/usr/bin/bwrap','--unshare-all' if full else '--unshare-user','--die-with-parent',
          '--new-session','--cap-drop','ALL','--uid','65534','--gid','65534',
          '--ro-bind',str(runtime),'/app']
    if full: args+=['--proc','/proc','--dev','/dev','--size','16777216','--tmpfs','/tmp']
    return args+['--chdir','/app','--','/app/probe',op]


def record(report, name, status, **data):
    report['checks'][name]={'status':status,**data}


def evaluate(report):
    checks=report.get('checks',{})
    if any(v.get('status')=='FAIL' for v in checks.values()): return 'FAIL'
    return 'PASS' if set(checks)==set(CHECKS) and all(v.get('status')=='SUPPORTED' for v in checks.values()) else 'BLOCKED'


def run(outdir):
    outdir.mkdir(parents=True,exist_ok=True)
    report={'gate':'4B.2B-F','head':os.environ.get('VERIFIED_HEAD'),'kernel':platform.release(),
            'reference_pr13':'98dae66b96efdf8fefb33d15f69e73e40489cac8','phase2_authorized':False,
            'checks':{n:{'status':'NOT_RUN'} for n in CHECKS},'measurements':[],
            'host_apparmor_confined':value('/proc/self/attr/current') not in ('unconfined','UNAVAILABLE'),
            'policies_changed':False,'decoder_executed':False,
            'host_settings':{n:value('/proc/sys/'+p) for n,p in {
                'unprivileged_userns_clone':'kernel/unprivileged_userns_clone',
                'apparmor_restrict_unprivileged_userns':'kernel/apparmor_restrict_unprivileged_userns',
                'max_user_namespaces':'user/max_user_namespaces'}.items()}}
    # Exact source review blocker: no stream/object/feature instrumentation selected or implemented.
    record(report,'pdf_budget','BLOCKED',code='NO_VERIFIED_32MIB_CUMULATIVE_PDF_ACCOUNTING',
           basis='STATIC_REVIEW_PR13_WORKER_AND_NATIVE_INVENTORY',dynamic_pdf_test='NOT_RUN')
    groups=[]
    try:
        assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()==report['head'],'HEAD_MISMATCH'
        changed=subprocess.check_output(['git','diff','--name-only','5ff844e3761de65b14acf9a69b8a090d43a40152','HEAD'],cwd=ROOT,text=True).splitlines()
        assert all(p.startswith(('tools/UniversalDocDecoder.LinuxFeasibility/','docs/gate4b2bf/')) or p in {
            '.github/scripts/gate4b2bf_linux.py','.github/scripts/test_gate4b2bf_linux.py',
            '.github/workflows/gate4b2bf-linux-feasibility.yml','docs/UNIVERSAL_DOC_ENGINE_GATE4B2B_F_LINUX_FEASIBILITY.md'} for p in changed),'ISOLATION_SCOPE'
        record(report,'nonroot','SUPPORTED' if os.geteuid()!=0 else 'HOST_RESTRICTION',uid=os.geteuid())
        if os.geteuid()==0:return report
        with tempfile.TemporaryDirectory(prefix='shoedocx-feasibility-') as temp:
            runtime=Path(temp)/'runtime';runtime.mkdir(); probe=runtime/'probe'
            build=subprocess.run(['gcc','-std=c11','-O2','-Wall','-Wextra','-Werror','-static',str(ROOT/'tools/UniversalDocDecoder.LinuxFeasibility/probe.c'),'-o',str(probe)],capture_output=True,text=True)
            assert build.returncode==0,'TRUSTED_STATIC_PROBE_BUILD_FAILED'
            report['binary_sha256']=hashlib.sha256(probe.read_bytes()).hexdigest()
            report['package_versions']=subprocess.check_output(['dpkg-query','-W','-f=${Package}=${Version}\n','bubblewrap','strace','gcc','libc6-dev'],text=True).splitlines()
            safe_env={'LANG':'C'}
            control, text, _=command([str(probe),'identity'],env=safe_env)
            report['trusted_control']={**control,'identity':json.loads(text)}
            assert control['exit_code']==0,'TRUSTED_CONTROL_FAILED'
            for kind in ('user','pid','net','mount'):
                result,text,err=command([str(probe),'ns-'+kind],env=safe_env)
                name='direct_user_mapping' if kind=='user' else 'direct_'+kind
                record(report,name,'SUPPORTED' if result['exit_code']==0 else 'SECURITY_POLICY_OR_HOST_DENIAL',**result,probe=json.loads(text))
                if result['exit_code']!=0:break
            # Executable-specific AppArmor policy can differ; one bwrap control discriminates it.
            base,_,err=command(bwrap(runtime,'identity',False),env=safe_env)
            record(report,'bwrap_mapping','SUPPORTED' if base['exit_code']==0 else 'BLOCKED',**base,**classify(err))
            if base['exit_code']!=0:
                trace=Path(temp)/'bootstrap.trace'
                traced,_,traceerr=command(['/usr/bin/strace','-f','-qq','-e','trace=unshare,mount,mount_setattr,clone,clone3,execve,openat,write','-o',str(trace)]+bwrap(runtime,'identity',False),env=safe_env)
                assert trace.stat().st_size<=1048576,'TRUSTED_TRACE_LIMIT'
                report['bootstrap_diagnostic']={'untraced':base,'traced':traced,**classify(traceerr,value(trace,'')),
                    'trace_is_trusted_probe_only':True,'raw_trace_archived':False}
                return report
            parent=Path('/sys/fs/cgroup/shoedocx-feasibility-'+str(os.geteuid()))
            assert '/shoedocx-feasibility-' in value('/proc/self/cgroup'),'SUPERVISOR_OUTSIDE_DELEGATION'
            parent_ns={n:os.stat('/proc/self/ns/'+n).st_ino for n in ('user','pid','net','mnt')}
            os.environ['SHOEDOCX_SYNTHETIC_PARENT_SECRET']='synthetic-not-a-credential'
            fd=os.open(probe,os.O_RDONLY);os.dup2(fd,200,inheritable=True);os.close(fd)
            for op in ('ready','network','read','write','spawn','tmp','memory','cpu','pids','hang'):
                group=makegroup(parent);groups.append(group)
                before=counters(group)
                result,text,err=command(bwrap(runtime,op),env=safe_env,timeout=3 if op in ('pids','hang') else 8,preexec=lambda:attach(group),on_timeout=lambda:killgroup(group))
                after=counters(group)
                killgroup(group)
                for _ in range(40):
                    if not populated(group):break
                    time.sleep(.025)
                report['measurements'].append({'operation':op,**result,**after,'empty_after_cleanup':not populated(group)})
                data=json.loads(text) if text.strip() else {}
                if op=='ready':
                    ready=result['exit_code']==0 and data.get('uid')==65534 and data.get('gid')==65534
                    record(report,'confined_bootstrap','SUPPORTED' if ready else 'BLOCKED',**result,**classify(err))
                    if not ready:return report
                    record(report,'namespace_identity','SUPPORTED' if all(data.get(k+'_ns',0)!=parent_ns[n] and data.get(k+'_ns',0)>0 for k,n in [('user','user'),('pid','pid'),('net','net'),('mount','mnt')]) else 'FAIL')
                    record(report,'nnp_seccomp','SUPPORTED' if data.get('nnp')==1 and data.get('seccomp')==2 else 'FAIL')
                    record(report,'parent_env_fd','SUPPORTED' if data.get('parent_env_absent') and data.get('parent_fd_closed') else 'FAIL')
                elif op in ('network','read','write','spawn'):
                    expected={'network':errno.EPERM,'read':errno.ENOENT,'write':errno.EROFS,'spawn':errno.EPERM}[op]
                    record(report,op,'SUPPORTED' if not result['timeout'] and result['exit_code']==0 and data.get('result')==-1 and data.get('errno')==expected else 'FAIL',**result,probe=data)
                elif op=='tmp':record(report,op,'SUPPORTED' if data.get('errno')==errno.ENOSPC and data.get('written',2**30)<=16777216 else 'FAIL',probe=data)
                elif op=='memory':record(report,op,'SUPPORTED' if after['memory'].get('oom_kill',0)>before['memory'].get('oom_kill',0) and not result['timeout'] else 'FAIL',**result)
                elif op=='cpu':record(report,'cpu_primitive','SUPPORTED' if data.get('event')=='SIGXCPU' and result['exit_code']==126 and not result['timeout'] else 'FAIL',note='1s soft/2s hard trusted calibration; NOT per-page 5s or aggregate 15s acceptance')
                elif op=='pids':record(report,op,'SUPPORTED' if data.get('errno')==errno.EAGAIN and after['pids'].get('max',0)>0 else 'FAIL',probe=data,note='trusted fork stress; not decoder syscall policy')
                elif op=='hang':record(report,'cancellation','SUPPORTED' if result['timeout'] and not populated(group) else 'FAIL')
                if any(v['status']=='FAIL' for v in report['checks'].values()):return report
            # No decoder expansion: parser prerequisite remains blocked; parent-death needs an independent supervisor-kill test.
            record(report,'parent_death','NOT_RUN',code='INDEPENDENT_SUPERVISOR_DEATH_TEST_NOT_IMPLEMENTED')
            return report
    except Exception as error:
        report['investigation_error']=str(error) if isinstance(error,AssertionError) else type(error).__name__
        return report
    finally:
        for group in groups:
            killgroup(group)
            try:group.rmdir()
            except OSError:pass
        report['status']=evaluate(report)
        report['recommendation']='STOP' if report['checks']['bwrap_mapping']['status']=='BLOCKED' else 'REDESIGN'
        (outdir/'summary.json').write_text(json.dumps(report,indent=2)+'\n')


def audit(lines):
    events=[]
    for line in lines:
        if 'apparmor="DENIED"' not in line or not re.search(r'comm="(?:bwrap|probe)"',line): continue
        op=re.search(r'operation="([a-z0-9_]+)"',line)
        error=re.search(r'\berror=(-?\d+)',line)
        events.append({'operation':op.group(1) if op else 'UNKNOWN','errno':int(error.group(1)) if error else None})
    return {'events':events[:20],'raw_audit_archived':False,'note':'Absence of events is not evidence AppArmor allowed the operation.'}


if __name__=='__main__' and sys.argv[1:]==['--audit']:
    print(json.dumps(audit(sys.stdin),indent=2));raise SystemExit(0)
if __name__=='__main__':
    report=run(Path(sys.argv[1]))
    print(json.dumps(report,indent=2))
    raise SystemExit(0 if report.get('status')=='PASS' else 1)
