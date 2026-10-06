using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StandaloneController
{
    public static class NativeInput
    {
        [StructLayout(LayoutKind.Sequential)] public struct Gamepad {
            public ushort Buttons; public byte LT,RT; public short LX,LY,RX,RY;
        }
        [StructLayout(LayoutKind.Sequential)] public struct XState { public uint Packet; public Gamepad Pad; }
        [StructLayout(LayoutKind.Sequential)] public struct Vibration { public ushort Left,Right; }
        [DllImport("xinput1_4.dll",EntryPoint="XInputGetState")] static extern uint State14(uint index,out XState state);
        [DllImport("xinput1_3.dll",EntryPoint="XInputGetState")] static extern uint State13(uint index,out XState state);
        [DllImport("xinput9_1_0.dll",EntryPoint="XInputGetState")] static extern uint State91(uint index,out XState state);
        [DllImport("xinput1_4.dll",EntryPoint="XInputSetState")] static extern uint Vibe14(uint index,ref Vibration vibe);
        [DllImport("xinput1_3.dll",EntryPoint="XInputSetState")] static extern uint Vibe13(uint index,ref Vibration vibe);
        [DllImport("xinput9_1_0.dll",EntryPoint="XInputSetState")] static extern uint Vibe91(uint index,ref Vibration vibe);
        [StructLayout(LayoutKind.Sequential)] public struct MouseInput {
            public int X,Y; public uint Data,Flags,Time; public UIntPtr Extra;
        }
        [StructLayout(LayoutKind.Sequential)] public struct KeyInput {
            public ushort Key,Scan; public uint Flags,Time; public UIntPtr Extra;
        }
        [StructLayout(LayoutKind.Explicit)] public struct InputUnion {
            [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyInput Key;
        }
        [StructLayout(LayoutKind.Sequential)] public struct WinInput { public uint Type; public InputUnion Union; }
        [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint id);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window,out Rect rect);
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window,ref Point point);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
        [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,WinInput[] input,int size);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code,uint mapType);
        static readonly uint ProcessId=(uint)Process.GetCurrentProcess().Id;
        static int backend; // 0 unresolved; 14, 13, 91 or -1 unavailable.
        static readonly HashSet<int> injectedKeys=new HashSet<int>();
        static bool mouseLeft,mouseRight;
        static float cursorRemainderX,cursorRemainderY;
        public static bool Focused {
            get { uint pid; IntPtr window=GetForegroundWindow(); return window!=IntPtr.Zero && GetWindowThreadProcessId(window,out pid)!=0 && pid==ProcessId; }
        }
        static uint GetState(uint index,out XState state)
        {
            state=new XState();
            if(backend==-1) return 1167;
            if(backend==0) {
                try { uint r=State14(index,out state); backend=14; return r; } catch(DllNotFoundException) {} catch(EntryPointNotFoundException) {}
                try { uint r=State13(index,out state); backend=13; return r; } catch(DllNotFoundException) {} catch(EntryPointNotFoundException) {}
                try { uint r=State91(index,out state); backend=91; return r; } catch(DllNotFoundException) {} catch(EntryPointNotFoundException) {}
                backend=-1; return 1167;
            }
            return backend==14?State14(index,out state):backend==13?State13(index,out state):State91(index,out state);
        }
        public static float Axis(short v) { return v<0?v/32768f:v/32767f; }
        public static PadSample Poll(int preferred,int previous)
        {
            XState state;
            if(preferred>=0) { if(GetState((uint)preferred,out state)==0) return Decode(state,preferred); return new PadSample(); }
            if(previous>=0&&GetState((uint)previous,out state)==0) return Decode(state,previous);
            for(int i=0;i<4;i++) if(i!=previous&&GetState((uint)i,out state)==0) return Decode(state,i);
            return new PadSample();
        }
        public static PadSample Decode(XState s,int index)
        {
            return new PadSample { Connected=true, Index=index, Buttons=s.Pad.Buttons,
                LX=Axis(s.Pad.LX),LY=Axis(s.Pad.LY),RX=Axis(s.Pad.RX),RY=Axis(s.Pad.RY),LT=s.Pad.LT/255f,RT=s.Pad.RT/255f };
        }
        public static void Rumble(int index,float left,float right)
        {
            if(index<0||backend<=0) return;
            Vibration v=new Vibration { Left=(ushort)(Settings.Bound(left,0,1,0)*65535),Right=(ushort)(Settings.Bound(right,0,1,0)*65535) };
            if(backend==14) Vibe14((uint)index,ref v); else if(backend==13) Vibe13((uint)index,ref v); else Vibe91((uint)index,ref v);
        }
        static bool Send(WinInput input) { return SendInput(1,new[]{input},Marshal.SizeOf(typeof(WinInput)))==1; }
        public static bool Mouse(bool right,bool held)
        {
            bool old=right?mouseRight:mouseLeft; if(old==held) return true;
            if(held && (!Focused||(GetAsyncKeyState(right?2:1)&0x8000)!=0)) return false;
            WinInput input=new WinInput(); input.Union.Mouse.Flags=right?(held?8u:16u):(held?2u:4u);
            if(!Send(input)) return false;
            if(right) mouseRight=held; else mouseLeft=held; return true;
        }
        public static bool Key(int key,bool held)
        {
            if(injectedKeys.Contains(key)==held) return true;
            if(held && (!Focused||(GetAsyncKeyState(key)&0x8000)!=0)) return false;
            WinInput input=new WinInput { Type=1 };
            input.Union.Key.Scan=(ushort)MapVirtualKey((uint)key,0);
            // Scancodes reach Unity's legacy input as well as the Flash menus.
            input.Union.Key.Flags=8u|(held?0u:2u); if(key>=0x21&&key<=0x2E) input.Union.Key.Flags|=1;
            if(!Send(input)) return false;
            if(held) injectedKeys.Add(key); else injectedKeys.Remove(key); return true;
        }
        public static bool Character(char c)
        {
            if(!Focused) return false;
            WinInput down=new WinInput { Type=1 }; down.Union.Key.Scan=c; down.Union.Key.Flags=4;
            WinInput up=down; up.Union.Key.Flags=6;
            return SendInput(2,new[]{down,up},Marshal.SizeOf(typeof(WinInput)))==2;
        }
        public static void Scroll(int steps)
        {
            if(!Focused||steps==0) return;
            WinInput input=new WinInput(); input.Union.Mouse.Flags=0x800; input.Union.Mouse.Data=unchecked((uint)(steps*120)); Send(input);
        }
        public static void MoveCursor(float x,float y,float dt,float speed)
        {
            if(!Focused||(x==0&&y==0)) return;
            IntPtr window=GetForegroundWindow(); Rect r; Point origin=new Point(),p;
            if(!GetClientRect(window,out r)||!ClientToScreen(window,ref origin)||!GetCursorPos(out p)) return;
            if(r.Right<=r.Left||r.Bottom<=r.Top) return;
            int mx,my; CursorMotion(x,y,dt,speed,ref cursorRemainderX,ref cursorRemainderY,out mx,out my);
            int nx=p.X+mx,ny=p.Y-my;
            SetCursorPos(Math.Max(origin.X,Math.Min(origin.X+r.Right-1,nx)),Math.Max(origin.Y,Math.Min(origin.Y+r.Bottom-1,ny)));
        }
        public static void Release()
        {
            Mouse(false,false); Mouse(true,false);
            foreach(int key in new List<int>(injectedKeys)) Key(key,false);
            cursorRemainderX=cursorRemainderY=0;
        }
        public static void CursorMotion(float x,float y,float dt,float speed,ref float remainderX,ref float remainderY,out int mx,out int my)
        {
            float tx=x*speed*dt+remainderX,ty=y*speed*dt+remainderY;
            mx=(int)tx; my=(int)ty; remainderX=tx-mx; remainderY=ty-my;
        }
    }
}
