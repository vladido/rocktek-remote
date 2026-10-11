using System;
using System.Collections.Generic;
class InputDiagnosticsTests {
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static int Main(){try{
 long now=0;var lines=new List<string>();Func<string,bool> target=s=>s=="target";
 var disabled=new InputDiagnostics(0,target,()=>now,lines.Add);disabled.Target("target","RAW");disabled.Lifecycle("START");Check(lines.Count==0,"default must be silent");
 var d=new InputDiagnostics(2,target,()=>now,lines.Add);d.Target("ordinary-keyboard","Keyboard 0041");Check(lines.Count==0,"ordinary keyboard never logged");
 d.Target("target","Consumer 0223");Check(lines.Count==1,"target intake recorded");now=1999;d.Lifecycle("POWER");Check(lines.Count==2,"within duration");now=2000;d.Target("target","Consumer 0223");d.Lifecycle("POWER");Check(lines.Count==2,"expiry stops target and lifecycle logs");
 now=0;lines.Clear();var bounded=new InputDiagnostics(600,target,()=>now,lines.Add);for(int i=0;i<1000;i++)bounded.Target("target","RAW");Check(lines.Count==512,"bounded line count");
 Check(InputDiagnostics.Duration(new string[0])==0,"opt-in only");Check(InputDiagnostics.Duration(new[]{"--input-diagnostics-seconds","600"})==600,"maximum duration accepted");foreach(string value in new[]{"0","601","-1","bad"}){bool threw=false;try{InputDiagnostics.Duration(new[]{"--input-diagnostics-seconds",value});}catch(ArgumentException){threw=true;}Check(threw,"invalid diagnostic duration "+value);}
 bool missing=false;try{InputDiagnostics.Duration(new[]{"--input-diagnostics-seconds"});}catch(ArgumentException){missing=true;}Check(missing,"missing duration rejected");Console.WriteLine("InputDiagnosticsTests PASS: opt-in, target filtering, expiry, budget, arguments");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
