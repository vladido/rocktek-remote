public class HoldRepeater {
 ushort Held;long Next,Started;bool Enabled;int Interval;
 public void Update(ushort usage,long now){Update(usage,now,usage==0x42||usage==0x43||usage==0x44||usage==0x45||usage==0x9C||usage==0x9D,350,150);}
 public void Update(ushort usage,long now,bool enabled,int delay,int interval){if(usage==Held)return;Held=usage;Enabled=enabled;Started=now;Next=now+delay;Interval=interval;}
 public ushort Due(long now){if(Held==0||!Enabled||now-Started>=30000||now<Next)return 0;Next=now+Interval;return Held;}
}
