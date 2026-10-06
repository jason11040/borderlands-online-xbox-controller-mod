using System;
using System.Collections.Generic;
using System.Reflection;
using StandaloneController;
using UnityEngine;

// Test doubles isolate production input routing from Unity and Windows. No real events are injected.
namespace UnityEngine {
    public enum KeyCode { F10,Escape }
    public class RenderTexture { public static RenderTexture active; }
    public struct Matrix4x4 { public static Matrix4x4 identity; public static Matrix4x4 TRS(Vector3 p,Quaternion q,Vector3 s) { return identity; } }
    public struct Quaternion { public static Quaternion identity; }
    public struct Vector3 { public static Vector3 zero; public Vector3(float x,float y,float z) {} }
    public struct Color { public static Color white; public Color(float r,float g,float b) {} }
    public struct Rect { public Rect(float x,float y,float w,float h) {} }
    public static class Screen { public static int width=1280,height=720; }
    public static class Time { public static int frameCount; public static float deltaTime=.016f,realtimeSinceStartup; }
    public static class Input { public static HashSet<KeyCode> Keys=new HashSet<KeyCode>(); public static bool GetKeyDown(KeyCode key) { return Keys.Contains(key); } }
    public static class GUI {
        public static Matrix4x4 matrix; public static Color color,backgroundColor,contentColor; public static int depth; public static bool enabled;
        public static void Box(Rect rect,string text) {} public static void Label(Rect rect,string text) {}
        public static bool Button(Rect rect,string text) { return false; }
        public static bool Toggle(Rect rect,bool value,string text) { return value; }
        public static float HorizontalSlider(Rect rect,float value,float min,float max) { return value; }
    }
}
namespace ns0 { public class MonoSingleton<T> { public static T Instance; } }
namespace pumpkin.display { public class Stage { public object host=new MovieClipOverlayCameraBehaviour(); } }
public class MovieClipOverlayCameraBehaviour { public bool enableMouse=true,enableTouch=true,enableKeyboard=true; private void initTouchIputState() {} }
public class GameFlowManager { public static bool m_needLockedCursor=true; }
public class GameUIManager { public void method_15(bool value,string name) { GameFlowManager.m_needLockedCursor=!value; } }
public class UniUIManager : GameUIManager { public pumpkin.display.Stage Stage_0=new pumpkin.display.Stage(); }
public class AppManager { GameUIManager ui=new UniUIManager(); public GameUIManager method_0() { return ui; } }
public class TargetingManager { public object m_availableInteraction; }
public class Owner { public bool Mine=true; public bool IsMyActor() { return Mine; } }
public class EntityPlayer { public Owner Owner=new Owner(); public Owner vmethod_25() { return Owner; } }
public class EntityWeapon { public EntityPlayer m_owner; }
namespace StandaloneController {
    public static class NativeInput {
        public static bool Focused=true,Left,Right; public static PadSample Sample;
        public static HashSet<int> Keys=new HashSet<int>(); public static string Text="";
        public static int ScrollCount,MouseDowns,KeyDowns,Rumbles,Polls; public static float CursorDelta;
        public static PadSample Poll(int preferred,int previous) { Polls++; return Sample; }
        public static bool Mouse(bool right,bool held) { if(held&&!(right?Right:Left)) MouseDowns++; if(right) Right=held; else Left=held; return true; }
        public static bool Key(int key,bool held) { if(held) { KeyDowns++;Keys.Add(key); } else Keys.Remove(key); return true; }
        public static bool Character(char c) { Text+=c;return true; }
        public static void Scroll(int steps) { ScrollCount+=steps; }
        public static void MoveCursor(float x,float y,float dt,float speed) { CursorDelta=dt; }
        public static void Release() { Left=Right=false;Keys.Clear(); }
        public static void Rumble(int index,float left,float right) { if(left>0||right>0) Rumbles++; }
    }
}
class RuntimeSmoke {
    static int checks;
    static void Check(bool value,string message) { checks++;if(!value) throw new Exception(message); }
    static PadSample Neutral() { return new PadSample { Connected=true,Index=0 }; }
    static void Step(PadSample s,float dt=.016f) {
        Time.frameCount++; Time.realtimeSinceStartup+=dt; Time.deltaTime=dt; NativeInput.Sample=s; ControllerSupport.Tick(); Input.Keys.Clear();
    }
    static void Button(PadButton buttons,float dt=.016f) { var s=Neutral();s.Buttons=(uint)buttons;Step(s,dt); }
    static void Main() {
        ns0.MonoSingleton<AppManager>.Instance=new AppManager(); ns0.MonoSingleton<TargetingManager>.Instance=new TargetingManager();
        Step(Neutral()); Check(ControllerSupport.Held(true,"Forward"),"Keyboard input remains available.");
        var sample=Neutral(); sample.RT=1;sample.LT=.6f;sample.LX=.7f;sample.LY=.7f;sample.RX=.8f;
        Step(sample);
        Check(ControllerSupport.Held(false,"Fire1")&&ControllerSupport.Held(false,"Fire2"),"Gameplay triggers.");
        Check(ControllerSupport.Held(false,"Forward")&&ControllerSupport.Held(false,"Right"),"Gameplay diagonal movement.");
        int polls=NativeInput.Polls; ControllerSupport.Pressed(false,"Jump");ControllerSupport.Axis(0,"Mouse X");
        Check(NativeInput.Polls==polls,"Input reads poll only once per rendered frame.");
        float aimed=ControllerSupport.Axis(0,"Mouse X"); sample.LT=0;Step(sample);float normal=ControllerSupport.Axis(0,"Mouse X");
        Check(aimed>0&&Math.Abs(aimed*2-normal)<.00001f,"ADS look multiplier.");
        ControllerSupport.ObserveLook(30,20,false);float adjusted=ControllerSupport.Axis(0,"Mouse X");
        Check(Math.Abs(adjusted*2-normal)<.00001f,"Controller speed compensates native mouse sensitivity.");
        ControllerSupport.ObserveLook(15,15,false);sample.RY=.8f;Step(sample);float upright=ControllerSupport.Axis(0,"Mouse Y");
        ControllerSupport.ObserveLook(15,15,true);float inverted=ControllerSupport.Axis(0,"Mouse Y");
        Check(Math.Abs(upright+inverted)<.00001f,"Controller vertical direction compensates native mouse inversion.");
        ControllerSupport.ObserveLook(15,15,false);
        Step(Neutral()); Button(PadButton.X);
        Check(ControllerSupport.Pressed(false,"Reload")&&!ControllerSupport.Pressed(false,"Interaction"),"X reload without interaction target.");
        Step(Neutral());ns0.MonoSingleton<TargetingManager>.Instance.m_availableInteraction=new object();Button(PadButton.X);
        Check(!ControllerSupport.Pressed(false,"Reload")&&ControllerSupport.Pressed(false,"Interaction"),"X interaction excludes reload.");
        Step(Neutral());Button(PadButton.Y);Check(ControllerSupport.Axis(0,"SwitchWeapon")==1,"Cycle weapon down edge.");
        Button(PadButton.Y);Check(ControllerSupport.Axis(0,"SwitchWeapon")==0,"Holding Y does not cycle every frame.");
        Step(Neutral()); Button(PadButton.Back);Check(!ControllerSupport.Pressed(false,"UIInventory"),"Back waits for release.");
        Step(Neutral());Check(ControllerSupport.Pressed(false,"UIInventory"),"Back release opens inventory.");
        Button(PadButton.Back|PadButton.Start); Button(PadButton.Back|PadButton.Start,.6f);
        Check(!ControllerSupport.Pressed(true,"Escape"),"Settings chord suppresses underlying game input.");
        Check(!ControllerSupport.FilterMouse(true)&&ControllerSupport.FilterScroll(1)==0,"Settings block underlying menu clicks and scrolling.");
        var stage=((UniUIManager)ns0.MonoSingleton<AppManager>.Instance.method_0()).Stage_0;
        var camera=(MovieClipOverlayCameraBehaviour)stage.host;
        Check(!camera.enableMouse&&!camera.enableTouch&&camera.enableKeyboard,"Settings disable Flash mouse/touch and preserve text input.");
        Button(PadButton.Back);Step(Neutral());Check(!ControllerSupport.Pressed(false,"UIInventory"),"Staggered chord release cannot open inventory.");
        Step(Neutral());Button(PadButton.B);Check(!ControllerSupport.Pressed(true,"Escape"),"Closing settings cannot also close game menu.");
        Step(Neutral());Check(camera.enableMouse&&camera.enableTouch,"Closing settings restores original Flash mouse flags.");
        Step(Neutral());GameFlowManager.m_needLockedCursor=false;Step(Neutral());Button(PadButton.A|PadButton.LB);
        Check(NativeInput.Left&&NativeInput.Keys.Contains(0xA0),"Menu drag and Shift modifier.");
        Check(!ControllerSupport.Held(false,"Jump")&&!ControllerSupport.Held(false,"Fire1"),"Menu controller actions cannot fire or jump.");
        Time.frameCount++;Time.realtimeSinceStartup+=.016f;Time.deltaTime=0;NativeInput.Sample=Neutral();ControllerSupport.Tick();
        Check(NativeInput.CursorDelta>0,"Menu cursor remains active when simulation is paused.");
        Step(Neutral());Check(!NativeInput.Left&&!NativeInput.Keys.Contains(0xA0),"Menu drag/modifier release.");
        Button(PadButton.B);Check(ControllerSupport.Pressed(false,"Escape"),"Menu B uses native back handler.");
        Step(Neutral());Button(PadButton.RB);Check(NativeInput.Keys.Contains(9),"Tab injected for text fields.");
        Step(Neutral());Check(!NativeInput.Keys.Contains(9),"Tab released on next frame.");
        Button(PadButton.Y);Step(Neutral());Button(PadButton.A);
        Check(NativeInput.Text=="1"&&!NativeInput.Left,"Keyboard types selected key without clicking underlying UI.");
        Check(!ControllerSupport.FilterMouse(true)&&!camera.enableMouse&&camera.enableKeyboard,"Keyboard shields underlying UI while preserving text focus.");
        Step(Neutral());Button(PadButton.X);Check(NativeInput.Keys.Contains(8),"Keyboard backspace.");
        Step(Neutral());Button(PadButton.B);Step(Neutral());Button(PadButton.A);
        Check(NativeInput.Left,"Keyboard close restores menu click.");
        NativeInput.Focused=false;Step(Neutral());Check(!NativeInput.Left&&NativeInput.Keys.Count==0,"Alt-tab releases injected inputs.");
        Check(!ControllerSupport.Held(true,"Forward")&&ControllerSupport.Axis(1,"Mouse X")==0,"Unfocused gameplay input suppressed.");
        NativeInput.Focused=true;Button(PadButton.A);Check(!NativeInput.Left,"Focus return with held button cannot click.");
        Step(Neutral());Button(PadButton.A);Check(NativeInput.Left,"Fresh click works after neutral.");
        Step(new PadSample());Check(!NativeInput.Left,"Disconnect releases drag.");
        Step(Neutral());GameFlowManager.m_needLockedCursor=true;Step(Neutral());var firing=Neutral();firing.RT=1;Step(firing);
        var gun=new EntityWeapon { m_owner=new EntityPlayer() };ControllerSupport.Shot(gun);Check(NativeInput.Rumbles==1,"Local controller shot vibrates.");
        gun.m_owner.Owner.Mine=false;ControllerSupport.Shot(gun);Check(NativeInput.Rumbles==1,"Other players' shots do not vibrate.");
        ControllerSupport.Shutdown();Check(!NativeInput.Left&&NativeInput.Keys.Count==0,"Shutdown releases injected controls.");
        Console.WriteLine("PASS: "+checks+" gameplay, menus, keyboard, chord, focus, disconnect and local-shot routing checks.");
    }
}
