using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;

internal static partial class Scenarios
{
    static void SwapLostPointer(DualRig d,int index,bool lost)
    { SwapFixturePointer(d.F,index,lost); }
    static void SwapFixturePointer(ToolSwapFixture f,int index,bool lost)
    {
        var c=RecordProxy.Of(f.Couplers[index]).Values;
        c["IsAttached"]=!lost;c["Top"]=lost?null:f.ArmTip;c["TopGrid"]=lost?null:f.ArmGrid;
        RecordProxy.Of(f.ArmTip).Values["Base"]=lost?null:f.Couplers[index];
    }
    static void SwapField(object value,string key,object setting)=>value.GetType().GetField(key,All)!.SetValue(value,setting);
    static void SwapTransitionCases(Type armType,Type toolType)
    {
        var park=new DualRig(armType,toolType);DualStart(park);park.Command("Tool 2");DualPlant(park,"Dock");park.F.LockSource(1);
        SwapLostPointer(park,0,true);
        for(int i=0;i<5;i++)park.Tick();
        Check(SwapPhase(park.Tool)=="Lock"&&!park.F.AnyDrive&&!park.F.Mutations.Any(m=>m.Kind=="Detach"),"Initial merge pointer gap required premature support flags.");
        park.F.LockSource(1);for(int i=0;i<5;i++)park.Tick();
        Check(SwapPhase(park.Tool)=="Lock"&&!park.F.Mutations.Any(m=>m.Kind=="Detach"),"Partial merge support allowed detach.");
        park.F.LockSource();SwapLostPointer(park,0,false);
        for(int i=0;i<5;i++)park.Tick();
        Check(!park.F.Mutations.Any(m=>m.Kind=="Detach"),"Parking skipped continuous attachment/support settling.");
        SwapLostPointer(park,0,true);park.Tick();SwapLostPointer(park,0,false);
        for(int i=0;i<5;i++)park.Tick();
        Check(!park.F.Mutations.Any(m=>m.Kind=="Detach"),"A false attachment sample did not reset the settling interval.");
        DualPlant(park,"Idle");for(int i=0;i<20;i++)park.Tick();
        Check(Enabled(park.Arm)&&park.F.Couplers[1].Top==park.F.ArmTip,"Parking refresh did not continue the original swap automatically.");
        foreach(string outcome in new[]{"Restore","Missing","WrongTop","Owner"})
        {
            var d=new DualRig(armType,toolType,true);DualStart(d);d.Command("Tool 2");DualPlant(d,"Release");
            d.F.SplitTool(1);SwapLostPointer(d,1,true);
            for(int i=0;i<5;i++)d.Tick();
            Check(SwapPhase(d.Tool)=="Release"&&!d.F.AnyDrive,"Expected unmerge pointer gap stopped or drove the arm.");
            var proxy=RecordProxy.Of(d.F.Couplers[1]).Values;
            if(outcome=="Restore")
            {
                SwapLostPointer(d,1,false);for(int i=0;i<5;i++)d.Tick();
                Check(SwapPhase(d.Tool)=="Release","Unmerge resumed before continuous reciprocal attachment confirmation.");
                SwapLostPointer(d,1,true);d.Tick();SwapLostPointer(d,1,false);
                for(int i=0;i<5;i++)d.Tick();Check(SwapPhase(d.Tool)=="Release","Unmerge settling interval survived a false sample.");
                DualPlant(d,"Idle");for(int i=0;i<20;i++)d.Tick();
                Check(Enabled(d.Arm)&&d.F.Couplers[1].Top==d.F.ArmTip,"Pickup did not resume ON after attachment refreshed.");
                Check(d.ToolRig.Log.Any(s=>s.Contains("arm ON")),"Tool status never confirmed actual ON.");
            }
            else
            {
                if(outcome=="WrongTop")proxy["Top"]=d.F.WrongArmPart();
                if(outcome=="Owner")RecordProxy.Of(d.F.ArmTip).Values["Base"]=d.F.Couplers[0];
                for(int i=0;i<140;i++)d.Tick();
                Check(SwapPhase(d.Tool)=="Idle"&&!Enabled(d.Arm)&&!d.F.AnyDrive,"Unmerge refresh accepted permanent/contradictory "+outcome);
            }
        }
        var lost=new DualRig(armType,toolType,true);DualStart(lost);bool dropped=false;
        var channel=RecordProxy.Of(lost.ToolRig.IGC);var send=channel.Call;
        channel.Call=(method,args)=>
        {
            if(method.Name=="SendUnicastMessage"&&!dropped)
            { var packet=new MyIni();packet.TryParse((string)args![2]!);if(packet.Get("Link","Operation").ToString()=="RESUME"){dropped=true;return true;} }
            return send!(method,args);
        };
        lost.Command("Tool 2");for(int i=0;i<8;i++)lost.Tick();DualPlant(lost,"Idle");for(int i=0;i<120;i++)lost.Tick();
        Check(dropped&&Enabled(lost.Arm)&&lost.ToolRig.Log.Any(s=>s.Contains("arm ON")),"A lost handoff left both PBs waiting without retry/confirmation: "+string.Join(" | ",lost.F.Rig.Log.TakeLast(4))+" / "+string.Join(" | ",lost.ToolRig.Log.TakeLast(4))+"; requested="+Get(lost.Tool,"ResumeRequested")+", acquired="+Get(lost.Tool,"Acquired")+", retry="+Get(lost.Tool,"ResumeRetries"));
        var delayed=new DualRig(armType,toolType,true);DualStart(delayed);bool suppress=true,began=false,changed=false;int frames=0;
        var replies=RecordProxy.Of(delayed.F.Rig.IGC);var reply=replies.Call;
        replies.Call=(method,args)=>
        {
            if(method.Name=="SendUnicastMessage"&&suppress)
            { var packet=new MyIni();packet.TryParse((string)args![2]!);if(packet.Get("Link","Operation").ToString()=="STATE"&&!packet.Get("Link","Lease").ToBoolean()){began=true;return true;} }
            return reply!(method,args);
        };
        delayed.Command("Tool 2");for(int i=0;i<8;i++)delayed.Tick();DualPlant(delayed,"Idle");
        for(int i=0;i<100;i++)
        {
            if(began&&++frames>=20)suppress=false;
            delayed.Tick();
            if(!changed&&Enabled(delayed.Arm)) { delayed.Command("MoveTo 1 2 3");changed=true; }
        }
        Check(began&&changed&&(int)Get(delayed.Tool,"ResumeRetries")!>0&&Enabled(delayed.Arm),"Lost confirmation made a duplicate handoff stop the enabled arm.");
        VCNear(VCVector(delayed.Arm,"TargetP"),new VRageMath.Vector3D(1,2,3),"Duplicate handoff recaptured an already-active target");
        Check(delayed.ToolRig.Log.Any(s=>s.Contains("arm ON")),"Delayed handoff confirmation never reported actual ON.");
        var stale=new DualRig(armType,toolType);DualStart(stale);var staleClient=Get(stale.Arm,"Tools")!;
        SwapField(staleClient,"Lease",true);SwapField(staleClient,"Resumed",false);SwapField(staleClient,"Error","historical stop");
        Tests.Call(stale.Tool,"EnableControl",false);
        for(int i=0;i<12;i++)stale.Tick();
        Check(Enabled(stale.Arm)&&stale.ToolRig.Log.Any(s=>s.Contains("arm ON"))&&!stale.ToolRig.Log.Any(s=>s.Contains("Arm resume failed: historical")),"A prior protocol error poisoned current resume confirmation: "+string.Join(" | ",stale.F.Rig.Log.TakeLast(3))+" / "+string.Join(" | ",stale.ToolRig.Log.TakeLast(4))+"; error="+Get(staleClient,"Error")+", pending="+Get(staleClient,"ResumePending")+", lease="+Get(staleClient,"Lease"));
        stale.Command("Off");Tests.Call(stale.Tool,"EnableControl",true);
        for(int i=0;i<12;i++)stale.Tick();
        Check(!Enabled(stale.Arm)&&!(bool)Get(stale.Tool,"ResumeRequested")!&&stale.ToolRig.Log.Any(s=>s.Contains("Arm resume failed: Off command")),"Off before handoff confirmation restarted the arm or left ToolSwap waiting.");
        var clock=new DualRig(armType,toolType);DualStart(clock);var observer=SwapController(clock.Tool);
        Tests.Call(observer,"Observe",false,0d);
        Check(!(bool)Tests.Call(observer,"Observe",true,.25)!,"First true sample credited an unobserved settling interval.");
        Check((bool)Tests.Call(observer,"Observe",true,.25)!,"Consecutive true observations did not settle.");
        foreach(string failure in new[]{"StaleError","Topology","Permit","Budget","Geometry"})
        {
            var d=new DualRig(armType,toolType);DualStart(d);Tests.Call(d.Arm,"StopMotion","");
            var client=Get(d.Arm,"Tools")!;
            SwapField(client,"ResumePending",true);SwapField(client,"ResumeElapsed",0d);SwapField(client,"Lease",false);SwapField(client,"Rebuilding",false);SwapField(client,"Transition",false);
            SwapField(client,"Error","old failure");
            if(failure=="Topology")SwapField(Get(d.Arm,"Topology")!,"Ready",false);
            if(failure=="Permit")SwapField(client,"Permitted",false);
            if(failure=="Budget")RecordProxy.Of(d.F.Rig.Runtime).Values["CurrentInstructionCount"]=16000;
            if(failure=="Geometry")RecordProxy.Of(d.F.Base).Values["IsAttached"]=false;
            for(int i=0;i<140;i++)Tests.Call(client,"Tick",1d/60,1d/60);
            if(failure=="StaleError")Check(Enabled(d.Arm)&&(string)Get(client,"Error")! =="","Successful handoff retained an old stop reason.");
            else Check(!Enabled(d.Arm)&&!(bool)Get(client,"ResumePending")!&&((string)Get(client,"Error")!).Length>0,"Resume remained pending or claimed ON after "+failure);
        }
        Console.WriteLine("Swap transitions: staggered merge flags, continuous stopped connection settling, unmerge refresh, contradictory/permanent loss, handoff retry and bounded resume failure.");
    }
}
