using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace StandaloneController
{
    [Flags]
    public enum PadButton : uint
    {
        None=0, Up=1, Down=2, Left=4, Right=8, Start=16, Back=32,
        LS=64, RS=128, LB=256, RB=512, A=4096, B=8192, X=16384, Y=32768,
        LT=65536, RT=131072
    }
    public struct PadSample
    {
        public bool Connected;
        public int Index;
        public uint Buttons;
        public float LX, LY, RX, RY, LT, RT;
    }
    public sealed class Settings
    {
        public bool Enabled=true, InvertY=false, Hints=true, Rumble=true;
        public float MoveDeadzone=.20f, LookDeadzone=.18f, Trigger=.15f;
        public float LookSpeed=180f, AimScale=.5f, CursorSpeed=850f;
        public int Controller=-1;
        public readonly Dictionary<string, PadButton> Bindings = new Dictionary<string, PadButton>();
        public static readonly string[] Actions = {
            "Jump", "Crouch", "Run", "Reload", "Interaction", "Fire1", "Fire2",
            "Skill", "Throw", "MeleeAttack", "NextWeapon", "ChangeToPreviousWeapon",
            "ChangeWeapon1", "ChangeWeapon2", "ChangeWeapon3", "ChangeWeapon4", "UIInventory", "Escape",
            "UISkillTree", "UIQuest", "UIMap", "Friend", "Guild", "Group", "CashShop", "Achievement", "ShowScore", "UIChatInput"
        };
        public Settings()
        {
            foreach(string a in Actions) Bindings[a]=PadButton.None;
            Bindings["Jump"]=PadButton.A; Bindings["Crouch"]=PadButton.B;
            Bindings["Reload"]=PadButton.X; Bindings["Interaction"]=PadButton.X;
            Bindings["Run"]=PadButton.LS; Bindings["MeleeAttack"]=PadButton.RS;
            Bindings["Skill"]=PadButton.LB; Bindings["Throw"]=PadButton.RB;
            Bindings["Fire1"]=PadButton.RT; Bindings["Fire2"]=PadButton.LT;
            Bindings["NextWeapon"]=PadButton.Y;
            Bindings["ChangeWeapon1"]=PadButton.Up; Bindings["ChangeWeapon2"]=PadButton.Right;
            Bindings["ChangeWeapon3"]=PadButton.Down; Bindings["ChangeWeapon4"]=PadButton.Left;
            Bindings["UIInventory"]=PadButton.Back; Bindings["Escape"]=PadButton.Start;
        }
        public static float Bound(float v,float lo,float hi,float fallback)
        { return float.IsNaN(v)||float.IsInfinity(v)?fallback:Math.Max(lo,Math.Min(hi,v)); }
        public void Validate()
        {
            MoveDeadzone=Bound(MoveDeadzone,.05f,.8f,.20f); LookDeadzone=Bound(LookDeadzone,.05f,.8f,.18f);
            Trigger=Bound(Trigger,.05f,.95f,.15f); LookSpeed=Bound(LookSpeed,30,720,180);
            AimScale=Bound(AimScale,.1f,1,.5f); CursorSpeed=Bound(CursorSpeed,100,2500,850);
            Controller=Math.Max(-1,Math.Min(3,Controller));
        }
        public static Settings Load(string path)
        {
            Settings s=new Settings();
            if(!File.Exists(path)) return s;
            foreach(string line in File.ReadAllLines(path))
            {
                string t=line.Trim(); if(t.StartsWith("#")||t.Length==0) continue;
                int eq=t.IndexOf('='); if(eq<1) continue;
                string k=t.Substring(0,eq).Trim(), v=t.Substring(eq+1).Trim();
                float f; bool b; int n;
                if(k.StartsWith("bind.")) {
                    string a=k.Substring(5);
                    try { PadButton p=(PadButton)Enum.Parse(typeof(PadButton),v,true);
                        if(s.Bindings.ContainsKey(a)&&((uint)p & ~0x3F3FFu)==0) s.Bindings[a]=p;
                    } catch(ArgumentException) {} catch(OverflowException) {}
                }
                else if(bool.TryParse(v,out b)) {
                    if(k=="Enabled") s.Enabled=b; else if(k=="InvertY") s.InvertY=b;
                    else if(k=="Hints") s.Hints=b; else if(k=="Rumble") s.Rumble=b;
                }
                else if(k=="Controller"&&int.TryParse(v,out n)) s.Controller=n;
                else if(float.TryParse(v,NumberStyles.Float,CultureInfo.InvariantCulture,out f)) {
                    switch(k) { case "MoveDeadzone":s.MoveDeadzone=f;break; case "LookDeadzone":s.LookDeadzone=f;break;
                        case "Trigger":s.Trigger=f;break; case "LookSpeed":s.LookSpeed=f;break;
                        case "AimScale":s.AimScale=f;break; case "CursorSpeed":s.CursorSpeed=f;break; }
                }
            }
            s.Validate(); return s;
        }
        public void Save(string path)
        {
            Validate(); List<string> lines=new List<string>();
            lines.Add("# Standalone Xbox controller mod. F10 or hold Back+Start: settings. Reload from settings.");
            lines.Add("Enabled="+Enabled); lines.Add("Controller="+Controller);
            lines.Add("MoveDeadzone="+MoveDeadzone.ToString(CultureInfo.InvariantCulture));
            lines.Add("LookDeadzone="+LookDeadzone.ToString(CultureInfo.InvariantCulture));
            lines.Add("Trigger="+Trigger.ToString(CultureInfo.InvariantCulture));
            lines.Add("LookSpeed="+LookSpeed.ToString(CultureInfo.InvariantCulture));
            lines.Add("AimScale="+AimScale.ToString(CultureInfo.InvariantCulture));
            lines.Add("CursorSpeed="+CursorSpeed.ToString(CultureInfo.InvariantCulture));
            lines.Add("InvertY="+InvertY); lines.Add("Hints="+Hints); lines.Add("Rumble="+Rumble);
            foreach(string a in Actions) lines.Add("bind."+a+"="+Bindings[a]);
            string temp=path+".tmp"; File.WriteAllLines(temp,lines.ToArray());
            if(File.Exists(path)) File.Replace(temp,path,path+".previous"); else File.Move(temp,path);
        }
    }
    public sealed class PadCore
    {
        public Settings Config;
        public PadSample Raw;
        public uint Held, Down, Up;
        public float LX,LY,RX,RY;
        public bool Ready;
        bool awaitingNeutral=true;
        int lastIndex=-1;
        public PadCore(Settings config) { Config=config; }
        public static void Stick(float x,float y,float deadzone,out float ox,out float oy)
        {
            x=Settings.Bound(x,-1,1,0); y=Settings.Bound(y,-1,1,0);
            float length=(float)Math.Sqrt(x*x+y*y);
            if(length<=deadzone) { ox=oy=0; return; }
            float scale=(Math.Min(1,length)-deadzone)/(1-deadzone)/length;
            ox=x*scale; oy=y*scale;
        }
        public void Reset()
        { Held=Down=Up=0; LX=LY=RX=RY=0; Ready=false; awaitingNeutral=true; }
        public void Step(PadSample sample,bool focused,bool allowDisabled=false)
        {
            Raw=sample;
            if(!sample.Connected||!focused||(!Config.Enabled&&!allowDisabled)||sample.Index!=lastIndex) {
                Reset(); lastIndex=sample.Connected?sample.Index:-1;
                if(!sample.Connected||!focused||(!Config.Enabled&&!allowDisabled)) return;
            }
            float lx,ly,rx,ry;
            Stick(sample.LX,sample.LY,Config.MoveDeadzone,out lx,out ly);
            Stick(sample.RX,sample.RY,Config.LookDeadzone,out rx,out ry);
            uint buttons=sample.Buttons;
            if(sample.LT>Config.Trigger) buttons|=(uint)PadButton.LT;
            if(sample.RT>Config.Trigger) buttons|=(uint)PadButton.RT;
            if(awaitingNeutral) {
                if(buttons!=0||lx!=0||ly!=0||rx!=0||ry!=0) return;
                awaitingNeutral=false;
            }
            uint previous=Held; Held=buttons; Down=buttons & ~previous; Up=previous & ~buttons;
            LX=lx; LY=ly; RX=rx; RY=ry; Ready=true;
        }
        public bool Button(PadButton b,int edge)
        {
            uint mask=(uint)b; if(mask==0||!Ready) return false;
            if(edge==0) return (Held&mask)==mask;
            if(edge==1) return (Held&mask)==mask && (Down&mask)!=0;
            return ((Held|Up)&mask)==mask && (Up&mask)!=0;
        }
        public bool Action(string name,int edge)
        {
            PadButton b; return Config.Bindings.TryGetValue(name,out b)&&Button(b,edge);
        }
    }
}
