using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

internal static partial class Scenarios
{
    static void FreshTipConfig(ToolSwapFixture f, string value="2.5 0 -2.5 | Right | Up")
    {
        var ini=new MyIni(); ini.TryParse(f.Rig.PB.CustomData); ini.Set("Tools","ArmTip",value);
        RecordProxy.Of(f.Rig.PB).Values["CustomData"]=ini.ToString();
    }
    static void FreshHeadlessTipCases(Type type)
    {
        var first=new ToolSwapFixture(headless:true);
        foreach(var merge in first.Heads[0]) RecordProxy.Of(merge).Values["Enabled"]=true;
        first.LockSource();
        var reference=first.ArmRef.WorldMatrix;
        RecordProxy.Of(first.ArmRef).Values["WorldMatrix"]=MatrixD.CreateWorld(reference.Translation,Vector3D.Forward,Vector3D.Up);
        FreshTipConfig(first,"0 2.5 -2.5");
        var unmounted=SwapStart(type,first); SwapFrame(unmounted,"Tool 1",false,0); SwapPlant(unmounted,first,"Idle");
        Check(first.Couplers[0].Top==first.ArmTip && first.Mutations.Count==1 && first.Mutations[0].Kind=="Attach" && !Enabled(unmounted),"Fresh bare arm failed default-axis pickup of first parked tool without teaching.");
        SwapStationUntouched(first);
        foreach(bool locked in new[]{false,true}) foreach(string subtype in new[]{"LargeRotor","LargeAdvancedRotor"})
        {
            var f=new ToolSwapFixture(headless:true,destinationAngle:.4);
            RecordProxy.Of(f.ArmTip).Values["BlockDefinition"]=ToolSwapFixture.Definition(subtype);
            RecordProxy.Of(f.Couplers[1]).Values["RotorLock"]=locked;
            FreshTipConfig(f); Check(f.Rig.Storage=="" && f.Couplers.All(c=>c.Top==null),"Fresh bare pickup accidentally starts with Storage or attachment.");
            var script=SwapStart(type,f); var tip=Get(SwapController(script),"Tip")!;
            Check((long)Get(tip,"EntityId")! ==0 && !((TestHost)script).Storage.Contains("[AutoArm Arm Tip]"),"Declared tip invented/persisted an unobserved entity identity.");
            SwapFrame(script,"On",false,0); var cockpit=f.Rig.Blocks.OfType<IMyShipController>().Single();
            RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-1); SwapFrame(script);
            Check(Enabled(script) && f.AnyDrive,"Fresh headless arm could not start and respond to pilot movement.");
            RecordProxy.Of(cockpit).Values["MoveIndicator"]=Vector3.Zero; SwapFrame(script,"Stop",false,0);
            SwapFrame(script,"Tool 2",false,0); bool drove=SwapPlant(script,f,"Release");
            Check(drove && f.Mutations.Count==1 && f.Mutations[0].Tool==1 && f.Mutations[0].Kind=="Attach","Fresh bare pickup parked/detached a nonexistent source or failed to locate/approach incoming rotor.");
            Check(f.Mutations[0].Supported && f.Mutations[0].Stopped && f.Heads[1].All(m=>!m.Enabled),"Fresh verified pickup lost its support/stop/release ordering.");
            tip=Get(SwapController(script),"Tip")!;
            Check((long)Get(tip,"EntityId")! ==f.ArmTip.EntityId && f.Couplers[1].Top==f.ArmTip && f.ArmTip.Base==f.Couplers[1],"First pickup did not adopt the actual reciprocal arm-tip identity.");
            f.SplitTool(1); SwapPlant(script,f,"Idle");
            Check(!Enabled(script) && Get(Get(script,"Topology")!,"Head")==f.Markers[1],"Fresh headless pickup failed to finish mounted OFF after support split.");
            Check(f.Couplers[1].RotorLock==locked,"Pickup changed the tool rotor's requested lock state.");
            var saved=new MyIni(); saved.TryParse(((TestHost)script).Storage);
            Check(saved.Get("AutoArm Arm Tip","E"+f.ArmRef.EntityId).ToString().StartsWith(f.ArmTip.EntityId+"|"),"Verified first pickup did not persist its real identity.");
            SwapStationUntouched(f);
        }
        foreach(string bad in new[]{"2.5 0","NaN 0 0","0 0 0","2.5 0 -1","2.5 0 -2.5 | Up | Up","2.5 0 -2.5 | Whatever | Up"})
        {
            var f=new ToolSwapFixture(headless:true); FreshTipConfig(f,bad); string original=f.Rig.PB.CustomData;
            var script=Tests.Create(type,f.Rig); SwapFrame(script,"On",false,0); SwapFrame(script,"Tool 2",false,0);
            Check(!Enabled(script) && !f.AnyDrive && f.Mutations.Count==0 && f.Rig.PB.CustomData==original,"Bad fresh arm-tip geometry enabled/mutated setup: "+bad);
        }
        foreach(string mismatch in new[]{"Cell","Axis","Type","NaN"})
        {
            var f=new ToolSwapFixture(headless:true); FreshTipConfig(f); var script=SwapStart(type,f);
            if(mismatch=="Cell") f.AttachResult=f.WrongArmPart();
            if(mismatch=="Type") RecordProxy.Of(f.ArmTip).Values["BlockDefinition"]=ToolSwapFixture.Definition("LargeHingeHead");
            SwapFrame(script,"Tool 2",false,0); SwapPlant(script,f,"Attach");
            if(mismatch=="Axis") { var pose=f.ArmTip.WorldMatrix; RecordProxy.Of(f.ArmTip).Values["WorldMatrix"]=MatrixD.CreateWorld(pose.Translation,-pose.Forward,pose.Up); }
            if(mismatch=="NaN") { var pose=f.ArmTip.WorldMatrix; pose.M11=double.NaN; RecordProxy.Of(f.ArmTip).Values["WorldMatrix"]=pose; }
            SwapFrame(script);
            Check(SwapPhase(script)=="Idle" && !Enabled(script) && f.Heads[1].All(m=>m.Enabled),"Wrong initially unobserved rotor cell/axes/type released support: "+mismatch);
            Check(!f.Heads[1].Any(m=>RecordProxy.Of(m).Writes.Any(w=>w.Name=="Enabled" && Equals(w.Value,false))),"Mismatched unknown rotor produced a release write.");
        }
        var owned=new ToolSwapFixture(headless:true); var owner=owned.OtherOwner(); FreshTipConfig(owned);
        var values=RecordProxy.Of(owner).Values; values["IsAttached"]=true; values["Top"]=owned.ArmTip; values["TopGrid"]=owned.ArmGrid;
        RecordProxy.Of(owned.ArmTip).Values["Base"]=owner; RecordProxy.Of(owner).Writes.Clear();
        var rejected=Tests.Create(type,owned.Rig); for(int i=0;i<160 && SwapPhase(rejected)!="Idle";i++) SwapFrame(rejected);
        SwapFrame(rejected,"On",false,0); SwapFrame(rejected,"Tool 2",false,0); for(int i=0;i<160 && SwapPhase(rejected)!="Idle";i++) SwapFrame(rejected);
        Check(!Enabled(rejected) && !owned.AnyDrive && owned.Mutations.Count==0 && RecordProxy.Of(owner).Writes.Count==0,"Declared fresh tip attached elsewhere permitted movement or foreign writes.");
    }
}
