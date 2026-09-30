using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

internal static partial class Scenarios
{
    sealed class ServicePeer
    {
        internal Rig Rig;
        internal ModuleBus Bus;
        internal object Arm;
        internal Rig ArmRig;
        internal long Seq, Epoch=1;
        internal string ArmName="Arm 1";
        internal MyIni Last=new();
        internal ServicePeer(Rig rig,ModuleBus bus,object arm,Rig armRig)
        { Rig=rig; Bus=bus; Arm=arm; ArmRig=armRig; }
        internal MyIni Packet(string op)
        {
            var p=new MyIni(); p.Set("Link","Version",4); p.Set("Link","Service",1); p.Set("Link","Arm",ArmName);
            p.Set("Link","Epoch",Epoch); p.Set("Link","Sequence",++Seq); p.Set("Link","Operation",op);
            p.Set("Link","Seen",op=="HELLO"?0:Last.Get("Link","Sequence").ToInt64());
            p.Set("Link","Remote",op=="HELLO"?0:Last.Get("Link","Epoch").ToInt64()); p.Set("Link","Generation",Last.Get("Link","Generation").ToInt64()); return p;
        }
        internal MyIni Send(MyIni p)
        {
            Rig.IGC.SendUnicastMessage(ArmRig.PB.EntityId,"AutoArm/4",p.ToString());
            InvokeFrame(Arm,ArmRig,"AutoArm/4",UpdateType.IGC,0);
            while(Bus.Queues[Rig.PB.EntityId].Count>0) { Last=new MyIni(); Last.TryParse((string)Bus.Queues[Rig.PB.EntityId].Dequeue().Data); }
            return Last;
        }
        internal MyIni Send(string op)=>Send(Packet(op));
    }
    static void ServiceCases(Type type,Type toolType)
    {
        var rig=Fixtures.Serial(out _,out var piston,out var head,out var pilot);
        RecordProxy.Of(rig.PB).Values["CustomName"]="Arm 1 - Arm PB";
        var bus=new ModuleBus{ArmId=rig.PB.EntityId}; bus.Bind(rig);
        var planners=new[]{new Rig(rig,"Planner A"),new Rig(rig,"Planner B")}; var observer=new Rig(rig,"Observer"); var safety=new Rig(rig,"Safety");
        foreach(var peer in planners.Concat(new[]{observer,safety})) bus.Bind(peer);
        var config=new MyIni(); config.Set("AutoArm","Format",6); config.Set("AutoArm","Arm","Arm 1");
        config.Set("Modules","Peers","Planner A | Plan\nPlanner B | Plan\nObserver | Observe\nSafety | Stop"); RecordProxy.Of(rig.PB).Values["CustomData"]=config.ToString();
        var arm=Start(type,rig); Run(arm,"On");
        var a=new ServicePeer(planners[0],bus,arm,rig); var b=new ServicePeer(planners[1],bus,arm,rig);
        var o=new ServicePeer(observer,bus,arm,rig); var s=new ServicePeer(safety,bus,arm,rig);
        foreach(var p in new[]{a,b,o,s}) { var status=p.Send("HELLO"); Check(status.Get("Link","Ready").ToBoolean()&&status.Get("Link","Service").ToInt32()==1,"Registered peer did not receive independent arm state."); }
        var destination=head.WorldMatrix; destination.Translation+=Vector3D.Forward*.12;
        var denied=o.Packet("PATH"); denied.Set("Link","Waypoints",PCRow(destination)); o.Send(denied);
        Check(o.Last.Get("Link","Error").ToString().Contains("read only")&&Enabled(arm)&&Get(arm,"LocalPath")==null,"Observer altered motion.");
        var malformed=a.Packet("PATH"); malformed.Set("Link","Waypoints",PCRow(destination)+"|bad"); a.Send(malformed);
        Check(a.Last.Get("Link","Error").ToString().Length>0&&Enabled(arm)&&Get(arm,"LocalPath")==null,"Malformed planner path partially changed manual control.");
        var path=a.Packet("PATH"); path.Set("Link","Waypoints",PCRow(destination)); path.Set("Link","Moves","0"); path.Set("Link","Lines","true"); a.Send(path);
        Check(a.Last.Get("Link","Owner").ToInt64()==a.Rig.PB.EntityId&&Get(arm,"LocalPath")!=null,"Planner path failed to obtain exclusive ownership.");
        var active=Get(arm,"LocalPath"); b.Send("STATUS"); var conflict=b.Packet("PATH"); conflict.Set("Link","Waypoints",PCRow(destination)); b.Send(conflict);
        Check(b.Last.Get("Link","Error").ToString().Contains("owned")&&ReferenceEquals(active,Get(arm,"LocalPath")),"Second planner stole a moving arm.");
        o.Send("STATUS"); Check(o.Last.Get("Link","Owner").ToInt64()==a.Rig.PB.EntityId,"Observer state could not follow a changing motion generation.");
        o.Send("STOP"); Check(Enabled(arm)&&Get(arm,"LocalPath")!=null,"Observer obtained stop authority.");
        // Safety stop deliberately does not require the latest motion generation.
        s.Send("STOP"); Check(!Enabled(arm)&&Get(arm,"LocalPath")==null,"Safety peer failed to stop an independently started planner path."); NoVelocity(rig);
        bus.Queues[rig.PB.EntityId].Enqueue(new MyIGCMessage(path.ToString(),"AutoArm/4",a.Rig.PB.EntityId)); InvokeFrame(arm,rig,"AutoArm/4",UpdateType.IGC,0);
        Check(!Enabled(arm)&&Get(arm,"LocalPath")==null,"Replayed planner request crossed Stop.");
        Run(arm,"On"); a.Send("HELLO"); path=a.Packet("PATH"); path.Set("Link","Waypoints",PCRow(destination)); a.Send(path);
        double q=piston.CurrentPosition;
        for(int i=0;i<2400&&Get(arm,"LocalPath")!=null;i++)
        {
            if(i%6==0) a.Send("PING"); InvokeFrame(arm,rig,"",UpdateType.Update1,1d/60);
            q+=piston.Velocity/60; RecordProxy.Of(piston).Values["CurrentPosition"]=(float)q;
            var frame=head.WorldMatrix; frame.Translation=destination.Translation-Vector3D.Forward*(.12-(q-5)); RecordProxy.Of(head).Values["WorldMatrix"]=frame;
        }
        InvokeFrame(arm,rig,"",UpdateType.Update1,1d/60); a.Send("STATUS");
        Check(Enabled(arm)&&Get(arm,"LocalPath")==null&&a.Last.Get("Link","Completed").ToInt32()==1&&a.Last.Get("Link","State").ToString()=="Complete","Peer path did not report measured completion and resume hold: "+a.Last+" / "+string.Join(" | ",rig.Log.TakeLast(3)));
        path=a.Packet("PATH"); path.Set("Link","Waypoints",PCRow(destination)); a.Send(path);
        for(int i=0;i<35;i++) InvokeFrame(arm,rig,"",UpdateType.Update1,1d/60);
        // A goal already reached can finish; use an unreachable distant goal for loss.
        a.Send("STATUS"); destination.Translation+=Vector3D.Forward*2;
        path=a.Packet("PATH"); path.Set("Link","Waypoints",PCRow(destination)); a.Send(path);
        for(int i=0;i<35;i++) InvokeFrame(arm,rig,"",UpdateType.Update1,1d/60);
        Check(!Enabled(arm)&&Get(arm,"LocalPath")==null,"Lost planner heartbeat left arm driving."); NoVelocity(rig);
        Run(arm,"On"); a.Send("HELLO"); path=a.Packet("PATH"); path.Set("Link","Waypoints",PCRow(destination)); a.Send(path); bus.DropArm=true;
        for(int i=0;i<40;i++) { if(i%6==0) a.Send("PING"); InvokeFrame(arm,rig,"",UpdateType.Update1,1d/60); }
        Check(!Enabled(arm)&&Get(arm,"LocalPath")==null,"One-way reply loss with continuing planner heartbeats left arm driving."); NoVelocity(rig); bus.DropArm=false;
        Run(arm,"On"); a.Send("HELLO"); path=a.Packet("PATH"); path.Set("Link","Waypoints",PCRow(destination)); a.Send(path);
        a.Epoch++; a.Seq=0; a.Send("HELLO"); Check(!Enabled(arm)&&Get(arm,"LocalPath")==null,"Planner restart did not revoke its path.");
        var old=a.Packet("PATH"); old.Set("Link","Epoch",a.Epoch-1); old.Set("Link","Waypoints",PCRow(destination)); a.Send(old); Check(!Enabled(arm),"Older peer epoch revived motion.");
        Run(arm,"On"); a.Send("HELLO"); path=a.Packet("PATH"); path.Set("Link","Arm","Arm 2"); path.Set("Link","Waypoints",PCRow(destination)); a.Send(path); Check(Get(arm,"LocalPath")==null,"Cross-arm packet obtained motion ownership.");
        var intruder=new Rig(rig,"Unregistered"); bus.Bind(intruder); bus.Queues[rig.PB.EntityId].Enqueue(new MyIGCMessage(s.Packet("STOP").ToString(),"AutoArm/4",intruder.PB.EntityId)); InvokeFrame(arm,rig,"AutoArm/4",UpdateType.IGC,0); Check(Enabled(arm),"Unregistered peer stopped the arm.");
        // One planner can address two arm owners directly, with independent
        // arm names/epochs/sequences/leases; neither relays through ToolSwap.
        var otherRig=new Rig(rig,"Arm 2 - Arm PB"); var middle=rig.Grid(); var tip=rig.Grid();
        rig.Rotor("Arm 2 - Base - Rotor",rig.Root,middle); rig.Piston("Arm 2 piston",middle,tip,axis:Vector3D.Forward);
        var otherHead=rig.Block<IMyShipDrill>("Arm 2 - Head - Drill",tip,new Vector3D(0,0,-12));
        var oc=new MyIni(); oc.Set("AutoArm","Format",6); oc.Set("AutoArm","Arm","Arm 2"); oc.Set("Modules","Peers","Planner A | Plan"); RecordProxy.Of(otherRig.PB).Values["CustomData"]=oc.ToString(); bus.Bind(otherRig);
        var otherType=Tests.Script(File.ReadAllText(Path.Combine(Tests.Workspace,"AutoArm_Source.txt")).Replace("const string ArmName = \"Arm 1\";","const string ArmName = \"Arm 2\";")); var other=Start(otherType,otherRig); Run(other,"On");
        var secondArm=new ServicePeer(planners[0],bus,other,otherRig){ArmName="Arm 2"}; secondArm.Send("HELLO");
        path=secondArm.Packet("PATH"); var next=otherHead.WorldMatrix; next.Translation+=Vector3D.Forward*.2; path.Set("Link","Waypoints",PCRow(next)); secondArm.Send(path);
        a.Send("HELLO"); path=a.Packet("PATH"); path.Set("Link","Waypoints",PCRow(destination)); a.Send(path);
        Check(Get(arm,"LocalPath")!=null&&Get(other,"LocalPath")!=null,"Shared planner could not lease two independent arms directly.");
        s.Send("HELLO"); s.Send("STOP"); Check(!Enabled(arm)&&Enabled(other)&&Get(other,"LocalPath")!=null,"Arm 1 safety stop affected Arm 2's independent lease."); secondArm.Send("STOP"); NoVelocity(rig);
        // Additional peers coexist with the original tool topology owner.
        var dual=new DualRig(type,toolType); var extra=new Rig(dual.F.Rig,"Tool observer"); dual.Bus.Bind(extra);
        var dc=new MyIni(); dc.TryParse(dual.F.Rig.PB.CustomData); dc.Set("Modules","Peers","Tool observer | Observe"); RecordProxy.Of(dual.F.Rig.PB).Values["CustomData"]=dc.ToString(); DualStart(dual);
        var dp=new ServicePeer(extra,dual.Bus,dual.Arm,dual.F.Rig); dp.Send("HELLO"); Check(dp.Last.Get("Link","Ready").ToBoolean(),"Third PB could not observe a tool-enabled arm.");
        dual.Command("Tool 2"); for(int i=0;i<8;i++) dual.Tick(); dp.Send("STATUS"); Check(dp.Last.Get("Link","Busy").ToBoolean(),"Observer could not see ToolSwap ownership."); DualPlant(dual,"Idle"); for(int i=0;i<12;i++) dual.Tick(); Check(Enabled(dual.Arm),"Additional peer disrupted tool swap resume.");
        Console.WriteLine("Direct peers: independent observer/planner/safety sessions, atomic paths, contention, native solver completion, watchdog, boot/replay/arm isolation and ToolSwap coexistence.");
    }
}
