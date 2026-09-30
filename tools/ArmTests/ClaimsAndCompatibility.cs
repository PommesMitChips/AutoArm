using System.Reflection;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

internal static partial class Scenarios
{
    static void ClaimsAndCompatibility(Type type)
    {
        UnequalRoutes(type);
        AlignmentBoundary(type);
        SnapshotGuarantees(type);
        SchemaCompatibility(type);
        ManualPriorityCompatibility(type);
        IntegralConstraints(type);
        UserControlCompatibility(type);
        OffsetSerialGeometry(type);
        PhysicalStallLead(type);
    }

    static void UnequalRoutes(Type type)
    {
        foreach(var lengths in new[]{(Left:1,Right:2),(Left:2,Right:3)})
        {
            var rig=new Rig();var fork=rig.Grid();var join=rig.Grid();
            rig.Rotor("Arm 1 - Base - Rotor",rig.Root,fork);
            for(int branch=0;branch<2;branch++)
            {
                int count=branch==0?lengths.Left:lengths.Right;var grid=fork;
                for(int stage=0;stage<count;stage++)
                {
                    var next=stage==count-1?join:rig.Grid();
                    rig.Piston($"branch {branch} stage {stage}",grid,next,new Vector3D(branch,0,-stage*5),Vector3D.Forward);grid=next;
                }
            }
            rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",join,new Vector3D(0,0,-20));rig.Cockpit();
            var script=Start(type,rig);Run(script,"On");
            Check(!Enabled(script),"Unequal split/rejoin lengths were silently grouped.");
            Check(Groups(script).Length==0,"A rejected unequal route published partial control ownership.");
            NoVelocity(rig);
            Console.WriteLine($"Unequal {lengths.Left}/{lengths.Right} branch diagnostic: {rig.Log.FirstOrDefault(l=>l.Contains("Discovery failed"))}");
        }
    }

    static void AlignmentBoundary(Type type)
    {
        // Put the reference at the first pivot to isolate angular tolerance from the separate lever-arm discrepancy check.
        foreach(double degrees in new[]{.49,.51,.7,1.0})
        {
            var rig=Fixtures.Parallel(false,out _,out var second);
            var head=rig.Blocks.Single(b=>b.CustomName.StartsWith("Arm 1 - Head"));
            RecordProxy.Of(head).Values["WorldMatrix"]=MatrixD.CreateWorld(Vector3D.Zero,Vector3D.Forward,Vector3D.Up);
            double a=degrees*Math.PI/180;
            var axis=new Vector3D(Math.Sin(a),Math.Cos(a),0);
            RecordProxy.Of(second).Values["WorldMatrix"]=MatrixD.CreateWorld(Vector3D.Zero,Vector3D.Forward,axis);
            var script=Start(type,rig);Run(script,"On");
            Check(Enabled(script)==(degrees<.5),"Unexpected parallel-axis acceptance at "+degrees+" degrees.");
            Run(script,"Stop");NoVelocity(rig);
        }
    }

    static void SnapshotGuarantees(Type type)
    {
        var rig=Fixtures.Serial(out _,out var piston,out var head,out _);var script=Start(type,rig);Run(script,"On");
        int count=rig.Blocks.OfType<IMyMechanicalConnectionBlock>().Count();
        RecordProxy.Of(piston).Values["TopGrid"]=rig.Grid();Run(script);
        Check(rig.Blocks.OfType<IMyMechanicalConnectionBlock>().Count()==count&&!Enabled(script),"Same-count reparenting escaped validation.");NoVelocity(rig);
        var renamed=Fixtures.Serial(out _,out _,out var tool,out _);
        var spare=renamed.Block<IMyShipDrill>("spare",tool.CubeGrid);var active=Start(type,renamed);Run(active,"On");
        int total=renamed.Blocks.Count,mechanical=renamed.Blocks.OfType<IMyMechanicalConnectionBlock>().Count();
        RecordProxy.Of(spare).Values["CustomName"]="Arm 1 - Head duplicate";
        for(int i=0;i<30&&Enabled(active);i++)Run(active);
        Check(renamed.Blocks.Count==total&&renamed.Blocks.OfType<IMyMechanicalConnectionBlock>().Count()==mechanical&&!Enabled(active),"Same-count marker rename escaped validation.");NoVelocity(renamed);
        var budget=Fixtures.Serial(out _,out _,out _,out _);var protectedScript=Start(type,budget);Run(protectedScript,"On");
        int before=budget.InventoryCalls;
        RecordProxy.Of(budget.Runtime).Values["CurrentInstructionCount"]=(int)(budget.Runtime.MaxInstructionCount*.60);Run(protectedScript,"Check");
        Check(budget.InventoryCalls==before,"Low-budget validation still enumerated terminals.");
        Check(!Enabled(protectedScript),"Pre-enumeration budget refusal left motion enabled.");NoVelocity(budget);
    }

