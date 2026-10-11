using System;
// Explicit, short-lived diagnostics: never records an unfiltered keyboard event.
public sealed class InputDiagnostics {
 readonly Func<string,bool> target;readonly Func<long> clock;readonly Action<string> write;readonly long deadline;readonly bool enabled;int lines;
 public InputDiagnostics(int seconds,Func<string,bool> isTarget,Func<long> elapsed,Action<string> output){if(seconds<0||seconds>600)throw new ArgumentException("Diagnostic duration must be 1..600 seconds");target=isTarget;clock=elapsed;write=output;enabled=seconds>0;deadline=elapsed()+seconds*1000L;}
 public bool Active {get{return enabled&&clock()<deadline&&lines<512;}}
 public void Target(string device,string phase){if(!Active||!target(device))return;Lifecycle("TARGET "+phase);}
 public void Lifecycle(string phase){if(!Active)return;lines++;write("INPUT_DIAG "+phase);}
 public static int Duration(string[] args){int index=Array.IndexOf(args,"--input-diagnostics-seconds");if(index<0)return 0;int seconds;if(index+1>=args.Length||!Int32.TryParse(args[index+1],out seconds)||seconds<1||seconds>600)throw new ArgumentException("--input-diagnostics-seconds requires 1..600");return seconds;}
}
