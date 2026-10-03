using System.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;
internal static partial class Scenarios
{
    static void BindClaimRun(object host,Rig rig){var p=RecordProxy.Of(rig.PB);var prior=p.Call;p.Values["IsRunning"]=false;p.Call=(m,a)=>{if(m.Name!="TryRun")return prior!(m,a);if((bool)p.Values["IsRunning"]!||!rig.PB.IsFunctional||!rig.PB.Enabled)return false;p.Values["IsRunning"]=true;try{HostFrame(host,rig,(string)a![0]!,0,UpdateType.Script);return true;}finally{p.Values["IsRunning"]=false;}};}
    internal static void ArmClaimCases(Type type)
    {
        var rig=MultiFixture(out var a,out var b,out var seat);var owner=Tests.Create(type,rig);HostReady(owner,rig,"Arm 1");HostReady(owner,rig,"Arm 2");HostFrame(owner,rig,"Select Arm 1");
        var receiver=new Rig(rig,"New Arm PB");var data=new MyIni();data.Set("global","Format",7);data.Set("global","HeadSpeed",.1);RecordProxy.Of(receiver.PB).Values["CustomData"]=data.ToString();
        var bus=new ModuleBus();bus.Bind(rig);bus.Bind(receiver);var next=Tests.Create(type,receiver);BindClaimRun(owner,rig);BindClaimRun(next,receiver);
        RecordProxy.Of(a).Values["Velocity"]=.2f;HostFrame(next,receiver,"Claim Arm 1");
        var oldConfig=new MyIni();oldConfig.TryParse(rig.PB.CustomData);var newConfig=new MyIni();newConfig.TryParse(receiver.PB.CustomData);
        Check(!oldConfig.ContainsSection("Arm 1")&&newConfig.ContainsSection("Arm 1"),"Claim did not transfer configuration ownership.");
        Check(((IDictionary)Get(owner,"Arms")!).Count==1&&((IDictionary)Get(next,"Arms")!).Count==1,"Claim left duplicate runtime controllers.");
        Check(a.Velocity==0&&!Enabled(Core(next,"Arm 1")),"Claim moved the transferred arm before On.");
        Check(Enabled(Core(owner,"Arm 2"))&&newConfig.Get("Arm 1","HeadSpeed").ToDouble()==.7,"Claim stopped a neighboring arm or lost effective settings.");
        Check(newConfig.Get("global","HeadSpeed").ToDouble()==.1&&newConfig.Get("Arm 1","Actuators").ToString().Contains("Rotor x1"),"Claim changed destination globals or dropped inherited joint weights.");
        var saved=new MyIni();saved.TryParse(((TestHost)owner).Storage);Check(!saved.ContainsSection("State Arm 1")&&!saved.ContainsSection("Pilot Arm 1"),"Old restart state/cockpit pairing still claimed the transferred arm.");
        HostReady(next,receiver,"Arm 1");HostFrame(next,receiver,"Claim Arm 1");Check(Enabled(Core(next,"Arm 1")),"Idempotent Claim interrupted its existing arm.");
        HostFrame(owner,rig,"Claim Arm 1");Check(!((IDictionary)Get(next,"Arms")!).Contains("Arm 1")&&((IDictionary)Get(owner,"Arms")!).Contains("Arm 1"),"Claim-back failed.");
        receiver.Storage=((TestHost)next).Storage;var empty=Tests.Create(type,receiver);Check(((IDictionary)Get(empty,"Arms")!).Count==0,"Empty former owner recreated its arm on restart.");
        RecordProxy.Of(rig.PB).Values["Enabled"]=false;HostFrame(next,receiver,"Claim Arm 1");Check(!rig.PB.Enabled&&((IDictionary)Get(next,"Arms")!).Contains("Arm 1"),"Claim failed from a disabled owner or changed its enabled state.");
        HostFrame(next,receiver,"Unclaim Arm 1");Check(((IDictionary)Get(next,"Arms")!).Count==0,"Unclaim failed on the last arm.");
        HostFrame(next,receiver,"Claim Arm 1");Check(((IDictionary)Get(next,"Arms")!).Contains("Arm 1"),"Unowned/deleted-owner recovery failed.");
        var before=receiver.PB.CustomData;HostFrame(next,receiver,"Claim global");Check(receiver.PB.CustomData==before,"Invalid Claim mutated configuration.");
        var tool=new Rig(rig,"ToolSwap PB");var toolData=new MyIni();toolData.Set("global","Format",2);toolData.Set("global","Link.ArmPB","@"+receiver.PB.EntityId);toolData.Set("Arm 1","Tools.Enabled",true);toolData.Set("Arm 2","Link.ArmPB","@"+rig.PB.EntityId);RecordProxy.Of(tool.PB).Values["CustomData"]=toolData.ToString();var commands=new List<string>();var tp=RecordProxy.Of(tool.PB);var call=tp.Call;tp.Call=(m,args)=>{if(m.Name!="TryRun")return call!(m,args);commands.Add((string)args![0]!);return true;};
        HostFrame(owner,rig,"Claim Arm 1");toolData.TryParse(tool.PB.CustomData);Check(toolData.Get("Arm 1","Link.ArmPB").ToString()=="@"+rig.PB.EntityId&&commands.SequenceEqual(new[]{"Stop(Arm 1)","Reload(Arm 1)"}),"Claim did not stop/rebind only its ToolSwap context.");Check(toolData.Get("Arm 2","Link.ArmPB").ToString()=="@"+rig.PB.EntityId,"Claim changed a different tool context.");commands.Clear();HostFrame(owner,rig,"Claim Arm 1");Check(commands.Count==0,"Idempotent Claim canceled a ToolSwap operation.");
        RecordProxy.Of(rig.PB).Values["IsRunning"]=true;before=receiver.PB.CustomData;var priorData=rig.PB.CustomData;HostFrame(next,receiver,"Claim Arm 1");Check(before==receiver.PB.CustomData&&priorData==rig.PB.CustomData,"Busy-owner refusal modified assignments.");RecordProxy.Of(rig.PB).Values["IsRunning"]=false;
        var ownerProxy=RecordProxy.Of(rig.PB);var ownerCall=ownerProxy.Call;
        foreach(bool accepted in new[]{false,true})
        {
            ownerProxy.Call=(m,args)=>m.Name=="TryRun"?accepted:ownerCall!(m,args);
            HostFrame(next,receiver,"Claim Arm 1");
            Check(receiver.PB.CustomData==before&&rig.PB.CustomData==priorData,"Claim took ownership without a verified release acknowledgement.");
        }
        ownerProxy.Call=ownerCall;
        var full=new MyIni();full.Set("global","Format",7);for(int i=0;i<8;i++)full.Set("Other "+i,"ToolSwapPB","");RecordProxy.Of(receiver.PB).Values["CustomData"]=full.ToString();
        HostFrame(next,receiver,"Claim Arm 1");Check(receiver.PB.CustomData==full.ToString()&&rig.PB.CustomData==priorData,"Full destination released an arm it cannot host.");RecordProxy.Of(receiver.PB).Values["CustomData"]=before;
        // A blocked bootstrap with a pre-existing duplicate section can Claim.
        var duplicate=new MyIni();duplicate.Set("global","Format",7);duplicate.Set("Arm 1","ToolSwapPB","");RecordProxy.Of(receiver.PB).Values["CustomData"]=duplicate.ToString();receiver.Storage="";var blocked=Tests.Create(type,receiver);HostFrame(blocked,receiver,"Claim Arm 1");Check(((IDictionary)Get(blocked,"Arms")!).Contains("Arm 1")&&!((IDictionary)Get(owner,"Arms")!).Contains("Arm 1"),"Claim did not recover duplicate-ownership startup failure.");
        HostFrame(blocked,receiver,"StopAll");HostFrame(owner,rig,"StopAll");NoVelocity(rig);
        Console.WriteLine("Arm Claim: live transfer, settings/weight inheritance, neighbors, pairing/storage cleanup, empty restart, disabled/unowned owner, idempotence, busy refusal and duplicate bootstrap PASS.");
    }
}
