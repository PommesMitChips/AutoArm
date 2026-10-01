using System.Collections;
using System.Reflection;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

internal static partial class Scenarios
{
    internal static void CollisionCases(Type armType, Type collisionType)
    {
        CollisionOverlayCases(armType);
        CollisionScopeCases(armType);
        CollisionGeometryCases(armType, collisionType);
        CollisionContactCases(collisionType);
        Console.WriteLine($"Collision service: authenticated fresh overlays, bounded null-space preference, blocked advancement, Home/loss/replay refusal, occupied-cell geometry, closing limits and outward escape. PASS ({Tests.Assertions} total assertions).");
    }
    static MyIni LastCheck(ModuleBus bus,long arm,long service)
    {
        foreach(var item in bus.Sent.AsEnumerable().Reverse()) if(item.Source==arm&&item.Target==service)
        { var p=new MyIni();p.TryParse(item.Data);if(p.Get("Link","Operation").ToString()=="CHECK")return p; }
        throw new Exception("No collision CHECK was sent.");
    }
    static MyIni Guide(MyIni check,long seq,string bias,string rows="",bool hold=false,double scale=1)
    {
        var p=new MyIni();p.Set("Link","Version",5);p.Set("Link","Service",1);p.Set("Link","Arm",check.Get("Link","Arm").ToString());
        p.Set("Link","Epoch",91);p.Set("Link","Remote",check.Get("Link","Epoch").ToInt64());p.Set("Link","Sequence",seq);
        p.Set("Link","Seen",check.Get("Link","Sequence").ToInt64());p.Set("Link","Ack",check.Get("Link","Sequence").ToInt64());
        p.Set("Link","Generation",check.Get("Link","Generation").ToInt64());p.Set("Link","Operation",check.Get("Link","Remote").ToInt64()==0?"HELLO":"GUIDE");
        p.Set("Link","Fingerprint",check.Get("Link","Fingerprint").ToString());p.Set("Link","Bias",bias);p.Set("Link","Rows",rows);
        p.Set("Link","Hold",hold);p.Set("Link","Scale",scale);p.Set("Link","Reason","test clearance");return p;
    }
    static void SendGuide(object host,Rig rig,Rig peer,ModuleBus bus,MyIni packet)
    { bus.Queues[rig.PB.EntityId].Enqueue(new MyIGCMessage(packet.ToString(),"AutoArm/5",peer.PB.EntityId));HostFrame(host,rig,"AutoArm/5",0,UpdateType.IGC); }
    static void CollisionOverlayCases(Type type)
    {
        var rig=new Rig();var middle=rig.Grid();var end=rig.Grid();
        var a=rig.Piston("Arm 1 - Base",rig.Root,middle,Vector3D.Zero,Vector3D.Forward);
        var b=rig.Piston("second",middle,end,Vector3D.Forward*5,Vector3D.Forward);
        rig.Block<IMyShipDrill>("Arm 1 - Head",end,Vector3D.Forward*10);var pilot=rig.Cockpit();var peer=new Rig(rig,"Collision PB");
        var bus=new ModuleBus();bus.Bind(rig);bus.Bind(peer);var data=new MyIni();data.Set("global","Format",7);data.Set("Arm 1","Peers","Collision PB | Safety");
        RecordProxy.Of(rig.PB).Values["CustomData"]=data.ToString();var host=Tests.Create(type,rig);HostReady(host,rig,"Arm 1");
        var core=Core(host,"Arm 1");for(int i=0;i<10;i++)HostFrame(host,rig);Check(a.Velocity==0&&b.Velocity==0,"Configured service absence did not hold outputs.");
        long sequence=0,seen=0;string rows="";MyIni latest=new();
        void Tick()
        {
            HostFrame(host,rig);var check=LastCheck(bus,rig.PB.EntityId,peer.PB.EntityId);long id=check.Get("Link","Sequence").ToInt64();
            if(id!=seen){seen=id;latest=Guide(check,++sequence,"0.04 -0.04",rows);SendGuide(host,rig,peer,bus,latest);}
        }
        for(int i=0;i<40;i++)Tick();
        Check(a.Velocity>.02&&b.Velocity<-.02&&Math.Abs(a.Velocity+b.Velocity)<1e-6,"Secondary preference did not reshape redundant pistons with held head.");
        Check(((Vector3D)Get(core,"LastAchievedLinear")!).Length()<1e-6,"Null-space preference leaked substantial head motion.");
        var safety=Get(Get(core,"Services")!,"Safety")!;var requestForBounds=LastCheck(bus,rig.PB.EntityId,peer.PB.EntityId);
        for(int i=0;i<8;i++)Tick();requestForBounds=LastCheck(bus,rig.PB.EntityId,peer.PB.EntityId);
        // Feed the next fresh acknowledgement rather than a previously consumed one.
        for(int i=0;i<7;i++)HostFrame(host,rig);
        requestForBounds=LastCheck(bus,rig.PB.EntityId,peer.PB.EntityId);
        SendGuide(host,rig,peer,bus,Guide(requestForBounds,++sequence,"0 0","1 0 0.08\n0 -1 -0.02"));
        var conflicting=new double[]{.1,.1};var receiver=PackedProgramChecks.Unwrap(safety);
        receiver.GetType().GetMethod(PackedProgramChecks.Name(receiver.GetType(),"Limit"),All)!.Invoke(receiver,new object[]{conflicting});
        Check(conflicting.All(rate=>rate==0),"Scaling for one constraint violated another positive escape requirement.");
        rows="";for(int i=0;i<8;i++)Tick();
        var held=(Vector3D)Get(core,"TargetP")!;rows="1 1 0";RecordProxy.Of(pilot).Values["MoveIndicator"]=new Vector3(0,0,1);
        for(int i=0;i<24;i++)Tick();
        Check(a.Velocity+b.Velocity>=-1e-7,"Clearance constraint admitted closing motion.");
        Check(((Vector3D)Get(core,"TargetP")!-held).Length()<.003,"Blocked pilot advanced the held target.");
        RecordProxy.Of(pilot).Values["MoveIndicator"]=Vector3.Zero;
        // Replaying an accepted reply with a new sequence cannot renew its lease.
        var replay=new MyIni();replay.TryParse(latest.ToString());replay.Set("Link","Sequence",++sequence);
        for(int i=0;i<24;i++){SendGuide(host,rig,peer,bus,replay);HostFrame(host,rig);}
        Check(a.Velocity==0&&b.Velocity==0&&Enabled(core),"Consumed acknowledgement/replay kept collision guidance alive or canceled control.");
        var request=LastCheck(bus,rig.PB.EntityId,peer.PB.EntityId);var bad=Guide(request,++sequence,"NaN 0");SendGuide(host,rig,peer,bus,bad);
        Check(a.Velocity==0&&b.Velocity==0,"Malformed guidance resumed motion.");
        var wrong=Guide(request,++sequence,"0.04 -0.04");wrong.Set("Link","Fingerprint","wrong topology");SendGuide(host,rig,peer,bus,wrong);
        Check(a.Velocity==0&&b.Velocity==0,"Wrong topology guidance resumed motion.");
        HostFrame(host,rig,"SetHome");RecordProxy.Of(a).Values["CurrentPosition"]=4.8f;HostFrame(host,rig,"GoHome");
        for(int i=0;i<12;i++)HostFrame(host,rig);Check(a.Velocity==0&&b.Velocity==0,"Home bypassed expired collision permission.");
        HostFrame(host,rig,"StopAll");
    }
    static void GridCells(IMyCubeGrid grid,HashSet<Vector3I> cells,Vector3D origin)
    {
        var proxy=RecordProxy.Of(grid);proxy.Values["Min"]=new Vector3I(cells.Min(c=>c.X),cells.Min(c=>c.Y),cells.Min(c=>c.Z));
        proxy.Values["Max"]=new Vector3I(cells.Max(c=>c.X),cells.Max(c=>c.Y),cells.Max(c=>c.Z));proxy.Values["WorldMatrix"]=MatrixD.CreateTranslation(origin);
        proxy.Call=(m,args)=>m.Name=="CubeExists"?cells.Contains((Vector3I)args![0]!):m.Name=="GridIntegerToWorld"?Vector3D.Transform((Vector3D)(Vector3I)args![0]!*grid.GridSize,grid.WorldMatrix):null;
    }
    static void CollisionScopeCases(Type type)
    {
        var rig=MultiFixture(out _,out var piston,out var pilot);var peer=new Rig(rig,"Collision PB");var bus=new ModuleBus();bus.Bind(rig);bus.Bind(peer);
        var data=new MyIni();data.TryParse(rig.PB.CustomData);data.Set("global","Peers","Collision PB | Safety");RecordProxy.Of(rig.PB).Values["CustomData"]=data.ToString();
        var host=Tests.Create(type,rig);HostReady(host,rig,"Arm 1");HostReady(host,rig,"Arm 2");
        var seen=new Dictionary<string,long>();var sequences=new Dictionary<string,long>();
        for(int i=0;i<60;i++)
        {
            HostFrame(host,rig);
            foreach(string name in new[]{"Arm 1","Arm 2"})
            {
                var check=new MyIni();foreach(var item in bus.Sent.AsEnumerable().Reverse())
                {var p=new MyIni();p.TryParse(item.Data);if(p.Get("Link","Operation").ToString()=="CHECK"&&p.Get("Link","Arm").ToString()==name){check=p;break;}}
                long seq=check.Get("Link","Sequence").ToInt64();if(seq==0||seen.GetValueOrDefault(name)==seq)continue;seen[name]=seq;
                long next=sequences.GetValueOrDefault(name)+1;sequences[name]=next;SendGuide(host,rig,peer,bus,Guide(check,next,"0 0",hold:name=="Arm 1"));
            }
        }
        var a=Get(Get(Core(host,"Arm 1"),"Services")!,"Safety")!;var b=Get(Get(Core(host,"Arm 2"),"Services")!,"Safety")!;
        Check((bool)Get(a,"Ready")!&&(bool)Get(a,"Blocked")!&&(bool)Get(b,"Ready")!&&!(bool)Get(b,"Blocked")!,"Shared peer mixed arm-specific collision permission.");
        HostFrame(host,rig,"Select Arm 2");RecordProxy.Of(pilot).Values["MoveIndicator"]=new Vector3(0,0,-1);for(int i=0;i<6;i++)HostFrame(host,rig);
        Check(piston.Velocity!=0,"Another arm's collision hold blocked selected safe arm.");
        HostFrame(host,rig,"StopAll");
    }
    static void CollisionGeometryCases(Type armType,Type collisionType)
    {
        var rig=new Rig();var moving=rig.Grid();var piston=rig.Piston("Arm 1 - Base",rig.Root,moving,Vector3D.Zero,Vector3D.Right);
        var head=rig.Block<IMyShipDrill>("Arm 1 - Head",moving,new Vector3D(4.8,0,0));var pilot=rig.Cockpit();var peer=new Rig(rig,"Collision PB");
        var occupancy=new HashSet<Vector3I>{Vector3I.Zero,new Vector3I(3,0,0)};GridCells(rig.Root,occupancy,Vector3D.Zero);GridCells(moving,new HashSet<Vector3I>{Vector3I.Zero},new Vector3D(4.8,0,0));
        var bus=new ModuleBus();bus.Bind(rig);bus.Bind(peer);
        var data=new MyIni();data.Set("global","Format",7);data.Set("Arm 1","Peers","Collision PB | Safety");RecordProxy.Of(rig.PB).Values["CustomData"]=data.ToString();
        var host=Tests.Create(armType,rig);var service=Tests.Create(collisionType,peer);HostReady(host,rig,"Arm 1");
        void Tick(){HostFrame(service,peer);HostFrame(host,rig);}
        for(int i=0;i<40;i++)Tick();Check((int)((IDictionary)Get(service,"Arms")!).Count==1,"Collision service did not register an authenticated arm.");
        var safety=Get(Get(Core(host,"Arm 1"),"Services")!,"Safety")!;
        Check((bool)Get(safety,"Ready")!&&!(bool)Get(safety,"Blocked")!,"Occupied-cell clearance geometry did not produce fresh guidance: "+string.Join(" | ",peer.Log.TakeLast(3)));
        RecordProxy.Of(pilot).Values["MoveIndicator"]=new Vector3(1,0,0);
        float maximum=0;for(int i=0;i<24;i++){Tick();maximum=Math.Max(maximum,piston.Velocity);Check(piston.Velocity<=.05001,"Closing motion exceeded clearance velocity limit.");}
        Check(maximum>.005,"Safe approaching motion was unnecessarily prohibited.");
        RecordProxy.Of(pilot).Values["MoveIndicator"]=new Vector3(-1,0,0);for(int i=0;i<40;i++)Tick();
        Check(piston.Velocity<-.05,"Clearance policy blocked safe outward escape.");
        occupancy.Add(new Vector3I(2,0,0));HostFrame(service,peer,"Rescan");for(int i=0;i<20;i++)Tick();
        Check(piston.Velocity==0&&(bool)Get(safety,"Blocked")!,"Rescan did not hold overlapping newly occupied geometry.");
        HostFrame(service,peer,"Off");for(int i=0;i<24;i++)Tick();Check(piston.Velocity==0,"Disabled collision service silently removed protection.");
        occupancy.Remove(new Vector3I(2,0,0));HostFrame(service,peer,"On");for(int i=0;i<30;i++)Tick();
        Check((bool)Get(safety,"Ready")!&&!(bool)Get(safety,"Blocked")!&&piston.Velocity<0,"Service reload epoch did not rehandshake and resume safe motion.");
        peer.Storage=((TestHost)service).Storage;service=Tests.Create(collisionType,peer);for(int i=0;i<30;i++)Tick();
        Check((bool)Get(safety,"Ready")!&&!(bool)Get(safety,"Blocked")!,"Service recompilation did not replace the old epoch.");
        HostFrame(host,rig,"StopAll");
    }
    static void Put(object value,string name,object data)
    {value.GetType().GetField(PackedProgramChecks.Name(value.GetType(),name),All)!.SetValue(value,data);}
    static void CollisionContactCases(Type type)
    {
        var rig=new Rig();var other=rig.Grid();var first=rig.Block<IMyShipMergeBlock>("first",rig.Root);var second=rig.Block<IMyShipMergeBlock>("second",other);
        RecordProxy.Of(first).Values["BlockDefinition"]=ToolSwapFixture.Definition("SmallShipSmallMergeBlock");RecordProxy.Of(second).Values["BlockDefinition"]=ToolSwapFixture.Definition("SmallShipSmallMergeBlock");
        RecordProxy.Of(second).Values["WorldMatrix"]=MatrixD.CreateWorld(Vector3D.Zero,Vector3D.Backward,Vector3D.Down);
        var host=PackedProgramChecks.Unwrap(Tests.Create(type,rig));var owner=host.GetType();
        object Make(string name)=>Activator.CreateInstance(owner.GetNestedType(PackedProgramChecks.Name(owner,name),All)!,true)!;
        object Invoke(string name,params object[] args)=>owner.GetMethod(PackedProgramChecks.Name(owner,name),All)!.Invoke(host,args)!;
        var arm=Make("Arm");var a=Make("GridShape");var b=Make("GridShape");Put(a,"Grid",rig.Root);Put(b,"Grid",other);
        var x=Make("Shape");var y=Make("Shape");Put(x,"Centre",Vector3D.Zero);Put(y,"Centre",Vector3D.Zero);Put(x,"Half",new Vector3D(1.25));Put(y,"Half",new Vector3D(1.25));Put(x,"Tag",first.EntityId);Put(y,"Tag",second.EntityId);
        var pair=Make("ContactPair");Put(pair,"A",first.EntityId);Put(pair,"B",second.EntityId);Put(pair,"Axis","Auto");var contacts=(IList)Get(arm,"Contacts")!;contacts.Add(pair);
        Check((bool)Invoke("Contact",arm,a,x,b,y),"Small merge Up faces were not permitted as a declared pair.");
        RecordProxy.Of(second).Values["WorldMatrix"]=MatrixD.Identity;Check(!(bool)Invoke("Contact",arm,a,x,b,y),"Non-facing declared merges were exempted.");
        contacts.Clear();var rotor=rig.Block<IMyMotorStator>("mount",other);pair=Make("ContactPair");Put(pair,"A",0L);Put(pair,"B",rotor.EntityId);Put(pair,"Axis","Auto");contacts.Add(pair);
        Put(arm,"Socket",true);Put(arm,"TipGrid",rig.Root.EntityId);Put(arm,"TipCell",Vector3I.Zero);Put(arm,"TipUp",Vector3D.Up);Put(y,"Tag",rotor.EntityId);
        Put(x,"Half",new Vector3D(3.75,1.25,1.25));Put(x,"Centre",new Vector3D(2.5,0,0));
        Check(!(bool)Invoke("Contact",arm,a,x,b,y),"Socket contact exempted an entire coalesced armour run.");
        ((IList)Get(a,"Shapes")!).Add(x);var split=(IList)Invoke("BodyShapes",arm,a);
        Check(split.Count==2&&(bool)Invoke("Contact",arm,a,split[0]!,b,y)&&!(bool)Invoke("Contact",arm,a,split[1]!,b,y),"Socket splitting did not restrict allowance to the hidden top cell.");
        Put(arm,"TipUp",Vector3D.Down);Check(!(bool)Invoke("Contact",arm,a,split[0]!,b,y),"Opposite rotor shaft direction was exempted.");
    }
}
