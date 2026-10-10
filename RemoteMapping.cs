public static class RemoteMapping {
 public static string Action(string device, ushort usage, bool murglarActive) {
  if(!DeviceFilter.IsTarget(device))return "Ignore";
  if(usage==0x223)return "OpenTargetApplication";
  if(usage==0x9C)return "VolumeUp";
  if(usage==0x9D)return "VolumeDown";
  if(!murglarActive)return "Ignore";
  switch(usage){case 0x41:return "PlayPause";case 0x224:return "Escape";case 0x42:return "Up";case 0x43:return "Down";case 0x44:return "SeekBack";case 0x45:return "SeekForward";default:return "Ignore";}
 }
}
