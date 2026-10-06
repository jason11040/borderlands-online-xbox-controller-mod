using System;
using System.IO;
using System.Runtime.InteropServices;
using StandaloneController;

class PadSmoke
{
    static int checks;
    static void Assert(bool value,string message) { checks++; if(!value) throw new Exception(message); }
    static PadSample Sample(uint buttons=0,float lx=0,float ly=0,float rx=0,float ry=0,float lt=0,float rt=0,int index=0)
    { return new PadSample { Connected=true,Index=index,Buttons=buttons,LX=lx,LY=ly,RX=rx,RY=ry,LT=lt,RT=rt }; }
    static void Main()
    {
        var config=new Settings(); var core=new PadCore(config);
        core.Step(Sample((uint)PadButton.RT,rt:1),true);
        Assert(!core.Ready&&!core.Action("Fire1",0),"Held trigger on first connection must not fire.");
        core.Step(Sample(),true); Assert(core.Ready,"Neutral controller activates.");
        core.Step(Sample(rt:1,lt:.5f),true);
        Assert(core.Action("Fire1",0)&&core.Action("Fire2",0),"Independent simultaneous trigger support.");
        Assert(core.Action("Fire1",1)&&core.Action("Fire1",1),"Same-frame down reads must remain stable.");
        core.Step(Sample(rt:1),true); Assert(!core.Action("Fire1",1)&&core.Action("Fire2",2),"Hold is not a repeated down; other trigger releases.");
        core.Step(Sample(),true); Assert(core.Action("Fire1",2)&&!core.Action("Fire1",0),"Trigger release.");
        core.Step(Sample((uint)PadButton.A),true); Assert(core.Action("Jump",1),"Jump edge.");
        core.Step(Sample((uint)PadButton.A),false); Assert(!core.Ready&&core.Held==0,"Focus loss clears controller state.");
        core.Step(Sample((uint)PadButton.A),true); Assert(!core.Ready,"Focus return waits for release.");
        core.Step(Sample(),true); core.Step(Sample((uint)PadButton.A),true); Assert(core.Action("Jump",1),"Fresh press after focus return.");
        core.Step(new PadSample(),true); Assert(!core.Ready&&core.Held==0,"Disconnect clears action state.");
        core.Step(Sample(lx:1),true); Assert(!core.Ready,"Reconnect with held stick does not move.");
        core.Step(Sample(),true); core.Step(Sample(index:1,rt:1),true); Assert(!core.Ready,"Changing controller slot requires neutral.");
        core.Step(Sample(index:1),true); core.Step(Sample(index:1,rt:1),true); Assert(core.Action("Fire1",1),"New slot activates correctly.");
        config.Enabled=false; core.Step(Sample(index:1,rt:1),true); Assert(!core.Ready,"Disabled mod has no controller action.");
        config.Enabled=true; core.Step(Sample(index:1,rt:1),true); Assert(!core.Ready,"Re-enabling requires neutral.");
        float x,y; PadCore.Stick(.1f,.1f,.2f,out x,out y); Assert(x==0&&y==0,"Radial deadzone removes diagonal drift.");
        PadCore.Stick(1,1,.2f,out x,out y); Assert(Math.Abs(x*x+y*y-1)<.00001f,"Diagonal stick is capped to magnitude one.");
        PadCore.Stick(.6f,0,.2f,out x,out y); Assert(Math.Abs(x-.5f)<.00001f&&y==0,"Deadzone scales useful stick range.");
        PadCore.Stick(float.NaN,float.PositiveInfinity,.2f,out x,out y); Assert(x==0&&y==0,"Nonfinite sticks sanitized.");
        config.Bindings["Skill"]=PadButton.LB|PadButton.Y;
        core.Step(Sample(index:1),true); core.Step(Sample((uint)PadButton.LB,index:1),true); Assert(!core.Action("Skill",1),"Partial chord not active.");
        core.Step(Sample((uint)(PadButton.LB|PadButton.Y),index:1),true); Assert(core.Action("Skill",1)&&core.Action("Skill",0),"Full chord active once.");
        core.Step(Sample((uint)PadButton.LB,index:1),true); Assert(core.Action("Skill",2),"Releasing part of chord produces up.");
        config.Bindings["Skill"]=PadButton.None; Assert(!core.Action("Skill",0),"Unbound action stays off.");
        Assert(NativeInput.Axis(short.MinValue)==-1&&NativeInput.Axis(short.MaxValue)==1,"Asymmetric native stick endpoints normalize correctly.");
        Assert(Marshal.SizeOf(typeof(NativeInput.Gamepad))==12&&Marshal.SizeOf(typeof(NativeInput.XState))==16,"XInput ABI layouts.");
        Assert(Marshal.SizeOf(typeof(NativeInput.WinInput))==(IntPtr.Size==4?28:40),"Windows INPUT layout for process bitness.");
        Assert(Marshal.OffsetOf(typeof(NativeInput.WinInput),"Union").ToInt32()==(IntPtr.Size==4?4:8),"Union alignment.");
        float remX=0,remY=0; int moved=0,mx,my;
        for(int i=0;i<100;i++) { NativeInput.CursorMotion(.01f,0,.001f,850,ref remX,ref remY,out mx,out my); moved+=mx; }
        Assert(moved==0&&remX>.84f,"Subpixel motion accumulates without jumping prematurely.");
        for(int i=0;i<100;i++) { NativeInput.CursorMotion(.01f,0,.001f,850,ref remX,ref remY,out mx,out my); moved+=mx; }
        Assert(moved==1&&remX>.69f,"Slow analog cursor remains responsive at high frame rates.");
        string folder=Path.Combine(Path.GetTempPath(),"controller-smoke-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,"settings.ini");
        config.Bindings["Skill"]=PadButton.LB|PadButton.Y; config.MoveDeadzone=float.NaN; config.LookSpeed=100000; config.Save(path);
        var loaded=Settings.Load(path);
        Assert(loaded.Bindings["Skill"]==(PadButton.LB|PadButton.Y),"Chord configuration round trip.");
        Assert(loaded.MoveDeadzone==.2f&&loaded.LookSpeed==720,"Configuration bounded and nonfinite-safe.");
        loaded.Save(path); Assert(File.Exists(path+".previous"),"Atomic settings backup.");
        File.WriteAllText(path,"LookSpeed=Infinity\nbind.Jump=garbage\nbind.Fire1=4294967295\nMoveDeadzone=-10\nController=7\n");
        loaded=Settings.Load(path); Assert(loaded.LookSpeed==180&&loaded.MoveDeadzone==.05f&&loaded.Controller==3,"Malformed configuration fails safely.");
        Assert(loaded.Bindings["Jump"]==PadButton.A&&loaded.Bindings["Fire1"]==PadButton.RT,"Invalid bindings preserve defaults.");
        // Only files created by this test are removed, without recursive deletion.
        File.Delete(path); File.Delete(path+".previous"); Directory.Delete(folder);
        var live=NativeInput.Poll(-1,-1); Console.WriteLine("Read-only hardware detection: "+(live.Connected?"XInput slot "+(live.Index+1):"no Xbox controller connected"));
        Console.WriteLine("PASS: "+checks+" controller input, persistence and native ABI checks.");
    }
}
