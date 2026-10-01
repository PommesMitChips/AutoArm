using Sandbox.ModAPI.Ingame;
using VRageMath;

internal static partial class Scenarios
{
    static void BareManualFrames(Type armType,Type toolType)
    {
        ToolToleranceFeedback(armType);
        SlowPrecisionLine(armType);
        foreach(string mode in VCModes)
        {
            var d=new DualRig(armType,toolType,true,true); DualStart(d); d.Command("Mode "+mode);
            var cockpit=d.F.Rig.Blocks.OfType<IMyShipController>().Single();
            var basis=mode=="HEAD"?d.F.Base.WorldMatrix:cockpit.WorldMatrix;
            foreach(string direction in new[]{"Forward","Back","Left","Right","Up","Down"})
            {
                d.Command(direction);
                var expected=direction switch{"Forward"=>basis.Forward,"Back"=>basis.Backward,"Left"=>basis.Left,"Right"=>basis.Right,"Up"=>basis.Up,_=>basis.Down};
                VCNear(-VCVector(d.Arm,"TargetU"),VCInFrame(expected,d.F.Base.WorldMatrix),"Bare socket "+mode+" "+direction);
            }
            var poseArgs=new object?[]{Vector3D.Zero,Vector3D.Zero,Vector3D.Zero};
            Check((bool)d.Arm.GetType().GetMethod("ReadPose",All)!.Invoke(d.Arm,poseArgs)!,"Bare native pose unavailable.");
            var f=(Vector3D)poseArgs[1]!; var u=(Vector3D)poseArgs[2]!;
            VCInput(cockpit,new Vector3(0,0,-1),Vector2.Zero,1);
            var args=new object?[]{f,u,Vector3D.Zero,Vector3D.Zero}; d.Arm.GetType().GetMethod("ReadPilot",All)!.Invoke(d.Arm,args);
            var forward=mode=="HEAD"?-u:VCInFrame(mode=="VRT"?cockpit.WorldMatrix.Up:cockpit.WorldMatrix.Forward,d.F.Base.WorldMatrix);
            VCNear((Vector3D)args[2]!,forward*Convert.ToDouble(Get(d.Arm,"MoveMps")),"Bare keyboard "+mode);
            VCNear((Vector3D)args[3]!,-u*Convert.ToDouble(Get(d.Arm,"TurnDeg"))*Math.PI/180,"Bare Q/E socket roll "+mode);
            VCInput(cockpit,Vector3.Zero,Vector2.Zero);
            var shaft=-VCVector(d.Arm,"TargetU"); d.Command("Step Roll 11");
            VCNear(-VCVector(d.Arm,"TargetU"),shaft,"Bare Roll step changed shaft direction");
            d.Command("OrientTo 1 0 0 0 0 1");
            VCNear(-VCVector(d.Arm,"TargetU"),new Vector3D(1,0,0),"Bare OrientTo socket aim");
            VCNear(VCVector(d.Arm,"TargetF"),new Vector3D(0,0,1),"Bare OrientTo socket up");
            var held=VCVector(d.Arm,"TargetU"); d.Command("MoveTo 1 2 3");
            VCNear(VCVector(d.Arm,"TargetU"),held,"Bare MoveTo transformed held orientation twice");
            poseArgs=new object?[]{Vector3D.Zero,Vector3D.Zero,Vector3D.Zero}; d.Arm.GetType().GetMethod("ReadPose",All)!.Invoke(d.Arm,poseArgs);
            VCNear((Vector3D)poseArgs[1]!,f,"Manual command changed observed docking frame");
            VCNear((Vector3D)poseArgs[2]!,u,"Manual command changed observed docking shaft");
        }
    }
    static void SlowPrecisionLine(Type type)
    {
        var rig=new Rig(); var moving=rig.Grid(); var piston=rig.Piston("Arm 1 - Base - Piston",rig.Root,moving,Vector3D.Zero,Vector3D.Forward);
        var head=rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",moving,Vector3D.Forward*5); rig.Cockpit();
        var script=Start(type,rig); Run(script,"On"); var initial=head.WorldMatrix; var target=initial; target.Translation+=Vector3D.Forward*.02;
        InvokeFrame(script,rig,"Path "+PCRow(target),UpdateType.Terminal,0);
        var path=Get(script,"LocalPath")!; var config=new VRage.Game.ModAPI.Ingame.Utilities.MyIni();
        config.Set("Link","Moves","0.001");config.Set("Link","Turns","5");config.Set("Link","Lines","true");
        Tests.Call(script,"ConfigurePathStages",path,config);
        double q=piston.CurrentPosition,origin=q;
        for(int i=0;i<2400&&Get(script,"LocalPath")!=null;i++)
        {
            InvokeFrame(script,rig,"",UpdateType.Update1,1d/60);
            q+=piston.Velocity/60; RecordProxy.Of(piston).Values["CurrentPosition"]=(float)q;
            var pose=initial; pose.Translation+=Vector3D.Forward*(q-origin);RecordProxy.Of(head).Values["WorldMatrix"]=pose;
            Check(Enabled(script)&&Math.Abs(piston.Velocity)<=.001001,"Slow precision path stopped or exceeded its cap.");
        }
        Check(Get(script,"LocalPath")==null&&Vector3D.Distance(head.GetPosition(),target.Translation)<=.0051,"Precision deadband stranded a minimum-speed linear path.");
    }
    static void ToolToleranceFeedback(Type type)
    {
        foreach(var sample in new (double Position,double Angle,double PosTol,double AngTol,bool Inside)[]{(.004,.4,.01,1,true),(.012,1.2,.01,1,false),(.0014,.09,.003,.2,true)})
        {
            var script=VCStart(type,out var rig,out _,out _,out var head,out _);
            var pose=head.WorldMatrix; pose.Translation+=pose.Forward*sample.Position;
            var spin=Vector3D.Up*(sample.Angle*Math.PI/180);
            pose=MatrixD.CreateWorld(pose.Translation,RotateVector(pose.Forward,spin),RotateVector(pose.Up,spin));
            var args=new object?[]{pose,1d/60,1d/60,.05,2d,sample.PosTol,sample.AngTol,false,"",0d};
            Check((bool)script.GetType().GetMethod("ToolPoseStep",All)!.Invoke(script,args)!,"Tool tolerance feedback stopped motion.");
            if(sample.Inside)
            {
                VCNear(VCVector(script,"LastFeedbackLinear"),Vector3D.Zero,"Tool chased position inside precision band");
                VCNear(VCVector(script,"LastFeedbackAngular"),Vector3D.Zero,"Tool chased orientation inside precision band");
                Check((bool)args[7]!,"Acceptable stopped pose did not settle.");
            }
            else Check(!(bool)args[7]!&&VCVector(script,"LastFeedbackLinear").Length()>0&&VCVector(script,"LastFeedbackAngular").Length()>0,"Outside docking tolerance was accepted.");
        }
        var held=VCStart(type,out _,out _,out _,out var marker,out _);
        held.GetType().GetField("PositionI",All)!.SetValue(held,new Vector3D(.02,0,0));
        var holdArgs=new object?[]{marker.WorldMatrix,1d/60,1d/60,.05,2d,.01,1d,false,"",0d};
        held.GetType().GetMethod("ToolPoseStep",All)!.Invoke(held,holdArgs);
        VCNear(VCVector(held,"LastFeedbackLinear"),new Vector3D(.02,0,0),"Tool precision band erased retained load bias");
    }
    static bool SwapGuard(object tools,out string why)
    {
        var args=new object?[]{""}; bool ok=(bool)tools.GetType().GetMethod("Guard",All)!.Invoke(tools,args)!; why=(string)args[0]!; return ok;
    }
    static void SwapPhaseGuards(Type armType,Type toolType)
    {
        foreach(bool axial in new[]{false,true})
        {
            var d=new DualRig(armType,toolType,axial); DualStart(d); d.Command("Tool 2"); DualPlant(d,"ApproachTop");
            var tools=SwapController(d.Tool); var dest=Get(tools,"Destination")!; var c=d.F.Couplers[1];
            RecordProxy.Of(d.F.Couplers[0]).Values["Angle"]=.4f;
            Check(SwapGuard(tools,out var why),"Irrelevant source phase stopped pickup: "+why);
            RecordProxy.Of(c).Values["Angle"]=(float)(2*Math.PI);
            Check(SwapGuard(tools,out why),"Equivalent full turn stopped pickup: "+why);
            RecordProxy.Of(c).Values["Angle"]=.1f;
            Check(SwapGuard(tools,out why)==axial,"Incoming retained phase guard ignored axial policy: "+why);
            RecordProxy.Of(c).Values["Angle"]=(float)(double)Get(dest,"Angle")!;
            RecordProxy.Of(d.F.Heads[1][0]).Values["IsConnected"]=false;
            d.Tick(); Check(SwapPhase(d.Tool)=="Idle","Lost support survived pickup guard.");
        }
        foreach(bool restore in new[]{true,false})
        {
            var d=new DualRig(armType,toolType); DualStart(d); d.Command("Tool 2"); DualPlant(d,"Lock"); d.F.LockSource();
            RecordProxy.Of(d.F.Markers[0].CubeGrid).Values["EntityId"]=90001L;
            var c=RecordProxy.Of(d.F.Couplers[0]).Values;
            c["IsAttached"]=false;c["Top"]=null;c["TopGrid"]=null; RecordProxy.Of(d.F.ArmTip).Values["Base"]=null;
            for(int i=0;i<8;i++) d.Tick();
            Check(SwapPhase(d.Tool)=="Lock"&&!d.F.AnyDrive,"Verified merge refresh did not wait with stopped output.");
            Check(!d.F.Mutations.Any(m=>m.Kind=="Detach"),"Merge refresh issued premature detach.");
            if(restore)
            {
                c["IsAttached"]=true;c["Top"]=d.F.ArmTip;c["TopGrid"]=d.F.ArmGrid;RecordProxy.Of(d.F.ArmTip).Values["Base"]=d.F.Couplers[0];
                DualPlant(d,"BareSettle");Check(d.F.Mutations.Any(m=>m.Kind=="Detach"&&m.Supported&&m.Stopped),"Recovered merge refresh did not detach safely.");
            }
            else
            { for(int i=0;i<130;i++)d.Tick(); Check(SwapPhase(d.Tool)=="Idle"&&!d.F.AnyDrive,"Permanent source loss escaped the bounded merge refresh wait."); }
        }
        foreach(string failure in new[]{"Top","Owner","Support"})
        {
            var d=new DualRig(armType,toolType); DualStart(d); d.Command("Tool 2"); DualPlant(d,"Lock"); d.F.LockSource();
            var c=RecordProxy.Of(d.F.Couplers[0]).Values;
            c["IsAttached"]=false; c["Top"]=null; RecordProxy.Of(d.F.ArmTip).Values["Base"]=null;
            if(failure=="Top") c["Top"]=d.F.WrongArmPart();
            if(failure=="Owner") RecordProxy.Of(d.F.ArmTip).Values["Base"]=d.F.Couplers[1];
            if(failure=="Support") { d.Tick(); Check((bool)Get(SwapController(d.Tool),"SupportProven")!,"Support-loss test never established its proof."); RecordProxy.Of(d.F.Heads[0][0]).Values["IsConnected"]=false; }
            d.Tick(); Check(SwapPhase(d.Tool)=="Idle"&&!d.F.AnyDrive&&!d.F.Mutations.Any(m=>m.Kind=="Detach"),"Merge refresh accepted contradictory "+failure);
        }
        Console.WriteLine("Control regressions: bare socket manual frames, retained/axial incoming phase, support loss and bounded stopped merge refresh.");
    }
}