    static void SchemaCompatibility(Type type)
    {
        var rig=Fixtures.Serial(out _,out _,out _,out _);var script=Start(type,rig);
        var ini=new MyIni();ini.TryParse(rig.PB.CustomData);
        Check(ini.Get("AutoArm","Format").ToInt32()==4,"Generated config has no supported Format 4 tag.");
        Check(!ini.ContainsKey("AutoArm","Fingerprint"),"Fingerprint still stored in editable configuration.");
        Check(!rig.PB.CustomData.Contains("R01 = grid"),"Rigid-grid diagnostic was moved into another persistent INI key.");
        Check(((string)Get(Get(script,"Topology")!,"Report")!).Contains("R01 = grid"),"Rigid-grid IDs disappeared from diagnostics.");
        string current=rig.PB.CustomData,storage=((TestHost)script).Storage;
        var rejected=new List<(string Label,string Text)>();
        foreach(string format in new[]{"1","2","3","5","99","bogus","0","-1","1.5","3.0","4.0",""})
        {
            var candidate=FCIni(current);candidate.Set("AutoArm","Format",format);rejected.Add(("format "+format,candidate.ToString()));
        }
        var missingFormat=FCIni(current);missingFormat.Delete("AutoArm","Format");rejected.Add(("missing format",missingFormat.ToString()));
        var missingArm=FCIni(current);missingArm.Delete("AutoArm","Arm");rejected.Add(("missing arm",missingArm.ToString()));
        var wrongArm=FCIni(current);wrongArm.Set("AutoArm","Arm","Arm 2");rejected.Add(("different arm",wrongArm.ToString()));
        rejected.Add(("missing header","[Other]\nKeep=untouched\n"));
        rejected.Add(("untagged legacy","[MArmOS]\nArm=Arm 1\n[MArmOS Group 01]\nTranslation=0.37\nOrientation=2.5\n"));
        rejected.Add(("malformed INI","non-INI user notes that must survive"));
        foreach(var candidate in rejected)
        {
            RecordProxy.Of(rig.PB).Values["CustomData"]=current;Run(script,"Reload");Run(script,"On");
            Check(Enabled(script),"Schema refusal precondition failed: "+candidate.Label);
            rig.Log.Clear();RecordProxy.Of(rig.PB).Values["CustomData"]=candidate.Text;
            Run(script,"Reload");Run(script,"On");
            Check(!Enabled(script)&&rig.PB.CustomData==candidate.Text&&((TestHost)script).Storage==storage,"Rejected configuration was overwritten or enabled motion: "+candidate.Label);
            Check(rig.Log.Any(x=>x.Contains("delete Custom Data",StringComparison.OrdinalIgnoreCase)),"Schema refusal omitted regeneration instructions: "+candidate.Label);
            NoVelocity(rig);

            var firstBoot=Fixtures.Serial(out _,out _,out _,out _);firstBoot.Storage="opaque saved state that must survive";
            RecordProxy.Of(firstBoot.PB).Values["CustomData"]=candidate.Text;
            var refused=Start(type,firstBoot);Run(refused,"On");
            Check(!Enabled(refused)&&firstBoot.PB.CustomData==candidate.Text&&((TestHost)refused).Storage==firstBoot.Storage,"Invalid first-boot configuration mutated external state: "+candidate.Label);
            Check(firstBoot.Log.Any(x=>x.Contains("delete Custom Data",StringComparison.OrdinalIgnoreCase)),"First-boot refusal omitted regeneration instructions: "+candidate.Label);
            NoVelocity(firstBoot);
        }
    }

