using System;
using System.Diagnostics;
// Only callers that have selected an exact same-session image may pass a process.
public static class PlayerClose {
 public static string Run(Process target){
  if(target==null)return "ALREADY_CLOSED";
  target.Refresh();if(target.HasExited)return "ALREADY_CLOSED";
  int id=target.Id;
  if(target.MainWindowHandle==IntPtr.Zero||!target.CloseMainWindow())throw new InvalidOperationException("player-close-window-unavailable");
  if(!target.WaitForExit(15000))throw new InvalidOperationException("player-close-timeout");
  return "CLOSED pid="+id;
 }
}
