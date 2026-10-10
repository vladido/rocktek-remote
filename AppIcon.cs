using System;
using System.Drawing;
using System.Reflection;
public static class AppIcon {
 // Every owner receives its own cloned Icon; no lifetime depends on the stream.
 public static Icon Create(){using(var stream=typeof(AppIcon).Assembly.GetManifestResourceStream("app-icon.ico")){if(stream==null)throw new InvalidOperationException(UiText.Get("Ресурс значка не найден."));using(var icon=new Icon(stream,new Size(32,32)))return (Icon)icon.Clone();}}
}
