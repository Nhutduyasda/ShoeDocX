#define _GNU_SOURCE
#include <errno.h>
#include <fcntl.h>
#include <linux/audit.h>
#include <linux/filter.h>
#include <linux/seccomp.h>
#include <sched.h>
#include <signal.h>
#include <stddef.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/prctl.h>
#include <sys/resource.h>
#include <sys/socket.h>
#include <sys/syscall.h>
#include <sys/types.h>
#include <sys/stat.h>
#include <unistd.h>

/* Trusted synthetic test executable only. No document input or decoder library. */
static int map(const char *path, const char *s) {
    int fd=open(path,O_WRONLY); if(fd<0)return -1;
    int ok=write(fd,s,strlen(s))==(ssize_t)strlen(s); close(fd); return ok?0:-1;
}
static int nsprobe(const char *kind) {
    uid_t uid=getuid(); gid_t gid=getgid(); int e=0; const char *stage="user_namespace";
    if(unshare(CLONE_NEWUSER)<0){e=errno;goto out;}
    stage="setgroups";
    if(map("/proc/self/setgroups","deny")<0){e=errno;goto out;}
    char b[100]; stage="uid_map"; snprintf(b,sizeof b,"65534 %u 1",uid);
    if(map("/proc/self/uid_map",b)<0){e=errno;goto out;}
    stage="gid_map"; snprintf(b,sizeof b,"65534 %u 1",gid);
    if(map("/proc/self/gid_map",b)<0){e=errno;goto out;}
    stage=kind;
    int flags=!strcmp(kind,"pid")?CLONE_NEWPID:!strcmp(kind,"net")?CLONE_NEWNET:!strcmp(kind,"mount")?CLONE_NEWNS:0;
    if(flags && unshare(flags)<0)e=errno;
out:
    printf("{\"stage\":\"%s\",\"errno\":%d,\"uid\":%u,\"gid\":%u}\n",stage,e,getuid(),getgid());
    return e?1:0;
}
#define ALLOW(n) BPF_JUMP(BPF_JMP|BPF_JEQ|BPF_K,(n),0,1), BPF_STMT(BPF_RET|BPF_K,SECCOMP_RET_ALLOW)
static unsigned long nsid(const char *name){char p[80];struct stat s;snprintf(p,sizeof p,"/proc/self/ns/%s",name);return stat(p,&s)==0?(unsigned long)s.st_ino:0;}
static int guard(void) {
    struct sock_filter f[]={
        BPF_STMT(BPF_LD|BPF_W|BPF_ABS,offsetof(struct seccomp_data,arch)),
        BPF_JUMP(BPF_JMP|BPF_JEQ|BPF_K,AUDIT_ARCH_X86_64,1,0),
        BPF_STMT(BPF_RET|BPF_K,SECCOMP_RET_KILL_PROCESS),
        BPF_STMT(BPF_LD|BPF_W|BPF_ABS,offsetof(struct seccomp_data,nr)),
        ALLOW(SYS_read),ALLOW(SYS_write),ALLOW(SYS_close),ALLOW(SYS_openat),ALLOW(SYS_newfstatat),
        ALLOW(SYS_fstat),ALLOW(SYS_lseek),ALLOW(SYS_fcntl),ALLOW(SYS_mmap),ALLOW(SYS_munmap),
        ALLOW(SYS_mprotect),ALLOW(SYS_brk),ALLOW(SYS_getuid),ALLOW(SYS_getgid),ALLOW(SYS_getpid),
        ALLOW(SYS_prctl),ALLOW(SYS_clock_gettime),ALLOW(SYS_rt_sigaction),ALLOW(SYS_rt_sigprocmask),
        ALLOW(SYS_rt_sigreturn),ALLOW(SYS_pause),ALLOW(SYS_futex),ALLOW(SYS_exit),ALLOW(SYS_exit_group),
        BPF_STMT(BPF_RET|BPF_K,SECCOMP_RET_ERRNO|EPERM)
    };
    struct sock_fprog p={.len=sizeof(f)/sizeof(f[0]),.filter=f};
    return prctl(PR_SET_NO_NEW_PRIVS,1,0,0,0)||syscall(SYS_seccomp,SECCOMP_SET_MODE_FILTER,SECCOMP_FILTER_FLAG_TSYNC,&p);
}
static void cpu_signal(int sig){(void)sig; const char m[]="{\"event\":\"SIGXCPU\"}\n"; ssize_t n=write(1,m,sizeof(m)-1);_exit(n==(ssize_t)sizeof(m)-1?126:127);}
int main(int argc,char **argv){
    if(argc!=2)return 2;
    const char *op=argv[1];
    if(!strncmp(op,"ns-",3))return nsprobe(op+3);
    if(!strcmp(op,"identity")){
        printf("{\"uid\":%u,\"gid\":%u,\"nnp\":%d,\"seccomp\":%d}\n",getuid(),getgid(),prctl(PR_GET_NO_NEW_PRIVS,0,0,0,0),prctl(PR_GET_SECCOMP,0,0,0,0));return 0;
    }
    if(!strcmp(op,"pids")){
        /* Dedicated trusted controller stress probe, not the decoder seccomp profile. */
        if(prctl(PR_SET_NO_NEW_PRIVS,1,0,0,0))return 3;
        int n=0; while(n<128){pid_t p=fork();if(p<0)break;if(!p){for(;;)pause();}n++;}
        printf("{\"children\":%d,\"errno\":%d}\n",n,errno);fflush(stdout);for(;;)pause();
    }
    if(!strcmp(op,"cpu")){struct rlimit r={1,2};if(setrlimit(RLIMIT_CPU,&r))return 3;signal(SIGXCPU,cpu_signal);}
    unsigned long un=nsid("user"),pn=nsid("pid"),nn=nsid("net"),mn=nsid("mnt");
    if(guard())return 3;
    if(!strcmp(op,"ready")){
        printf("{\"uid\":%u,\"gid\":%u,\"nnp\":%d,\"seccomp\":%d,\"user_ns\":%lu,\"pid_ns\":%lu,\"net_ns\":%lu,\"mount_ns\":%lu,\"parent_env_absent\":%s,\"parent_fd_closed\":%s}\n",getuid(),getgid(),prctl(PR_GET_NO_NEW_PRIVS,0,0,0,0),prctl(PR_GET_SECCOMP,0,0,0,0),un,pn,nn,mn,getenv("SHOEDOCX_SYNTHETIC_PARENT_SECRET")==NULL?"true":"false",fcntl(200,F_GETFD)==-1&&errno==EBADF?"true":"false");return 0;
    }
    if(!strcmp(op,"hang")){puts("{\"event\":\"READY\"}");fflush(stdout);for(;;)pause();}
    if(!strcmp(op,"cpu")){volatile unsigned long x=1;for(;;)x=x*1664525+1013904223;}
    if(!strcmp(op,"memory")){for(;;){char *p=malloc(8*1024*1024);if(!p)return 4;for(size_t i=0;i<8*1024*1024;i+=4096)((volatile char*)p)[i]=1;}}
    int rc=-1,e=0;
    if(!strcmp(op,"network")){rc=socket(AF_INET,SOCK_STREAM,0);e=errno;if(rc>=0)close(rc);}
    else if(!strcmp(op,"read")){rc=open("/parent-only-synthetic-canary",O_RDONLY);e=errno;if(rc>=0)close(rc);}
    else if(!strcmp(op,"write")){rc=open("/app/unauthorized-write",O_CREAT|O_WRONLY,0600);e=errno;if(rc>=0)close(rc);}
    else if(!strcmp(op,"spawn")){rc=(int)syscall(SYS_clone,SIGCHLD,0,0,0,0);e=errno;}
    else if(!strcmp(op,"tmp")){
        int fd=open("/tmp/synthetic",O_CREAT|O_WRONLY,0600);if(fd<0)return 4;
        char b[32768]={0};long total=0;while(total<20*1024*1024){ssize_t n=write(fd,b,sizeof b);if(n<0){e=errno;break;}total+=n;}close(fd);
        printf("{\"written\":%ld,\"errno\":%d}\n",total,e);return 0;
    }else return 2;
    printf("{\"result\":%d,\"errno\":%d}\n",rc,e);return 0;
}
