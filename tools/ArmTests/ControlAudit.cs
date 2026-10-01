using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

internal static partial class Scenarios
{
    internal static void ControlCompare(Type previousType,Type currentType)
    {
        var oldRig=Fixtures.FullMixed(); var currentRig=Fixtures.FullMixed();
        var oldScript=Start(previousType,oldRig); var currentScript=Start(currentType,currentRig);
        foreach(var item in new[]{(Script:oldScript,Rig:oldRig),(Script:currentScript,Rig:currentRig)})
        {
            var data=new MyIni(); data.TryParse(item.Rig.PB.CustomData); data.Set("Config","PositionDamping",0); data.Set("Config","OrientationDamping",0);
            RecordProxy.Of(item.Rig.PB).Values["CustomData"]=data.ToString(); Run(item.Script,"Reload"); Run(item.Script,"On");
        }
        var oldJoints=oldRig.Blocks.OfType<IMyMechanicalConnectionBlock>().ToArray();
        var currentJoints=currentRig.Blocks.OfType<IMyMechanicalConnectionBlock>().ToArray();
        double maximum=0;
        for(int i=0;i<1800;i++)
        {
            var move=i<1200?new Vector3((float)(.2*Math.Sin(i*.023)),(float)(.15*Math.Cos(i*.017)),(float)(-.3*Math.Sin(i*.013))):Vector3.Zero;
            foreach(var rig in new[]{oldRig,currentRig})
            { var pilot=rig.Blocks.OfType<IMyShipController>().Single(); RecordProxy.Of(pilot).Values["MoveIndicator"]=move; RecordProxy.Of(pilot).Values["RollIndicator"]=i<1200?.1f:0f; }
            InvokeFrame(oldScript,oldRig,"",UpdateType.Update1,1d/60); InvokeFrame(currentScript,currentRig,"",UpdateType.Update1,1d/60);
            Check(Enabled(oldScript)&&Enabled(currentScript),"Matched multi-joint command audit faulted.");
            for(int j=0;j<oldJoints.Length;j++)
            {
                double oldRate=oldJoints[j] is IMyPistonBase p?p.Velocity:((IMyMotorStator)oldJoints[j]).TargetVelocityRad;
                double currentRate=currentJoints[j] is IMyPistonBase q?q.Velocity:((IMyMotorStator)currentJoints[j]).TargetVelocityRad;
                maximum=Math.Max(maximum,Math.Abs(oldRate-currentRate));
            }
        }
        Console.WriteLine($"Matched frozen-geometry mixed/parallel arm: {oldJoints.Length} native actuators, 1800 ticks, maximum per-joint command difference={maximum:R}.");
        Check(maximum<1e-7,"Undamped matched controller commands differ from the historical script.");
    }
    // Diagnostic characterization, not an SE joint-flex or physics model.
    internal static void ControlAudit(Type type)
    {
        bool supportsDamping=type.GetField("OrientationDamping",All)!=null;
        foreach(double damping in supportsDamping?new[]{0d,.2}:new[]{0d})
        {
            var rig=new Rig(); var moving=rig.Grid();
            var rotor=rig.Rotor("Arm 1 - Base - Rotor",rig.Root,moving,Vector3D.Zero,Vector3D.Up);
            var head=rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",moving,Vector3D.Zero);
            var initial=MatrixD.CreateWorld(Vector3D.Zero,Vector3D.Up,Vector3D.Forward);
            RecordProxy.Of(head).Values["WorldMatrix"]=initial; var pilot=rig.Cockpit();
            var script=Start(type,rig); var data=new MyIni(); data.TryParse(rig.PB.CustomData);
            data.Set("Config","HeadTurnSpeed",1); data.Set("Config","OrientationDamping",damping);
            RecordProxy.Of(rig.PB).Values["CustomData"]=data.ToString(); Run(script,"Reload"); Run(script,"On");
            double angle=0,released=0,travel=0,integral=0,firstRequest=0,errorAtRelease=0;
            for(int i=0;i<3600;i++)
            {
                RecordProxy.Of(pilot).Values["RollIndicator"]=i<1800?1f:0f;
                if(i==1800) released=angle;
                InvokeFrame(script,rig,"",UpdateType.Update1,1d/60);
                Check(Enabled(script),"Angular control audit faulted.");
                if(i==1800)
                {
                    integral=((Vector3D)Get(script,"OrientationI")!).Length()*180/Math.PI;
                    firstRequest=((Vector3D)Get(script,"LastRequestedAngular")!).Length()*180/Math.PI;
                    errorAtRelease=(double)Get(script,"LastOrientationErrorDeg")!;
                }
                angle+=rotor.TargetVelocityRad/60;
                RecordProxy.Of(rotor).Values["Angle"]=(float)angle;
                RecordProxy.Of(head).Values["WorldMatrix"]=MatrixD.CreateWorld(Vector3D.Zero,
                    RotateVector(initial.Forward,-Vector3D.Up*angle),RotateVector(initial.Up,-Vector3D.Up*angle));
                if(i>=1800) travel=Math.Max(travel,Math.Abs(angle-released)*180/Math.PI);
            }
            Console.WriteLine($"Full controller angular audit D={damping}, Turn={Get(script,"TurnDeg")}: release error={errorAtRelease:F6} deg, I={integral:F6} deg/s, request={firstRequest:F6} deg/s, post-release travel={travel:F6} deg.");
            Run(script,"Stop"); NoVelocity(rig);
        }
        foreach(double damping in supportsDamping?new[]{0d,.2}:new[]{0d})
        {
            var rig=new Rig(); var moving=rig.Grid();
            var piston=rig.Piston("Arm 1 - Base - Piston",rig.Root,moving,Vector3D.Zero,Vector3D.Forward);
            var head=rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",moving,Vector3D.Forward*5); var pilot=rig.Cockpit();
            var script=Start(type,rig); var data=new MyIni(); data.TryParse(rig.PB.CustomData);
            data.Set("Config","HeadSpeed",.05); data.Set("Config","PositionDamping",damping);
            RecordProxy.Of(rig.PB).Values["CustomData"]=data.ToString(); Run(script,"Reload"); Run(script,"On");
            double position=5,released=0,travel=0,integral=0,firstRequest=0;
            for(int i=0;i<3600;i++)
            {
                RecordProxy.Of(pilot).Values["MoveIndicator"]=i<1800?new Vector3(0,0,-1):Vector3.Zero;
                if(i==1800) released=position;
                InvokeFrame(script,rig,"",UpdateType.Update1,1d/60);
                Check(Enabled(script),"Linear control audit faulted.");
                if(i==1800) { integral=((Vector3D)Get(script,"PositionI")!).Length(); firstRequest=((Vector3D)Get(script,"LastRequestedLinear")!).Length(); }
                position+=piston.Velocity/60;
                RecordProxy.Of(piston).Values["CurrentPosition"]=(float)position;
                RecordProxy.Of(head).Values["WorldMatrix"]=MatrixD.CreateWorld(Vector3D.Forward*position,Vector3D.Forward,Vector3D.Up);
                if(i>=1800) travel=Math.Max(travel,Math.Abs(position-released));
            }
            Console.WriteLine($"Full controller linear audit D={damping}, Speed={Get(script,"MoveMps")}: release I={integral:F6} m/s, request={firstRequest:F6} m/s, post-release travel={travel:F6} m.");
            Run(script,"Stop"); NoVelocity(rig);
        }
    }
}