    static void ManualPriorityCompatibility(Type type)
    {
        var reference=Tests.Script(File.ReadAllText(Path.Combine(Tests.Workspace,"reference","MiningArm_v2_0_HeadOnly.txt")));
        var oldBlend=reference.GetMethod("BlendManualCorrection",All)!;
        var newBlend=type.GetMethod("BlendManualCorrection",All) ?? throw new Exception("Missing reviewable manual blend helper.");
        var rig=Fixtures.Serial(out _,out _,out _,out _);var script=Start(type,rig);
        var random=new Random(20260930);
        Func<Vector3D> vector=()=>new Vector3D(random.NextDouble()-.5,random.NextDouble()-.5,random.NextDouble()-.5);
        for(int i=0;i<3000;i++)
        {
            var pilot=i%20==0?Vector3D.Zero:vector();var correction=vector();double fraction=i%3*.5;
            object?[] a={pilot,correction,fraction,false},b={pilot,correction,fraction,false};
            var expected=(Vector3D)oldBlend.Invoke(null,a)!;var actual=(Vector3D)newBlend.Invoke(script,b)!;
            Check((actual-expected).Length()<1e-12&&Equals(a[3],b[3]),"Manual opposition blend differs from the supplied working reference.");
            if(pilot.LengthSquared()<1e-20){Check(actual==correction,"Idle correction was restricted by manual priority.");continue;}
            var direction=Vector3D.Normalize(pilot);
            Check(Vector3D.Dot(pilot+actual,direction)>=(1-fraction)*pilot.Length()-1e-12,"Manual requested-velocity floor failed.");
            Check(Vector3D.Cross(actual-correction,direction).Length()<1e-12,"Manual priority modified perpendicular correction.");
        }
        Console.WriteLine("Claim regressions: unequal paths, axis boundaries, same-count graph/marker changes, enumeration pre-guard, versioned config and 3,000 differential manual-priority cases.");
    }

