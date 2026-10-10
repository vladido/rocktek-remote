using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class FileSafety {
 [StructLayout(LayoutKind.Sequential)] struct TagInfo {public uint Attributes,Tag;}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern SafeFileHandle CreateFile(string p,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandleEx(SafeFileHandle h,int info,out TagInfo data,uint size);
 public static void CheckPath(string path){string full=Path.GetFullPath(path);for(string p=full;!String.IsNullOrEmpty(p);p=Path.GetDirectoryName(p)){if(!File.Exists(p)&&!Directory.Exists(p))continue;var attrs=File.GetAttributes(p);if((attrs&FileAttributes.ReparsePoint)==0)continue;using(var h=CreateFile(p,0,7,IntPtr.Zero,3,0x02200000,IntPtr.Zero)){TagInfo info;if(h.IsInvalid||!GetFileInformationByHandleEx(h,9,out info,8))throw new IOException(UiText.Get("Не удалось проверить путь: ")+p); // OneDrive cloud placeholders are data reparse points, not path redirects.
 if((info.Tag&0x20000000)!=0)throw new IOException(UiText.Get("Перенаправленный путь запрещён: ")+p);}}}
}
