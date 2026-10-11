using System;
class MappingTests {
 static int Main(){string a=RemoteMapping.Action("HID_AABBCCDDEEFF_COL02",0x41,true);if(a!="PlayPause"){Console.WriteLine("FAIL: remote OK in target application expected PlayPause, got "+a);return 1;}Console.WriteLine("PASS: remote OK toggles playback");
 ushort[] codes={0x223,0x224,0x42,0x43,0x44,0x45,0x9C,0x9D,0xE2,0,0xFFFF};
 string[] expect={"OpenTargetApplication","Escape","Up","Down","SeekBack","SeekForward","VolumeUp","VolumeDown","Ignore","Ignore","Ignore"};
 for(int i=0;i<codes.Length;i++){a=RemoteMapping.Action("HID_AABBCCDDEEFF_COL02",codes[i],true);if(a!=expect[i]){Console.WriteLine("FAIL: usage "+codes[i].ToString("X4")+" expected "+expect[i]+", got "+a);return 1;}}
 Console.WriteLine("PASS: all captured HID usages mapped");
 foreach(ushort u in codes){if(RemoteMapping.Action("OTHER_KEYBOARD",u,true)!="Ignore")throw new Exception("Other device affected");}
 ushort[] scoped={0x41,0x224,0x42,0x43,0x44,0x45};foreach(ushort u in scoped){if(RemoteMapping.Action("HID_AABBCCDDEEFF_COL02",u,false)!="Ignore")throw new Exception("Player key not scoped to target application");}
 Console.WriteLine("PASS: other devices ignored; player actions scoped to target application; mute untouched");return 0;}
}
