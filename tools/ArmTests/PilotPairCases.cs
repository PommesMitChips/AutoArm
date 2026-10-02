using System.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;
internal static partial class Scenarios
{
    internal static void PilotPairCases(Type type)
    {
        var rig=MultiFixture(out _,out _,out var one);var two=rig.Cockpit();
        // Deliberately duplicate default names: matching never depends on names.
        RecordProxy.Of(two).Values["WorldMatrix"]=MatrixD.CreateWorld(new(20,0,0),Vector3D.Forward,Vector3D.Right);
        var host=Tests.Create(type,rig);HostReady(host,rig,"Arm 1");HostReady(host,rig,"Arm 2");
        var a=Core(host,"Arm 1");var b=Core(host,"Arm 2");
        RecordProxy.Of(one).Values["MoveIndicator"]=new Vector3(0,0,-1);RecordProxy.Of(two).Values["MoveIndicator"]=new Vector3(0,0,-1);
        for(int i=0;i<15;i++)HostFrame(host,rig);
        Check(((Vector3D)Get(a,"LastPilotLinear")!).Length()==0,"Ambiguous unpaired cockpits were guessed at startup.");
        string selected=(string)Get(host,"Selected")!;HostFrame(host,rig,"Select(HRZ,Arm 1)");
        const string error="More than one cockpit is occupied. Please ensure exactly one cockpit is occupied.";
        Check((string)Get(host,"HostError")! ==error&&((IDictionary)Get(host,"Pilots")!).Count==0&&(string)Get(host,"Selected")! ==selected,"Ambiguous Select did not fail atomically with the exact message.");
        RecordProxy.Of(two).Values["IsUnderControl"]=false;for(int i=0;i<20;i++)HostFrame(host,rig);
        Check(((IDictionary)Get(host,"Pilots")!).Count==0,"Failed selection silently retried after a seat became vacant.");
        HostFrame(host,rig,"Select(HRZ,Arm 1)");Check((long)Get(a,"PilotSeatId")! ==one.EntityId&&Get(a,"MovementMode")!.ToString()=="HRZ","Atomic selection did not pair the unnamed seat and mode.");
        RecordProxy.Of(one).Values["IsUnderControl"]=false;RecordProxy.Of(two).Values["IsUnderControl"]=true;HostFrame(host,rig,"Select(VRT,Arm 2)");
        Check((long)Get(b,"PilotSeatId")! ==two.EntityId&&Get(b,"MovementMode")!.ToString()=="VRT","Second unnamed seat failed to pair independently.");
        RecordProxy.Of(one).Values["IsUnderControl"]=true;for(int i=0;i<15;i++)HostFrame(host,rig);
        Vector3D WorldPilot(object core) {var v=(Vector3D)Get(core,"LastPilotLinear")!;var frame=((IMyMechanicalConnectionBlock)Get(Get(core,"Topology")!,"Base")!).WorldMatrix;return frame.Forward*v.X+frame.Left*v.Y+frame.Up*v.Z;}
        Check(Vector3D.Distance(WorldPilot(a),one.WorldMatrix.Forward*.7)<1e-9&&Vector3D.Distance(WorldPilot(b),two.WorldMatrix.Up*.35)<1e-9,"Two players' HRZ/VRT cockpit frames were mixed or one was suppressed.");
        HostFrame(host,rig,"Select(HRZ,Arm 2)");Check((string)Get(host,"HostError")! ==error&&(long)Get(a,"PilotSeatId")! ==one.EntityId&&(long)Get(b,"PilotSeatId")! ==two.EntityId&&Get(b,"MovementMode")!.ToString()=="VRT","Failed multi-occupied selection altered live bindings/mode.");
        RecordProxy.Of(one).Values["MoveIndicator"]=Vector3.Zero;for(int i=0;i<12;i++)HostFrame(host,rig);
        Check(((Vector3D)Get(a,"LastPilotLinear")!).Length()==0&&((Vector3D)Get(b,"LastPilotLinear")!).Length()>.3,"Neutral player input leaked between seats.");
        HostFrame(host,rig,"Mode(HEAD,Arm 1)");Check(Get(a,"MovementMode")!.ToString()=="HEAD"&&Get(b,"MovementMode")!.ToString()=="VRT","Scoped mode changed another player's view.");
        rig.Storage=((TestHost)host).Storage;var cold=Tests.Create(type,rig);HostReady(cold,rig,"Arm 1");HostReady(cold,rig,"Arm 2");
        Check((long)Get(Core(cold,"Arm 1"),"PilotSeatId")! ==one.EntityId&&(long)Get(Core(cold,"Arm 2"),"PilotSeatId")! ==two.EntityId,"Pairing required manual setup after restart.");
        RecordProxy.Of(one).Values["Closed"]=true;for(int i=0;i<15;i++)HostFrame(cold,rig);
        Check(((Vector3D)Get(Core(cold,"Arm 1"),"LastPilotLinear")!).Length()==0&&((Vector3D)Get(Core(cold,"Arm 2"),"LastPilotLinear")!).Length()>.3,"Missing bound cockpit fell back to another player.");RecordProxy.Of(one).Values["Closed"]=false;
        HostFrame(cold,rig,"Release Arm 1");for(int i=0;i<12;i++)HostFrame(cold,rig);Check((long)Get(Core(cold,"Arm 1"),"PilotSeatId")! ==0&&!(bool)Get(Core(cold,"Arm 1"),"PilotSelected")!,"Release resumed unbound fallback input.");
        RecordProxy.Of(one).Values["IsUnderControl"]=false;RecordProxy.Of(two).Values["IsUnderControl"]=false;HostFrame(cold,rig,"Select Arm 1");Check((string)Get(cold,"HostError")! =="No cockpit is occupied. Please ensure exactly one cockpit is occupied.","Empty-seat selection guessed a cockpit.");
        HostFrame(cold,rig,"StopAll");NoVelocity(rig);
        PilotPairHosts(type);
        Console.WriteLine("Automatic cockpit pairing: exact occupancy failure, no retry/guessing/names/config, independent players/modes, persistence, release/missing-seat and scoped cross-PB claims PASS ("+Tests.Assertions+" assertions).");
    }
    static void PilotPairHosts(Type type)
    {
        var rig=MultiFixture(out _,out _,out var one);var two=rig.Cockpit();RecordProxy.Of(two).Values["IsUnderControl"]=false;
        var peer=new Rig(rig,"Second arm PB");var firstData=new MyIni();firstData.TryParse(rig.PB.CustomData);firstData.DeleteSection("Arm 2");RecordProxy.Of(rig.PB).Values["CustomData"]=firstData.ToString();
        var secondData=new MyIni();secondData.Set("global","Format",7);secondData.Set("Arm 2","ToolSwapPB","");RecordProxy.Of(peer.PB).Values["CustomData"]=secondData.ToString();
        var bus=new ModuleBus();bus.Bind(rig);bus.Bind(peer);var a=Tests.Create(type,rig);var b=Tests.Create(type,peer);HostReady(a,rig,"Arm 1");HostReady(b,peer,"Arm 2");
        void Tick(int count=12){for(int i=0;i<count;i++){HostFrame(a,rig);HostFrame(b,peer);}}
        HostFrame(a,rig,"Select(HRZ,Arm 1)");Tick();RecordProxy.Of(one).Values["IsUnderControl"]=false;RecordProxy.Of(two).Values["IsUnderControl"]=true;
        HostFrame(b,peer,"Select(VRT,Arm 2)");Tick();RecordProxy.Of(one).Values["IsUnderControl"]=true;RecordProxy.Of(one).Values["MoveIndicator"]=new Vector3(0,0,-1);RecordProxy.Of(two).Values["MoveIndicator"]=new Vector3(0,1,0);Tick();
        Check((bool)Get(Core(a,"Arm 1"),"PilotSelected")!&&(bool)Get(Core(b,"Arm 2"),"PilotSelected")!&&((Vector3D)Get(Core(a,"Arm 1"),"LastPilotLinear")!).Length()>.1&&((Vector3D)Get(Core(b,"Arm 2"),"LastPilotLinear")!).Length()>.1,"Construct-wide selection suppressed an independently paired cockpit.");
        RecordProxy.Of(one).Values["IsUnderControl"]=false;HostFrame(a,rig,"Select Arm 1");Tick();
        Check((long)Get(Core(a,"Arm 1"),"PilotSeatId")! ==two.EntityId&&(long)Get(Core(b,"Arm 2"),"PilotSeatId")! ==0&&!(bool)Get(Core(b,"Arm 2"),"PilotSelected")!,"Same-seat claim did not transfer atomically across owners.");
        for(int i=0;i<90;i++)HostFrame(b,peer);Check(!(bool)Get(Core(b,"Arm 2"),"PilotSelected")!,"Expired remote claim resurrected a stolen local binding.");
        HostFrame(a,rig,"StopAll");HostFrame(b,peer,"StopAll");NoVelocity(rig);
    }
}
