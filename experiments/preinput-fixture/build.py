from pathlib import Path
import subprocess, hashlib, json
p=Path(__file__).resolve().parent
subprocess.run(['as','--64','-o',str(p/'stub.o'),str(p/'stub.S')],check=True)
# Link local RIP references; raw object extraction would leave relocations unpatched.
subprocess.run(['ld','-nostdlib','-Ttext=0','-e','stub','-o',str(p/'stub.elf'),str(p/'stub.o')],check=True)
subprocess.run(['objcopy','-O','binary','-j','.text',str(p/'stub.elf'),str(p/'stub.bin')],check=True)
nm=subprocess.check_output(['nm','-n',str(p/'stub.elf')],text=True)
symbols={x[2]:x[0] for line in nm.splitlines() if len(x:=line.split())==3}
(p/'offsets.txt').write_text(''.join(f'{name} {symbols[name]}\n' for name in ['original','clock','start','deadline']))
dis=subprocess.check_output(['objdump','-d','-Mintel',str(p/'stub.elf')],text=True)
(p/'stub-disassembly.txt').write_text(dis)
print(json.dumps({'bytes':len((p/'stub.bin').read_bytes()),'sha256':hashlib.sha256((p/'stub.bin').read_bytes()).hexdigest(),'offsets':(p/'offsets.txt').read_text()}))
