/* Explicit opt-in bounded diagnostic. No service state changes, privilege changes,
 * unload, or remote IAT writes. Loaded helper is retained until target exit. */
#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif
#include <windows.h>
#include <tlhelp32.h>
#include <aclapi.h>
#include <bcrypt.h>
#include <stdio.h>
#include <stdint.h>
#include "runner-frozen.h"
__declspec(noreturn) static void die(const char*s){fprintf(stderr,"REFUSE %s winerr=%lu\n",s,GetLastError());ExitProcess(1);}
static HANDLE locks[32];static int lockcount;
static BYTE system_sid[SECURITY_MAX_SID_SIZE],admin_sid[SECURITY_MAX_SID_SIZE],user_buffer[512];static PSID user_sid;
static int trusted(PSID sid,int fixture){return EqualSid(sid,system_sid)||EqualSid(sid,admin_sid)||(fixture&&EqualSid(sid,user_sid));}
static void secure_path(const WCHAR*path,int fixture,int directory){
 DWORD attr=GetFileAttributesW(path);if(attr==INVALID_FILE_ATTRIBUTES||(attr&FILE_ATTRIBUTE_REPARSE_POINT))die("missing/reparse path");
 HANDLE h=CreateFileW(path,GENERIC_READ|READ_CONTROL,FILE_SHARE_READ|(directory==2?FILE_SHARE_WRITE:0),NULL,OPEN_EXISTING,directory?FILE_FLAG_BACKUP_SEMANTICS:FILE_ATTRIBUTE_NORMAL,NULL);if(h==INVALID_HANDLE_VALUE)die("lock task file/directory");
 if(lockcount>=32)die("path depth");locks[lockcount++]=h;
 PSID owner=NULL;PACL acl=NULL;PSECURITY_DESCRIPTOR sd=NULL;
 if(GetSecurityInfo(h,SE_FILE_OBJECT,OWNER_SECURITY_INFORMATION|DACL_SECURITY_INFORMATION,&owner,NULL,&acl,NULL,&sd)!=ERROR_SUCCESS||!acl||!trusted(owner,fixture))die("untrusted owner/null ACL");
 for(DWORD i=0;i<acl->AceCount;i++){void*a=NULL;if(!GetAce(acl,i,&a))die("ACE read");ACE_HEADER*header=a;
  if(header->AceType==ACCESS_ALLOWED_ACE_TYPE){ACCESS_ALLOWED_ACE*x=a;DWORD mask=x->Mask;DWORD writes=GENERIC_ALL|GENERIC_WRITE|FILE_WRITE_DATA|FILE_APPEND_DATA|FILE_WRITE_EA|FILE_WRITE_ATTRIBUTES|DELETE|WRITE_DAC|WRITE_OWNER|FILE_DELETE_CHILD;
   if((mask&writes)&&!trusted((PSID)&x->SidStart,fixture))die("untrusted writable ACL");
  }else if(header->AceType!=ACCESS_DENIED_ACE_TYPE&&header->AceType!=SYSTEM_AUDIT_ACE_TYPE)die("unsupported ACL type");
 }LocalFree(sd);
}
static void digest(const WCHAR*path,const char*expected){
 HANDLE h=CreateFileW(path,GENERIC_READ,FILE_SHARE_READ,NULL,OPEN_EXISTING,0,NULL);if(h==INVALID_HANDLE_VALUE)die("hash open");
 BCRYPT_ALG_HANDLE alg=NULL;BCRYPT_HASH_HANDLE hash=NULL;BYTE data[65536],out[32];DWORD got;char hex[65];
 if(BCryptOpenAlgorithmProvider(&alg,BCRYPT_SHA256_ALGORITHM,NULL,0)<0||BCryptCreateHash(alg,&hash,NULL,0,NULL,0,0)<0)die("hash init");
 for(;;){if(!ReadFile(h,data,sizeof(data),&got,NULL))die("hash read");if(!got)break;if(BCryptHashData(hash,data,got,0)<0)die("hash update");}
 if(BCryptFinishHash(hash,out,32,0)<0)die("hash finish");for(int i=0;i<32;i++)sprintf(hex+i*2,"%02x",out[i]);
 BCryptDestroyHash(hash);BCryptCloseAlgorithmProvider(alg,0);CloseHandle(h);if(strcmp(hex,expected))die("frozen SHA256 mismatch");
}
static uintptr_t module(DWORD pid,const WCHAR*path,int rejecthelper){
 HANDLE s=CreateToolhelp32Snapshot(TH32CS_SNAPMODULE|TH32CS_SNAPMODULE32,pid);if(s==INVALID_HANDLE_VALUE)die("module enumeration");MODULEENTRY32W e={0};e.dwSize=sizeof(e);uintptr_t result=0;
 if(!Module32FirstW(s,&e))die("module first");do{if(rejecthelper&&!_wcsicmp(e.szModule,L"timedhook.dll"))die("helper already loaded: one-shot refused");if(!_wcsicmp(e.szExePath,path))result=(uintptr_t)e.modBaseAddr;}while(Module32NextW(s,&e));CloseHandle(s);return result;
}
static DWORD service_pid(void){SC_HANDLE scm=OpenSCManagerW(NULL,NULL,SC_MANAGER_CONNECT|SC_MANAGER_ENUMERATE_SERVICE);if(!scm)die("SCM open");SC_HANDLE svc=OpenServiceW(scm,L"hidserv",SERVICE_QUERY_STATUS);SERVICE_STATUS_PROCESS p={0};DWORD needed;if(!svc||!QueryServiceStatusEx(svc,SC_STATUS_PROCESS_INFO,(BYTE*)&p,sizeof(p),&needed)||p.dwCurrentState!=SERVICE_RUNNING||!p.dwProcessId)die("hidserv must be Running");
 DWORD bytes=0,count=0,resume=0;EnumServicesStatusExW(scm,SC_ENUM_PROCESS_INFO,SERVICE_WIN32,SERVICE_STATE_ALL,NULL,0,&bytes,&count,&resume,NULL);BYTE*b=HeapAlloc(GetProcessHeap(),0,bytes);resume=0;if(!b||!EnumServicesStatusExW(scm,SC_ENUM_PROCESS_INFO,SERVICE_WIN32,SERVICE_STATE_ALL,b,bytes,&bytes,&count,&resume,NULL))die("SCM enum");DWORD matches=0;ENUM_SERVICE_STATUS_PROCESSW*es=(void*)b;for(DWORD i=0;i<count;i++)if(es[i].ServiceStatusProcess.dwProcessId==p.dwProcessId){matches++;if(_wcsicmp(es[i].lpServiceName,L"hidserv"))die("shared service process");}if(matches!=1)die("not sole hidserv");HeapFree(GetProcessHeap(),0,b);CloseServiceHandle(svc);CloseServiceHandle(scm);return p.dwProcessId;}
