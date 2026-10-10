using System;
using System.Text.RegularExpressions;
/// <summary>Match only the explicitly selected Bluetooth address; never capture arbitrary keyboards.</summary>
public static class DeviceFilter {
 public static string Target {get{string value=Environment.GetEnvironmentVariable("ROCKTEK_REMOTE_DEVICE")??"";return Regex.IsMatch(value,"\\A[0-9A-Fa-f]{12}\\z")?value:"";}}
 public static bool IsTarget(string device){string target=Target;return target.Length==12&&device!=null&&device.IndexOf(target,StringComparison.OrdinalIgnoreCase)>=0;}
}
