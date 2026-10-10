using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
// WorkingArea and Bounds are read on the same DPI-aware UI thread. Never mix
// virtualized logical coordinates with physical desktop/screenshot coordinates.
public static class WindowLayout {
 [DllImport("user32.dll")]static extern bool SetProcessDpiAwarenessContext(IntPtr value);
 [DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
 [DllImport("shcore.dll")]static extern int SetProcessDpiAwareness(int value);
 [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr window);
 public static string DpiMode {get;private set;}
 public static void InitializeDpi(){
  // Must run before Application.EnableVisualStyles / any HWND in the EXE.
  try{bool process=SetProcessDpiAwarenessContext(new IntPtr(-4));IntPtr prior=SetThreadDpiAwarenessContext(new IntPtr(-4));if(process||prior!=IntPtr.Zero){DpiMode="PerMonitorV2";return;}}catch(EntryPointNotFoundException){}catch(DllNotFoundException){}
  try{if(SetProcessDpiAwareness(2)==0){DpiMode="PerMonitor";return;}}catch(EntryPointNotFoundException){}catch(DllNotFoundException){}
  SystemFallback();
 }
 static void SystemFallback(){try{DpiMode=SetProcessDPIAware()?"SystemAware fallback":"Existing DPI context fallback";}catch(EntryPointNotFoundException){DpiMode="Unaware fallback";}
 }
 public static Rectangle Fit(Rectangle work,Size preferred,int margin){
  int mx=Math.Min(Math.Max(0,margin),Math.Max(0,work.Width/4)),my=Math.Min(Math.Max(0,margin),Math.Max(0,work.Height/4));
  int width=Math.Max(1,Math.Min(preferred.Width,Math.Max(1,work.Width-2*mx))),height=Math.Max(1,Math.Min(preferred.Height,Math.Max(1,work.Height-2*my)));
  return new Rectangle(work.Left+(work.Width-width)/2,work.Top+(work.Height-height)/2,width,height);
 }
 public static void ScaleAndFit(Form form,Form owner){
  form.AutoScaleMode=AutoScaleMode.None;float factor=1;
  try{uint dpi=GetDpiForWindow(owner==null?form.Handle:owner.Handle);if(dpi>0)factor=dpi/96f;}catch(EntryPointNotFoundException){using(var g=form.CreateGraphics())factor=g.DpiX/96f;}
  Size logical=form.Size;if(factor!=1)form.Scale(new SizeF(factor,factor));
  var work=Screen.FromHandle(owner==null?form.Handle:owner.Handle).WorkingArea;int margin=(int)(12*factor);
  var bounds=Fit(work,new Size((int)(logical.Width*factor),(int)(logical.Height*factor)),margin);
  form.MinimumSize=new Size(Math.Min(bounds.Width,(int)(640*factor)),Math.Min(bounds.Height,(int)(460*factor)));
  form.MaximumSize=work.Size;form.StartPosition=FormStartPosition.Manual;form.Bounds=bounds;
 }
 public static float ScaleFor(Control control){var form=control.FindForm();if(form==null||!form.IsHandleCreated)return 1;try{return GetDpiForWindow(form.Handle)/96f;}catch(EntryPointNotFoundException){using(var g=form.CreateGraphics())return g.DpiX/96f;}}
 // Shared EXE/file picker geometry in current HWND units. Layout runs again
 // after manual parent scaling, so static and dynamically rebuilt rows agree.
 public static Control PathPicker(TextBox value,EventHandler click){
  var panel=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};
  var browse=new Button{Text=UiText.Get("Обзор…"),AutoSize=false,MinimumSize=Size.Empty,FlatStyle=FlatStyle.Flat,Margin=Padding.Empty};browse.Click+=click;
  value.Dock=DockStyle.None;value.Margin=Padding.Empty;panel.Controls.Add(value);panel.Controls.Add(browse);
  panel.Layout+=(s,e)=>{float scale=ScaleFor(panel);int width=Math.Min((int)(88*scale),panel.ClientSize.Width),height=Math.Min((int)(30*scale),panel.ClientSize.Height),gap=(int)(4*scale);browse.Bounds=new Rectangle(Math.Max(0,panel.ClientSize.Width-width),Math.Max(0,(panel.ClientSize.Height-height)/2),width,height);value.Bounds=new Rectangle(0,Math.Max(0,(panel.ClientSize.Height-value.PreferredHeight)/2),Math.Max(0,panel.ClientSize.Width-width-gap),value.PreferredHeight);};
  return panel;
 }
 public static void Clamp(Form form){var work=Screen.FromHandle(form.Handle).WorkingArea;form.MinimumSize=new Size(Math.Min(form.MinimumSize.Width,work.Width),Math.Min(form.MinimumSize.Height,work.Height));form.MaximumSize=work.Size;if(!work.Contains(form.Bounds))form.Bounds=Fit(work,form.Size,8);}
}
