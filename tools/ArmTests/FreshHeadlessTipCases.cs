using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;
using VRage.Game.ModAPI.Ingame;

internal static partial class Scenarios
{
    static void FreshHeadlessTipCases(Type type)
    {
        foreach(bool hinge in new[]{false,true}) foreach(bool locked in new[]{false,true}) foreach(bool quarterTurn in new[]{false,true})
        {
            var f=new ToolSwapFixture(headless:true,destinationAngle:.4); f.NoNamedArmReference(); if(hinge) f.HingeEnd();
            if(quarterTurn) { var m=f.ArmTip.WorldMatrix; RecordProxy.Of(f.ArmTip).Values["WorldMatrix"]=MatrixD.CreateWorld(m.Translation,Vector3D.Cross(m.Up,m.Forward),m.Up); }
            RecordProxy.Of(f.Couplers[1]).Values["RotorLock"]=locked;
            Check(f.Rig.Storage=="" && f.Couplers.All(c=>c.Top==null),"Cold auto pickup starts with saved coupling metadata or attachment.");
            var script=SwapStart(type,f); var tip=Get(SwapController(script),"Tip")!; var topology=Get(script,"Topology")!;
            Check((long)Get(tip,"EntityId")! ==0 && ((IMyCubeGrid)Get(tip,"CubeGrid")!).EntityId==f.ArmTip.CubeGrid.EntityId,"Auto scan guessed identity or chose the wrong moving grid.");
            VCNear((Vector3D)Get(topology,"EndPosition")!,f.ArmTip.CubeGrid.GridIntegerToWorld(f.ArmTip.Position),"Bare control focus is actual detected rotor pivot");
            var config=new MyIni(); config.TryParse(f.Rig.PB.CustomData);
            Check(!config.ContainsKey("Tools","Mount") && !config.ContainsKey("Tools","ArmTip") && !config.ContainsSection("Instructions"),"Auto setup generated redundant mount/offset/instructions configuration.");
            Check(Groups(script).Sum(g=>Members(g).Length)==(hinge?3:2),"Auto bare corridor omitted final unnamed hinge or included tool coupler.");
            if(hinge) Check(Members(Groups(script).Last()).Any(m=>Get(m,"B")==f.LastHinge),"Unmarked final hinge was not acquired for control/stop.");
            SwapFrame(script,"On",false,0); var cockpit=f.Rig.Blocks.OfType<IMyShipController>().Single(); RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-1); SwapFrame(script);
            Check(Enabled(script) && f.AnyDrive,"Cold headless auto arm failed manual movement without reference/offset.");
            RecordProxy.Of(cockpit).Values["MoveIndicator"]=Vector3.Zero; SwapFrame(script,"Stop",false,0);
            SwapFrame(script,"Tool 2",false,0); bool drove=SwapPlant(script,f,"Release");
            Check(drove && f.Mutations.Count==1 && f.Mutations[0].Tool==1 && f.Mutations[0].Kind=="Attach","Auto cold pickup failed to locate incoming tool or detached nonexistent source.");
            Check(f.Mutations[0].Supported && f.Mutations[0].Stopped && f.Heads[1].All(m=>!m.Enabled),"Auto pickup released unverified support or wrote while driving.");
            tip=Get(SwapController(script),"Tip")!;
            Check((long)Get(tip,"EntityId")! ==f.ArmTip.EntityId && f.Couplers[1].Top==f.ArmTip && f.ArmTip.Base==f.Couplers[1],"Auto pickup failed reciprocal actual rotor ID adoption.");
            f.SplitTool(1); SwapPlant(script,f,"Idle");
            Check(!Enabled(script) && Get(Get(script,"Topology")!,"Head")==f.Markers[1],"Auto cold pickup failed mounted OFF completion.");
            Check(f.Couplers[1].RotorLock==locked,"First pickup did not restore requested rotor lock state."); SwapStationUntouched(f);
        }
        foreach(bool flipped in new[]{false,true})
        {
            var f=new ToolSwapFixture(headless:true); f.NoNamedArmReference(); f.LargeSmallGridPart(flipped);
            var script=SwapStart(type,f); VCNear((Vector3D)Get(Get(script,"Topology")!,"EndPosition")!,f.ArmTip.CubeGrid.GridIntegerToWorld(f.ArmTip.Position),"Multi-cell advanced small-grid part pivot inference");
            SwapFrame(script,"Tool 2",false,0); SwapPlant(script,f,"Idle");
            Check(f.Couplers[1].Top==f.ArmTip && !Enabled(script),"Multi-cell advanced rotor automatic pickup failed on signed mounting face.");
        }
        var timed=new ToolSwapFixture(headless:true); timed.NoNamedArmReference(); var running=SwapStart(type,timed);
        var gt=RecordProxy.Of(timed.Rig.GTS); int calls=0; var original=gt.Call;
        gt.Call=(m,a)=>{if(m.Name=="GetBlocks") calls++; return original!(m,a);};
        RecordProxy.Of(timed.Rig.Runtime).Values["CurrentInstructionCount"]=40000; SwapFrame(running,"ToolScan",false,0);
        Check(calls==0 && timed.Mutations.Count==0 && !timed.AnyDrive,"Low-budget scan enumerated before reserve check or changed equipment.");
        RecordProxy.Of(timed.Rig.Runtime).Values["CurrentInstructionCount"]=0;
        var detached=new ToolSwapFixture(headless:true); detached.NoNamedArmReference(); detached.HingeEnd(); var controlling=SwapStart(type,detached);
        SwapFrame(controlling,"On",false,0); RecordProxy.Of(detached.LastHinge!).Values["IsAttached"]=false; RecordProxy.Of(detached.LastHinge!).Values["Top"]=null; RecordProxy.Of(detached.LastHinge!).Values["TopGrid"]=null; SwapFrame(controlling);
        Check(!Enabled(controlling) && !detached.AnyDrive,"Detached automatically found end hinge kept control active.");
        foreach(string fault in new[]{"None","Multiple","Face"})
        {
            var f=new ToolSwapFixture(headless:true); f.NoNamedArmReference();
            if(fault=="None") f.NoPart(); else if(fault=="Multiple") f.WrongArmPart(); else f.ExtraFace();
            var script=Tests.Create(type,f.Rig); for(int i=0;i<200 && SwapPhase(script)!="Idle";i++) SwapFrame(script);
            SwapFrame(script,"On",false,0); SwapFrame(script,"Tool 2",false,0); for(int i=0;i<200 && SwapPhase(script)!="Idle";i++) SwapFrame(script);
            Check(!Enabled(script) && !f.AnyDrive && f.Mutations.Count==0,"Ambiguous/missing loose part authorized motion/attachment: "+fault); SwapStationUntouched(f);
        }
        foreach(string mismatch in new[]{"Cell","Axis","Type","NaN"})
        {
            var f=new ToolSwapFixture(headless:true); f.NoNamedArmReference(); var script=SwapStart(type,f);
            if(mismatch=="Cell") f.AttachResult=f.WrongArmPart();
            if(mismatch=="Type") RecordProxy.Of(f.ArmTip).Values["BlockDefinition"]=ToolSwapFixture.Definition("LargeHingeHead");
            SwapFrame(script,"Tool 2",false,0); SwapPlant(script,f,"Attach");
            if(mismatch=="Axis") { var m=f.ArmTip.WorldMatrix; RecordProxy.Of(f.ArmTip).Values["WorldMatrix"]=MatrixD.CreateWorld(m.Translation,m.Forward,-m.Up); }
            if(mismatch=="NaN") { var m=f.ArmTip.WorldMatrix; m.M11=double.NaN; RecordProxy.Of(f.ArmTip).Values["WorldMatrix"]=m; }
            SwapFrame(script); Check(SwapPhase(script)=="Idle" && !Enabled(script) && f.Heads[1].All(m=>m.Enabled),"Wrong observed rotor released support: "+mismatch); SwapStationUntouched(f);
        }
        var cancelled=new ToolSwapFixture(headless:true); cancelled.NoNamedArmReference(); cancelled.AutoAttach=false; RecordProxy.Of(cancelled.Couplers[1]).Values["RotorLock"]=true;
        var active=SwapStart(type,cancelled); SwapFrame(active,"Tool 2",false,0); SwapPlant(active,cancelled,"Attach");
        Check(!cancelled.Couplers[1].RotorLock && cancelled.Couplers[1].PendingAttachment,"Cold rotor was not safely unlocked for first attach.");
        SwapFrame(active,"SwapCancel",false,0); cancelled.Rig.Storage=((TestHost)active).Storage;
        var restarted=Tests.Create(type,cancelled.Rig); cancelled.Host=(TestHost)restarted; for(int i=0;i<200 && SwapPhase(restarted)!="Idle";i++) SwapFrame(restarted);
        Check(!cancelled.Couplers[1].RotorLock && !Enabled(restarted),"Restart reactivated uncertain lock/pending attachment.");
        SwapFrame(restarted,"ToolScan",false,0); for(int i=0;i<200 && SwapPhase(restarted)!="Idle";i++) SwapFrame(restarted);
        Check(!cancelled.Couplers[1].PendingAttachment && cancelled.Couplers[1].RotorLock && !(bool)Get(SwapController(restarted),"Recovery")!,"Explicit scan did not cancel bare pending request then restore persisted lock.");
        var bounded=new ToolSwapFixture(headless:true); bounded.NoNamedArmReference(); RecordProxy.Of(bounded.Couplers[1]).Values["LowerLimitRad"]=-.5f; RecordProxy.Of(bounded.Couplers[1]).Values["UpperLimitRad"]=.5f;
        var refused=SwapStart(type,bounded); SwapFrame(refused,"Tool 2",false,0); for(int i=0;i<200 && SwapPhase(refused)!="Idle";i++) SwapFrame(refused);
        Check(!Enabled(refused) && bounded.Mutations.Count==0 && !bounded.AnyDrive,"Unknown phase with finite rotor limits was attached/moved blindly.");
    }
}