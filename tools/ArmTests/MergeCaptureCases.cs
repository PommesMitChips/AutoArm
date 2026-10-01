using Sandbox.ModAPI.Ingame;
using VRageMath;

internal static partial class Scenarios
{
    static void MergeCaptureCases(Type armType,Type toolType)
    {
        foreach(bool partial in new[]{false,true})
        {
            var d=new DualRig(armType,toolType);d.F.AutoMerge=false;DualStart(d);d.Command("Tool 2");DualPlant(d,"Dock");
            var client=Get(d.Arm,"Tools")!;var path=Get(client,"Path")!;
            Check((bool)Get(path,"Capture")!&&d.F.Heads[0].All(m=>m.Enabled),"Approach magnets enabled without an accepted capture policy.");
            Check((bool)Get(client,"Driving")!,"Capture case never exercised a moving path.");
            Check(d.F.Couplers[1].Top==null,"Unrelated parked support was mistaken for mounted-tool capture.");
            d.F.MoveSource(SwapMotionGoal(d.Tool,"Dock"));
            SwapField(d.Arm,"FilteredV",new Vector3D(.2,0,0));SwapField(d.Arm,"FilteredW",new Vector3D(0,.1,0));
            RecordProxy.Of(d.F.Piston).Values["Velocity"]=.05f;
            if(partial)d.F.LockSource(1);else { d.F.AutoMerge=true;Check(d.F.CaptureSource(),"Magnetic capture fixture failed inside its native range."); }
            // Run the Arm PB alone: stopping cannot depend on ToolSwap's next
            // callback, a waypoint's settling gate, or topology failure.
            for(int i=0;i<3;i++)d.Frame(d.Arm,d.F.Rig);
            Check(!d.F.AnyDrive&&!(bool)Get(client,"Driving")!&&Get(client,"Path")==null&&!(bool)Get(Get(d.Arm,"Topology")!,"Ready")!,"Arm continued driving after merge closure.");
            Check(((string)Get(d.Arm,"LastFault")!).Length==0,"Expected capture was routed through a fault/timeout.");
            Check(!d.F.Mutations.Any(m=>m.Kind=="Detach"),"Arm-side capture detached the tool without stand verification.");
            d.Tick();Check(SwapPhase(d.Tool)=="Lock"&&!d.F.AnyDrive,"Coordinator failed to adopt early capture independently of path completion.");
            if(partial)
            {
                for(int i=0;i<10;i++)d.Tick();
                Check(!d.F.Mutations.Any(m=>m.Kind=="Detach"),"Partial support allowed mount detachment.");
                d.F.LockSource();
            }
            DualPlant(d,"Idle");for(int i=0;i<20;i++)d.Tick();
            Check(Enabled(d.Arm)&&d.F.Couplers[1].Top==d.F.ArmTip&&d.F.Mutations.All(m=>m.Stopped&&m.Supported),"Captured tool did not complete swap/resume with stopped mutation proofs.");
        }
        var bare=new DualRig(armType,toolType,true);DualStart(bare);bare.Command("Tool 2");DualPlant(bare,"ApproachTop");
        for(int i=0;i<8;i++)bare.Tick();
        Check(!(bool)Get(Get(Get(bare.Arm,"Tools")!,"Path")!,"Capture")!&&(bool)Get(Get(bare.Arm,"Tools")!,"Driving")!,"Pickup enabled parking capture or stopped for an unrelated stored tool.");
        var wrong=new DualRig(armType,toolType);wrong.F.AutoMerge=false;DualStart(wrong);wrong.Command("Tool 2");DualPlant(wrong,"Dock");
        // A connected flag alone is sufficient to stop motion, but is never
        // sufficient evidence of the intended reciprocal stand pairing.
        RecordProxy.Of(wrong.F.Heads[0][0]).Values["IsConnected"]=true;
        for(int i=0;i<15;i++)wrong.Tick();
        Check(SwapPhase(wrong.Tool)=="Lock"&&!wrong.F.AnyDrive&&!wrong.F.Mutations.Any(m=>m.Kind=="Detach"),"Unproven/foreign capture was treated as support.");
        SwapField(SwapController(wrong.Tool),"PhaseBudget",.01d);wrong.Tick();
        Check(SwapPhase(wrong.Tool)=="Idle"&&!Enabled(wrong.Arm)&&!wrong.F.Mutations.Any(m=>m.Kind=="Detach"),"Unproven capture escaped the bounded support wait.");
        Console.WriteLine("Early merge capture: acknowledged live magnets, arm-side stop before topology checks, no quiet-motion prerequisite, partial support refusal and automatic swap resume.");
    }
}
