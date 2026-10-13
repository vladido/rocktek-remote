/* Task-owned native controller-death test, independent of PowerShell policies. */
#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
static void check(int ok,const char *message) {if(!ok){fprintf(stderr,"FAIL %s winerror=%lu\n",message,GetLastError());ExitProcess(1);}}
static char directory[MAX_PATH];
static void path(char *out,const char *name) {snprintf(out,MAX_PATH,"%s%s",directory,name);}
static DWORD readpid(const char *name) {char p[MAX_PATH];path(p,name);FILE *f=fopen(p,"r");DWORD pid=0;if(f){fscanf(f,"%lu",&pid);fclose(f);}return pid;}
static void writepid(const char *name,DWORD pid) {char p[MAX_PATH];path(p,name);FILE *f=fopen(p,"w");check(f!=NULL,"write ready PID");fprintf(f,"%lu",pid);fclose(f);}
static PROCESS_INFORMATION spawn(const char *exe,const char *args,HANDLE output) {
 char file[MAX_PATH],command[1024];path(file,exe);snprintf(command,sizeof(command),"\"%s\" %s",file,args);
 STARTUPINFOA startup={0};PROCESS_INFORMATION info={0};startup.cb=sizeof(startup);
 if(output){startup.dwFlags=STARTF_USESTDHANDLES;startup.hStdOutput=output;startup.hStdError=output;startup.hStdInput=GetStdHandle(STD_INPUT_HANDLE);}
 check(CreateProcessA(file,command,NULL,NULL,output!=NULL,0,NULL,directory,&startup,&info),"spawn task-owned executable");CloseHandle(info.hThread);return info;
}
int main(int argc,char **argv) {
 DWORD ttl=argc>2?strtoul(argv[2],NULL,10):1500;check(ttl>=1500 && ttl<=60000,"test TTL range");
 check(GetModuleFileNameA(NULL,directory,MAX_PATH)<MAX_PATH,"own path");char *slash=strrchr(directory,'\\');check(slash!=NULL,"own directory");slash[1]=0;
 SetCurrentDirectoryA(directory);
 if(argc>1 && !strcmp(argv[1],"child-controller")) {
  char logfile[MAX_PATH],args[80];path(logfile,"victim.out");SECURITY_ATTRIBUTES sa={sizeof(sa),NULL,TRUE};
  HANDLE output=CreateFileA(logfile,GENERIC_WRITE,FILE_SHARE_READ,&sa,CREATE_ALWAYS,FILE_ATTRIBUTE_NORMAL,NULL);check(output!=INVALID_HANDLE_VALUE,"open victim log");
  snprintf(args,sizeof(args),"%lu ready.txt",ttl);PROCESS_INFORMATION victim=spawn("fixture.exe",args,output);CloseHandle(output);writepid("victim.pid",victim.dwProcessId);
  ULONGLONG until=GetTickCount64()+10000;
  while(!readpid("ready.txt")){check(WaitForSingleObject(victim.hProcess,0)==WAIT_TIMEOUT && GetTickCount64()<until,"victim readiness");Sleep(10);}
  writepid("controller.ready",GetCurrentProcessId());
  WaitForSingleObject(victim.hProcess,INFINITE);CloseHandle(victim.hProcess);return 0;
 }
 DeleteFileA("ready.txt");DeleteFileA("controller.ready");DeleteFileA("victim.pid");
 char args[80];snprintf(args,sizeof(args),"child-controller %lu",ttl);PROCESS_INFORMATION controller=spawn("native-test.exe",args,NULL);
 ULONGLONG until=GetTickCount64()+10000;
 while(!readpid("controller.ready")){check(WaitForSingleObject(controller.hProcess,0)==WAIT_TIMEOUT && GetTickCount64()<until,"controller readiness");Sleep(10);}
 DWORD victim_pid=readpid("victim.pid");check(victim_pid!=0,"victim PID");
 HANDLE victim=OpenProcess(SYNCHRONIZE|PROCESS_QUERY_LIMITED_INFORMATION,FALSE,victim_pid);check(victim!=NULL,"open disposable victim for exit observation only");
 char actual[MAX_PATH],expected[MAX_PATH];DWORD len=MAX_PATH;path(expected,"fixture.exe");
 check(QueryFullProcessImageNameA(victim,0,actual,&len) && !_stricmp(actual,expected),"exact task-owned victim image");
 check(readpid("controller.ready")==controller.dwProcessId,"exact controller PID");
 check(TerminateProcess(controller.hProcess,99),"kill controller");check(WaitForSingleObject(controller.hProcess,1000)==WAIT_OBJECT_0,"controller is dead");
 printf("CONTROLLER_KILLED pid=%lu victim=%lu ttl=%lu\n",controller.dwProcessId,victim_pid,ttl);fflush(stdout);CloseHandle(controller.hProcess);
 check(WaitForSingleObject(victim,ttl+10000)==WAIT_OBJECT_0,"victim completed after controller death");DWORD status=0;check(GetExitCodeProcess(victim,&status),"victim exit code");CloseHandle(victim);
 FILE *log=fopen("victim.out","r");check(log!=NULL,"read victim log");char buffer[8192];size_t bytes=fread(buffer,1,sizeof(buffer)-1,log);buffer[bytes]=0;fclose(log);printf("%s",buffer);
 check(status==0 && strstr(buffer,"PASS private-IAT native worker restored") && strstr(buffer,"active=8 restored=8"),"native victim pass evidence");
 printf("PASS controller-death; victim exit=%lu; no external restoration; worker restored ordinary private IAT\n",status);return 0;
}
