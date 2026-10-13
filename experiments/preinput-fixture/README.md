# Bounded pre-input fixture

Experimental test harness, not a RockTekRemote fix or an installer. It never attaches to HIDServ and does not generate real keyboard input. The mock input callback returns 77.

The x64 stub suppresses only the tested BrowserBack INPUT shape during a bounded interval and forwards other calls. The tests exercise policy, ABI, concurrency, expiry and controller death using an isolated fixture pointer slot.

## Build and test

On Linux with Python, GNU binutils and GCC:

```sh
python3 build.py
gcc -O2 -Wall -Wextra -Wno-unused-parameter -Wno-misleading-indentation -pthread -o portable_fixture portable_fixture.c
./portable_fixture
```

On Windows, generate `stub.bin` and `offsets.txt` first, then compile `Fixture.cs` with the x64 .NET Framework compiler using `/unsafe /platform:x64`. Run the resulting executable from the folder containing those two generated files. Use an isolated writable folder; disposable subprocesses and temporary report files are created there.

## Limitations

A fixture shared-slot restoration is not restoration of a live service IAT. Live CFG/protection, target-side atomic restoration, unwind registration and exact export identity are not verified by this harness. BrowserBack at this boundary carries no physical-device identity. Executable pages remain until fixture process exit to avoid use-after-free during restoration.

Do not attach these fixtures to a system service or package them as a runtime fix. The RockTekRemote runtime and installer remain unchanged in this branch.
