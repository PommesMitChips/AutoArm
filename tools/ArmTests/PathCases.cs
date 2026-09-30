using Sandbox.ModAPI.Ingame;
using VRageMath;

internal static partial class Scenarios
{
    static string PCRow(MatrixD pose) => string.Join(" ",new[]{pose.Translation.X,pose.Translation.Y,pose.Translation.Z,pose.Forward.X,pose.Forward.Y,pose.Forward.Z,pose.Up.X,pose.Up.Y,pose.Up.Z}.Select(x=>x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
    static void PathCases(Type type)
    {
        var rig=Fixtures.Serial(out _,out var piston,out var head,out var cockpit); var script=Start(type,rig); Run(script,"On");
        var initial=head.WorldMatrix; var a=initial; a.Translation+=Vector3D.Forward*.12; var b=initial; b.Translation-=Vector3D.Forward*.08;
        InvokeFrame(script,rig,"Path "+PCRow(a)+" | "+PCRow(b),UpdateType.Terminal,0);
        var path=Get(script,"LocalPath")!; Check(path!=null && (int)Get(path,"Completed")! ==0,"Whole path was not accepted atomically.");
        double q=piston.CurrentPosition; int observed=0;
        for(int i=0;i<6000 && Get(script,"LocalPath")!=null;i++)
        {
            InvokeFrame(script,rig,"",UpdateType.Update1,1d/60);
            int completed=(int)Get(path,"Completed")!;
            Check(completed>=observed && completed<=observed+1,"Path skipped/reversed waypoint progress.");
            if(completed>observed) { var target=completed==1?a:b; Check(Vector3D.Distance(head.GetPosition(),target.Translation)<.0051,"Waypoint acknowledged away from requested position."); observed=completed; }
            q+=piston.Velocity/60; RecordProxy.Of(piston).Values["CurrentPosition"]=(float)q;
            var pose=initial; pose.Translation+=Vector3D.Forward*(q-5); RecordProxy.Of(head).Values["WorldMatrix"]=pose;
        }
        Check(observed==2 && Get(script,"LocalPath")==null && Enabled(script),"Two-waypoint ideal path failed to complete/resume hold.");
        Check(Vector3D.Distance(head.GetPosition(),b.Translation)<.006,"Path ended at the wrong waypoint.");
        foreach(string invalid in new[]{PCRow(a)+" | 1 2 broken",PCRow(a)+" | 0 0 0 0 0 0 0 1 0",string.Join("|",Enumerable.Repeat(PCRow(a),33)),"NaN 0 0 0 0 -1 0 1 0"})
        { Run(script,"On"); InvokeFrame(script,rig,"Path "+invalid,UpdateType.Terminal,0); Check(!Enabled(script) && Get(script,"LocalPath")==null,"Malformed path accepted a partial prefix."); NoVelocity(rig); }
        Run(script,"On"); InvokeFrame(script,rig,"Path "+PCRow(a),UpdateType.Terminal,0); InvokeFrame(script,rig,"Stop",UpdateType.Terminal,0); for(int i=0;i<15;i++) Run(script);
        Check(!Enabled(script) && Get(script,"LocalPath")==null,"Stop resumed a canceled path."); NoVelocity(rig);
        Run(script,"On"); InvokeFrame(script,rig,"Path "+PCRow(a),UpdateType.Terminal,0); RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-1); InvokeFrame(script,rig,"",UpdateType.Update1,1d/60);
        Check(!Enabled(script) && Get(script,"LocalPath")==null,"Pilot input did not cancel autonomous path ownership."); NoVelocity(rig); RecordProxy.Of(cockpit).Values["MoveIndicator"]=Vector3.Zero;
        Run(script,"On"); InvokeFrame(script,rig,"Path "+PCRow(a),UpdateType.Terminal,0); InvokeFrame(script,rig,"Hold",UpdateType.Terminal,0);
        Check(Enabled(script) && Get(script,"LocalPath")==null,"Hold failed to take over a path.");
        Console.WriteLine("Paths: two ordered world-space poses, measured settling, whole-list rejection, 32-waypoint limit, Stop/pilot cancellation and Hold takeover.");
    }
}
