using Sandbox.ModAPI.Ingame;
using VRageMath;

internal static partial class Scenarios
{
    // These plants apply the written velocity plus an explicit constant disturbance.
    // They exercise feedback over time; they do not simulate SE joints or gravity.
    static void GravityHoldCases(Type type)
    {
        foreach(double disturbance in new[]{-.08,-.02,.02,.08})
            GravityPistonHold(type,disturbance);
        foreach(double disturbance in new[]{-.5,.5})
            GravityAngularHold(type,disturbance*Math.PI/180);
        GravityBlockedAllocation(type);
        GravityIndependentAxis(type);
        GravityIntegralRetention(type);
        GravityAngularPilot(type);
        Console.WriteLine("Gravity hold: signed constant velocity disturbances, fixed-target settling, retained bias, load removal, angular learning, additive pilot priority, bounded allocation and independent-axis learning (ideal plants only).");
    }

    static void GravityPistonHold(Type type,double disturbance)
    {
        var rig=new Rig();var moving=rig.Grid();
        var piston=rig.Piston("Arm 1 - Base - Piston",rig.Root,moving,Vector3D.Zero,Vector3D.Forward);
        var head=rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",moving,Vector3D.Forward*5);
        var cockpit=rig.Cockpit();var script=Start(type,rig);Run(script,"On");
        var target=(Vector3D)Get(script,"TargetP")!;
        double q=5,load=disturbance;float previous=piston.Velocity;
        Action tick=()=>
        {
            Run(script);
            Check(Enabled(script),"Constant-load piston hold unexpectedly disabled.");
            Check(Math.Abs(piston.Velocity)<=.200001,"Constant-load piston speed cap exceeded.");
            Check(Math.Abs(piston.Velocity-previous)<=.4/60+1e-6,"Constant-load piston acceleration cap exceeded.");
            previous=piston.Velocity;
            Check(((Vector3D)Get(script,"PositionI")!).Length()<=.200001,"Position integral cap exceeded.");
            q+=(piston.Velocity+load)/60;
            RecordProxy.Of(piston).Values["CurrentPosition"]=(float)q;
            RecordProxy.Of(head).Values["WorldMatrix"]=MatrixD.CreateWorld(Vector3D.Forward*q,Vector3D.Forward,Vector3D.Up);
        };
        for(int i=0;i<25*60;i++)tick();
        Check(Math.Abs(q-5)<.0036,$"Piston hold retained constant-load error: disturbance={disturbance}, error={q-5}.");
        Check((((Vector3D)Get(script,"TargetP")!)-target).Length()<1e-12,"Idle hold moved its captured target to follow a disturbance.");
        var learned=(Vector3D)Get(script,"PositionI")!;
        Check(learned.Length()>.90*Math.Abs(disturbance),"Piston hold did not learn the constant load as a velocity bias.");
        Check(Math.Abs(piston.Velocity+disturbance)<.001,"Piston hold command did not cancel the sustained disturbance.");
        double maximumError=0;
        for(int i=0;i<30*60;i++){tick();maximumError=Math.Max(maximumError,Math.Abs(q-5));}
        Check(maximumError<.0036,"Learned piston bias decayed and allowed drift during prolonged hold.");
        Check(((Vector3D)Get(script,"PositionI")!).Length()>.90*Math.Abs(disturbance),"Learned piston bias was lost inside the deadband.");

        // Oppose the learned load compensation with a deliberately slow live input.
        // The correction may oppose at most half of the pilot speed.
        var direction=disturbance>0?-1f:1f;
        type.GetField("MoveMps",All)!.SetValue(script,.04);
        RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,direction);
        bool compensationOpposedPilot=false;
        for(int i=0;i<120;i++)
        {
            tick();
            var pilot=(Vector3D)Get(script,"LastPilotLinear")!;
            var feedback=(Vector3D)Get(script,"LastFeedbackLinear")!;
            var request=(Vector3D)Get(script,"LastRequestedLinear")!;
            Check(pilot.Length()>.039999,"Learned bias suppressed live pilot demand.");
            Check((request-pilot-feedback).Length()<1e-10,"Learned bias broke additive pilot demand.");
            Check(Vector3D.Dot(feedback,pilot)>=-.500001*pilot.LengthSquared(),"Learned bias exceeded the half-pilot opposition bound.");
            Check(Vector3D.Dot(request,pilot)>=.499999*pilot.LengthSquared(),"Opposing learned bias reversed the requested pilot motion.");
            compensationOpposedPilot|=Vector3D.Dot(feedback,pilot)<-1e-6;
        }
        Check(compensationOpposedPilot,"Live-input fixture did not oppose a learned compensation bias.");
        if(Math.Abs(disturbance)>.04)
        {
            RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-direction);
            load=disturbance*3;
            bool actuallyOpposedMovement=false;
            for(int i=0;i<120;i++)
            {
                tick();
                var pilot=(Vector3D)Get(script,"LastPilotLinear")!;
                var feedback=(Vector3D)Get(script,"LastFeedbackLinear")!;
                var request=(Vector3D)Get(script,"LastRequestedLinear")!;
                Check(pilot.Length()>.039999&&(request-pilot-feedback).Length()<1e-10,"Physical drift against input suppressed or replaced live pilot demand.");
                Check(Vector3D.Dot(feedback,pilot)>=-.500001*pilot.LengthSquared(),"Actual opposed movement defeated the half-pilot correction bound.");
                actuallyOpposedMovement|=(piston.Velocity+load)*direction<0;
            }
            Check(actuallyOpposedMovement,"Opposed-movement fixture did not exhibit physical drift against the pilot command.");
            load=disturbance;
        }
        RecordProxy.Of(cockpit).Values["MoveIndicator"]=Vector3.Zero;
        // Recapture the current pose to give load-removal recovery its own fixed target.
        Run(script,"Hold");previous=piston.Velocity;
        double recoveryTarget=q;
        for(int i=0;i<25*60;i++)tick();
        Check(Math.Abs(q-recoveryTarget)<.0036,"Piston did not settle after manual release.");
        load=0;
        for(int i=0;i<25*60;i++)tick();
        Check(Math.Abs(q-recoveryTarget)<.0036,"Removing a constant load left a persistent hold offset.");
        Check(((Vector3D)Get(script,"PositionI")!).Length()<.001,"Learned piston bias did not unwind after load removal.");
        Check(Math.Abs(piston.Velocity)<.001,"Piston retained load-compensation motion after load removal.");
        Run(script,"Stop");NoVelocity(rig);
    }

    static void GravityAngularHold(Type type,double disturbance)
    {
        var rig=new Rig();var moving=rig.Grid();
        var rotor=rig.Rotor("Arm 1 - Base - Rotor",rig.Root,moving,Vector3D.Zero,Vector3D.Up);
        // A tool at the pivot isolates rotation from translational feedback.
        var head=rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",moving,Vector3D.Zero);rig.Cockpit();
        var script=Start(type,rig);Run(script,"OrientationTolerance 0.1");Run(script,"On");
        var targetForward=(Vector3D)Get(script,"TargetF")!;
        double angle=0,load=disturbance;float previous=rotor.TargetVelocityRad;
        Action tick=()=>
        {
            Run(script);Check(Enabled(script),"Constant-load angular hold unexpectedly disabled.");
            Check(Math.Abs(rotor.TargetVelocityRad)<=.5*Math.PI/30+1e-6,"Angular hold speed cap exceeded.");
            Check(Math.Abs(rotor.TargetVelocityRad-previous)<=Math.PI/30/60+1e-6,"Angular hold acceleration cap exceeded.");
            previous=rotor.TargetVelocityRad;
            Check(((Vector3D)Get(script,"OrientationI")!).Length()<=5*Math.PI/180+1e-6,"Angular integral cap exceeded.");
            angle+=(rotor.TargetVelocityRad+load)/60;
            RecordProxy.Of(rotor).Values["Angle"]=(float)angle;
            var rotation=Vector3D.Up*-angle;
            RecordProxy.Of(head).Values["WorldMatrix"]=MatrixD.CreateWorld(Vector3D.Zero,RotateVector(Vector3D.Forward,rotation),RotateVector(Vector3D.Up,rotation));
        };
        for(int i=0;i<30*60;i++)tick();
        Check(Math.Abs(angle)<.13*Math.PI/180,"Angular hold failed to settle near its configured deadband.");
        Check(((Vector3D)Get(script,"OrientationI")!).Length()>.85*Math.Abs(disturbance),"Angular integral did not learn a nonzero constant load.");
        Check((((Vector3D)Get(script,"TargetF")!)-targetForward).Length()<1e-12,"Idle angular hold followed the disturbed orientation.");
        double maximumAngle=0;
        for(int i=0;i<30*60;i++){tick();maximumAngle=Math.Max(maximumAngle,Math.Abs(angle));}
        Check(maximumAngle<.13*Math.PI/180,"Angular load bias decayed during prolonged deadband hold.");
        load=0;
        for(int i=0;i<30*60;i++)tick();
        Check(Math.Abs(angle)<.13*Math.PI/180,"Angular load removal left a persistent held-pose offset.");
        Check(((Vector3D)Get(script,"OrientationI")!).Length()<.03*Math.PI/180,"Angular load bias failed to unwind after removal.");
        Run(script,"Stop");NoVelocity(rig);
    }

    static void GravityBlockedAllocation(Type type)
    {
        var rig=new Rig();var moving=rig.Grid();
        var piston=rig.Piston("Arm 1 - Base - Piston",rig.Root,moving,Vector3D.Zero,Vector3D.Forward);
        var head=rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",moving,Vector3D.Forward*5);rig.Cockpit();
        var script=Start(type,rig);Run(script,"On");
        RecordProxy.Of(piston).Values["MinLimit"]=5f;RecordProxy.Of(piston).Values["MaxLimit"]=5f;
        var matrix=head.WorldMatrix;matrix.Translation+=Vector3D.Backward*.05;
        RecordProxy.Of(head).Values["WorldMatrix"]=matrix;
        for(int i=0;i<20*60;i++)
        {
            Run(script);Check(Enabled(script),"Blocked piston disabled the controller.");
            Check(Math.Abs(piston.Velocity)<1e-10,"Blocked piston received infeasible velocity.");
            Check(((Vector3D)Get(script,"PositionI")!).Length()<1e-8,"Physical bound permitted constant-error integral windup.");
        }
        var unconstrained=(Vector3D)Get(script,"LastUnconstrainedLinear")!;
        var achieved=(Vector3D)Get(script,"LastAchievedLinear")!;
        Check(unconstrained.Length()>.05&&achieved.Length()<1e-10,"Bounded allocation did not expose a nonzero unconstrained reference.");
        Run(script,"Stop");NoVelocity(rig);
    }

    static void GravityIndependentAxis(Type type)
    {
        var rig=new Rig();var stage=rig.Grid();var tool=rig.Grid();
        var x=rig.Piston("Arm 1 - Base - Piston",rig.Root,stage,Vector3D.Zero,Vector3D.Right);
        var y=rig.Piston("independent vertical piston",stage,tool,Vector3D.Right*5,Vector3D.Up);
        RecordProxy.Of(x).Values["MinLimit"]=5f;RecordProxy.Of(x).Values["MaxLimit"]=5f;
        var head=rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",tool,Vector3D.Right*5+Vector3D.Up*5);rig.Cockpit();
        var script=Start(type,rig);Run(script,"On");double q=5;
        for(int i=0;i<25*60;i++)
        {
            Run(script);Check(Enabled(script),"Independent-axis load fixture disabled.");
            Check(Math.Abs(x.Velocity)<1e-10,"Locked independent axis received velocity.");
            q+=(y.Velocity+.02)/60;
            RecordProxy.Of(y).Values["CurrentPosition"]=(float)q;
            RecordProxy.Of(head).Values["WorldMatrix"]=MatrixD.CreateWorld(Vector3D.Right*5+Vector3D.Up*q,Vector3D.Forward,Vector3D.Up);
        }
        Check(Math.Abs(q-5)<.0036,"An independent physical bound suppressed vertical bias learning.");
        Check(((Vector3D)Get(script,"PositionI")!).Length()>.018,"Unbounded vertical axis did not learn its constant load.");
        Run(script,"Stop");NoVelocity(rig);
    }

    static void GravityIntegralRetention(Type type)
    {
        var rig=Fixtures.Serial(out _,out _,out _,out _);var script=Start(type,rig);
        var method=type.GetMethod("UpdateIntegral",All)!;
        Func<Vector3D,Vector3D,bool,Vector3D> advance=(integral,error,permit)=>
        {
            object[] args={integral,error,.003,Vector3D.Zero,Vector3D.Zero,.5,.02,.1,1d/60,permit};
            method.Invoke(script,args);return (Vector3D)args[0];
        };
        var learned=new Vector3D(.015,0,0);
        var retained=learned;
        for(int i=0;i<30*60;i++)retained=advance(retained,new Vector3D(.001,0,0),true);
        Check((retained-learned).Length()<1e-12,"Permitted in-deadband feedback erased learned load compensation.");
        for(int i=0;i<10*60;i++)retained=advance(retained,Vector3D.Zero,false);
        Check(retained.Length()<learned.Length()*.01,"Integral survived prolonged loss of permission.");
        retained=Vector3D.Zero;
        for(int i=0;i<10*60;i++)retained=advance(retained,new Vector3D(.05,0,0),true);
        Check(Math.Abs(retained.Length()-.02)<1e-12,"Integral failed to respect the supplied velocity-bias cap.");
    }

    static void GravityAngularPilot(Type type)
    {
        foreach(float input in new[]{-.1f,.1f})
        {
            var rig=Fixtures.Serial(out _,out _,out _,out var cockpit);var script=Start(type,rig);Run(script,"On");
            var forward=(Vector3D)Get(script,"TargetF")!;
            type.GetField("OrientationI",All)!.SetValue(script,-forward*(input>0?1:-1)*4*Math.PI/180);
            RecordProxy.Of(cockpit).Values["RollIndicator"]=input;Run(script);
            var pilot=(Vector3D)Get(script,"LastPilotAngular")!;var feedback=(Vector3D)Get(script,"LastFeedbackAngular")!;
            var requested=(Vector3D)Get(script,"LastRequestedAngular")!;
            Check(pilot.Length()>0&&(requested-pilot-feedback).Length()<1e-12,"Angular load bias replaced live roll input.");
            Check(Vector3D.Dot(feedback,pilot)<0&&Vector3D.Dot(feedback,pilot)>=-.500001*pilot.LengthSquared(),"Angular load bias exceeded half-pilot opposition.");
        }
    }
}
