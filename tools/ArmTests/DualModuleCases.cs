using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

internal sealed class ModuleBus
{
    internal readonly Dictionary<long,Queue<MyIGCMessage>> Queues=new();
    internal readonly List<(long Source,long Target,string Data)> Sent=new();
    internal readonly Dictionary<(long Id,string Tag),Queue<MyIGCMessage>> Broadcasts=new();
    internal bool DropArm,DropTool;
    internal long ArmId,ToolId;
    internal void Bind(Rig rig)
    {
        var queue=new Queue<MyIGCMessage>(); Queues[rig.PB.EntityId]=queue;
        var listener=RecordProxy.Make<IMyUnicastListener>(); var proxy=RecordProxy.Of(listener);
        proxy.Call=(m,a)=>
        {
            if(m.Name=="get_HasPendingMessage") return queue.Count>0;
            if(m.Name=="AcceptMessage") return queue.Dequeue();
            return null;
        };
        RecordProxy.Of(rig.IGC).Values["UnicastListener"]=listener;
        RecordProxy.Of(rig.IGC).Call=(m,a)=>
        {
            if(m.Name=="RegisterBroadcastListener")
            {
                string tag=(string)a![0]!; var messages=new Queue<MyIGCMessage>(); Broadcasts[(rig.PB.EntityId,tag)]=messages;
                var receiver=RecordProxy.Make<IMyBroadcastListener>(); RecordProxy.Of(receiver).Call=(method,args)=>method.Name=="get_HasPendingMessage"?messages.Count>0:method.Name=="AcceptMessage"?messages.Dequeue():null;
                return receiver;
            }
            if(m.Name=="SendBroadcastMessage")
            {
                string tag=(string)a![0]!,data=(string)a[1]!;
                foreach(var pair in Broadcasts) if(pair.Key.Tag==tag) pair.Value.Enqueue(new MyIGCMessage(data,tag,rig.PB.EntityId));
                return null;
            }
            if(m.Name=="SendUnicastMessage")
            {
                long target=(long)a![0]!; string tag=(string)a[1]!,data=(string)a[2]!; Sent.Add((rig.PB.EntityId,target,data));
                if(!(rig.PB.EntityId==ArmId && DropArm || rig.PB.EntityId==ToolId && DropTool) && Queues.TryGetValue(target,out var destination))
                    destination.Enqueue(new MyIGCMessage(data,tag,rig.PB.EntityId));
                return m.ReturnType==typeof(bool)?true:null;
            }
            return null;
        };
    }
}
internal sealed class DualRig
{
    internal ToolSwapFixture F;
    internal Rig ToolRig;
    internal object Arm,Tool;
    internal ModuleBus Bus;
    internal DualRig(Type armType,Type toolType,bool bare=false,bool hinge=false,Action<ToolSwapFixture>? customise=null)
    {
        F=new ToolSwapFixture(headless:bare); F.NoNamedArmReference(); if(hinge) F.HingeEnd();
        customise?.Invoke(F);
        var toolData=new MyIni(); toolData.TryParse(F.Rig.PB.CustomData); toolData.Set("AutoArm","Format",1); toolData.Set("Link","ArmPB","Arm 1 - Arm PB");
        RecordProxy.Of(F.Rig.PB).Values["CustomName"]="Arm 1 - Arm PB";
        ToolRig=new Rig(F.Rig,"Arm 1 - ToolSwap PB"); RecordProxy.Of(ToolRig.PB).Values["CustomData"]=toolData.ToString();
        var armData=new MyIni(); armData.Set("AutoArm","Arm","Arm 1"); armData.Set("AutoArm","Format",6); armData.Set("Modules","ToolSwapPB",ToolRig.PB.CustomName);
        RecordProxy.Of(F.Rig.PB).Values["CustomData"]=armData.ToString();
        Bus=new ModuleBus{ArmId=F.Rig.PB.EntityId,ToolId=ToolRig.PB.EntityId}; Bus.Bind(F.Rig); Bus.Bind(ToolRig);
        Arm=Tests.Create(armType,F.Rig); Tool=Tests.Create(toolType,ToolRig); F.Host=(TestHost)Tool;
    }
    internal void Frame(object script,Rig rig,string command="",bool timed=true,double dt=1d/60,UpdateType? callback=null)
    {
        RecordProxy.Actor=script==Arm?"Arm":"ToolSwap";
        RecordProxy.Of(rig.Runtime).Values["TimeSinceLastRun"]=TimeSpan.FromSeconds(dt);
        try { script.GetType().GetMethod("Main")!.Invoke(script,new object[]{command,callback ?? (timed?UpdateType.Update1:UpdateType.Terminal)}); }
        finally { RecordProxy.Actor=""; }
    }
    internal void Tick(double dt=1d/60)
    {
        if(Bus.Queues[F.Rig.PB.EntityId].Count>0) Frame(Arm,F.Rig,"AutoArm/4",false,0,UpdateType.IGC);
        Frame(Arm,F.Rig,dt:dt);
        if(Bus.Queues[ToolRig.PB.EntityId].Count>0) Frame(Tool,ToolRig,"AutoArm/4",false,0,UpdateType.IGC);
        Frame(Tool,ToolRig,dt:dt);
    }
    internal void Command(string command) { Frame(Arm,F.Rig,command,false,0); }
}
internal static partial class Scenarios
{
    static void DualStart(DualRig d)
    {
        d.Command("On");
        for(int i=0;i<400 && !Enabled(d.Arm);i++) d.Tick();
        Check(Enabled(d.Arm),"Dual On failed: ARM "+string.Join(" | ",d.F.Rig.Log.TakeLast(4))+" TOOL "+string.Join(" | ",d.ToolRig.Log.TakeLast(4)));
    }
    static void DualPlant(DualRig d,string until)
    {
        string previous=""; int phaseTicks=0;
        for(int i=0;i<7000;i++)
        {
            string phase=SwapPhase(d.Tool); if(phase==until) return;
            Check(phase!="Idle" || i<50,"Dual operation stopped before "+until+": "+string.Join(" | ",d.ToolRig.Log.TakeLast(4))+" / "+string.Join(" | ",d.F.Rig.Log.TakeLast(4)));
            phaseTicks=phase==previous?phaseTicks+1:0;
            string? field=phase=="ApproachDock"?"Approach":phase=="Dock"?"Dock":phase=="Retreat"?"Retreat":phase=="ApproachTop"?"TopApproach":phase=="AlignTop"?"Attach":null;
            int source=Get(SwapController(d.Tool),"Source") is object s?Array.IndexOf(d.F.Markers,(IMyTerminalBlock)Get(s,"Marker")!):0;
            if(field!=null && phaseTicks>8)
            { var goal=SwapGoal(d.Tool,field); if(phase=="ApproachDock" || phase=="Dock") d.F.MoveSource(goal,source); else d.F.MoveArm(goal); }
            else if(phase=="Lock") d.F.LockSource(source:source);
            else if(phase=="Release") d.F.SplitTool(Array.IndexOf(d.F.Markers,(IMyTerminalBlock)Get(Get(SwapController(d.Tool),"Destination")!,"Marker")!));
            d.Tick(); previous=phase;
        }
        throw new Exception("Dual operation failed to reach "+until+": "+SwapPhase(d.Tool)+" / "+string.Join(" | ",d.ToolRig.Log.TakeLast(4)));
    }
    static void DualModuleCases(Type armType,Type toolType)
    {
        foreach(bool bare in new[]{false,true})
        {
            var d=new DualRig(armType,toolType,bare,bare); DualStart(d);
            var cockpit=d.F.Rig.Blocks.OfType<IMyShipController>().Single(); RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-1); d.Tick();
            Check(d.F.AnyDrive,"Dual manual movement failed."); RecordProxy.Of(cockpit).Values["MoveIndicator"]=Vector3.Zero;
            d.Command("Tool 2"); for(int i=0;i<8;i++) d.Tick(); DualPlant(d,"Idle"); for(int i=0;i<12;i++) d.Tick();
            Check(Enabled(d.Arm) && d.F.Couplers[1].Top==d.F.ArmTip,"Dual swap did not mount/resume: "+string.Join(" | ",d.ToolRig.Log.TakeLast(6))+" / "+string.Join(" | ",d.F.Rig.Log.TakeLast(6)));
            Check(d.F.Mutations.All(m=>m.Stopped && m.Supported),"Dual attachment changed without confirmed stopped arm/support.");
            var paths=d.Bus.Sent.Select(x=>{var ini=new MyIni(); ini.TryParse(x.Data); return ini;}).Where(x=>x.Get("Link","Operation").ToString()=="PATH").ToArray();
            Check(paths.Any(p=>p.Get("Link","Waypoints").ToString().Split('|').Length==2),"ToolSwap did not submit approach/insertion as a whole path.");
            Check(d.Bus.Sent.Any(x=>{var ini=new MyIni(); ini.TryParse(x.Data); return ini.Get("Link","Completed").ToInt32()==1 && ini.Get("Link","Count").ToInt32()==2;}),"Arm did not report intermediate path progress.");
            Check(d.F.Rig.Blocks.All(b=>!RecordProxy.Of(b).ActorWrites.Any(w=>w.Actor=="ToolSwap" && w.Name is "Velocity" or "TargetVelocityRad")),"ToolSwap PB wrote an arm drive.");
            SwapStationUntouched(d.F);
            if(!bare)
            {
                d.Command("Tool 1"); for(int i=0;i<8;i++) d.Tick(); DualPlant(d,"Idle"); for(int i=0;i<12;i++) d.Tick();
                Check(Enabled(d.Arm) && d.F.Couplers[0].Top==d.F.ArmTip,"Reverse dual swap failed: "+string.Join(" | ",d.ToolRig.Log.TakeLast(8))+" / "+string.Join(" | ",d.F.Rig.Log.TakeLast(3)));
            }
        }
        var park=new DualRig(armType,toolType); DualStart(park); park.Command("Park"); for(int i=0;i<8;i++) park.Tick(); DualPlant(park,"Idle"); for(int i=0;i<12;i++) park.Tick();
        Check(!Enabled(park.Arm) && park.F.ArmTip.Base==null,"Dual parking did not finish bare OFF.");
        var timeout=new DualRig(armType,toolType); DualStart(timeout);
        var pilot=timeout.F.Rig.Blocks.OfType<IMyShipController>().Single(); RecordProxy.Of(pilot).Values["MoveIndicator"]=new Vector3(0,0,-1); timeout.Tick(); Check(timeout.F.AnyDrive,"Timeout fixture never drove.");
        timeout.Bus.DropTool=true; for(int i=0;i<45;i++) timeout.Tick(); Check(!Enabled(timeout.Arm) && !timeout.F.AnyDrive,"Lost module heartbeat did not stop manual control.");
        var stopped=new DualRig(armType,toolType,true); stopped.Command("On"); stopped.Command("Stop"); for(int i=0;i<120;i++) stopped.Tick();
        Check(!Enabled(stopped.Arm) && !stopped.F.AnyDrive && stopped.F.Mutations.Count==0,"Stop failed to revoke in-flight startup.");
        foreach(bool toArm in new[]{false,true})
        {
            var lost=new DualRig(armType,toolType); DualStart(lost); lost.Command("Tool 2"); for(int i=0;i<8;i++) lost.Tick(); DualPlant(lost,"ApproachDock");
            for(int i=0;i<8;i++) lost.Tick();
            int mutations=lost.F.Mutations.Count; if(toArm) lost.Bus.DropTool=true; else lost.Bus.DropArm=true;
            for(int i=0;i<50;i++) lost.Tick();
            Check(!Enabled(lost.Arm) && !lost.F.AnyDrive && lost.F.Mutations.Count==mutations,"One-way communication loss permitted motion/attachment.");
        }
        var powered=new DualRig(armType,toolType); DualStart(powered); var pc=powered.F.Rig.Blocks.OfType<IMyShipController>().Single(); RecordProxy.Of(pc).Values["MoveIndicator"]=new Vector3(0,0,-1); powered.Tick();
        RecordProxy.Of(powered.ToolRig.PB).Values["IsFunctional"]=false; for(int i=0;i<50;i++) powered.Tick();
        Check(!Enabled(powered.Arm) && !powered.F.AnyDrive,"An unavailable peer prevented the watchdog from zeroing drives.");
        var replay=new DualRig(armType,toolType); DualStart(replay); replay.Command("Tool 2"); for(int i=0;i<8;i++) replay.Tick(); DualPlant(replay,"ApproachDock"); for(int i=0;i<8;i++) replay.Tick();
        string oldPath=replay.Bus.Sent.Last(x=>{var ini=new MyIni(); ini.TryParse(x.Data); return ini.Get("Link","Operation").ToString()=="PATH";}).Data;
        replay.Command("Stop"); for(int i=0;i<10;i++) replay.Tick(); int prior=replay.F.Mutations.Count;
        replay.Bus.Queues[replay.F.Rig.PB.EntityId].Enqueue(new MyIGCMessage(oldPath,"AutoArm/4",replay.ToolRig.PB.EntityId));
        replay.Bus.Queues[replay.F.Rig.PB.EntityId].Enqueue(new MyIGCMessage(oldPath,"AutoArm/4",999999));
        for(int i=0;i<10;i++) replay.Tick();
        Check(!Enabled(replay.Arm) && !replay.F.AnyDrive && replay.F.Mutations.Count==prior,"A replayed/foreign path crossed the Stop generation fence.");
        var canceled=new DualRig(armType,toolType,true); canceled.F.AutoAttach=false; RecordProxy.Of(canceled.F.Couplers[1]).Values["RotorLock"]=true; DualStart(canceled);
        canceled.Command("Tool 2"); for(int i=0;i<8;i++) canceled.Tick(); DualPlant(canceled,"Attach");
        Check(canceled.F.Couplers[1].PendingAttachment && !canceled.F.Couplers[1].RotorLock,"Pending first attach did not preserve deferred lock restoration.");
        canceled.Command("SwapCancel"); for(int i=0;i<12;i++) canceled.Tick();
        canceled.ToolRig.Storage=((TestHost)canceled.Tool).Storage; canceled.Tool=Tests.Create(toolType,canceled.ToolRig); canceled.F.Host=(TestHost)canceled.Tool;
        for(int i=0;i<250;i++) canceled.Tick();
        Check(!Enabled(canceled.Arm) && canceled.F.Couplers[1].PendingAttachment && !canceled.F.Couplers[1].RotorLock,"Tool PB restart resumed a pending attachment or restored an unsafe lock.");
        canceled.Command("On"); for(int i=0;i<400 && !Enabled(canceled.Arm);i++) canceled.Tick();
        Check(Enabled(canceled.Arm) && !canceled.F.Couplers[1].PendingAttachment && canceled.F.Couplers[1].RotorLock,"One On failed to reconcile a bare pending attachment after Tool PB restart.");
        DualAdversarialCases(armType,toolType);
        ManualBareRecovery(armType,toolType);
        ToolPathRevisionCases(armType,toolType);
        Console.WriteLine("Two-PB integration: mounted/bare On, final hinge, motion ownership, stopped attachment fence, automatic resume, parking, heartbeat timeout and in-flight Stop.");
    }
    static void ManualBareRecovery(Type armType,Type toolType)
    {
        foreach(string mode in new[]{"Active","FailedSwap","Stop","Off","SwapCancel","ToolStop","BodyBroken","Pending","MissingPart"})
        {
            var d=new DualRig(armType,toolType); DualStart(d);
            if(mode=="FailedSwap")
            {
                d.Command("Tool 2"); for(int i=0;i<8;i++) d.Tick(); DualPlant(d,"ApproachDock");
                Tests.Call(d.Arm,"Fault","Tool operation failed."); for(int i=0;i<20;i++) d.Tick();
            }
            else if(mode=="Stop"||mode=="Off"||mode=="SwapCancel") { d.Command(mode); for(int i=0;i<20;i++) d.Tick(); }
            else if(mode=="ToolStop") { d.Frame(d.Tool,d.ToolRig,"Stop",false,0); for(int i=0;i<50;i++) d.Tick(); }
            d.F.LockSource(); d.F.Couplers[0].Detach();
            if(mode=="BodyBroken") { RecordProxy.Of(d.F.Piston).Values["IsAttached"]=false; RecordProxy.Of(d.F.Piston).Values["Top"]=null; }
            if(mode=="Pending") RecordProxy.Of(d.F.Couplers[0]).Values["PendingAttachment"]=true;
            if(mode=="MissingPart") d.F.NoPart();
            for(int i=0;i<400;i++) d.Tick();
            bool expected=mode=="Active"||mode=="FailedSwap";
            Check(Enabled(d.Arm)==expected,"Manual bare-arm recovery mishandled "+mode+": "+string.Join(" | ",d.F.Rig.Log.TakeLast(3)));
            Check(d.F.Couplers[1].Top==null&&!d.F.Mutations.Any(m=>m.Kind=="Attach"),"Manual recovery resumed the canceled tool pickup.");
            if(expected)
            {
                Check(((VRage.Game.ModAPI.Ingame.IMyCubeBlock)Get(Get(d.Arm,"Topology")!,"End")!).CubeGrid==d.F.ArmGrid,"Bare recovery retained the parked tool endpoint.");
                var pilot=d.F.Rig.Blocks.OfType<IMyShipController>().Single(); RecordProxy.Of(pilot).Values["MoveIndicator"]=new Vector3(0,0,-1);
                for(int i=0;i<12;i++) d.Tick();
                Check(((Vector3D)Get(d.Arm,"LastPilotLinear")!).Length()>.01,"Bare recovery lost cockpit responsiveness.");
                RecordProxy.Of(pilot).Values["MoveIndicator"]=Vector3.Zero; d.Command("Stop");
            }
            NoVelocity(d.F.Rig);
        }
    }
    static void DualAdversarialCases(Type armType,Type toolType)
    {
        foreach(bool largePart in new[]{false,true}) foreach(bool flipped in new[]{false,true}) foreach(bool locked in new[]{false,true})
        {
            var d=new DualRig(armType,toolType,true,customise:f=>
            {
                if(largePart) f.LargeSmallGridPart(flipped);
                else if(flipped) { var pose=f.ArmTip.WorldMatrix; RecordProxy.Of(f.ArmTip).Values["WorldMatrix"]=MatrixD.CreateWorld(pose.Translation,pose.Up,pose.Forward); }
                RecordProxy.Of(f.Couplers[1]).Values["RotorLock"]=locked;
            });
            // One-cell rotor azimuth is free, but its shaft axis remains defined by the mounting face.
            if(!largePart && flipped) { var pose=d.F.ArmTip.WorldMatrix; RecordProxy.Of(d.F.ArmTip).Values["WorldMatrix"]=MatrixD.CreateWorld(pose.Translation,Vector3D.Right,Vector3D.Up); }
            DualStart(d); d.Command("Tool 2"); for(int i=0;i<8;i++) d.Tick(); DualPlant(d,"Idle"); for(int i=0;i<12;i++) d.Tick();
            Check(Enabled(d.Arm) && d.F.Couplers[1].Top==d.F.ArmTip && d.F.Couplers[1].RotorLock==locked,"Signed/multi-cell/locked fresh pickup failed across PB boundary.");
        }
        foreach(string fault in new[]{"NoPart","Multiple","Face","HingeBase","NonfiniteConfig"})
        {
            var d=new DualRig(armType,toolType,true,customise:f=>
            { if(fault=="NoPart") f.NoPart(); else if(fault=="Multiple") f.WrongArmPart(); else if(fault=="Face") f.ExtraFace(); else if(fault=="HingeBase") RecordProxy.Of(f.Couplers[1]).Values["BlockDefinition"]=ToolSwapFixture.Definition("LargeHinge"); });
            if(fault=="NonfiniteConfig") { var ini=new MyIni(); ini.TryParse(d.ToolRig.PB.CustomData); ini.Set("Tools","MoveSpeed","NaN"); RecordProxy.Of(d.ToolRig.PB).Values["CustomData"]=ini.ToString(); }
            d.Command("On"); for(int i=0;i<180;i++) d.Tick();
            Check(!Enabled(d.Arm) && !d.F.AnyDrive && d.F.Mutations.Count==0,"Invalid equipment enabled through split controller: "+fault);
        }
        foreach(string wrong in new[]{"Cell","Axis","Type","NaN"})
        {
            var d=new DualRig(armType,toolType,true); DualStart(d); if(wrong=="Cell") d.F.AttachResult=d.F.WrongArmPart();
            d.Command("Tool 2"); for(int i=0;i<8;i++) d.Tick(); DualPlant(d,"Attach");
            if(wrong=="Axis") { var pose=d.F.ArmTip.WorldMatrix; RecordProxy.Of(d.F.ArmTip).Values["WorldMatrix"]=MatrixD.CreateWorld(pose.Translation,pose.Forward,-pose.Up); }
            if(wrong=="Type") RecordProxy.Of(d.F.ArmTip).Values["BlockDefinition"]=ToolSwapFixture.Definition("LargeHingeHead");
            if(wrong=="NaN") { var pose=d.F.ArmTip.WorldMatrix; pose.M11=double.NaN; RecordProxy.Of(d.F.ArmTip).Values["WorldMatrix"]=pose; }
            for(int i=0;i<25;i++) d.Tick();
            Check(!Enabled(d.Arm) && !d.F.AnyDrive && d.F.Heads[1].All(m=>m.Enabled),"Split controller released support for wrong observed top: "+wrong);
        }
        var info=new DualRig(armType,toolType); DualStart(info); info.Command("Tool 2"); for(int i=0;i<8;i++) info.Tick(); DualPlant(info,"ApproachDock"); for(int i=0;i<10;i++) info.Tick();
        long pathId=(long)Get(Get(Get(info.Arm,"Tools")!,"Path")!,"Id")!; info.Command("ToolInfo"); for(int i=0;i<5;i++) info.Tick();
        Check((long)Get(Get(Get(info.Arm,"Tools")!,"Path")!,"Id")! ==pathId && info.F.Rig.Log.Last().Contains("Tools"),"Read-only ToolInfo restarted an in-flight path or failed to report to arm PB.");
    }
    static void ToolPathRevisionCases(Type armType,Type toolType)
    {
        var speed=new DualRig(armType,toolType); var ini=new MyIni(); ini.TryParse(speed.F.Rig.PB.CustomData); ini.Set("Config","HeadSpeed",2); ini.Set("Config","HeadTurnSpeed",12); RecordProxy.Of(speed.F.Rig.PB).Values["CustomData"]=ini.ToString();
        DualStart(speed); speed.Command("Tool 2"); for(int i=0;i<8;i++) speed.Tick(); DualPlant(speed,"ApproachDock"); for(int i=0;i<12;i++) speed.Tick();
        var path=Get(Get(speed.Arm,"Tools")!,"Path")!;
        Check(((double[])Get(path,"Moves")!)[0]==2 && ((double[])Get(path,"Turns")!)[0]==12,"Travel did not inherit arm movement defaults.");
        Check(((double[])Get(path,"Moves")!)[1]==.05 && ((bool[])Get(path,"Lines")!)[1],"Final approach lost its slow straight-line policy.");
        var displaced=speed.F.Markers[0].WorldMatrix; displaced.Translation+=Vector3D.Up*4; speed.F.MoveSource(displaced); for(int i=0;i<3;i++) speed.Tick();
        Check(((VRageMath.Vector3D)Get(speed.Arm,"LastRequestedLinear")!).Length()>1,"Manual correction cap still imposed a hidden travel-speed ceiling.");
        speed.Command("Stop"); for(int i=0;i<10;i++) speed.Tick();
        var parked=new DualRig(armType,toolType,true,customise:f=>f.LockSource());
        DualStart(parked);
        var initial=parked.F.Markers[1].WorldMatrix * MatrixD.Invert(parked.F.Stands[1][0].WorldMatrix);
        Check(((TestHost)parked.Tool).Storage.Contains("AutoArm Park Poses"),"All-parked startup did not record return poses.");
        parked.Command("Tool 2"); for(int i=0;i<8;i++) parked.Tick(); DualPlant(parked,"Idle"); for(int i=0;i<12;i++) parked.Tick();
        parked.ToolRig.Storage=((TestHost)parked.Tool).Storage; parked.Tool=Tests.Create(toolType,parked.ToolRig); parked.F.Host=(TestHost)parked.Tool;
        for(int i=0;i<180;i++) parked.Tick(); parked.Command("On"); for(int i=0;i<300 && !Enabled(parked.Arm);i++) parked.Tick();
        var changed=parked.F.Markers[1].WorldMatrix; changed=MatrixD.CreateWorld(changed.Translation,Vector3D.Cross(changed.Up,changed.Forward),changed.Up); parked.F.MoveSource(changed,1);
        parked.Command("Tool 1"); for(int i=0;i<8;i++) parked.Tick(); DualPlant(parked,"ApproachDock");
        var returned=SwapGoal(parked.Tool,"Dock") * MatrixD.Invert(parked.F.Stands[1][0].WorldMatrix);
        VCNear(returned.Forward,initial.Forward,"Return preserved recorded parked forward",1e-6); VCNear(returned.Up,initial.Up,"Return preserved recorded parked up",1e-6); VCNear(returned.Translation,initial.Translation,"Return preserved parked relative position",1e-6);
        var active=Get(parked.Arm,"Tools")!; for(int i=0;i<12;i++) parked.Tick();
        parked.Command("ToolInfo"); for(int i=0;i<5;i++) parked.Tick();
        Check((bool)Get(active,"Busy")!,"Read-only status cleared tool ownership.");
        DualPlant(parked,"Lock");
        Check(!(bool)Get(Get(parked.Arm,"Topology")!,"Ready")! && (bool)Get(active,"Transition")!,"Expected merge did not pause stale topology.");
        DualPlant(parked,"Idle"); for(int i=0;i<12;i++) parked.Tick();
        Check(Enabled(parked.Arm),"Expected topology transitions required another On to finish the swap.");
        Console.WriteLine("Tool revision: inherited travel speeds, slow linear entry, recorded parked pose across restart and expected-topology resume.");
    }
}
