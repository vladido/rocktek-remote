/* Disposable native fixture: linked SendInput comes from provider.dll, never user32. */
#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
__declspec(dllimport) UINT WINAPI SendInput(UINT, LPINPUT, int);
typedef DWORD (WINAPI *BEGIN)(HMODULE,DWORD);
typedef DWORD (WINAPI *STATE)(void);
extern IMAGE_DOS_HEADER __ImageBase;
static void fail(const char *s) { fprintf(stderr,"FAIL %s error=%lu\n",s,GetLastError()); ExitProcess(1); }
static void check(int v,const char *s) { if(!v) fail(s); }
static void *slot(void) {
 BYTE *b=(BYTE*)&__ImageBase; IMAGE_NT_HEADERS64 *n=(void*)(b+((IMAGE_DOS_HEADER*)b)->e_lfanew);
 IMAGE_IMPORT_DESCRIPTOR *d=(void*)(b+n->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT].VirtualAddress);
 for(;d->Name;d++) if(!_stricmp((char*)(b+d->Name),"provider.dll")) {
  IMAGE_THUNK_DATA64 *a=(void*)(b+d->OriginalFirstThunk),*i=(void*)(b+d->FirstThunk);
  for(;a->u1.AddressOfData;a++,i++) if(!IMAGE_SNAP_BY_ORDINAL64(a->u1.Ordinal) && !strcmp((char*)((IMAGE_IMPORT_BY_NAME*)(b+a->u1.AddressOfData))->Name,"SendInput")) return &i->u1.Function;
 }
 return NULL;
}
static HANDLE active_gate,restored_gate;
static volatile LONG active_calls,restored_calls;
static DWORD WINAPI caller(void *unused) {
 (void)unused; INPUT key={0};key.type=INPUT_KEYBOARD;key.ki.wVk=0xA6;key.ki.dwFlags=1;
 WaitForSingleObject(active_gate,INFINITE);
 check(SendInput(1,&key,sizeof(key))==1,"concurrent active");InterlockedIncrement(&active_calls);
 WaitForSingleObject(restored_gate,INFINITE);
 check(SendInput(1,&key,sizeof(key))==77,"concurrent restored");InterlockedIncrement(&restored_calls);return 0;
}
int main(int argc,char **argv) {
 DWORD ttl=argc>1?strtoul(argv[1],NULL,10):1500;
 void **iat=slot(); MEMORY_BASIC_INFORMATION before,after;
 check(iat!=NULL,"private IAT exists"); void *original=*(void *volatile*)iat;
 check(VirtualQuery(iat,&before,sizeof(before))!=0,"query before");
 PROCESS_MITIGATION_CONTROL_FLOW_GUARD_POLICY cfg={0};
 check(GetProcessMitigationPolicy(GetCurrentProcess(),ProcessControlFlowGuardPolicy,&cfg,sizeof(cfg)),"CFG query");
 printf("CFG enabled=%u exportSuppression=%u strict=%u\n",cfg.EnableControlFlowGuard,cfg.EnableExportSuppression,cfg.StrictMode); fflush(stdout);
 PROCESS_MITIGATION_DYNAMIC_CODE_POLICY dynamic={0};PROCESS_MITIGATION_USER_SHADOW_STACK_POLICY shadow={0};PROCESS_MITIGATION_DEP_POLICY dep={0};
 check(GetProcessMitigationPolicy(GetCurrentProcess(),ProcessDynamicCodePolicy,&dynamic,sizeof(dynamic)),"dynamic policy query");
 check(GetProcessMitigationPolicy(GetCurrentProcess(),ProcessUserShadowStackPolicy,&shadow,sizeof(shadow)),"shadow-stack policy query");
 check(GetProcessMitigationPolicy(GetCurrentProcess(),ProcessDEPPolicy,&dep,sizeof(dep)),"DEP policy query");
 printf("MITIGATIONS dynamic=0x%lx shadow=0x%lx DEP=0x%lx sizes=%zu/%zu/%zu\n",dynamic.Flags,shadow.Flags,dep.Flags,sizeof(dynamic),sizeof(shadow),sizeof(dep));
 HMODULE h=LoadLibraryW(L"timedhook.dll"); check(h!=NULL,"load native diagnostic DLL");
 BEGIN begin=(BEGIN)GetProcAddress(h,"BeginFixture"); STATE state=(STATE)GetProcAddress(h,"HookState");
 check(begin && state,"resolve diagnostic exports");
 check(begin((HMODULE)&__ImageBase,0)==ERROR_INVALID_PARAMETER,"TTL zero rejected");
 check(begin((HMODULE)&__ImageBase,60001)==ERROR_INVALID_PARAMETER,"TTL over60s rejected");
 check(*(void *volatile*)iat==original,"reject does not publish");
 BEGIN real_begin=(BEGIN)GetProcAddress(h,"BeginHidserv");check(real_begin!=NULL,"real image validation export");
 check(real_begin((HMODULE)&__ImageBase,ttl)==ERROR_REVISION_MISMATCH,"wrong module rejected without writes");
 INPUT in={0}; in.type=INPUT_KEYBOARD;in.ki.wVk=0xA6;in.ki.dwFlags=1;
 check(SendInput(1,&in,sizeof(in))==77,"baseline exact forwards");
 DWORD started=begin((HMODULE)&__ImageBase,ttl);
 check(started==0,"start hook");
 check(*(void *volatile*)iat!=original,"IAT really replaced");
 UINT (WINAPI *retained_hook)(UINT,LPINPUT,int)=(void*)*iat;
 check(begin((HMODULE)&__ImageBase,ttl)==ERROR_INVALID_FUNCTION,"second publication rejects modified original export");
 DWORD64 unwind_base=0; PRUNTIME_FUNCTION unwind=RtlLookupFunctionEntry((DWORD64)*iat,&unwind_base,NULL);
 check(unwind!=NULL,"hook native unwind entry registered by loader");
 CONTEXT context={0};DWORD64 fake_stack[8]={0x12345678};void *handler=NULL;DWORD64 establisher=0;
 context.ContextFlags=CONTEXT_FULL;context.Rip=(DWORD64)*iat;context.Rsp=(DWORD64)fake_stack;
 RtlVirtualUnwind(UNW_FLAG_NHANDLER,unwind_base,context.Rip,unwind,&context,&handler,&establisher,NULL);
 check(context.Rip==0x12345678 && context.Rsp==(DWORD64)(fake_stack+1),"native virtual unwind at hook entry");
 printf("UNWIND lookup and entry virtual-unwind PASS\n");
 HANDLE threads[8];active_gate=CreateEventW(NULL,TRUE,FALSE,NULL);restored_gate=CreateEventW(NULL,TRUE,FALSE,NULL);
 check(active_gate && restored_gate,"concurrency gates");
 for(int j=0;j<8;j++){threads[j]=CreateThread(NULL,0,caller,NULL,0,NULL);check(threads[j]!=NULL,"create concurrent caller");}
 SetEvent(active_gate);ULONGLONG barrier_deadline=GetTickCount64()+1000;
 while(InterlockedCompareExchange(&active_calls,0,0)<8 && GetTickCount64()<barrier_deadline)Sleep(1);
 check(active_calls==8,"each concurrent worker reached active half");
 check(SendInput(1,&in,sizeof(in))==1,"exact A6 flags1 suppressed");
 in.ki.dwFlags=3;check(SendInput(1,&in,sizeof(in))==1,"exact A6 flags3 suppressed");
 in.ki.dwFlags=0;check(SendInput(1,&in,sizeof(in))==77,"flags0 forwarded");
 in.ki.dwFlags=1;in.ki.wVk=0xA7;check(SendInput(1,&in,sizeof(in))==77,"other VK forwarded");
 in.ki.wVk=0xA6;in.ki.wScan=1;check(SendInput(1,&in,sizeof(in))==77,"scan1 forwarded");
 in.ki.wScan=0;in.ki.time=1;check(SendInput(1,&in,sizeof(in))==77,"time1 forwarded");
 in.ki.time=0;in.ki.dwExtraInfo=1;check(SendInput(1,&in,sizeof(in))==77,"extra1 forwarded");
 in.ki.dwExtraInfo=0;check(SendInput(2,&in,sizeof(in))==77,"count2 forwards without payload read");
 check(SendInput(1,NULL,sizeof(in))==77,"null forwards");
 check(SendInput(1,&in,39)==77,"size mismatch forwards");
 check(VirtualQuery(iat,&after,sizeof(after))!=0 && after.Protect==before.Protect,"protection restored immediately after publication");
 if(argc>2) { FILE *f=fopen(argv[2],"w");check(f!=NULL,"ready file");fprintf(f,"%lu\n",GetCurrentProcessId());fclose(f); }
 printf("READY pid=%lu ttl=%lu slot=%p original=%p hook=%p\n",GetCurrentProcessId(),ttl,iat,original,*iat);fflush(stdout);
 Sleep(ttl+300);
 check(state()==2,"worker restoration completed");
 check(*(void *volatile*)iat==original,"exact original IAT restored");
 check(VirtualQuery(iat,&after,sizeof(after))!=0 && after.Protect==before.Protect,"original page protection restored");
 check(retained_hook(1,(LPINPUT)(uintptr_t)1,sizeof(INPUT))==77,"expired retained pointer forwards before dereference");
 SetEvent(restored_gate);check(WaitForMultipleObjects(8,threads,TRUE,1000)==WAIT_OBJECT_0,"all caller threads completed");
 check(restored_calls==8,"each concurrent worker reached restored half");
 for(int j=0;j<8;j++)CloseHandle(threads[j]);CloseHandle(active_gate);CloseHandle(restored_gate);
 in.ki.dwFlags=1;check(SendInput(1,&in,sizeof(in))==77,"post-expiry exact forwards");
 printf("PASS private-IAT native worker restored; active=%ld restored=%ld; original pointer and protection verified; diagnostic module retained until process exit\n",active_calls,restored_calls);
 return 0;
}
