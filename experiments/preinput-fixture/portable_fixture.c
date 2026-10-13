#define _GNU_SOURCE
// Explicit Linux runner for the Windows x64 ABI machine code. Not Windows evidence.
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <string.h>
#include <stdatomic.h>
#include <sys/mman.h>
#include <unistd.h>
#include <time.h>
#include <pthread.h>
#include <sys/wait.h>
#include <errno.h>
#define MS __attribute__((ms_abi))
typedef uint32_t (MS *send_fn)(uint32_t,void*,int32_t);
typedef uint64_t (MS *clock_fn)(void);
static _Atomic uint64_t calls,errors,now;
static _Thread_local uint32_t expect_n;
static _Thread_local void *expect_p;
static _Thread_local int32_t expect_cb;
static _Atomic int blocked,entered,released;
static int checks;
static void check(int v,const char *s){if(!v){fprintf(stderr,"FAIL %s\n",s);exit(1);}checks++;}
static uint64_t tick(void){struct timespec t;clock_gettime(CLOCK_MONOTONIC,&t);return (uint64_t)t.tv_sec*1000+t.tv_nsec/1000000;}
static MS uint32_t original(uint32_t n,void*p,int32_t cb){if(n!=expect_n||p!=expect_p||cb!=expect_cb)atomic_fetch_add(&errors,1);atomic_fetch_add(&calls,1);return 77;}
static MS uint64_t fake_clock(void){if(atomic_load(&blocked)){atomic_store(&entered,1);uint64_t due=tick()+3000;while(!atomic_load(&released)&&tick()<due)usleep(1000);check(atomic_load(&released),"clock release");} uint64_t result=atomic_load(&now); __asm__ volatile("xor %%rcx,%%rcx; xor %%rdx,%%rdx; xor %%r8,%%r8; xor %%r9,%%r9; xor %%r10,%%r10; xor %%r11,%%r11":::"rcx","rdx","r8","r9","r10","r11","cc");return result;}
static MS uint64_t real_clock(void){return tick();}
static void *make_stub(uint64_t start,uint64_t ttl,clock_fn clock){
 check(ttl>0&&ttl<=60000&&start<=UINT64_MAX-ttl,"deadline validated");
 FILE*f=fopen("stub.bin","rb");check(f!=NULL,"stub exists");uint8_t buf[2048];size_t n=fread(buf,1,sizeof(buf),f);fclose(f);check(n>0&&n<2048,"code budget");
 void*p=mmap(NULL,4096,PROT_READ|PROT_WRITE,MAP_PRIVATE|MAP_ANONYMOUS,-1,0);check(p!=MAP_FAILED,"allocate RW");memcpy(p,buf,n);
 f=fopen("offsets.txt","r");check(f!=NULL,"offsets");char name[64];unsigned off;while(fscanf(f,"%63s %x",name,&off)==2){uint64_t value=0;if(!strcmp(name,"original"))value=(uint64_t)original;else if(!strcmp(name,"clock"))value=(uint64_t)clock;else if(!strcmp(name,"start"))value=start;else if(!strcmp(name,"deadline"))value=start+ttl;else check(0,"symbol");check(off+8<=n,"patch range");memcpy((uint8_t*)p+off,&value,8);}fclose(f);
 check(mprotect(p,4096,PROT_READ|PROT_EXEC)==0,"W^X RX");__builtin___clear_cache(p,(char*)p+4096);return p;
 // Never munmap stub: callers may hold old pointers.
}
static uint32_t invoke(void*fn,uint32_t n,void*p,int32_t cb){expect_n=n;expect_p=p;expect_cb=cb;return ((send_fn)fn)(n,p,cb);}
static void field(void*p,int off,uint64_t v,int size){memcpy((char*)p+off,&v,size);}
static void input(uint8_t*p,int flags){memset(p,0,40);field(p,0,1,4);field(p,8,0xA6,2);field(p,12,flags,4);}
static void policy(void){atomic_store(&now,1000);void*s=make_stub(1000,60000,fake_clock);
 for(int mask=0;mask<2048;mask++){uint8_t p[40];input(p,(mask&32)?3:1);uint32_t n=(mask&1)?2:1;int cb=(mask&2)?39:40;
 if(mask&4)field(p,0,0,4);if(mask&8)field(p,8,0xA7,2);if(mask&16)field(p,10,1,2);if(mask&64)field(p,12,5,4);if(mask&128)field(p,16,1,4);if(mask&256)field(p,24,1,8);if(mask&512)p[4]=0x55;if(mask&1024)p[20]=0x55;
 int match=(mask&479)==0;uint64_t before=atomic_load(&calls);uint32_t r=invoke(s,n,p,cb);if(r!=(match?1u:77u)){fprintf(stderr,"mask=%d got=%u expected=%d\n",mask,r,match?1:77);check(0,"exact suppression policy");}check(atomic_load(&calls)-before==(match?0u:1u),"single forwarding");}
 check(invoke(s,1,NULL,40)==77,"null forwarded");check(invoke(s,0,(void*)1,40)==77,"zero count invalid pointer untouched");check(invoke(s,1,(void*)1,0)==77,"invalid size pointer untouched");uint8_t p[40];input(p,1);
 atomic_store(&now,60999);check(invoke(s,1,p,40)==1,"last active millisecond");atomic_store(&now,61000);check(invoke(s,1,p,40)==77,"deadline exact failopen");
 pid_t invalid=fork();check(invalid>=0,"fork expired pointer probe");if(invalid==0)_exit(invoke(s,1,(void*)1,40)==77?0:1);int invalid_status;waitpid(invalid,&invalid_status,0);check(WIFEXITED(invalid_status)&&WEXITSTATUS(invalid_status)==0,"expired state must bypass all INPUT dereferences");
 atomic_store(&now,999);check(invoke(s,1,p,40)==77,"backwards tick failopen");atomic_store(&now,UINT64_MAX);check(invoke(s,1,p,40)==77,"large tick failopen");
 // Mutate each meaningful field bit independently: every nonzero mutation forwards.
 atomic_store(&now,1000);int offsets[]={0,8,10,12,16,24},sizes[]={4,2,2,4,4,8};
 for(int k=0;k<6;k++)for(int b=0;b<sizes[k]*8;b++){input(p,1);p[offsets[k]+b/8]^=1u<<(b%8);uint32_t expected=(k==3&&b==1)?1:77;check(invoke(s,1,p,40)==expected,"field bit mutation");}
 check(atomic_load(&errors)==0,"ABI arguments preserved with volatile clock clobber");puts("POLICY PASS 2048 structural combinations + all meaningful field bits + null/deadline/ABI");}
