using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

internal static partial class Scenarios
{
    internal static string ServoCandidate(string source)
    {
        // Rejected experimental learner, compiled only by --servo-compare.
        // It is never substituted into generated scripts or the normal suite.
        int a=source.IndexOf("    UpdateIntegral(ref PositionI,",StringComparison.Ordinal),b=source.IndexOf("\n\n    LastLinearAdvance",a,StringComparison.Ordinal);
        if(a<0||b<0)throw new Exception("Missing candidate servo boundary.");
        string patch=@"    UpdateIntegral(ref PositionI, LastUnconstrainedLinear - PositionI - FilteredV, 0, positionClipResidual, positionAllocationResidual,
        PositionKi / PositionKp, Math.Min(PositionIntegralCap, ToolDriving ? ToolDriveMoveCap : PositionCorrectionCap), double.MaxValue,
        dt, LastSolverConverged && !LastSolverBudgetLimited && (ToolDriving || LastPositionError <= .10));
    UpdateIntegral(ref OrientationI, LastUnconstrainedAngular - OrientationI - FilteredW, 0, orientationClipResidual, orientationAllocationResidual,
        OrientationKi / OrientationKp, Math.Min(OrientationIntegralCapDeg * Math.PI / 180, ToolDriving ? ToolDriveTurnCap : Math.Min(TurnDeg, OrientationCorrectionCapDeg) * Math.PI / 180), double.MaxValue,
        dt, LastSolverConverged && !LastSolverBudgetLimited && (ToolDriving || LastOrientationErrorDeg <= 10));";
        return source.Substring(0,a)+patch+source.Substring(b);
    }
    internal static void ServoComparison(Type previous,Type revised)
    {
        Console.WriteLine("Experimental velocity-bias learner comparison; this candidate is NOT production code and is rejected for increased undamped release drift.");
        foreach(double lag in new[]{0d,.25,.75})foreach(double damping in new[]{0d,.2})
        {
            var before=ServoAngularRelease(previous,lag,damping);var after=ServoAngularRelease(revised,lag,damping);
            Console.WriteLine($"Angular release lag={lag}, D={damping}: residual bias {before.Bias:F6} -> {after.Bias:F6} deg/s, late travel {before.Travel:F6} -> {after.Travel:F6} deg.");
        }
    }
    static (double Bias,double Travel) ServoAngularRelease(Type type,double lag,double damping)
    {
        var rig=new Rig();var moving=rig.Grid();var rotor=rig.Rotor("Arm 1 - Base - Rotor",rig.Root,moving,Vector3D.Zero,Vector3D.Up);
        var head=rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",moving,Vector3D.Zero);var pilot=rig.Cockpit();
        var initial=MatrixD.CreateWorld(Vector3D.Zero,Vector3D.Up,Vector3D.Forward);RecordProxy.Of(head).Values["WorldMatrix"]=initial;
        var script=Start(type,rig);var data=new MyIni();data.TryParse(rig.PB.CustomData);data.Set("Config","HeadTurnSpeed",1);data.Set("Config","OrientationDamping",damping);
        RecordProxy.Of(rig.PB).Values["CustomData"]=data.ToString();Run(script,"On");
        double angle=0,velocity=0,bias=0,reference=0,travel=0;
        for(int i=0;i<3600;i++)
        {
            RecordProxy.Of(pilot).Values["RollIndicator"]=i<1800?1f:0f;
            InvokeFrame(script,rig,"",UpdateType.Update1,1d/60);Check(Enabled(script),"Release comparison faulted.");
            velocity=lag==0?rotor.TargetVelocityRad:velocity+(rotor.TargetVelocityRad-velocity)*(1-Math.Exp(-1d/(60*lag)));
            angle+=velocity/60;RecordProxy.Of(rotor).Values["Angle"]=(float)angle;
            RecordProxy.Of(head).Values["WorldMatrix"]=MatrixD.CreateWorld(Vector3D.Zero,RotateVector(initial.Forward,-Vector3D.Up*angle),RotateVector(initial.Up,-Vector3D.Up*angle));
            if(i==1920) { reference=angle;bias=VCVector(script,"OrientationI").Length()*180/Math.PI; }
            if(i>1920)travel=Math.Max(travel,Math.Abs(angle-reference)*180/Math.PI);
        }
        return (bias,travel);
    }
}
