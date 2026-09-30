using System.Collections;
using Sandbox.ModAPI.Ingame;
using VRageMath;

internal static partial class Scenarios
{
    static object[] ProbeTrials(object script)
    {
        var probe=Get(script,"PivotProbe");return probe==null?Array.Empty<object>():((IEnumerable)Get(probe,"Trials")!).Cast<object>().ToArray();
    }
    static void SetTopPoint(IMyMotorStator rotor,Vector3D position)
    {
        RecordProxy.Of(rotor.Top).Call=(method,args)=>method.Name=="GetPosition"?position:
            method.ReturnType.IsValueType?Activator.CreateInstance(method.ReturnType):null;
    }
    static int DriveWrites(Rig rig)=>rig.Blocks.OfType<IMyMechanicalConnectionBlock>().Sum(b=>RecordProxy.Of(b).Writes.Count(w=>w.Name is "Velocity" or "TargetVelocityRad"));
    static void RotateTrial(Rig rig,IMyMotorStator rotor,IMyTerminalBlock head,Vector3D pivot,double angle)
    {
        var pose=head.WorldMatrix;var rotation=-rotor.WorldMatrix.Up*angle;
        RecordProxy.Of(head).Values["WorldMatrix"]=MatrixD.CreateWorld(pivot+RotateVector(pose.Translation-pivot,rotation),RotateVector(pose.Forward,rotation),RotateVector(pose.Up,rotation));
        RecordProxy.Of(rotor).Values["Angle"]=(float)(rotor.Angle+angle);
    }
    static void PivotProbeCases(Type type)
    {
        foreach(bool topIsPivot in new[]{false,true})
        {
            var rig=Fixtures.Serial(out var rotor,out _,out var head,out _);SetTopPoint(rotor,new Vector3D(1,0,0));
            var script=Start(type,rig);string config=rig.PB.CustomData,storage=((TestHost)script).Storage;int writes=DriveWrites(rig);
            InvokeFrame(script,rig,"PivotProbe Start 1",UpdateType.Terminal,0);
            RotateTrial(rig,rotor,head,topIsPivot?new Vector3D(1,0,0):Vector3D.Zero,.1);
            InvokeFrame(script,rig,"PivotProbe End",UpdateType.Terminal,0);
            var trials=ProbeTrials(script);Check(trials.Length==1,"Valid isolated rotary motion was rejected: "+rig.Log.Last());
            var error=(double[])Get(trials[0],"Error")!;int winner=topIsPivot?1:0;
            Check(error[winner]<1e-6&&error[1-winner]>.09&&error[2]>.04,"Probe did not distinguish base/top/midpoint geometry from actual angle/head changes.");
            Check(DriveWrites(rig)==writes,"Passive successful probe commanded an actuator.");
            Check(rig.PB.CustomData==config&&((TestHost)script).Storage==storage,"Probe overwrote configuration or home storage.");
            InvokeFrame(script,rig,"PivotProbe Report",UpdateType.Terminal,0);Check(rig.Log.Last().Contains("no pivot changed"),"Probe report implied automatic calibration.");
            InvokeFrame(script,rig,"On",UpdateType.Terminal,0);Check(Get(script,"PivotProbe")==null,"Enabling control retained a stale empirical snapshot.");
            writes=DriveWrites(rig);InvokeFrame(script,rig,"PivotProbe Start 1",UpdateType.Terminal,0);
            Check(Enabled(script)&&ProbeTrials(script).Length==0&&DriveWrites(rig)==writes,"Probe changed active control instead of refusing observation.");
        }
        var equal=Fixtures.Serial(out var r,out var p,out var h,out _);SetTopPoint(r,Vector3D.Up*.4);var observer=Start(type,equal);
        InvokeFrame(observer,equal,"PivotProbe Start 1",UpdateType.Terminal,0);RotateTrial(equal,r,h,Vector3D.Zero,.1);InvokeFrame(observer,equal,"PivotProbe End",UpdateType.Terminal,0);
        Check(ProbeTrials(observer).Length==1&&((string)Get(ProbeTrials(observer)[0],"Verdict")!).Contains("inconclusive"),"Axially equivalent points were falsely presented as a calibrated winner.");
        InvokeFrame(observer,equal,"PivotProbe Reset",UpdateType.Terminal,0);
        InvokeFrame(observer,equal,"PivotProbe Start 1",UpdateType.Terminal,0);InvokeFrame(observer,equal,"PivotProbe End",UpdateType.Terminal,0);
        Check(ProbeTrials(observer).Length==0&&equal.Log.Last().Contains("Inconclusive"),"No angular excitation was accepted as pivot evidence.");
        InvokeFrame(observer,equal,"PivotProbe Start 1",UpdateType.Terminal,0);RotateTrial(equal,r,h,Vector3D.Zero,-.1);RecordProxy.Of(p).Values["CurrentPosition"]=5.01f;
        InvokeFrame(observer,equal,"PivotProbe End",UpdateType.Terminal,0);Check(ProbeTrials(observer).Length==0&&equal.Log.Last().Contains("another group"),"Unisolated multi-joint motion was treated as pivot calibration.");
        Console.WriteLine("Passive pivot comparison: actual angle/head displacement, center/top/midpoint discrimination, axial equivalence, rejected unexcited/multi-joint trials and no successful-probe writes.");
    }
}
