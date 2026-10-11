using System;using System.IO;using System.Collections.Generic;
class WindowFake:IActionAdapter,IScopedKeyAdapter {
 public List<string> Calls=new List<string>();public bool Activate(string p){throw new Exception("unexpected activation");}public void Launch(string p,string a,string d){throw new Exception("unexpected launch");}public void Open(string p){throw new Exception("unexpected open");}public void Key(string k){Calls.Add("system:"+k);}public void KeyInApplication(string k,string p){Calls.Add("window:"+k+":"+p);}public void Mouse(string a,int n){throw new Exception("unexpected mouse");}
}
class ScopedWindowTests {
 static void Check(bool b,string s){if(!b)throw new Exception("FAIL "+s);Console.WriteLine("PASS "+s);}
 static void Main(){var f=new WindowFake();string cfg=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"window-fixture.json"),target=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Example.exe");
 using(var e=new ActionExecutor(f,s=>{throw new Exception(s);})){
 var a=new ButtonConfig{Action="Key",Key="Space",Scope="Application",ScopeApplicationPath="Example.exe"};
 Check(e.TryExecute(a,cfg,()=>true)&&e.WaitForIdle(2000),"scoped worker completes");Check(f.Calls.Count==1&&f.Calls[0]=="window:Space:"+target,"saved scoped key delivered only to its resolved application");
 f.Calls.Clear();a.Action="Sequence";a.Steps=new List<ActionSpec>{new ActionSpec{Action="Key",Key="Left"}};
 Check(e.TryExecute(a,cfg,()=>true)&&e.WaitForIdle(2000),"scoped sequence completes");Check(f.Calls.Count==1&&f.Calls[0]=="window:Left:"+target,"sequence child inherits parent scope target");
 f.Calls.Clear();a.Action="Key";a.Steps=null;a.Scope="Global";a.Key="VolumeUp";
 Check(e.TryExecute(a,cfg)&&e.WaitForIdle(2000),"global volume completes");Check(f.Calls.Count==1&&f.Calls[0]=="system:VolumeUp","working global volume transport unchanged");
 f.Calls.Clear();a.Scope="Application";a.Key="Space";
 Check(e.TryExecute(a,cfg,()=>false)&&e.WaitForIdle(2000),"changed scope cancels worker");Check(f.Calls.Count==0,"scope refusal sends no window or system input");
 }
 }
}
