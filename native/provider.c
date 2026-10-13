#define SendInput WindowsSendInput
#include <windows.h>
#undef SendInput
/* Synthetic forwarding endpoint: no input injected into the real desktop. */
__declspec(dllexport) UINT WINAPI SendInput(UINT count,LPINPUT inputs,int bytes) {
 (void)count;(void)inputs;(void)bytes; return 77;
}
BOOL WINAPI DllMain(HINSTANCE h,DWORD why,LPVOID reserved) {
 (void)h;(void)why;(void)reserved;return TRUE;
}
