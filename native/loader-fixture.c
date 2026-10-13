/* Only synthetic provider.dll input; remote loader fixture. */
#include <windows.h>
#include <stdio.h>
#include <string.h>
__declspec(dllimport) UINT WINAPI SendInput(UINT,LPINPUT,int);
static void check(int ok,const char *s){if(!ok){printf("FAIL %s error=%lu\n",s,GetLastError());ExitProcess(1);}}
static void **slot(void){BYTE*b=(BYTE*)GetModuleHandleW(NULL);IMAGE_NT_HEADERS64*n=(void*)(b+((IMAGE_DOS_HEADER*)b)->e_lfanew);IMAGE_IMPORT_DESCRIPTOR*d=(void*)(b+n->OptionalHeader.DataDirectory[1].VirtualAddress);for(;d->Name;d++)if(!_stricmp((char*)(b+d->Name),"provider.dll")){IMAGE_THUNK_DATA64*a=(void*)(b+d->OriginalFirstThunk),*i=(void*)(b+d->FirstThunk);for(;a->u1.AddressOfData;a++,i++)if(!IMAGE_SNAP_BY_ORDINAL64(a->u1.Ordinal)&&!strcmp((char*)((IMAGE_IMPORT_BY_NAME*)(b+a->u1.AddressOfData))->Name,"SendInput"))return(void**)&i->u1.Function;}return NULL;}
int main(int argc,char**argv){
 (void)argv;void**iat=slot();check(iat!=NULL,"slot");void*original=*iat;MEMORY_BASIC_INFORMATION before,after;check(VirtualQuery(iat,&before,sizeof(before)),"protection");
 INPUT in={0};in.type=INPUT_KEYBOARD;in.ki.wVk=0xA6;in.ki.dwFlags=1;check(SendInput(1,&in,sizeof(in))==77,"baseline");
 PROCESS_MITIGATION_CONTROL_FLOW_GUARD_POLICY cfg={0};check(GetProcessMitigationPolicy(GetCurrentProcess(),ProcessControlFlowGuardPolicy,&cfg,sizeof(cfg)),"cfg query");check(cfg.EnableControlFlowGuard,"CFG enabled");printf("READY pid=%lu CFG=%u\n",GetCurrentProcessId(),cfg.EnableControlFlowGuard);fflush(stdout);
 if(argc>1){check(LoadLibraryW(L"timedhook.dll")!=NULL,"preload");Sleep(4000);return 0;}
 ULONGLONG until=GetTickCount64()+15000;HMODULE h=NULL;DWORD(WINAPI*state)(void)=NULL;
 while(GetTickCount64()<until){h=GetModuleHandleW(L"timedhook.dll");if(h)state=(void*)GetProcAddress(h,"HookState");if(state&&state()==1)break;Sleep(5);}check(state&&state()==1,"remote publication observed");
 check(*iat!=original,"private slot changed");UINT(WINAPI*retained)(UINT,LPINPUT,int)=(void*)*iat;
 check(SendInput(1,&in,sizeof(in))==1,"exact suppressed");in.ki.dwFlags=3;check(SendInput(1,&in,sizeof(in))==1,"exact up suppressed");in.ki.wVk=0xA7;check(SendInput(1,&in,sizeof(in))==77,"other forwarded");
 check(VirtualQuery(iat,&after,sizeof(after))&&after.Protect==before.Protect,"publication protection");
 while(state()!=2&&GetTickCount64()<until)Sleep(5);check(state()==2,"autonomous restore");check(*iat==original,"exact pointer restored");check(VirtualQuery(iat,&after,sizeof(after))&&after.Protect==before.Protect,"restore protection");check(retained(1,(LPINPUT)1,sizeof(INPUT))==77,"expired stale pointer safe");in.ki.wVk=0xA6;check(SendInput(1,&in,sizeof(in))==77,"post restore forward");printf("PASS SAME remote loader CFG private-IAT expiry original/protection/stale pointer\n");return 0;
}
