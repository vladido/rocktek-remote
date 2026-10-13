.intel_syntax noprefix
.text
.global stub, original, clock, start, deadline
stub:
 .byte 0xf3,0x0f,0x1e,0xfa
 sub rsp, 72
 mov QWORD PTR [rsp+32], rcx
 mov QWORD PTR [rsp+40], rdx
 mov QWORD PTR [rsp+48], r8
 cmp ecx, 1
 jne forward
 cmp r8d, 40
 jne forward
 test rdx, rdx
 je forward
 call QWORD PTR [rip+clock]
 cmp rax, QWORD PTR [rip+start]
 jb forward
 cmp rax, QWORD PTR [rip+deadline]
 jae forward
 mov rdx, QWORD PTR [rsp+40]
 cmp DWORD PTR [rdx], 1
 jne forward
 cmp WORD PTR [rdx+8], 0xA6
 jne forward
 cmp WORD PTR [rdx+10], 0
 jne forward
 mov eax, DWORD PTR [rdx+12]
 cmp eax, 1
 je flags_ok
 cmp eax, 3
 jne forward
flags_ok:
 cmp DWORD PTR [rdx+16], 0
 jne forward
 cmp QWORD PTR [rdx+24], 0
 jne forward
 mov eax, 1
 add rsp, 72
 ret
forward:
 mov rcx, QWORD PTR [rsp+32]
 mov rdx, QWORD PTR [rsp+40]
 mov r8, QWORD PTR [rsp+48]
 add rsp, 72
 jmp QWORD PTR [rip+original]
.balign 8
original: .quad 0
clock: .quad 0
start: .quad 0
deadline: .quad 0