static DWORD thread(HANDLE process,uintptr_t address,void*arg){HANDLE t=CreateRemoteThread(process,NULL,0,(LPTHREAD_START_ROUTINE)address,arg,0,NULL);if(!t)die("thread creation; no unload");DWORD w=WaitForSingleObject(t,10000);if(w!=WAIT_OBJECT_0){fprintf(stderr,"INDETERMINATE thread in flight: module and argument retained, no unload/free/retry\n");ExitProcess(2);}DWORD status;if(!GetExitCodeThread(t,&status))die("thread status");CloseHandle(t);return status;}
static int watch(const WCHAR *app) {
 WCHAR own[MAX_PATH];GetModuleFileNameW(NULL,own,MAX_PATH);
 DWORD previous=0,last_owner=0;
 for(;;){
  DWORD pid=0;HANDLE snap=CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS,0);PROCESSENTRY32W e={0};e.dwSize=sizeof(e);
  if(snap!=INVALID_HANDLE_VALUE && Process32FirstW(snap,&e))do{
   if(_wcsicmp(e.szExeFile,L"RockTekRemote.exe"))continue;
   HANDLE p=OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION|SYNCHRONIZE,FALSE,e.th32ProcessID);
   WCHAR path[MAX_PATH],marker[100];DWORD n=MAX_PATH;
   if(p && QueryFullProcessImageNameW(p,0,path,&n) && !_wcsicmp(path,app)){
    swprintf_s(marker,100,L"Global\\RockTekRemote.BackOwner.%lu",e.th32ProcessID);
    HANDLE m=OpenMutexW(SYNCHRONIZE,FALSE,marker);
    if(m){pid=e.th32ProcessID;CloseHandle(m);}
   }
   if(p)CloseHandle(p);
   if(pid)break;
  }while(Process32NextW(snap,&e));
  if(snap!=INVALID_HANDLE_VALUE)CloseHandle(snap);
  if(pid || last_owner){
   WCHAR cmd[1024];if(pid)swprintf_s(cmd,1024,L"\"%s\" --enable %lu",own,pid);else swprintf_s(cmd,1024,L"\"%s\" --stop",own);
   STARTUPINFOW si={0};PROCESS_INFORMATION pi={0};si.cb=sizeof(si);
   if(CreateProcessW(own,cmd,NULL,NULL,FALSE,CREATE_NO_WINDOW,NULL,NULL,&si,&pi)){
    CloseHandle(pi.hThread);
    if(WaitForSingleObject(pi.hProcess,15000)==WAIT_TIMEOUT){CloseHandle(pi.hProcess);return 2;}
    DWORD code;GetExitCodeProcess(pi.hProcess,&code);CloseHandle(pi.hProcess);
    if(code!=previous){printf("watch owner=%lu status=%lu\n",pid,code);fflush(stdout);previous=code;}
   }
  }
  last_owner=pid;Sleep(5000);
 }
}
int wmain(int argc,WCHAR**argv){
 if(argc==3 && !wcscmp(argv[1],L"--watch"))return watch(argv[2]);
 int fixture=argc==3&&!wcscmp(argv[1],L"--fixture");
 int owned_fixture=argc==4&&!wcscmp(argv[1],L"--owned-fixture");fixture=fixture||owned_fixture;
 int owned=argc==3&&!wcscmp(argv[1],L"--enable"),stop=argc==2&&!wcscmp(argv[1],L"--stop");
 int live=(argc==2&&!wcscmp(argv[1],L"--test-hidserv"))||owned||stop;
 if(!fixture&&!live){fprintf(stderr,"usage: service-runner --watch APP | --enable OWNER_PID | --stop | --fixture PID | --owned-fixture PID OWNER_PID | --test-hidserv\n");return 1;}
 DWORD sidlen=sizeof(system_sid);CreateWellKnownSid(WinLocalSystemSid,NULL,system_sid,&sidlen);sidlen=sizeof(admin_sid);CreateWellKnownSid(WinBuiltinAdministratorsSid,NULL,admin_sid,&sidlen);HANDLE tok;DWORD n;if(!OpenProcessToken(GetCurrentProcess(),TOKEN_QUERY,&tok)||!GetTokenInformation(tok,TokenUser,user_buffer,sizeof(user_buffer),&n))die("current user SID");user_sid=((TOKEN_USER*)user_buffer)->User.Sid;CloseHandle(tok);
 WCHAR own[MAX_PATH],dir[MAX_PATH],dll[MAX_PATH],target[MAX_PATH],provider[MAX_PATH],log[MAX_PATH];DWORD chars=GetModuleFileNameW(NULL,own,MAX_PATH);if(!chars||chars>=MAX_PATH)die("own path");wcscpy_s(dir,MAX_PATH,own);WCHAR*slash=wcsrchr(dir,L'\\');if(!slash)die("own absolute path");*slash=0;
 if(live&&_wcsicmp(dir,L"C:\\ProgramData\\RockTekRemote\\NativeBack"))die("service runner requires admin/SYSTEM-only fixed task directory");
 secure_path(dir,fixture,1);secure_path(own,fixture,0);swprintf_s(dll,MAX_PATH,L"%s\\timedhook.dll",dir);secure_path(dll,fixture,0);digest(dll,HOOK_HASH);
 swprintf_s(log,MAX_PATH,L"%s\\runner-phase.log",dir);if(GetFileAttributesW(log)!=INVALID_FILE_ATTRIBUTES){secure_path(log,fixture,2);CloseHandle(locks[--lockcount]);}
 DWORD pid;if(fixture){WCHAR*end=NULL;unsigned long parsed=wcstoul(argv[2],&end,10);if(!*argv[2]||*end||!parsed)die("PID format");pid=(DWORD)parsed;}else pid=service_pid();
 HANDLE p=OpenProcess(PROCESS_QUERY_INFORMATION|PROCESS_VM_READ|PROCESS_VM_WRITE|PROCESS_VM_OPERATION|PROCESS_CREATE_THREAD|SYNCHRONIZE,FALSE,pid);if(!p)die("target open");BOOL wow;if(!IsWow64Process(p,&wow)||wow)die("not x64");n=MAX_PATH;if(!QueryFullProcessImageNameW(p,0,target,&n))die("target image");
 if(fixture){WCHAR expected[MAX_PATH];swprintf_s(expected,MAX_PATH,L"%s\\loader-fixture.exe",dir);if(_wcsicmp(target,expected))die("fixture exact path");secure_path(target,1,0);digest(target,FIXTURE_HASH);swprintf_s(provider,MAX_PATH,L"%s\\provider.dll",dir);secure_path(provider,1,0);digest(provider,PROVIDER_HASH);if(!module(pid,provider,0))die("synthetic provider identity");}
 else{WCHAR expected[MAX_PATH];GetSystemDirectoryW(expected,MAX_PATH);wcscat_s(expected,MAX_PATH,L"\\svchost.exe");if(_wcsicmp(target,expected)||service_pid()!=pid)die("SCM process identity changed");BYTE ub[512];if(!OpenProcessToken(p,TOKEN_QUERY,&tok)||!GetTokenInformation(tok,TokenUser,ub,sizeof(ub),&n)||!EqualSid(((TOKEN_USER*)ub)->User.Sid,system_sid))die("target not SYSTEM");CloseHandle(tok);WCHAR hd[MAX_PATH];GetSystemDirectoryW(hd,MAX_PATH);wcscat_s(hd,MAX_PATH,L"\\hidserv.dll");digest(hd,"92d5e4adc8e8128b17c74999df3ad882db5f10c4f27ea31fab55377b1303027a");if(!module(pid,hd,0))die("hidserv image absent");}
 uintptr_t existing=module(pid,dll,fixture&&!owned_fixture);
 if(stop && !existing){CloseHandle(p);return 0;}
 /* Resolve the module containing the true forwarded LoadLibraryW export, not a
  * kernel32-relative guess or a locally ASLR'd address. Match remote full path. */
 FARPROC loader=GetProcAddress(GetModuleHandleW(L"kernel32.dll"),"LoadLibraryW");HMODULE owner;if(!loader||!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,(LPCWSTR)loader,&owner))die("true loader owner");WCHAR ownerpath[MAX_PATH];if(!GetModuleFileNameW(owner,ownerpath,MAX_PATH))die("loader owner path");uintptr_t remoteowner=module(pid,ownerpath,0);if(!remoteowner)die("remote true loader module absent");
 HMODULE local=LoadLibraryExW(dll,NULL,DONT_RESOLVE_DLL_REFERENCES);if(!local)die("export map");FARPROC entry=GetProcAddress(local,stop?"StopOwned":owned_fixture?"RunOwnedFixture":owned?"RunOwnedHidserv":fixture?"RunFixture":"RunHidserv");if(!entry)die("entry export");uintptr_t rva=(uintptr_t)entry-(uintptr_t)local;FreeLibrary(local);
 if(!existing){
 SIZE_T size=(wcslen(dll)+1)*sizeof(WCHAR),written;void*arg=VirtualAllocEx(p,NULL,size,MEM_COMMIT|MEM_RESERVE,PAGE_READWRITE);if(!arg||!WriteProcessMemory(p,arg,dll,size,&written)||written!=size)die("loader argument");
 DWORD loaded=thread(p,remoteowner+((uintptr_t)loader-(uintptr_t)owner),arg);(void)loaded;VirtualFreeEx(p,arg,0,MEM_RELEASE);
 }
 uintptr_t base=module(pid,dll,0);if(!base)die("full-width module enumeration failed; no retry/unload");printf("LOADED pid=%lu base=%p loaderOwner=%ls; retained until process exit\n",pid,(void*)base,ownerpath);fflush(stdout);
 DWORD owner_pid=(owned||owned_fixture)?wcstoul(argv[owned_fixture?3:2],NULL,10):0;
 DWORD result=thread(p,base+rva,(void*)(uintptr_t)owner_pid);printf("START status=%lu; worker owns restore; helper remains resident\n",result);CloseHandle(p);for(int i=0;i<lockcount;i++)CloseHandle(locks[i]);return result?1:0;
}
