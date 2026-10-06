using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using ns0;

namespace StandaloneController
{
    public static class ControllerSupport
    {
        static Settings config;
        static PadCore pad;
        static int frame=-1, index=-1, suppressFrame=-1;
        static bool menu=true, lastMenu=true, overlay, keyboard, chordUsed, chordFired;
        static float chordAt=-1, nextScroll, nextNav, rumbleUntil;
        static float lastWallTime=-1,cursorDelta=.016f;
        static int rumbleIndex=-1;
        static string capture, notice="", pulse;
        static int pulseFrame=-1, tab, keyRow, keyCol;
        static bool caps;
        static float nativeYaw=15,nativePitch=15;
        static bool nativeInvert;
        static MovieClipOverlayCameraBehaviour shieldCamera;
        static bool savedMouseEnabled,savedChildrenEnabled;
        static readonly MethodInfo resetFlashInput=typeof(MovieClipOverlayCameraBehaviour).GetMethod("initTouchIputState",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly List<int> releaseNextFrame=new List<int>();
        static string configPath, logPath;
        static readonly string[] keyboardRows={"1234567890-=","qwertyuiop[]","asdfghjkl;'", "zxcvbnm,./\\"};
        static readonly string[] shiftedRows={"!@#$%^&*()_+","QWERTYUIOP{}","ASDFGHJKL:\"", "ZXCVBNM<>?|"};
        static readonly string[] special={"Space","Backspace","Tab","Enter","Shift","Done"};
        static int lastLoggedIndex=-2;
        static float nextError;
        static void Init()
        {
            if(pad!=null) return;
            string folder=Path.GetDirectoryName(typeof(ControllerSupport).Assembly.Location);
            configPath=Path.Combine(folder,"ControllerSupport.ini"); logPath=Path.Combine(folder,"ControllerSupport.log");
            try { config=Settings.Load(configPath); } catch(Exception e) { config=new Settings(); Log("Configuration defaults: "+e.Message); }
            pad=new PadCore(config);
            if(!File.Exists(configPath)) Save();
            Log("Standalone ControllerSupport 1.0 initialized; no CreativeMod dependency.");
        }
        static void Log(string message)
        { try { if(logPath!=null) File.AppendAllText(logPath,DateTime.Now.ToString("s")+" "+message+Environment.NewLine); } catch {} }
        static void Save() { try { config.Save(configPath); notice="Settings saved."; } catch(Exception e) { notice="Save failed: "+e.Message; Log(notice); } }
        public static void Tick()
        {
            try { PollFrame(); } catch(Exception e) {
                if(Time.realtimeSinceStartup>=nextError) { nextError=Time.realtimeSinceStartup+10; Log("Input error: "+e); }
                NativeInput.Release(); if(pad!=null) pad.Reset(); StopRumble();
            }
            finally { UpdateMouseShield(); }
        }
        static void UpdateMouseShield()
        {
            try {
                var app=MonoSingleton<AppManager>.Instance;
                var ui=app==null?null:app.method_0() as UniUIManager;
                var stage=ui==null?null:ui.Stage_0;
                var camera=stage==null?null:stage.host as MovieClipOverlayCameraBehaviour;
                bool block=overlay||keyboard||suppressFrame==frame;
                if(shieldCamera!=null&&(!block||shieldCamera!=camera)) {
                    if(resetFlashInput!=null) resetFlashInput.Invoke(shieldCamera,null);
                    shieldCamera.enableMouse=savedMouseEnabled;shieldCamera.enableTouch=savedChildrenEnabled;shieldCamera=null;
                }
                if(block&&camera!=null&&shieldCamera==null) {
                    shieldCamera=camera;savedMouseEnabled=camera.enableMouse;savedChildrenEnabled=camera.enableTouch;
                    camera.enableMouse=false;camera.enableTouch=false;
                    if(resetFlashInput!=null) resetFlashInput.Invoke(camera,null);
                }
            } catch(Exception e) { Log("Menu mouse shield: "+e.Message); }
        }
        static void PollFrame()
        {
            Init(); if(frame==Time.frameCount) return; frame=Time.frameCount;
            float wallTime=Time.realtimeSinceStartup;
            cursorDelta=lastWallTime<0?.016f:Math.Min(.05f,Math.Max(0,wallTime-lastWallTime)); lastWallTime=wallTime;
            foreach(int key in releaseNextFrame) NativeInput.Key(key,false); releaseNextFrame.Clear();
            bool focused=NativeInput.Focused;
            if(!focused) {
                NativeInput.Release(); StopRumble(); pad.Step(new PadSample(),false); index=-1; capture=null;
                chordAt=-1; chordUsed=chordFired=false; return;
            }
            PadSample sample=NativeInput.Poll(config.Controller,index); index=sample.Connected?sample.Index:-1;
            if(index!=lastLoggedIndex) { Log(index<0?"Controller disconnected.":"XInput controller connected in slot "+(index+1)+"; release controls to activate."); lastLoggedIndex=index; }
            menu=overlay||keyboard||!GameFlowManager.m_needLockedCursor;
            if(menu!=lastMenu) { NativeInput.Release(); pad.Reset(); lastMenu=menu; }
            pad.Step(sample,focused,overlay);
            if(!sample.Connected) { NativeInput.Release(); StopRumble(); capture=null; chordAt=-1; chordUsed=chordFired=false; }
            if(Input.GetKeyDown(KeyCode.F10)) SetOverlay(!overlay);
            if((overlay||keyboard)&&Input.GetKeyDown(KeyCode.Escape)) {
                if(keyboard) { keyboard=false; suppressFrame=frame; } else SetOverlay(false); capture=null; pad.Reset(); return;
            }
            if(!pad.Ready) { NativeInput.Release(); StopRumble(); return; }
            float now=Time.realtimeSinceStartup;
            bool chord=pad.Button(PadButton.Back|PadButton.Start,0);
            if(chord) {
                chordUsed=true;
                if(chordAt<0) chordAt=now;
                if(!chordFired&&now-chordAt>=.5f) { chordFired=true; SetOverlay(!overlay); return; }
            } else {
                chordAt=-1;
                if((pad.Held&48)==0&&(pad.Up&48)==0) { chordUsed=false; chordFired=false; }
            }
            if(capture!=null) {
                NativeInput.Release();
                if(pad.Down!=0) {
                    if(pad.Button(PadButton.Back,1)) { capture=null; notice="Binding canceled."; }
                    else { config.Bindings[capture]=(PadButton)pad.Held; notice=capture+" bound to "+((PadButton)pad.Held); capture=null; Save(); }
                    pad.Reset();
                }
                return;
            }
            if(keyboard) { NativeInput.Release(); TickKeyboard(now); }
            else if(menu) TickCursor(now);
            else NativeInput.Release();
            if(!config.Rumble||now>=rumbleUntil) StopRumble();
        }
        static void TickCursor(float now)
        {
            // Menus must remain navigable even when the game pauses its simulation clock.
            float dt=cursorDelta;
            float x=pad.LX,y=pad.LY;
            // Right stick offers slower precision aiming over small menu buttons.
            if(Math.Abs(pad.RX)+Math.Abs(pad.RY)>.01f) { x=pad.RX*.35f; y=pad.RY*.35f; }
            if(pad.Button(PadButton.Left,0)) x=-.3f;
            if(pad.Button(PadButton.Right,0)) x=.3f;
            if(pad.Button(PadButton.Up,0)) y=.3f;
            if(pad.Button(PadButton.Down,0)) y=-.3f;
            NativeInput.MoveCursor(x,y,dt,config.CursorSpeed);
            NativeInput.Mouse(false,pad.Button(PadButton.A,0));
            NativeInput.Mouse(true,pad.Button(PadButton.X,0));
            NativeInput.Key(0xA0,pad.Button(PadButton.LB,0));
            if(pad.Button(PadButton.RB,1)) TapKey(9);
            if(pad.Button(PadButton.Y,1)) { keyboard=true; NativeInput.Release(); pad.Reset(); }
            if(now>=nextScroll&&(pad.Button(PadButton.LT,0)||pad.Button(PadButton.RT,0))) {
                NativeInput.Scroll(pad.Button(PadButton.LT,0)?1:-1); nextScroll=now+.075f;
            }
            if(overlay && pad.Button(PadButton.B,1)) SetOverlay(false);
        }
        static void TapKey(int key)
        { if(NativeInput.Key(key,true)) releaseNextFrame.Add(key); }
        static void TickKeyboard(float now)
        {
            if(pad.Button(PadButton.B,1)||pad.Button(PadButton.Y,1)) { keyboard=false; suppressFrame=frame; pad.Reset(); return; }
            if(pad.Button(PadButton.X,1)) TapKey(8);
            if(pad.Button(PadButton.LB,1)) caps=!caps;
            if(pad.Button(PadButton.RB,1)) TapKey(9);
            if(pad.Button(PadButton.Start,1)) { keyboard=false; TapKey(13); pad.Reset(); return; }
            int dx=pad.Button(PadButton.Left,0)||pad.LX<-.5f?-1:pad.Button(PadButton.Right,0)||pad.LX>.5f?1:0;
            int dy=pad.Button(PadButton.Up,0)||pad.LY>.5f?-1:pad.Button(PadButton.Down,0)||pad.LY<-.5f?1:0;
            if(dx==0&&dy==0) nextNav=0;
            else if(now>=nextNav) {
                keyRow=(keyRow+dy+5)%5;
                int count=keyRow==4?special.Length:keyboardRows[keyRow].Length;
                keyCol=(keyCol+dx+count)%count;
                nextNav=now+(nextNav==0?.32f:.1f);
            }
            if(pad.Button(PadButton.A,1)) EnterKey(keyRow,keyCol);
        }
        static void EnterKey(int row,int col)
        {
            if(row<4) NativeInput.Character((caps?shiftedRows[row]:keyboardRows[row])[col]);
            else switch(col) {
                case 0:NativeInput.Character(' ');break; case 1:TapKey(8);break; case 2:TapKey(9);break;
                case 3:keyboard=false;TapKey(13);pad.Reset();break; case 4:caps=!caps;break; case 5:keyboard=false;pad.Reset();break;
            }
        }
        static void SetOverlay(bool value)
        {
            overlay=value; keyboard=false; capture=null; suppressFrame=frame;
            var app=MonoSingleton<AppManager>.Instance;
            if(app!=null&&app.method_0()!=null) app.method_0().method_15(value,"StandaloneController");
            NativeInput.Release(); StopRumble(); pad.Reset();
            UpdateMouseShield();
        }
        static bool Global(string name)
        { return name.StartsWith("UI",StringComparison.Ordinal)||name=="Escape"||name=="Friend"||name=="Guild"||name=="Group"||name=="CashShop"||name=="Achievement"||name=="ShowScore"||name=="PVP"; }
        static bool Action(string name,int edge)
        {
            if(overlay||keyboard) return false;
            if(pulse==name&&pulseFrame==frame) return edge!=2;
            if(pad==null||!pad.Ready) return false;
            if(menu) {
                if(name=="Escape"&&pad.Button(PadButton.B,edge)) return true;
                if(!Global(name)) return false;
            }
            if(chordUsed||pad.Button(PadButton.Back|PadButton.Start,0)) {
                PadButton binding; if(config.Bindings.TryGetValue(name,out binding)&&((uint)binding&48)!=0) return false;
            }
            if(name=="Forward") return edge==0&&pad.LY>.08f;
            if(name=="Backward") return edge==0&&pad.LY<-.08f;
            if(name=="Left") return edge==0&&pad.LX<-.08f;
            if(name=="Right") return edge==0&&pad.LX>.08f;
            // Resolve the shared X button against the game's own interaction target.
            if((name=="Reload"||name=="Interaction")&&config.Bindings["Reload"]==config.Bindings["Interaction"]&&config.Bindings["Reload"]!=PadButton.None) {
                var target=MonoSingleton<TargetingManager>.Instance;
                bool available=target!=null&&target.m_availableInteraction!=null;
                if((name=="Interaction")!=available) return false;
            }
            PadButton b;
            if(config.Bindings.TryGetValue(name,out b)&&(b==PadButton.Back||b==PadButton.Start))
                return edge!=2 && pad.Button(b,2); // Release allows Back+Start settings chord without opening two menus.
            return pad.Action(name,edge);
        }
        public static bool Held(bool original,string name) { Tick(); return NativeInput.Focused&&suppressFrame!=frame&&!overlay&&!keyboard && (original||Action(name,0)); }
        public static bool Pressed(bool original,string name) { Tick(); return NativeInput.Focused&&suppressFrame!=frame&&!overlay&&!keyboard && (original||Action(name,1)); }
        public static bool Released(bool original,string name) { Tick(); return NativeInput.Focused&&suppressFrame!=frame&&!overlay&&!keyboard && (original||Action(name,2)); }
        public static float Axis(float original,string name)
        {
            Tick(); if(!NativeInput.Focused) return 0;
            if((overlay||keyboard||suppressFrame==frame)&&(name=="Mouse X"||name=="Mouse Y"||name=="SwitchWeapon")) return 0;
            if(pad==null||!pad.Ready||menu) return original;
            if(name=="SwitchWeapon") return original+(Action("NextWeapon",1)?1:0);
            float scale=config.LookSpeed*Math.Min(.05f,Math.Max(0,Time.deltaTime));
            if(pad.Action("Fire2",0)) scale*=config.AimScale;
            if(name=="Mouse X") return original+pad.RX*scale/nativeYaw;
            if(name=="Mouse Y") return original+pad.RY*scale/nativePitch*(config.InvertY?-1:1)*(nativeInvert?-1:1);
            return original;
        }
        public static void ObserveLook(float yaw,float pitch,bool inverted)
        {
            // Native camera code multiplies the axis by these factors immediately afterwards.
            // Divide only the controller contribution to keep mouse settings and weapon zoom independent.
            nativeYaw=Settings.Bound(Math.Abs(yaw),.01f,1000,15);
            nativePitch=Settings.Bound(Math.Abs(pitch),.01f,1000,15); nativeInvert=inverted;
        }
        public static bool FilterMouse(bool original) { Tick();return original&&!overlay&&!keyboard&&suppressFrame!=frame; }
        public static float FilterScroll(float original) { Tick();return overlay||keyboard||suppressFrame==frame?0:original; }
        public static void Shot(EntityWeapon weapon)
        {
            Tick(); if(pad==null||!pad.Ready||!config.Rumble||menu||index<0||weapon==null||weapon.m_owner==null) return;
            EntityPlayer player=weapon.m_owner as EntityPlayer;
            if(player==null||player.vmethod_25()==null||!player.vmethod_25().IsMyActor()) return;
            if(!pad.Action("Fire1",0)) return;
            NativeInput.Rumble(index,.16f,.30f); rumbleIndex=index; rumbleUntil=Time.realtimeSinceStartup+.075f;
        }
        static void StopRumble() { if(rumbleIndex>=0) NativeInput.Rumble(rumbleIndex,0,0); rumbleIndex=-1; }
        public static void FocusChanged(bool focused) { if(!focused) { NativeInput.Release(); StopRumble(); if(pad!=null) pad.Reset(); } }
        public static void Shutdown() { NativeInput.Release(); StopRumble();overlay=keyboard=false;suppressFrame=-1;UpdateMouseShield(); }
        public static void Draw()
        {
            if(pad==null||!NativeInput.Focused) return;
            RenderTexture target=RenderTexture.active; Matrix4x4 matrix=GUI.matrix;
            Color color=GUI.color,background=GUI.backgroundColor,content=GUI.contentColor; int depth=GUI.depth; bool enabled=GUI.enabled;
            try {
                RenderTexture.active=null; GUI.matrix=Matrix4x4.identity; GUI.color=GUI.backgroundColor=GUI.contentColor=Color.white; GUI.depth=-10000; GUI.enabled=true;
                float scale=Math.Min(Screen.width/1000f,Screen.height/700f); if(scale<=0) return;
                GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1));
                float width=Screen.width/scale,height=Screen.height/scale;
                if(keyboard) DrawKeyboard(width,height);
                else if(overlay) DrawSettings(width,height);
                else if(config.Hints&&index>=0) {
                    string hint=!pad.Ready?"Controller connected: release sticks, triggers and buttons.":menu?
                        "LS cursor | RS precise | A click/drag | X right click | LT/RT scroll | LB Shift | RB Tab | B back | Y keyboard":
                        "A jump | B crouch | X interact/reload | Y weapon | LB skill | RB grenade | Back inventory | Start pause";
                    GUI.Box(new UnityEngine.Rect(12,height-62,width-24,50),hint+"\nHold Back+Start: controller settings");
                }
            } catch(Exception e) { if(Time.realtimeSinceStartup>=nextError) { Log("Overlay error: "+e.Message); nextError=Time.realtimeSinceStartup+10; } }
            finally { RenderTexture.active=target; GUI.matrix=matrix; GUI.color=color; GUI.backgroundColor=background; GUI.contentColor=content; GUI.depth=depth; GUI.enabled=enabled; }
        }
        static bool Button(float x,float y,float w,string text) { return GUI.Button(new UnityEngine.Rect(x,y,w,30),text); }
        static void DrawSettings(float width,float height)
        {
            float x=(width-870)/2,y=(height-650)/2;
            GUI.Box(new UnityEngine.Rect(x,y,870,650),"Standalone Xbox Controller Support");
            GUI.Label(new UnityEngine.Rect(x+20,y+30,810,25),"Slot: "+(index<0?"not connected":(index+1).ToString())+" | "+(pad.Ready?"ready":"release controls to activate")+" | "+notice);
            if(Button(x+20,y+60,170,"Controls")) tab=0;
            if(Button(x+200,y+60,170,"Bindings")) tab=1;
            if(Button(x+380,y+60,170,"Game menus")) tab=2;
            if(Button(x+660,y+60,180,"Close (B)")) SetOverlay(false);
            if(tab==0) {
                config.Enabled=GUI.Toggle(new UnityEngine.Rect(x+25,y+105,220,25),config.Enabled,"Enable controller");
                config.InvertY=GUI.Toggle(new UnityEngine.Rect(x+260,y+105,220,25),config.InvertY,"Invert right stick Y");
                config.Hints=GUI.Toggle(new UnityEngine.Rect(x+495,y+105,160,25),config.Hints,"Show hints");
                config.Rumble=GUI.Toggle(new UnityEngine.Rect(x+680,y+105,150,25),config.Rumble,"Shot vibration");
                config.LookSpeed=Slider(x,y+155,"Look degrees / second",config.LookSpeed,30,720);
                config.AimScale=Slider(x,y+205,"Aim sensitivity multiplier",config.AimScale,.1f,1);
                config.MoveDeadzone=Slider(x,y+255,"Movement deadzone",config.MoveDeadzone,.05f,.8f);
                config.LookDeadzone=Slider(x,y+305,"Look deadzone",config.LookDeadzone,.05f,.8f);
                config.Trigger=Slider(x,y+355,"Trigger threshold",config.Trigger,.05f,.95f);
                config.CursorSpeed=Slider(x,y+405,"Menu cursor speed",config.CursorSpeed,100,2500);
                if(Button(x+25,y+450,350,"Controller: "+(config.Controller<0?"Auto (first connected)":"Slot "+(config.Controller+1)))) {
                    config.Controller=config.Controller==3?-1:config.Controller+1; NativeInput.Release(); StopRumble(); pad.Reset();
                }
                GUI.Label(new UnityEngine.Rect(x+25,y+492,800,70),"Menus use the existing game cursor: hold A to drag inventory items, X for item menus.\nClick a text field, then Y to type with the on-screen keyboard.\nMovement uses the game's eight-direction network input; look and cursor are analog.");
                if(Button(x+25,y+578,235,"Save settings")) Save();
                if(Button(x+280,y+578,235,"Reload settings")) {
                    try { config=Settings.Load(configPath); pad.Config=config; pad.Reset(); notice="Settings reloaded."; } catch(Exception e) { notice=e.Message; }
                }
                if(Button(x+535,y+578,300,"Restore controller defaults")) { config=new Settings(); pad.Config=config; pad.Reset(); Save(); }
            } else if(tab==1) {
                GUI.Label(new UnityEngine.Rect(x+25,y+103,820,25),capture==null?"Select an action, release controls, then press a button or chord. Back cancels. None clears a binding.":"Waiting for binding: "+capture);
                for(int i=0;i<Settings.Actions.Length;i++) {
                    string a=Settings.Actions[i]; float bx=x+25+(i/14)*425,by=y+135+(i%14)*33;
                    GUI.Label(new UnityEngine.Rect(bx,by,173,28),a);
                    if(Button(bx+175,by,173,config.Bindings[a].ToString())) { capture=a; pad.Reset(); }
                    if(Button(bx+352,by,55,"None")) { config.Bindings[a]=PadButton.None; Save(); }
                }
                if(Button(x+25,y+610,250,"Save bindings")) Save();
            } else {
                string[] actions={"UIInventory","UISkillTree","UIQuest","UIMap","Friend","Guild","Group","CashShop","Achievement","ShowScore","UIChatInput","Escape"};
                for(int i=0;i<actions.Length;i++) {
                    string a=actions[i];
                    if(Button(x+25+(i%3)*277,y+120+(i/3)*75,260,a.Substring(a.StartsWith("UI")?2:0))) {
                        SetOverlay(false); pulse=a; pulseFrame=Time.frameCount+1;
                    }
                }
                GUI.Label(new UnityEngine.Rect(x+25,y+470,800,80),"All menus retain their original controls, confirmations and permissions.\nUse the cursor for equipment, skills, shops, quests, maps and dialogs.\nFor chat, open Chat, focus its text field and press Y for the keyboard.");
            }
        }
        static float Slider(float x,float y,string label,float value,float min,float max)
        {
            GUI.Label(new UnityEngine.Rect(x+25,y,285,30),label+": "+value.ToString("0.00"));
            return GUI.HorizontalSlider(new UnityEngine.Rect(x+320,y+8,510,25),value,min,max);
        }
        static void DrawKeyboard(float width,float height)
        {
            float x=(width-850)/2,y=height-365;
            GUI.Box(new UnityEngine.Rect(x,y,850,350),"Controller Keyboard - focused game field receives text");
            GUI.Label(new UnityEngine.Rect(x+15,y+30,820,30),"D-pad/LS select | A type | X backspace | LB Shift | RB Tab | Start Enter | B/Y close");
            for(int row=0;row<5;row++) {
                int count=row==4?special.Length:keyboardRows[row].Length; float w=820f/count;
                for(int col=0;col<count;col++) {
                    string label=row==4?special[col]:(caps?shiftedRows[row]:keyboardRows[row])[col].ToString();
                    GUI.backgroundColor=row==keyRow&&col==keyCol?new Color(.2f,.7f,1):Color.white;
                    if(Button(x+15+col*w,y+76+row*49,w-4,label)) EnterKey(row,col);
                }
            }
            GUI.backgroundColor=Color.white;
        }
    }
}
