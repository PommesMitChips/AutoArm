using System.Reflection;
using VRageMath;

internal static partial class Scenarios
{
    static void PoseCommandsCases(Type type)
    {
        var script=VCStart(type,out var rig,out var rotor,out _,out _,out var cockpit);
        var p=VCVector(script,"TargetP");var f=VCVector(script,"TargetF");var u=VCVector(script,"TargetU");
        VCCommand(script,rig,"MoveTo 1 2 3");
        VCNear(VCVector(script,"TargetP"),new Vector3D(1,2,3),"MoveTo Base Forward/Left/Up");
        VCNear(VCVector(script,"TargetF"),f,"MoveTo holds forward");VCNear(VCVector(script,"TargetU"),u,"MoveTo holds up");
        VCCommand(script,rig,"OrientTo 0 2 0 0 0 3");
        VCNear(VCVector(script,"TargetF"),new Vector3D(0,1,0),"OrientTo normalized forward");
        VCNear(VCVector(script,"TargetU"),new Vector3D(0,0,1),"OrientTo normalized up");
        VCNear(VCVector(script,"TargetP"),new Vector3D(1,2,3),"OrientTo holds position");
        VCCommand(script,rig,"MoveToPosOri .2 .3 .4 1 0 0 0 0 1");
        p=VCVector(script,"TargetP");f=VCVector(script,"TargetF");u=VCVector(script,"TargetU");
        foreach(var command in new[]{"MoveTo 1 2","MoveTo 1 2 3 4","MoveTo NaN 0 0","OrientTo 0 0 0 0 0 1","MoveToPosOri 9 8 7 1 0 0 2 0 0","MoveToPosOri 9 8 7 1 0 0 0 Infinity 0"})
        {
            bool rejected=false;
            try{Tests.Call(script,"RunCommand",command);}catch(TargetInvocationException){rejected=true;}
            Check(rejected,"Invalid pose command accepted: "+command);
            VCNear(VCVector(script,"TargetP"),p,"Invalid pose changed position");VCNear(VCVector(script,"TargetF"),f,"Invalid pose changed forward");VCNear(VCVector(script,"TargetU"),u,"Invalid pose changed up");
        }
        bool set=(bool)Tests.Call(script,"SetPoseTarget",new Vector3D(9,8,7),new Vector3D(1,0,0),new Vector3D(2,0,0),3)!;
        Check(!set,"Shared pose setter accepted collinear orientation");
        VCNear(VCVector(script,"TargetP"),p,"Shared setter partial position mutation");
        VCInput(cockpit,new Vector3(0,0,-1),Vector2.Zero);VCTick(script,rig);
        Check(VCVector(script,"LastPilotLinear").Length()>0,"Pose command disabled pilot input");
        VCNear(VCVector(script,"LastRequestedLinear"),VCVector(script,"LastPilotLinear")+VCVector(script,"LastFeedbackLinear"),"Pose additive pilot feedback");
        VCInput(cockpit,Vector3.Zero,Vector2.Zero);VCCommand(script,rig,"SetHome");VCCommand(script,rig,"GoHome");
        VCCommand(script,rig,"MoveTo .7 .8 .9");Check(!(bool)Get(script,"GoingHome")!,"MoveTo failed to interrupt Home");
        VCNear(VCVector(script,"TargetP"),new Vector3D(.7,.8,.9),"Home interrupt position");
        VCCommand(script,rig,"Off");bool offRejected=false;
        try{Tests.Call(script,"RunCommand","MoveTo 0 0 0");}catch(TargetInvocationException){offRejected=true;}
        Check(offRejected&&!Enabled(script),"Pose command enabled an OFF controller");
    }
}
