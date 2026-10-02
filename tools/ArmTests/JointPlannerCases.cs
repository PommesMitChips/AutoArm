using System.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
internal static partial class Scenarios
{
    static bool JointProfile;
    static object? JointCounter;
    static int JointPeak;
    static T JointCount<T>(Func<T> call){if(!JointProfile)return call();var injector=System.Reflection.Assembly.LoadFrom(Path.Combine(Tests.GameBin,"VRage.Library.dll")).GetType("VRage.Library.Compiler.IlInjector",true)!;JointCounter=injector.GetMethod("BeginRunBlock")!.Invoke(null,new object[]{50000,10000,false})!;try{return call();}finally{JointPeak=Math.Max(JointPeak,(int)JointCounter.GetType().GetProperty("InstructionCount")!.GetValue(JointCounter)!);((IDisposable)JointCounter).Dispose();JointCounter=null;}}
    static object JointCreate(Type type,Rig rig){if(JointProfile){var r=RecordProxy.Of(rig.Runtime);r.Values.Remove("CurrentInstructionCount");r.Call=(m,a)=>m.Name=="get_CurrentInstructionCount"?(JointCounter==null?0:(int)JointCounter.GetType().GetProperty("InstructionCount")!.GetValue(JointCounter)!):m.ReturnType.IsValueType?Activator.CreateInstance(m.ReturnType):null;}return JointCount(()=>Tests.Create(type,rig));}
    static void JointFrame(object host,Rig rig,string command=""){JointCount(()=>{HostFrame(host,rig,command);return 0;});}
    internal static void JointPlannerCases(Type armType,Type plannerType,bool profile=false)
    {
        JointProfile=profile;JointPeak=0;
        var rig=new Rig();var shoulder=rig.Grid();var wrist=rig.Grid();
        var first=rig.Rotor("Arm 1 - Base",rig.Root,shoulder,Vector3D.Zero,Vector3D.Up);
        var second=rig.Rotor("Wrist",shoulder,wrist,new Vector3D(0,.2,-10),Vector3D.Up);
        var head=rig.Block<IMyShipDrill>("Arm 1 - Head",wrist,new Vector3D(0,.4,-20));rig.Cockpit();
        GridCells(rig.Root,new HashSet<Vector3I>{new(0,0,0)},Vector3D.Zero);
        GridCells(shoulder,new HashSet<Vector3I>{new(0,0,0),new(0,0,-4)},new Vector3D(0,.2,0));
        GridCells(wrist,new HashSet<Vector3I>{new(0,0,0),new(0,0,-4)},new Vector3D(0,.4,-10));
        void Part(VRage.Game.ModAPI.Ingame.IMyCubeBlock block,Vector3I cell){var v=RecordProxy.Of(block).Values;v["Position"]=v["Min"]=v["Max"]=cell;}
        Part(first,Vector3I.Zero);Part(first.Top,Vector3I.Zero);Part(second,new(0,0,-4));Part(second.Top,Vector3I.Zero);Part(head,new(0,0,-4));
        var plannerRig=new Rig(rig,"Planner PB");var safetyRig=new Rig(rig,"Collision PB");
        Part(rig.PB,new(4,0,4));Part(plannerRig.PB,new(5,0,4));Part(safetyRig.PB,new(6,0,4));Part(rig.Blocks.OfType<IMyShipController>().First(),new(7,0,4));
        GridCells(rig.Root,new HashSet<Vector3I>{new(0,0,0),new(4,0,4),new(5,0,4),new(6,0,4),new(7,0,4)},Vector3D.Zero);
        foreach(var grid in new[]{rig.Root,shoulder,wrist})RecordProxy.Of(grid).Values["Closed"]=false;
        var data=new MyIni();data.Set("global","Format",7);data.Set("global","HeadSpeed",.2);data.Set("global","Peers","Planner PB | Plan\nCollision PB | Safety");data.Set("Arm 1","ToolSwapPB","");RecordProxy.Of(rig.PB).Values["CustomData"]=data.ToString();
        var config=new MyIni();config.Set("Planner","Format",1);config.Set("Planner","ArmPB","@"+rig.PB.EntityId);config.Set("Planner","Arm","Arm 1");RecordProxy.Of(plannerRig.PB).Values["CustomData"]=config.ToString();
        var bus=new ModuleBus();bus.Bind(rig);bus.Bind(plannerRig);bus.Bind(safetyRig);
        var arm=Tests.Create(armType,rig);HostReady(arm,rig,"Arm 1");var planner=JointCreate(plannerType,plannerRig);long sequence=0,seen=0;
        void Tick(string command="")
        {
            HostFrame(arm,rig);var check=LastCheck(bus,rig.PB.EntityId,safetyRig.PB.EntityId);long next=check.Get("Link","Sequence").ToInt64();
            if(next!=seen){seen=next;SendGuide(arm,rig,safetyRig,bus,Guide(check,++sequence,"0 0","") );}
            JointFrame(planner,plannerRig,command);
        }
        for(int i=0;i<40;i++)Tick();
        var gridStart=new Dictionary<IMyCubeGrid,MatrixD>{{rig.Root,rig.Root.WorldMatrix},{shoulder,shoulder.WorldMatrix},{wrist,wrist.WorldMatrix}};
        var blockLocal=rig.Blocks.ToDictionary(b=>b.EntityId,b=>b.WorldMatrix*MatrixD.Invert(b.CubeGrid.WorldMatrix));
        var topLocal=new Dictionary<IMyCubeBlock,MatrixD>{{first.Top,first.Top.WorldMatrix*MatrixD.Invert(shoulder.WorldMatrix)},{second.Top,second.Top.WorldMatrix*MatrixD.Invert(wrist.WorldMatrix)}};
        var relative=new[]{shoulder.WorldMatrix*MatrixD.Invert(rig.Root.WorldMatrix),wrist.WorldMatrix*MatrixD.Invert(shoulder.WorldMatrix)};
        void Plant()
        {
            foreach(var joint in new[]{first,second})RecordProxy.Of(joint).Values["Angle"]=(float)(joint.Angle+joint.TargetVelocityRad/60);
            int i=0;foreach(var joint in new[]{first,second}){var parent=joint.CubeGrid.WorldMatrix;var frame=blockLocal[joint.EntityId]*parent;var child=relative[i++]*parent;var rotation=MatrixD.CreateFromAxisAngle(-frame.Up,joint.Angle);var position=frame.Translation+Vector3D.TransformNormal(child.Translation-frame.Translation,rotation);child=child.GetOrientation()*rotation;child.Translation=position;RecordProxy.Of(joint.TopGrid).Values["WorldMatrix"]=child;}
            foreach(var block in rig.Blocks)RecordProxy.Of(block).Values["WorldMatrix"]=blockLocal[block.EntityId]*block.CubeGrid.WorldMatrix;
            foreach(var pair in topLocal)RecordProxy.Of(pair.Key).Values["WorldMatrix"]=pair.Value*pair.Key.CubeGrid.WorldMatrix;
        }
        Tick("PreviewTo "+NumberForTest(10+10*Math.Cos(.2))+" "+NumberForTest(-10*Math.Sin(.2))+" .4");
        for(int i=0;i<3000&&(string)Get(planner,"Phase")! !="Preview"&&(string)Get(planner,"Phase")! !="Idle";i++)Tick();
        Check((string)Get(planner,"Phase")! =="Preview","Planner preview failed: "+plannerRig.Log.Last());
        Check(!bus.Sent.Any(x=>x.Source==plannerRig.PB.EntityId&&x.Data.Contains("Operation=PATH")),"Preview sent movement.");
        var world=Get(planner,"World")!;var q=new[]{.2,-.2};var predicted=(MatrixD)world.GetType().GetMethod(PackedProgramChecks.Name(world.GetType(),"FK"),All)!.Invoke(world,new object[]{q})!;
        Check(Vector3D.Distance(predicted.Translation,new Vector3D(10*Math.Sin(.2),.4,-10-10*Math.Cos(.2)))<1e-8,"Joint FK position or rotor sign incorrect.");
        Check(Vector3D.Dot(predicted.Forward,Vector3D.Forward)>1-1e-8,"Joint FK failed compensating wrist orientation.");
        Check(first.Angle==0&&second.Angle==0&&shoulder.WorldMatrix==gridStart[shoulder],"Planning mutated native joints/grids.");
        Tick();Check(!(bool)Get(Get(Core(arm,"Arm 1"),"Services")!,"HoldingModel")!,"Preview retained a posture reservation.");
        string retry="PreviewTo "+NumberForTest(10+10*Math.Cos(.2))+" "+NumberForTest(-10*Math.Sin(.2))+" .4";
        Tick(retry);for(int i=0;i<100&&(string)Get(planner,"Phase")! =="Model";i++)Tick();
        Check((bool)Get(Get(Core(arm,"Arm 1"),"Services")!,"HoldingModel")!,"MODEL did not reserve the starting posture.");
        HostFrame(arm,rig,"Mode HRZ");
        Check(!(bool)Get(Get(Core(arm,"Arm 1"),"Services")!,"HoldingModel")!,"Local command did not release planning reservation.");
        for(int i=0;i<50&&(string)Get(planner,"Phase")! !="Idle";i++)Tick();Check((string)Get(planner,"Phase")! =="Idle","Planner ignored local cancellation.");
        HostFrame(arm,rig,"On");for(int i=0;i<40;i++)Tick();Tick(retry);for(int i=0;i<100&&(string)Get(planner,"Phase")! =="Model";i++)Tick();
        Check((bool)Get(Get(Core(arm,"Arm 1"),"Services")!,"HoldingModel")!,"Planning handshake did not recover after generation change: "+plannerRig.Log.Last());
        for(int i=0;i<40;i++){HostFrame(arm,rig);var check=LastCheck(bus,rig.PB.EntityId,safetyRig.PB.EntityId);SendGuide(arm,rig,safetyRig,bus,Guide(check,++sequence,"0 0"));}
        Check(!(bool)Get(Get(Core(arm,"Arm 1"),"Services")!,"HoldingModel")!,"Lost planner did not expire its reservation.");
        Tick("Cancel");for(int i=0;i<40;i++)Tick();
        Tick("MoveTo "+NumberForTest(10+10*Math.Cos(.2))+" "+NumberForTest(-10*Math.Sin(.2))+" .4");
        for(int i=0;i<12000&&(string)Get(planner,"Phase")! !="Complete"&&(string)Get(planner,"Phase")! !="Idle";i++){Plant();Tick();}
        Check((string)Get(planner,"Phase")! =="Complete","Joint route execution failed: "+plannerRig.Log.Last()+" / "+rig.Log.Last());
        Check(Vector3D.Distance(head.GetPosition(),predicted.Translation)<.005,"Joint-guided execution missed head goal.");
        Check(Enabled(Core(arm,"Arm 1")),"Completed route did not resume ON hold.");
        HostFrame(arm,rig,"StopAll");NoVelocity(rig);
        Console.WriteLine("Joint planner: FK/signs, unchanged native geometry, read-only preview, checked route and actual joint-rate plant completion PASS ("+Tests.Assertions+" assertions).");
        PrismaticPlanning(armType,plannerType);
        if(profile)Console.WriteLine("Installed SE resource rewriter: planner peak "+JointPeak+" / 50000 instructions in the two articulated fixtures.");
    }
    static void PrismaticPlanning(Type armType,Type plannerType)
    {
        var rig=new Rig();var middle=rig.Grid();var end=rig.Grid();
        var a=rig.Piston("Arm 1 - Base",rig.Root,middle,Vector3D.Zero,Vector3D.Right);
        var b=rig.Piston("Second",middle,end,new Vector3D(2,5,0),Vector3D.Forward);
        RecordProxy.Of(a).Values["CurrentPosition"]=RecordProxy.Of(b).Values["CurrentPosition"]=2f;
        var head=rig.Block<IMyShipDrill>("Arm 1 - Head",end,new Vector3D(2,10,-2));var seat=rig.Cockpit();
        var plannerRig=new Rig(rig,"Planner PB");var safetyRig=new Rig(rig,"Collision PB");
        void SetPart(IMyCubeBlock block,Vector3I cell){var v=RecordProxy.Of(block).Values;v["Position"]=v["Min"]=v["Max"]=cell;}
        SetPart(a,Vector3I.Zero);SetPart(a.Top,Vector3I.Zero);SetPart(b,new(0,2,0));SetPart(b.Top,Vector3I.Zero);SetPart(head,new(0,2,0));
        SetPart(rig.PB,new(6,0,6));SetPart(plannerRig.PB,new(7,0,6));SetPart(safetyRig.PB,new(8,0,6));SetPart(seat,new(9,0,6));
        var rootCells=new HashSet<Vector3I>{Vector3I.Zero,new(6,0,6),new(7,0,6),new(8,0,6),new(9,0,6),new(2,4,-2),new(2,2,-2)};
        GridCells(rig.Root,rootCells,Vector3D.Zero);GridCells(middle,new HashSet<Vector3I>{Vector3I.Zero,new(0,2,0)},new Vector3D(2,0,0));GridCells(end,new HashSet<Vector3I>{Vector3I.Zero,new(0,2,0)},new Vector3D(2,5,-2));
        foreach(var grid in new[]{rig.Root,middle,end})RecordProxy.Of(grid).Values["Closed"]=false;
        RecordProxy.Of(a.Top).Values["WorldMatrix"]=middle.WorldMatrix;RecordProxy.Of(b.Top).Values["WorldMatrix"]=end.WorldMatrix;
        var config=new MyIni();config.Set("global","Format",7);config.Set("global","Peers","Planner PB | Plan\nCollision PB | Safety");config.Set("Arm 1","ToolSwapPB","");RecordProxy.Of(rig.PB).Values["CustomData"]=config.ToString();
        var setup=new MyIni();setup.Set("Planner","Format",1);setup.Set("Planner","ArmPB","@"+rig.PB.EntityId);setup.Set("Planner","Arm","Arm 1");setup.Set("Planner","NodeLimit",1024);RecordProxy.Of(plannerRig.PB).Values["CustomData"]=setup.ToString();
        var bus=new ModuleBus();bus.Bind(rig);bus.Bind(plannerRig);bus.Bind(safetyRig);var arm=Tests.Create(armType,rig);HostReady(arm,rig,"Arm 1");var planner=JointCreate(plannerType,plannerRig);long seen=0,sequence=0;
        void Tick(string command=""){HostFrame(arm,rig);var check=LastCheck(bus,rig.PB.EntityId,safetyRig.PB.EntityId);long next=check.Get("Link","Sequence").ToInt64();if(next!=seen){seen=next;SendGuide(arm,rig,safetyRig,bus,Guide(check,++sequence,"0 0"));}JointFrame(planner,plannerRig,command);}
        for(int i=0;i<40;i++)Tick();Tick("PreviewTo 9 10 9");
        for(int i=0;i<2700&&(string)Get(planner,"Phase")! !="Preview"&&(string)Get(planner,"Phase")! !="Idle";i++)Tick();
        Check((string)Get(planner,"Phase")! =="Preview","Prismatic obstacle detour failed: "+plannerRig.Log.Last());
        Check(!(bool)Get(planner,"HeadRoute")!&&(int)Get(planner,"Nodes")! >0,"Obstacle route did not require joint search.");
        var route=(IList)Get(planner,"Route")!;Check(route.Count>2,"Detour omitted intermediate configurations.");
        Check(a.CurrentPosition==2&&b.CurrentPosition==2,"Preview changed native piston positions.");
        Check(!bus.Sent.Any(x=>x.Source==plannerRig.PB.EntityId&&x.Data.Contains("Operation=PATH")),"Obstacle preview commanded movement.");
        Console.WriteLine("Whole-arm detour around a static occupied-cell obstacle: PASS, nodes "+Get(planner,"Nodes")+", route "+route.Count+".");
        // Exercise the same detour through the Arm PB's sole actuator writer.
        var locals=rig.Blocks.ToDictionary(x=>x.EntityId,x=>x.WorldMatrix*MatrixD.Invert(x.CubeGrid.WorldMatrix));
        var tops=new Dictionary<IMyCubeBlock,MatrixD>{{a.Top,a.Top.WorldMatrix*MatrixD.Invert(middle.WorldMatrix)},{b.Top,b.Top.WorldMatrix*MatrixD.Invert(end.WorldMatrix)}};
        void Plant(){RecordProxy.Of(a).Values["CurrentPosition"]=(float)(a.CurrentPosition+a.Velocity/60);RecordProxy.Of(b).Values["CurrentPosition"]=(float)(b.CurrentPosition+b.Velocity/60);var m=middle.WorldMatrix;m.Translation=new Vector3D(a.CurrentPosition,0,0);RecordProxy.Of(middle).Values["WorldMatrix"]=m;var e=end.WorldMatrix;e.Translation=new Vector3D(a.CurrentPosition,5,-b.CurrentPosition);RecordProxy.Of(end).Values["WorldMatrix"]=e;foreach(var block in rig.Blocks)RecordProxy.Of(block).Values["WorldMatrix"]=locals[block.EntityId]*block.CubeGrid.WorldMatrix;foreach(var pair in tops)RecordProxy.Of(pair.Key).Values["WorldMatrix"]=pair.Value*pair.Key.CubeGrid.WorldMatrix;}
        Tick("MoveTo 9 10 9");int samples=0;
        for(;samples<60000&&(string)Get(planner,"Phase")! !="Complete"&&(string)Get(planner,"Phase")! !="Idle";samples++){
            Plant();Tick();
            Check(a.CurrentPosition>=0&&a.CurrentPosition<=10&&b.CurrentPosition>=0&&b.CurrentPosition<=10,"Guided detour crossed piston limits.");
            // Independent occupied-cell oracle for the head and second shaft.
            Check(Math.Abs(a.CurrentPosition-5)>=2.65||Math.Abs(b.CurrentPosition-5)>=2.65,"Head entered inflated obstacle cell.");
            Check(Math.Abs(a.CurrentPosition-5)>=2.65||b.CurrentPosition<=2.35,"Piston shaft entered obstacle cell.");
        }
        Check((string)Get(planner,"Phase")! =="Complete","Prismatic detour execution failed: "+plannerRig.Log.Last()+" / "+rig.Log.Last());
        Check(Vector3D.Distance(head.GetPosition(),new Vector3D(9,10,-9))<.005,"Executed detour missed head destination.");
        Check(Enabled(Core(arm,"Arm 1")),"Detour completion did not resume idle hold.");
        Console.WriteLine("Whole-arm detour execution and independent per-frame head/shaft clearance oracle: PASS ("+samples+" native-rate frames).");
        // A newly occupied interior cell has identical grid bounds and no terminal block.
        var world=Get(planner,"World")!;rootCells.Add(new(1,1,1));
        bool edited=false;for(int i=0;i<200&&!edited;i++)try{JointCount(()=>world.GetType().GetMethod(PackedProgramChecks.Name(world.GetType(),"Audit"),All)!.Invoke(world,null));}catch(System.Reflection.TargetInvocationException error){edited=error.InnerException!.Message.Contains("Interior geometry changed");}
        Check(edited,"Rolling occupancy audit missed an interior non-terminal edit.");rootCells.Remove(new(1,1,1));
        Tick("PreviewTo 5 10 5");
        int paths=bus.Sent.Count(x=>x.Source==plannerRig.PB.EntityId&&x.Data.Contains("Operation=PATH"));
        for(int i=0;i<3000&&(string)Get(planner,"Phase")! !="Idle"&&(string)Get(planner,"Phase")! !="Preview";i++)Tick();
        Check((string)Get(planner,"Phase")! =="Idle","Blocked goal was permitted.");
        Check(paths==bus.Sent.Count(x=>x.Source==plannerRig.PB.EntityId&&x.Data.Contains("Operation=PATH")),"Blocked goal published movement.");
        Tick("PreviewTo 2 10 2");
        for(int i=0;i<200&&(string)Get(planner,"Phase")! =="Model";i++)Tick();
        var peerPose=rig.Root.WorldMatrix;peerPose.Translation+=new Vector3D(.1,0,0);RecordProxy.Of(rig.Root).Values["WorldMatrix"]=peerPose;Tick();
        Check((string)Get(planner,"Phase")! =="Idle","Relative scene motion did not invalidate planning.");
        HostFrame(arm,rig,"StopAll");NoVelocity(rig);
        Console.WriteLine("Interior edits, blocked goals and changed scene refusal: PASS ("+Tests.Assertions+" cumulative assertions).");
    }
    static string NumberForTest(double value)=>value.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
}