    static void UserControlCompatibility(Type type)
    {
        var rig=Fixtures.Serial(out _,out _,out var head,out var cockpit);var script=Start(type,rig);Run(script,"MiningArm On");
        Check(Enabled(script),"Legacy MiningArm command prefix no longer enables.");
        Run(script,"-s 0.15");Check(Convert.ToDouble(Get(script,"MoveMps"))==.15,"Legacy speed alias failed.");
        RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-1);RecordProxy.Of(cockpit).Values["RollIndicator"]=1f;
        Run(script,"ReadKeyboard Off; ReadRoll Off");
        Check(((Vector3D)Get(script,"LastPilotLinear")!).LengthSquared()==0&&((Vector3D)Get(script,"LastPilotAngular")!).LengthSquared()==0,"Input-off flags ignored.");
        Run(script,"ReadKeyboard On; ReadRoll On");
        Check(((Vector3D)Get(script,"LastPilotLinear")!).LengthSquared()>0&&((Vector3D)Get(script,"LastPilotAngular")!).LengthSquared()>0,"Input-on flags did not restore pilot channels.");
        Run(script,"Stop");RecordProxy.Of(cockpit).Values["MoveIndicator"]=Vector3.Zero;RecordProxy.Of(cockpit).Values["RollIndicator"]=0f;
        Run(script,"On");
        var position=head.GetPosition();RecordProxy.Of(head).Values["WorldMatrix"]=MatrixD.CreateWorld(position+Vector3D.Forward*.002,Vector3D.Forward,Vector3D.Up);Run(script);
        Check(((Vector3D)Get(script,"LastFeedbackLinear")!).LengthSquared()<1e-18,"Position deadband no longer suppresses 2 mm idle jitter.");
        Run(script,"Hold");
        var tilted=Vector3D.Normalize(Vector3D.Forward+Math.Tan(2*Math.PI/180)*Vector3D.Right);
        RecordProxy.Of(head).Values["WorldMatrix"]=MatrixD.CreateWorld(head.GetPosition(),tilted,Vector3D.Up);Run(script);
        Check(((Vector3D)Get(script,"LastFeedbackAngular")!).LengthSquared()<1e-18,"Idle orientation tolerance no longer allows 2 degrees.");
        Run(script,"Hold; TurnSpeed 1; Yaw 30");
        Check(((Vector3D)Get(script,"LastFeedbackAngular")!).Length()<=Math.PI/180+1e-10,"TurnSpeed 1 did not cap a preset at 1 degree per second.");
        Run(script,"Hold; TurnSpeed 10; Yaw 30");
        Check(((Vector3D)Get(script,"LastFeedbackAngular")!).Length()<=5*Math.PI/180+1e-10,"Preset angular correction exceeded its separate 5 degree/s cap.");
        Run(script,"Hold; Yaw 2");
        Check(((Vector3D)Get(script,"LastFeedbackAngular")!).LengthSquared()>1e-8,"A 2-degree requested step was swallowed by the relaxed idle band.");
        Run(script,"OrientationTolerance 4");Check(!Enabled(script),"Changing orientation tolerance should stop before recapture.");
        Console.WriteLine("User controls: familiar aliases/prefix, keyboard and roll toggles, idle deadbands, tighter aiming and TurnSpeed preset caps.");
    }

    static void IntegralConstraints(Type type)
    {
        var rig=Fixtures.Serial(out _,out _,out _,out _);var script=Start(type,rig);
        var method=type.GetMethod("UpdateIntegral",All)!;
        Func<Vector3D,Vector3D,Vector3D,Vector3D,Vector3D> advance=(integral,error,clipped,allocated)=>
        {
            object[] arguments={integral,error,0d,clipped,allocated,1d,1d,1d,.1,true};
            method.Invoke(script,arguments);return (Vector3D)arguments[0];
        };
        var result=advance(Vector3D.Zero,new Vector3D(-.1,0,0),new Vector3D(-.05,0,0),new Vector3D(.05,0,0));
        Check(result.LengthSquared()<1e-20,"Opposing clip/allocation residuals cancelled and permitted new windup.");
        var clip=new Vector3D(-1,0,0);var allocation=new Vector3D(1,1,0);
        result=advance(Vector3D.Zero,new Vector3D(-.1,.1,0),clip,allocation);
        Check(Vector3D.Dot(result,clip)<=1e-12&&Vector3D.Dot(result,allocation)<=1e-12,"Sequential anti-windup projections reintroduced blocked integral growth.");
        result=advance(new Vector3D(.0001,0,0),new Vector3D(-.1,0,0),new Vector3D(-1,0,0),Vector3D.Zero);
        Check(result.X>=-1e-12&&result.X<.0001,"Integral unwind crossed zero into new blocked growth, or did not unwind.");
        result=advance(Vector3D.Zero,new Vector3D(0,.1,0),new Vector3D(-1,0,0),Vector3D.Zero);
        Check(result.Y>0&&Math.Abs(result.X)<1e-12,"An independently unblocked cross-track integral could not grow.");
        Console.WriteLine("Anti-windup: separate clip/allocation constraints, no projection-order bug, bounded unwind and unblocked perpendicular growth.");
    }

    static void OffsetSerialGeometry(Type type)
    {
        var rig=new Rig();var a=rig.Grid();var b=rig.Grid();var c=rig.Grid();
        var root=rig.Piston("Arm 1 - Base - Piston",rig.Root,a,new Vector3D(4,2,-1),Vector3D.Right);
        var rotor=rig.Rotor("off-axis rotor",a,b,new Vector3D(7,-3,4),Vector3D.Up);
        var hinge=rig.Rotor("angled hinge",b,c,new Vector3D(-2,5,9),Vector3D.Normalize(new Vector3D(1,1,.3)));
        var head=rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",c,new Vector3D(10,7,-8));rig.Cockpit();
        var script=Start(type,rig);Run(script,"On");
        Check(Enabled(script)&&Groups(script).Length==3,"Offset serial piston/rotor/hinge arm was rejected.");
        var frame=root.WorldMatrix;
        Func<Vector3D,Vector3D> local=v=>new Vector3D(Vector3D.Dot(v,frame.Forward),Vector3D.Dot(v,frame.Left),Vector3D.Dot(v,frame.Up));
        foreach(var group in Groups(script))
        {
            var block=(IMyMechanicalConnectionBlock)Get(Members(group)[0],"B")!;
            var angular=block is IMyPistonBase?Vector3D.Zero:-block.WorldMatrix.Up;
            var linear=block is IMyPistonBase?block.WorldMatrix.Up:Vector3D.Cross(angular,head.GetPosition()-block.GetPosition());
            Check(((Vector3D)Get(group,"Lin")!-local(linear)).Length()<1e-10&&((Vector3D)Get(group,"Ang")!-local(angular)).Length()<1e-10,"Serial joint lost its actual offset or orientation in the live Jacobian.");
        }
        Run(script,"Stop");NoVelocity(rig);
        Console.WriteLine("Geometry: piston base and spatially offset/angled serial rotary stages retain their live 3D axes and lever arms.");
    }

    static void PhysicalStallLead(Type type)
    {
        var rig=new Rig();var mid=rig.Grid();var end=rig.Grid();
        rig.Piston("Arm 1 - Base - Piston",rig.Root,mid,Vector3D.Zero,Vector3D.Forward);
        rig.Piston("second piston",mid,end,Vector3D.Forward*5,Vector3D.Forward);
        rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",end,Vector3D.Forward*10);
        var cockpit=rig.Cockpit();var script=Start(type,rig);Run(script,"On");
        RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-1);
        for(int i=0;i<1200;i++)
        {
            // Positions intentionally stay fixed despite nonzero commands: an external physical stall.
            Run(script);
            Check(Enabled(script),"Target-lead bounding unnecessarily disabled live pilot control.");
            Check(Convert.ToDouble(Get(script,"LastPositionError"))<=1.5+1e-9,"Reference lead grew beyond its physical-stall bound.");
            Check(((Vector3D)Get(script,"LastPilotLinear")!).Length()>.19,"Physical-stall protection gated raw pilot input.");
        }
        var target=(Vector3D)Get(script,"TargetP")!;
        RecordProxy.Of(cockpit).Values["MoveIndicator"]=Vector3.Zero;Run(script);
        Check((target-(Vector3D)Get(script,"TargetP")!).Length()<1e-12,"Idle protection recaptured the held target.");
        var limit=type.GetMethod("TargetLeadAdvance",All)!;
        Func<Vector3D,Vector3D,double> scale=(error,step)=>(double)limit.Invoke(null,new object[]{error,step,1.5})!;
        Check(Math.Abs(scale(new Vector3D(1.49,0,0),new Vector3D(.02,0,0))-.5)<1e-10,"Just-inside lead crossing was not trimmed at the boundary.");
        Check(scale(new Vector3D(2,0,0),new Vector3D(-.1,0,0))==1,"An inward command outside the lead bound was blocked.");
        Check(scale(new Vector3D(2,0,0),new Vector3D(.1,0,0))==0,"Outward growth beyond an existing large error was accepted.");
        Check(scale(new Vector3D(2,0,0),Vector3D.Zero)==1,"Idle target was modified by the lead guard.");
        var e=new Vector3D(1.5,0,0);
        for(int i=0;i<500;i++)
        {
            var step=Vector3D.Normalize(new Vector3D(-e.Y,e.X,0))*.01;
            e+=step*scale(e,step);
            Check(e.Length()<=1.5+1e-9,"Tangential pilot changes walked the target outside its lead sphere.");
        }
        Run(script,"Stop");NoVelocity(rig);
        Console.WriteLine("Target lead: a stalled two-piston arm remains responsive without unbounded reference growth; idle, inward, outward and tangential cases.");
    }
}
