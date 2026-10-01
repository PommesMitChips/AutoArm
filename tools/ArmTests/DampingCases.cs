using Sandbox.ModAPI.Ingame;
using VRageMath;

internal static partial class Scenarios
{
    static void DampingCases(Type type)
    {
        foreach(double lag in new[]{.25,.75})
        {
            var previous=DelayedPiston(type,lag,0); var damped=DelayedPiston(type,lag,.2);
            Console.WriteLine($"Delayed velocity plant tau={lag}: peak overshoot {previous.Peak:F4} -> {damped.Peak:F4}; error integral {previous.Error:F4} -> {damped.Error:F4}.");
            Check(damped.Peak<=previous.Peak,"Velocity damping increased peak overshoot in the delayed-response plant.");
        }
    }
    static (double Peak,double Error) DelayedPiston(Type type,double lag,double damping)
    {
        var rig=new Rig(); var moving=rig.Grid();
        var piston=rig.Piston("Arm 1 - Base - Piston",rig.Root,moving,Vector3D.Zero,Vector3D.Forward);
        var head=rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",moving,Vector3D.Forward*5); var cockpit=rig.Cockpit();
        var script=Start(type,rig); var data=new VRage.Game.ModAPI.Ingame.Utilities.MyIni(); data.TryParse(rig.PB.CustomData);
        data.Set("Config","PositionDamping",damping); RecordProxy.Of(rig.PB).Values["CustomData"]=data.ToString(); Run(script,"On");
        double q=5,velocity=0,errorIntegral=0,peak=0;
        for(int i=0;i<240+1200;i++)
        {
            RecordProxy.Of(cockpit).Values["MoveIndicator"]=i<240?new Vector3(0,0,-1):Vector3.Zero;
            Run(script); Check(Enabled(script),"Damped delayed-response controller faulted.");
            if(i>=240)
            {
                errorIntegral+=(double)Get(script,"LastPositionError")!/60;
                var target=(Vector3D)Get(script,"TargetP")!; var basis=piston.WorldMatrix;
                var world=piston.GetPosition()+basis.Forward*target.X+basis.Left*target.Y+basis.Up*target.Z;
                peak=Math.Max(peak,q-Vector3D.Dot(world,Vector3D.Forward));
            }
            velocity+=(piston.Velocity-velocity)*(1-Math.Exp(-1d/(60*lag)));
            q+=velocity/60;
            RecordProxy.Of(piston).Values["CurrentPosition"]=(float)q;
            RecordProxy.Of(head).Values["WorldMatrix"]=MatrixD.CreateWorld(Vector3D.Forward*q,Vector3D.Forward,Vector3D.Up);
            Check(Math.Abs(piston.Velocity)<=.200001,"Damping exceeded the piston drive cap.");
        }
        Check((double)Get(script,"LastPositionError")!<.005,"Damped delayed-response plant did not settle.");
        Run(script,"Stop"); NoVelocity(rig); return (peak,errorIntegral);
    }
}
