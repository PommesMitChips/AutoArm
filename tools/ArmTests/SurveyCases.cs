using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

internal static partial class Scenarios
{
    internal static void SurveyCases(Type armType, Type surveyType)
    {
        var rig=MultiFixture(out _,out _,out _);
        RecordProxy.Of(rig.PB).Values["CustomName"]="Arm PB";
        var surveyRig=new Rig(rig,"Arm Survey");
        var safetyRig=new Rig(rig,"Collision PB");
        var bus=new ModuleBus{ArmId=rig.PB.EntityId};
        bus.Bind(rig); bus.Bind(surveyRig); bus.Bind(safetyRig);
        var data=new MyIni();data.TryParse(rig.PB.CustomData);
        data.Set("global","Peers","Collision PB | Safety\nArm Survey | Observe");
        RecordProxy.Of(rig.PB).Values["CustomData"]=data.ToString();
        foreach(var grid in rig.Blocks.Select(b=>b.CubeGrid).Distinct())
            GridCells(grid,new HashSet<Vector3I>{new(0,0,0),new(1,0,0),new(3,0,0)},Vector3D.Zero);
        var arm=Tests.Create(armType,rig);
        HostReady(arm,rig,"Arm 1");HostReady(arm,rig,"Arm 2");
        var survey=Tests.Create(surveyType,surveyRig);
        void SurveyFrame(string command="")
        {
            RecordProxy.Actor="Survey";
            HostFrame(survey,surveyRig,command);
            RecordProxy.Actor="Arm";
            HostFrame(arm,rig);
            RecordProxy.Actor="";
        }
        string Complete(string command="Survey",Action<int>? step=null)
        {
            SurveyFrame(command);
            for(int i=0;i<350&&(int)Get(survey,"Phase")!!=0;i++){step?.Invoke(i);SurveyFrame();}
            Check((int)Get(survey,"Phase")! ==0,"Survey never finished.");
            return surveyRig.Log.Last();
        }
        string state=Complete();
        Check(state.Contains("stable endpoints"),"Two-arm survey failed: "+state);
        var report=new MyIni();Check(report.TryParse(surveyRig.PB.CustomData),"Survey corrupted its configuration.");
        string baseline=report.EndContent;
        Check(baseline.Contains("ARM Arm 1 | START WORLD")&&baseline.Contains("ARM Arm 2 | START WORLD"),"Survey omitted authoritative per-arm start poses.");
        Check(baseline.Contains("ROTARY ")&&baseline.Contains("PISTON ")&&baseline.Contains("CONTROLLER "),"Survey omitted native joint/control geometry.");
        Check(baseline.Contains("CELLS "+rig.Root.EntityId+" | 0 1 0 0")&&baseline.Contains("CELLS "+rig.Root.EntityId+" | 3 3 0 0"),"Occupied-cell runs lost gaps or endpoints.");
        Check(baseline.Contains("CUSTOM DATA "+rig.PB.EntityId+" BEGIN")&&baseline.Contains("CUSTOM DATA "+safetyRig.PB.EntityId+" BEGIN")==false,"Survey omitted Arm configuration or fabricated empty safety data.");
        state=Complete();Check(state.Contains("stable endpoints"),"A second capture reused rejected session sequences: "+state);
        report.TryParse(surveyRig.PB.CustomData);Check(report.EndContent.Length==baseline.Length || report.EndContent.Length<baseline.Length+250,"Second survey recursively included its old report.");
        SurveyFrame("Page 1");Check(surveyRig.Log.Last().Contains("AUTOARM SURVEY 1"),"Report page command failed.");
        var head=rig.Blocks.Single(b=>b.CustomName=="Arm 2 - Head - Drill");
        state=Complete(step:i=>{if(i==1){var frame=head.WorldMatrix;frame.Translation+=new Vector3D(.1,0,0);RecordProxy.Of(head).Values["WorldMatrix"]=frame;}});
        Check(state.Contains("movement detected"),"Motion during geometry capture appeared stable.");
        string prior=surveyRig.PB.CustomData;
        report.TryParse(prior);report.Set("Survey","CellLimit",1);RecordProxy.Of(surveyRig.PB).Values["CustomData"]=report.ToString();
        state=Complete();Check(state.Contains("CellLimit reached"),"Truncated occupancy was treated as a complete baseline.");
        var retained=new MyIni();retained.TryParse(surveyRig.PB.CustomData);
        Check(retained.EndContent==report.EndContent,"Failed survey replaced the previous report.");
        report.Set("Survey","CellLimit",262144);RecordProxy.Of(surveyRig.PB).Values["CustomData"]=report.ToString();
        data.TryParse(rig.PB.CustomData);data.Set("global","Peers","Arm Survey | Observe");RecordProxy.Of(rig.PB).Values["CustomData"]=data.ToString();
        int sends=bus.Sent.Count(x=>x.Source==surveyRig.PB.EntityId);
        state=Complete();Check(state.Contains("Safety PB")&&bus.Sent.Count(x=>x.Source==surveyRig.PB.EntityId)==sends,"Missing Safety peer did not reject before traffic.");
        data.Set("global","Peers","Collision PB | Safety\nArm Survey | Plan");RecordProxy.Of(rig.PB).Values["CustomData"]=data.ToString();
        state=Complete();Check(state.Contains("Observe"),"Survey accepted movement authority.");
        data.Set("global","Peers","Collision PB | Safety\nArm Survey | Observe");RecordProxy.Of(rig.PB).Values["CustomData"]=data.ToString();
        bus.DropArm=true;state=Complete();Check(state.Contains("reply timeout"),"Lost ArmService failed to terminate capture.");bus.DropArm=false;
        foreach(var sent in bus.Sent.Where(x=>x.Source==surveyRig.PB.EntityId))
        {
            var packet=new MyIni();Check(packet.TryParse(sent.Data),"Survey sent malformed protocol.");
            Check(packet.Get("Link","Operation").ToString() is "HELLO" or "STATUS","Survey sent a motion/stop/guide operation.");
            Check(sent.Target==rig.PB.EntityId,"Survey contacted another PB.");
        }
        foreach(var block in rig.Blocks.Where(b=>b.EntityId!=surveyRig.PB.EntityId))
            Check(RecordProxy.Of(block).ActorWrites.All(w=>w.Actor!="Survey"),"Survey mutated another block: "+block.CustomName);
        Check(Enabled(Core(arm,"Arm 1"))&&Enabled(Core(arm,"Arm 2")),"Survey disrupted either arm's ON state.");
        Console.WriteLine("Read-only two-arm survey: PASS ("+Tests.Assertions+" assertions).");
    }
}