static _Atomic(uintptr_t)*slot;static void*stub;
static void *pending(void*unused){uint8_t p[40];input(p,1);return (void*)(uintptr_t)invoke((void*)atomic_load(slot),1,p,40);}
static pthread_barrier_t stress_ready,stress_resume;
static void *worker(void*unused){uint8_t p[40];input(p,1);for(int j=0;j<10000;j++){if(j==5000){pthread_barrier_wait(&stress_ready);pthread_barrier_wait(&stress_resume);}uint32_t r=invoke((void*)atomic_load(slot),1,p,40);if(r!=(j<5000?1u:77u))atomic_fetch_add(&errors,1);}return NULL;}
static int swap(uintptr_t value,uintptr_t expected){return atomic_compare_exchange_strong(slot,&expected,value);}
static void concurrency(void){slot=mmap(NULL,4096,PROT_READ|PROT_WRITE,MAP_SHARED|MAP_ANONYMOUS,-1,0);check(slot!=MAP_FAILED,"shared slot");check(atomic_is_lock_free(slot),"native pointer CAS lockfree");atomic_store(slot,(uintptr_t)original);stub=make_stub(1000,60000,fake_clock);atomic_store(&now,1000);
 check(swap((uintptr_t)stub,(uintptr_t)original),"install atomic");check(!swap((uintptr_t)stub,0),"CAS refuses wrong owner");atomic_store(&blocked,1);pthread_t t;pthread_create(&t,NULL,pending,NULL);uint64_t due=tick()+2000;while(!atomic_load(&entered)&&tick()<due)usleep(1000);check(atomic_load(&entered),"pending in clock");check(swap((uintptr_t)original,(uintptr_t)stub),"restore pending");atomic_store(&released,1);void*result;pthread_join(t,&result);check((uintptr_t)result==1,"pending completes safely after restore");atomic_store(&blocked,0);
 uint8_t p[40];input(p,1);check(invoke((void*)atomic_load(slot),1,p,40)==77,"new call original");atomic_store(&now,61000);check(invoke(stub,1,p,40)==77,"cached old stub expires");atomic_store(&now,1000);check(swap((uintptr_t)stub,(uintptr_t)original),"stress install");pthread_barrier_init(&stress_ready,NULL,9);pthread_barrier_init(&stress_resume,NULL,9);pthread_t threads[8];for(int i=0;i<8;i++)pthread_create(&threads[i],NULL,worker,NULL);pthread_barrier_wait(&stress_ready);check(swap((uintptr_t)original,(uintptr_t)stub),"stress restore");pthread_barrier_wait(&stress_resume);for(int i=0;i<8;i++)pthread_join(threads[i],NULL);check(atomic_load(&errors)==0,"80000 concurrent calls valid ABI");puts("CONCURRENCY PASS pending across restoration; 8x10000 calls; retained executable page");}
