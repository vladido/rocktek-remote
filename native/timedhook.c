/* x64 native diagnostic, process-local only. No remote memory/process APIs.
 * Build with clang-cl/MSVC /guard:cf, link /guard:cf /CETCOMPAT /DYNAMICBASE /NXCOMPAT.
 * Module pinned for process lifetime: never FreeLibrary, never reclaim hook code.
 * Stable INPUT memory during calls is required; this is the Windows SendInput contract.
 */
#include <windows.h>
#include <string.h>
#include <stdint.h>
#include <bcrypt.h>
#pragma comment(lib,"bcrypt.lib")
static int exact_hidserv(HMODULE module) {
 static const BYTE expected[32]={0x92,0xd5,0xe4,0xad,0xc8,0xe8,0x12,0x8b,0x17,0xc7,0x49,0x99,0xdf,0x3a,0xd8,0x82,0xdb,0x5f,0x10,0xc4,0xf2,0x7e,0xa3,0x1f,0xab,0x55,0x37,0x7b,0x13,0x03,0x02,0x7a};
 static const BYTE call[7]={0x48,0xff,0x15,0xce,0x6d,0x00,0x00};
 WCHAR path[MAX_PATH],system[MAX_PATH];
 DWORD chars=GetModuleFileNameW(module,path,MAX_PATH);
 UINT len=GetSystemDirectoryW(system,MAX_PATH);
 if(!chars || chars>=MAX_PATH || !len || len+13>=MAX_PATH) return 0;
 wcscat_s(system,MAX_PATH,L"\\hidserv.dll");
 if(_wcsicmp(path,system)) return 0;
 HANDLE file=CreateFileW(path,GENERIC_READ,FILE_SHARE_READ,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
 if(file==INVALID_HANDLE_VALUE) return 0;
 LARGE_INTEGER size; BYTE *bytes=NULL; BCRYPT_ALG_HANDLE alg=NULL; BCRYPT_HASH_HANDLE hash=NULL;
 BYTE digest[32];DWORD got=0;int valid=0;
 if(!GetFileSizeEx(file,&size) || size.QuadPart<=0 || size.QuadPart>1024*1024) goto done;
 bytes=HeapAlloc(GetProcessHeap(),0,(SIZE_T)size.QuadPart);if(!bytes)goto done;
 if(!ReadFile(file,bytes,(DWORD)size.QuadPart,&got,NULL) || got!=size.QuadPart)goto done;
 if(BCryptOpenAlgorithmProvider(&alg,BCRYPT_SHA256_ALGORITHM,NULL,0)<0 ||
    BCryptCreateHash(alg,&hash,NULL,0,NULL,0,0)<0 || BCryptHashData(hash,bytes,got,0)<0 ||
    BCryptFinishHash(hash,digest,32,0)<0 || memcmp(digest,expected,32))goto done;
 IMAGE_DOS_HEADER *d=(void*)bytes;
 if(d->e_magic!=IMAGE_DOS_SIGNATURE || d->e_lfanew<=0 || (SIZE_T)d->e_lfanew+sizeof(IMAGE_NT_HEADERS64)>got)goto done;
 IMAGE_NT_HEADERS64 *disk=(void*)(bytes+d->e_lfanew),*live=(void*)((BYTE*)module+d->e_lfanew);
 IMAGE_NT_HEADERS64 expected_header=*disk;
 expected_header.OptionalHeader.ImageBase=(ULONGLONG)(uintptr_t)module;
 if(disk->Signature!=IMAGE_NT_SIGNATURE || memcmp(live,&expected_header,sizeof(*disk)) || disk->OptionalHeader.SizeOfImage!=0x13000 || memcmp((BYTE*)module+0x4523,call,7))goto done;
 IMAGE_SECTION_HEADER *sections=IMAGE_FIRST_SECTION(disk);
 if((BYTE*)(sections+disk->FileHeader.NumberOfSections)>bytes+got)goto done;
 /* The loader updates function-override thunks; ordinary code and disk hash remain checked. */
 for(WORD i=0;i<disk->FileHeader.NumberOfSections;i++) if((sections[i].Characteristics&IMAGE_SCN_MEM_EXECUTE) && memcmp(sections[i].Name,"fothk",5)) {
  DWORD n=sections[i].SizeOfRawData;
  if(n>sections[i].Misc.VirtualSize)n=sections[i].Misc.VirtualSize;
  if(sections[i].PointerToRawData>got || n>got-sections[i].PointerToRawData || sections[i].VirtualAddress>0x13000 || n>0x13000-sections[i].VirtualAddress || memcmp((BYTE*)module+sections[i].VirtualAddress,bytes+sections[i].PointerToRawData,n))goto done;
 }
 valid=1;
 done: if(hash)BCryptDestroyHash(hash);if(alg)BCryptCloseAlgorithmProvider(alg,0);if(bytes)HeapFree(GetProcessHeap(),0,bytes);CloseHandle(file);return valid;
}
static UINT (WINAPI *original)(UINT,LPINPUT,int);
static void **iat;
static void *hook_pointer;
static DWORD protection;
static ULONGLONG deadline;
static volatile LONG phase; /* 0 unused; 1 active; 2 restored; 3 restore retry; 4 conflict */
static volatile LONG claimed;
static HANDLE gate;
static HANDLE owner_process;
static DWORD owner_pid;
static int owner_alive(void) {return !owner_process || WaitForSingleObject(owner_process,0)==WAIT_TIMEOUT;}
static volatile LONG suppressed;
static volatile LONG calls, back_seen, back_scan, back_flags, back_time, back_extra;
static HANDLE log_file=INVALID_HANDLE_VALUE;
/* Fixed task-owned path; works under SCM SYSTEM without per-user environment. */
static int open_log(int fixture) {
 (void)fixture;
 if(log_file!=INVALID_HANDLE_VALUE)return 1;
 HMODULE self;WCHAR path[MAX_PATH];
 if(!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,(LPCWSTR)&open_log,&self) || !GetModuleFileNameW(self,path,MAX_PATH))return 0;
 WCHAR *slash=wcsrchr(path,L'\\');if(!slash)return 0;wcscpy_s(slash+1,MAX_PATH-(slash+1-path),L"runner-phase.log");
 log_file=CreateFileW(path,FILE_APPEND_DATA,FILE_SHARE_READ,NULL,OPEN_ALWAYS,FILE_ATTRIBUTE_NORMAL,NULL);
 return log_file!=INVALID_HANDLE_VALUE;
}
static void log_phase(const char *label,DWORD status) {
 char line[320];DWORD written;
 int n=wsprintfA(line,"%s pid=%lu phase=%ld suppressed=%ld status=%lu tick=%I64u calls=%ld backSeen=%ld scan=%ld flags=%ld nonzeroTime=%ld nonzeroExtra=%ld\r\n",label,GetCurrentProcessId(),InterlockedCompareExchange(&phase,0,0),InterlockedCompareExchange(&suppressed,0,0),status,GetTickCount64(),calls,back_seen,back_scan,back_flags,back_time,back_extra);
 if(log_file!=INVALID_HANDLE_VALUE) {WriteFile(log_file,line,(DWORD)n,&written,NULL);FlushFileBuffers(log_file);}
}
static volatile LONG cancel_wait, page_dirty;
static UINT WINAPI intercept(UINT n,LPINPUT p,int size) {
 InterlockedIncrement(&calls);
 if(owner_alive() && InterlockedCompareExchange(&phase,1,1)==1 && GetTickCount64()<deadline && n==1 && p && size==sizeof(INPUT) && p->type==INPUT_KEYBOARD && p->ki.wVk==0xA6) {
  InterlockedIncrement(&back_seen);
  InterlockedExchange(&back_scan,p->ki.wScan);
  InterlockedExchange(&back_flags,(LONG)p->ki.dwFlags);
  InterlockedExchange(&back_time,p->ki.time!=0);
  InterlockedExchange(&back_extra,p->ki.dwExtraInfo!=0);
 }
 if (owner_alive() && GetTickCount64()<deadline && InterlockedCompareExchange(&phase,1,1)==1 &&
     n==1 && p && size==sizeof(INPUT) && p->type==INPUT_KEYBOARD &&
     p->ki.wVk==0xA6 && p->ki.wScan==0 &&
     (p->ki.dwFlags==1 || p->ki.dwFlags==3) && p->ki.time==0 && p->ki.dwExtraInfo==0) {InterlockedIncrement(&suppressed);return 1;}
 return original(n,p,size);
}
static BOOLEAN WINAPI intercept_station(HANDLE server,DWORD session,DWORD object,ULONG_PTR window,DWORD message,ULONG_PTR key,LONG_PTR data,DWORD *result) {
 typedef BOOLEAN (WINAPI *STATION)(HANDLE,DWORD,DWORD,ULONG_PTR,DWORD,ULONG_PTR,LONG_PTR,DWORD*);
 InterlockedIncrement(&calls);
 if(object==5 && (message==0x100 || message==0x101) && key==0xA6) {
  InterlockedIncrement(&back_seen);
  if(owner_alive() && GetTickCount64()<deadline && InterlockedCompareExchange(&phase,1,1)==1) {InterlockedIncrement(&suppressed);if(result)*result=0;return TRUE;}
 }
 return ((STATION)(void*)original)(server,session,object,window,message,key,data,result);
}
static DWORD WINAPI restore_worker(void *unused) {
 (void)unused;
 WaitForSingleObject(gate,INFINITE);
 CloseHandle(gate); gate=NULL;
 for (;;) {
  ULONGLONG now=GetTickCount64();
  if(now>=deadline || !owner_alive() || InterlockedCompareExchange(&cancel_wait,0,0)) break;
  if(owner_process){WaitForSingleObject(owner_process,250);continue;}
  ULONGLONG left=deadline-now; Sleep((DWORD)(left>25?25:left));
 }
 InterlockedExchange(&phase,3); /* fail open BEFORE protection/CAS, also if restore fails */
 for (;;) {
  MEMORY_BASIC_INFORMATION m; DWORD prior,ignored;
  if(*(void *volatile*)iat==(void*)original && !InterlockedCompareExchange(&page_dirty,0,0)) {InterlockedExchange(&phase,2);goto finished;}
  if(!VirtualQuery(iat,&m,sizeof(m)) || (m.Protect!=protection && !(InterlockedCompareExchange(&page_dirty,0,0) && m.Protect==PAGE_READWRITE))) { Sleep(25);continue; }
  if(!VirtualProtect(iat,sizeof(void*),PAGE_READWRITE,&prior)) {Sleep(25);continue;}
  if(InterlockedCompareExchange(&page_dirty,0,0))prior=protection;
  void *seen=InterlockedCompareExchangePointer(iat,(void*)original,hook_pointer);
  if(!VirtualProtect(iat,sizeof(void*),prior,&ignored)) {
   /* Restoration of page protection is independently retried; hook already fail open. */
   while(!VirtualProtect(iat,sizeof(void*),prior,&ignored)) Sleep(25);
  }
  InterlockedExchange(&phase,seen==hook_pointer || seen==(void*)original?2:4);
  finished:
  log_phase("worker-finished",0);
  if(owner_process){CloseHandle(owner_process);owner_process=NULL;owner_pid=0;}
  InterlockedExchange(&claimed,0);
  return 0;
 }
}
static int inimage(DWORD rva,SIZE_T len,DWORD total) { return rva<total && len<=total-rva; }
static void **findslot(HMODULE module,const char *import_dll) {
 BYTE *b=(BYTE*)module;
 IMAGE_DOS_HEADER *d=(void*)b;
 if(d->e_magic!=IMAGE_DOS_SIGNATURE || d->e_lfanew<=0 || d->e_lfanew>0x1000) return NULL;
 IMAGE_NT_HEADERS64 *n=(void*)(b+d->e_lfanew);
 if(n->Signature!=IMAGE_NT_SIGNATURE || n->FileHeader.Machine!=IMAGE_FILE_MACHINE_AMD64 || n->OptionalHeader.Magic!=IMAGE_NT_OPTIONAL_HDR64_MAGIC) return NULL;
 DWORD total=n->OptionalHeader.SizeOfImage;
 IMAGE_DATA_DIRECTORY dir=n->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
 if(!inimage(dir.VirtualAddress,dir.Size,total) || dir.Size<sizeof(IMAGE_IMPORT_DESCRIPTOR)) return NULL;
 IMAGE_IMPORT_DESCRIPTOR *imp=(void*)(b+dir.VirtualAddress);
 SIZE_T descriptors=dir.Size/sizeof(*imp);
 for(SIZE_T j=0;j<descriptors && imp[j].Name;j++) {
  if(!inimage(imp[j].Name,1,total) || !memchr(b+imp[j].Name,0,total-imp[j].Name)) return NULL;
  if(_stricmp((char*)(b+imp[j].Name),import_dll)) continue;
  DWORD names=imp[j].OriginalFirstThunk,slots=imp[j].FirstThunk;
  if(!names || !slots) return NULL;
  for(SIZE_T k=0;inimage(names+k*8,8,total) && inimage(slots+k*8,8,total);k++) {
   ULONGLONG val=*(ULONGLONG*)(b+names+k*8);
   if(!val) break;
   if(val&IMAGE_ORDINAL_FLAG64) continue;
   if(val>MAXDWORD || !inimage((DWORD)val,sizeof(WORD)+1,total) || !memchr(b+(DWORD)val+2,0,total-(DWORD)val-2)) return NULL;
   if(!strcmp((char*)(b+(DWORD)val+2),"SendInput")) return (void**)(b+slots+k*8);
  }
 }
 return NULL;
}
static DWORD start(HMODULE module,DWORD ttl,const char *dll,int fixture) {
 if(!module || ttl==0 || (ttl>60000 && !owner_process)) return ERROR_INVALID_PARAMETER;
 void **found=findslot(module,dll);
 if(!found || ((uintptr_t)found&7)) return ERROR_INVALID_ADDRESS;
 HMODULE provider=GetModuleHandleA(dll);
 void *real=provider?(void*)GetProcAddress(provider,"SendInput"):NULL;
 if(!real || *(void *volatile*)found!=real) return ERROR_INVALID_FUNCTION;
 if(!fixture && (BYTE*)found-(BYTE*)module!=0xB2F8) return ERROR_REVISION_MISMATCH;
 hook_pointer=(void*)intercept;
 if(!fixture && *(DWORD*)((BYTE*)module+0xE8B0)!=0) {
  HMODULE station=GetModuleHandleW(L"winsta.dll");
  real=station?(void*)GetProcAddress(station,"WinStationSendWindowMessage"):NULL;
  found=(void**)((BYTE*)module+0xE8D8);
  if(!real || *(void *volatile*)found!=real) return ERROR_INVALID_FUNCTION;
  hook_pointer=(void*)intercept_station;
 }
 PROCESS_MITIGATION_DYNAMIC_CODE_POLICY dynamic={0};
 PROCESS_MITIGATION_CONTROL_FLOW_GUARD_POLICY cfg={0};
 if(!GetProcessMitigationPolicy(GetCurrentProcess(),ProcessDynamicCodePolicy,&dynamic,sizeof(dynamic)) ||
    !GetProcessMitigationPolicy(GetCurrentProcess(),ProcessControlFlowGuardPolicy,&cfg,sizeof(cfg))) return GetLastError();
 /* No dynamic code or mitigation changes; ordinary signed/CFG-compatible image loading only. */
 MEMORY_BASIC_INFORMATION m;
 if(!VirtualQuery(found,&m,sizeof(m)) || m.State!=MEM_COMMIT || m.Type!=MEM_IMAGE || m.AllocationBase!=module || (m.Protect!=PAGE_READONLY && m.Protect!=PAGE_READWRITE && m.Protect!=PAGE_WRITECOPY)) return ERROR_INVALID_ADDRESS;
 if(InterlockedCompareExchange(&claimed,1,0)!=0) return ERROR_ALREADY_EXISTS;
 HMODULE self;
 if(!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_PIN,(LPCWSTR)&intercept,&self)) return GetLastError();
 iat=found;original=(void*)real;protection=m.Protect;deadline=owner_process?~(ULONGLONG)0:GetTickCount64()+ttl;
 InterlockedExchange(&cancel_wait,0);InterlockedExchange(&page_dirty,0);
 gate=CreateEventW(NULL,FALSE,FALSE,NULL); if(!gate) return GetLastError();
 HANDLE worker=CreateThread(NULL,0,restore_worker,NULL,0,NULL);
 if(!worker) {DWORD e=GetLastError();CloseHandle(gate);gate=NULL;return e;}
 CloseHandle(worker);
 DWORD prior,ignored;
 if(!VirtualProtect(iat,8,PAGE_READWRITE,&prior)) {DWORD e=GetLastError();InterlockedExchange(&cancel_wait,1);SetEvent(gate);return e;}
 if(prior!=protection) {while(!VirtualProtect(iat,8,prior,&ignored))Sleep(25);InterlockedExchange(&cancel_wait,1);SetEvent(gate);return ERROR_INVALID_STATE;}
 InterlockedExchange(&phase,1);
 void *seen=InterlockedCompareExchangePointer(iat,hook_pointer,(void*)original);
 BOOL protected_ok=VirtualProtect(iat,8,prior,&ignored);
 DWORD result=seen==(void*)original?ERROR_SUCCESS:ERROR_BUSY;
 if(!protected_ok) {DWORD e=GetLastError();InterlockedExchange(&page_dirty,1);InterlockedExchange(&phase,3);InterlockedExchange(&cancel_wait,1);result=e;}
 if(seen!=(void*)original) {InterlockedExchange(&phase,3);InterlockedExchange(&cancel_wait,1);}
 SetEvent(gate);
 return result;
}
__declspec(dllexport) DWORD WINAPI BeginFixture(HMODULE module,DWORD ttl) {return start(module,ttl,"provider.dll",1);}
/* Exact OS image validation is retained for the owned service lifecycle. */
__declspec(dllexport) DWORD WINAPI BeginHidserv(HMODULE module,DWORD ttl) {
 if(!module || !exact_hidserv(module)) return ERROR_REVISION_MISMATCH;
 return start(module,ttl,"USER32.dll",0);
}
__declspec(dllexport) DWORD WINAPI RunHidserv(void *unused) {
 if(unused || InterlockedCompareExchange(&claimed,0,0))return ERROR_INVALID_PARAMETER;
 if(!open_log(0))return GetLastError();
 DWORD result=BeginHidserv(GetModuleHandleW(L"hidserv.dll"),60000);
 log_phase("RunHidserv",result);return result;
}
__declspec(dllexport) DWORD WINAPI RunFixture(void *unused) {
 if(unused || InterlockedCompareExchange(&claimed,0,0))return ERROR_INVALID_PARAMETER;
 if(!open_log(1))return GetLastError();
 DWORD result=BeginFixture(GetModuleHandleW(NULL),1500);
 log_phase("RunFixture",result);return result;
}
/* The mapper process handle is the lease: standby does not expire it. */
static DWORD run_owned(void *pid,int fixture) {
 DWORD requested=(DWORD)(uintptr_t)pid;
 if(!requested)return ERROR_INVALID_PARAMETER;
 if(InterlockedCompareExchange(&claimed,0,0))return phase==1 && owner_pid==requested?0:ERROR_BUSY;
 HANDLE owner=OpenProcess(SYNCHRONIZE|PROCESS_QUERY_LIMITED_INFORMATION,FALSE,requested);
 if(!owner || WaitForSingleObject(owner,0)!=WAIT_TIMEOUT){if(owner)CloseHandle(owner);return ERROR_INVALID_HANDLE;}
 WCHAR path[MAX_PATH];DWORD length=MAX_PATH;
 if(!QueryFullProcessImageNameW(owner,0,path,&length)){CloseHandle(owner);return ERROR_INVALID_HANDLE;}
 WCHAR *name=wcsrchr(path,L'\\');
 if(!fixture && (!name || _wcsicmp(name+1,L"RockTekRemote.exe"))){CloseHandle(owner);return ERROR_INVALID_NAME;}
 if(!fixture && !exact_hidserv(GetModuleHandleW(L"hidserv.dll"))){CloseHandle(owner);return ERROR_REVISION_MISMATCH;}
 if(!open_log(fixture)){CloseHandle(owner);return GetLastError();}
 owner_process=owner;owner_pid=requested;
 DWORD result=start(GetModuleHandleW(fixture?NULL:L"hidserv.dll"),60000,fixture?"provider.dll":"USER32.dll",fixture);
 if(result && !claimed){CloseHandle(owner_process);owner_process=NULL;owner_pid=0;}
 log_phase("owned-start",result);return result;
}
__declspec(dllexport) DWORD WINAPI RunOwnedHidserv(void *pid){return run_owned(pid,0);}
__declspec(dllexport) DWORD WINAPI RunOwnedFixture(void *pid){return run_owned(pid,1);}
__declspec(dllexport) DWORD WINAPI StopOwned(void *unused){
 (void)unused;InterlockedExchange(&cancel_wait,1);
 for(int i=0;i<40 && (phase==1 || phase==3);i++)Sleep(25);
 return phase==1 || phase==3?ERROR_BUSY:0;
}
static LONG station_forwarded;
static BOOLEAN WINAPI station_mock(HANDLE s,DWORD session,DWORD object,ULONG_PTR window,DWORD msg,ULONG_PTR key,LONG_PTR data,DWORD *r) {
 (void)s;(void)session;(void)object;(void)window;(void)msg;(void)key;(void)data;
 station_forwarded++;if(r)*r=77;return TRUE;
}
__declspec(dllexport) DWORD WINAPI TestStationPolicy(void) {
 original=(void*)station_mock;deadline=GetTickCount64()+10000;phase=1;
 for(DWORD k=0;k<256;k++)for(DWORD up=0;up<2;up++) {
  DWORD r=999;LONG before=station_forwarded;
  if(!intercept_station(NULL,1,5,123,0x100+up,k,0,&r))return 1;
  if(k==0xA6) {if(r!=0 || station_forwarded!=before)return 2;}
  else if(r!=77 || station_forwarded!=before+1)return 3;
 }
 DWORD r=0;phase=2;
 if(!intercept_station(NULL,1,5,123,0x100,0xA6,0,&r)||r!=77)return 4;
 return 0;
}
__declspec(dllexport) DWORD WINAPI HookState(void) {return (DWORD)InterlockedCompareExchange(&phase,0,0);}
BOOL WINAPI DllMain(HINSTANCE h,DWORD reason,LPVOID reserved) {(void)reserved;if(reason==DLL_PROCESS_ATTACH)DisableThreadLibraryCalls(h);return TRUE;}
