using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

internal static partial class Scenarios
{
    static void DockArrivalCases(Type armType,Type toolType)
    {
        foreach(var trial in new (bool Docking,double Speed,double Span)[]{(false,.019,.5),(true,.019,.5),(true,.12,.5),(true,.019,.005)})
        {
            bool docking=trial.Docking;bool admitted=docking&&trial.Speed<.05&&trial.Span>.01;
            var rig=new Rig();var moving=rig.Grid();var piston=rig.Piston("Arm 1 - Base - Piston",rig.Root,moving,Vector3D.Zero,Vector3D.Forward);
            var head=rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",moving,Vector3D.Forward*5);rig.Cockpit();
            var script=Start(armType,rig);Run(script,"On");var initial=head.WorldMatrix;
            var entry=initial;entry.Translation+=Vector3D.Forward*.01;
            entry=MatrixD.CreateWorld(entry.Translation,RotateVector(initial.Forward,Vector3D.Up*(.07*Math.PI/180)),initial.Up);
            var goal=entry;goal.Translation+=Vector3D.Forward*trial.Span;
            InvokeFrame(script,rig,"Path "+PCRow(entry)+" | "+PCRow(goal),UpdateType.Terminal,0);
            var path=Get(script,"LocalPath")!;var options=new MyIni();options.Set("Link","Docking",docking);
            options.Set("Link","Moves","0|0.05");options.Set("Link","Turns","0|2");options.Set("Link","Lines","false|true");
            Tests.Call(script,"ConfigurePathStages",path,options);
            SwapField(script,"FilteredV",VCInFrame(Vector3D.Forward,piston.WorldMatrix)*trial.Speed);
            SwapField(script,"FilteredW",VCInFrame(Vector3D.Up,piston.WorldMatrix)*(.35*Math.PI/180));
            SwapField(script,"LastQdot",new double[]{.019});
            for(int i=0;i<2;i++)VCTick(script,rig);
            Check((int)Get(path,"Completed")! ==(admitted?1:0),"The reported clearance stall was not separated from strict paths, excessive speed or a short approach.");
            if(admitted)
            {
                Check(piston.Velocity>0,"Admission inserted an unnecessary motor-zero stop.");
                var pose=initial;pose.Translation+=Vector3D.Forward*.1;
                pose=MatrixD.CreateWorld(pose.Translation,RotateVector(goal.Forward,Vector3D.Up*(.8*Math.PI/180)),goal.Up);
                RecordProxy.Of(head).Values["WorldMatrix"]=pose;
                SwapField(script,"ObservationValid",false);SwapField(script,"FilteredV",Vector3D.Zero);SwapField(script,"FilteredW",Vector3D.Zero);SwapField(script,"PositionI",Vector3D.Zero);
                VCTick(script,rig);
                Check((double)Get(script,"LastOrientationErrorDeg")!>.2&&VCVector(script,"LastRequestedLinear").Length()>.01,"Far insertion still demanded final shaft precision before moving.");
                Check((int)Get(path,"Completed")! ==1,"Coarse approach arrival acknowledged the final coupler pose.");
                pose.Translation=goal.Translation-Vector3D.Forward*.01;
                RecordProxy.Of(head).Values["WorldMatrix"]=pose;
                SwapField(script,"ObservationValid",false);SwapField(script,"PositionI",Vector3D.Zero);SwapField(script,"FilteredV",Vector3D.Zero);SwapField(script,"FilteredW",Vector3D.Zero);VCTick(script,rig);
                Check(VCVector(script,"LastRequestedLinear").Length()<1e-9&&(int)Get(path,"Completed")! ==1,"Near-coupler insertion reused the coarse angular gate.");
                pose=MatrixD.CreateWorld(pose.Translation,RotateVector(goal.Forward,Vector3D.Up*(3*Math.PI/180)),goal.Up);
                RecordProxy.Of(head).Values["WorldMatrix"]=pose;
                SwapField(script,"ObservationValid",false);SwapField(script,"PositionI",Vector3D.Zero);VCTick(script,rig);
                Check(VCVector(script,"LastRequestedLinear").Length()<1e-9,"Large shaft misalignment did not pause forward insertion.");
            }
        }
        foreach(var scenario in new (double Scale,double Velocity,double Spin,double Position,double Angle,bool Accept)[]{(0,.008,.35,0,0,false),(2,.008,.35,0,0,true),(2,.1,5,0,0,false),(2,.008,.35,.006,0,false),(2,.008,.35,0,.3,false)})
        {
            var script=VCStart(armType,out _,out _,out _,out var head,out _);var pose=head.WorldMatrix;
            pose.Translation+=pose.Forward*scenario.Position;
            pose=MatrixD.CreateWorld(pose.Translation,RotateVector(pose.Forward,Vector3D.Up*(scenario.Angle*Math.PI/180)),pose.Up);
            SwapField(script,"FilteredV",new Vector3D(scenario.Velocity,0,0));SwapField(script,"FilteredW",new Vector3D(0,0,scenario.Spin*Math.PI/180));
            var args=new object?[]{pose,1d/60,1d/60,.05,2d,.005,.2,false,"",scenario.Scale};
            Check((bool)script.GetType().GetMethod("ToolPoseStep",All)!.Invoke(script,args)!&&(bool)args[7]! ==scenario.Accept,"Dock settling accepted dangerous motion/pose or rejected bounded small jitter.");
        }
        foreach(double clearance in new[]{.01,.5})
        {
            var d=new DualRig(armType,toolType);var config=new MyIni();config.TryParse(d.ToolRig.PB.CustomData);
            config.Set("Tools","ApproachDistance",clearance);config.Set("Tools","PositionTolerance",.005);config.Set("Tools","AngleTolerance",.2);RecordProxy.Of(d.ToolRig.PB).Values["CustomData"]=config.ToString();
            DualStart(d);d.Command("Tool 2");DualPlant(d,"Dock");
            var exact=SwapGoal(d.Tool,"Dock");var stop=SwapMotionGoal(d.Tool,"Dock");var outward=Vector3D.Normalize(SwapGoal(d.Tool,"Approach").Translation-exact.Translation);
            var gap=stop.Translation-exact.Translation;
            Check(gap.Length()>0&&gap.Length()<=.0300001&&gap.Length()<clearance&&Vector3D.Dot(gap,outward)>0,"Parking did not leave a bounded capture gap along the stand normal.");
            VCNear(stop.Forward,exact.Forward,"Capture gap changed recorded parking orientation");
            Check(d.F.Heads[0].All(m=>!m.Enabled),"Support enabled before motion completed and stopped acknowledgement.");
            DualPlant(d,"Idle");for(int i=0;i<20;i++)d.Tick();
            Check(Enabled(d.Arm)&&d.F.Couplers[1].Top==d.F.ArmTip&&d.F.Mutations.All(m=>m.Stopped&&m.Supported),"Capture-gap parking broke safe swap completion.");
        }
        Console.WriteLine("Dock arrival: reported clearance stall, continuous low-speed entry, far/near angular alignment, tolerance-scaled jitter, dangerous-motion refusal and magnetic capture gap.");
    }
}