struct shared { _Atomic(uintptr_t) slot; _Atomic int ready,expired,restored,done;uint64_t start,expiry_tick,restore_tick;};
static void orphan(void){struct shared*q=mmap(NULL,4096,PROT_READ|PROT_WRITE,MAP_SHARED|MAP_ANONYMOUS,-1,0);check(q!=MAP_FAILED,"orphan shared");memset(q,0,4096);
 pid_t controller=fork();check(controller>=0,"fork controller");if(controller==0){pid_t victim=fork();if(victim<0)_exit(10);if(victim>0){for(;;)pause();}
 q->start=tick();void*s=make_stub(q->start,2000,real_clock);atomic_store(&q->slot,(uintptr_t)s);pid_t wd=fork();if(wd<0)_exit(11);if(wd==0){while(tick()<q->start+5000)usleep(1000);uintptr_t expected=(uintptr_t)s;int ok=atomic_compare_exchange_strong(&q->slot,&expected,(uintptr_t)original);q->restore_tick=tick();atomic_store(&q->restored,ok&&atomic_load(&q->slot)==(uintptr_t)original);_exit(ok?0:12);}
 uint8_t p[40];input(p,1);if(invoke((void*)atomic_load(&q->slot),1,p,40)!=1)_exit(13);atomic_store(&q->ready,1);while(tick()<q->start+2100)usleep(1000);
 q->expiry_tick=tick();atomic_store(&q->expired,atomic_load(&q->slot)==(uintptr_t)s&&invoke(s,1,p,40)==77);int status;waitpid(wd,&status,0);atomic_store(&q->done,WIFEXITED(status)&&WEXITSTATUS(status)==0&&invoke((void*)atomic_load(&q->slot),1,p,40)==77);_exit(0);}
 uint64_t ready_due=tick()+3000;while(!atomic_load(&q->ready)&&tick()<ready_due)usleep(1000);check(atomic_load(&q->ready),"victim ready before controller kill");check(kill(controller,SIGKILL)==0,"kill only fixture controller");int status;waitpid(controller,&status,0);check(WIFSIGNALED(status)&&WTERMSIG(status)==SIGKILL,"controller killed abruptly before deadline");uint64_t due=tick()+10000;while(!atomic_load(&q->done)&&tick()<due)usleep(1000);
 check(atomic_load(&q->ready),"orphan installed");check(atomic_load(&q->expired),"orphan independent expiry");check(atomic_load(&q->restored),"separate watchdog restores shared slot");check(atomic_load(&q->done),"orphan final original call");check(q->restore_tick<=q->start+90000,"watchdog <=90sec");printf("ORPHAN PASS controller exited; expiry observed +%lums; independent watchdog CAS +%lums; original readback\n",q->expiry_tick-q->start,q->restore_tick-q->start);
}
int main(void){setbuf(stdout,NULL);puts("PLATFORM Linux; actual generated machine code uses Microsoft x64 ABI; no Windows runtime or mitigation assertion");policy();concurrency();orphan();printf("PASS checks=%d mock_calls=%lu ABI_errors=%lu; no SendInput API or service access\n",checks,atomic_load(&calls),atomic_load(&errors));return 0;}
