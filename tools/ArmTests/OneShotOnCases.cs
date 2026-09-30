using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

internal static partial class Scenarios
{
    static void OneShotOnCases(Type type)
    {
        foreach(string command in new[]{"On","Toggle","OnOff On","OnOff Toggle"})
        {
            var rig=Fixtures.Serial(out _,out _,out _,out var cockpit); var script=Tests.Create(type,rig);
            var ini=new MyIni(); ini.TryParse(rig.PB.CustomData); ini.Set("Config","HeadSpeed",.37); RecordProxy.Of(rig.PB).Values["CustomData"]=ini.ToString();
            InvokeFrame(script,rig,command,UpdateType.Terminal,0);
            Check(!Enabled(script) && (bool)Get(script,"PendingOn")!,"One-shot simple startup skipped stopped observations: "+command); NoVelocity(rig);
            for(int i=0;i<6;i++) InvokeFrame(script,rig,"",UpdateType.Update1,1d/60);
            Check(Enabled(script) && Math.Abs((double)Get(script,"MoveMps")!-.37)<1e-12,"One-shot simple startup failed to load current settings: "+command);
            RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-1); InvokeFrame(script,rig,"",UpdateType.Update1,1d/60);
            Check(((Vector3D)Get(script,"LastPilotLinear")!).Length()>.36,"One-shot simple arm was not responsive.");
        }
        foreach(bool bare in new[]{false,true})
        {
            var f=new ToolSwapFixture(headless:bare); f.NoNamedArmReference(); if(bare) f.HingeEnd();
            var script=Tests.Create(type,f.Rig); f.Host=(TestHost)script;
            SwapFrame(script,"On",false,0);
            Check(!Enabled(script) && SwapPhase(script)=="Scan","On refused an in-progress startup scan instead of restarting it.");
            for(int i=0;i<200 && !Enabled(script);i++)
            {
                Check(!f.AnyDrive && f.Mutations.Count==0,"Setup moved the arm or changed a tool attachment before readiness."); SwapFrame(script);
            }
            Check(Enabled(script) && SwapPhase(script)=="Idle" && !(bool)Get(SwapController(script),"Recovery")!,"One-shot tool startup failed: "+string.Join(" | ",f.Rig.Log.TakeLast(4)));
            var cockpit=f.Rig.Blocks.OfType<IMyShipController>().Single(); RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-1); SwapFrame(script);
            Check(f.AnyDrive,"One-shot tool startup did not enable manual control."); RecordProxy.Of(cockpit).Values["MoveIndicator"]=Vector3.Zero;
            SwapFrame(script,"Tool 2",false,0); SwapPlant(script,f,"ApproachTop");
            string phase=SwapPhase(script); int mutations=f.Mutations.Count; SwapFrame(script,"On",false,0);
            Check(SwapPhase(script)==phase && f.Mutations.Count==mutations && !Enabled(script),"On interrupted an active automatic swap or enabled simultaneous manual control.");
            SwapPlant(script,f,"Idle");
            Check(Enabled(script) && Get(Get(script,"Topology")!,"Head")==f.Markers[1],"Successful swap failed to activate the new tool without another On.");
            RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-1); SwapFrame(script); Check(f.AnyDrive,"Mounted tool failed to respond immediately after automatic resume.");
            RecordProxy.Of(cockpit).Values["MoveIndicator"]=Vector3.Zero; SwapFrame(script,"Tool 2",false,0); for(int i=0;i<200 && SwapPhase(script)!="Idle";i++) SwapFrame(script);
            Check(Enabled(script),"Selecting the already mounted tool unnecessarily disabled manual control."); SwapStationUntouched(f);
        }
        foreach(bool tools in new[]{false,true})
        {
            var f=tools?new ToolSwapFixture(headless:true):null; var rig=f?.Rig ?? Fixtures.Serial(out _,out _,out _,out _);
            var script=Tests.Create(type,rig); InvokeFrame(script,rig,"On",UpdateType.Terminal,0); InvokeFrame(script,rig,"Stop",UpdateType.Terminal,0);
            for(int i=0;i<200;i++) InvokeFrame(script,rig,"",UpdateType.Update1,1d/60);
            Check(!Enabled(script) && !(bool)Get(script,"PendingOn")! && !(bool)Get(Get(script,"Tools")!,"StartAfterScan")!,"Stop failed to cancel deferred startup."); NoVelocity(rig);
        }
        var broken=new ToolSwapFixture(headless:true); broken.NoNamedArmReference(); broken.NoPart(); var failed=Tests.Create(type,broken.Rig);
        SwapFrame(failed,"On",false,0); for(int i=0;i<200;i++) SwapFrame(failed);
        Check(!Enabled(failed) && !(bool)Get(SwapController(failed),"StartAfterScan")! && broken.Mutations.Count==0,"Failed tool scan retained a deferred enable request."); NoVelocity(broken.Rig);
        Console.WriteLine("One-shot On: current configuration, stopped setup, mounted/bare control, immediate tool selection, swap exclusion, post-swap enable and cancellation/failure.");
    }
}
