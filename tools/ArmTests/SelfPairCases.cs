using System.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

internal static partial class Scenarios
{
    sealed class SelfPairRig
    {
        internal Rig Rig=new();
        internal object Service, Arm, Table=null!;
        internal IMyCubeGrid[] Grids;
        internal IMyMechanicalConnectionBlock[] Joints;
        internal MatrixD[] Relative, BlockLocal;
        internal double[] Initial;
        Type Owner;
        internal SelfPairRig(Type type,bool unlimited=false,bool rotaryBase=false)
        {
            Service=PackedProgramChecks.Unwrap(Tests.Create(type,Rig));Owner=Service.GetType();
            Grids=new[]{Rig.Root,Rig.Grid(),Rig.Grid(),Rig.Grid(),Rig.Grid()};
            var global=MatrixD.CreateFromYawPitchRoll(.7f,-.4f,.2f);global.Translation=new Vector3D(300,-40,60);
            var positions=new[]{Vector3D.Zero,new Vector3D(0,5,0),new Vector3D(6,5,0),new Vector3D(10,5,4),new Vector3D(18,5,3)};
            for(int i=0;i<Grids.Length;i++) {
                var local=MatrixD.CreateFromYawPitchRoll(i*.17f,i*.13f,-i*.11f);local.Translation=positions[i];
                RecordProxy.Of(Grids[i]).Values["WorldMatrix"]=local*global;
            }
            var axes=new[]{Vector3D.Up,Vector3D.Right,Vector3D.Normalize(new Vector3D(.2,1,.3)),Vector3D.Normalize(new Vector3D(1,.5,.1))};
            var pivots=new[]{positions[0],positions[1]+new Vector3D(.5,0,1),positions[2]+new Vector3D(0,.3,-.5),positions[3]};
            Joints=new IMyMechanicalConnectionBlock[]{
                rotaryBase?(IMyMechanicalConnectionBlock)Rig.Rotor("Arm 1 - Base",Grids[0],Grids[1],Vector3D.Transform(pivots[0],global),Vector3D.TransformNormal(axes[0],global)):
                    Rig.Piston("Arm 1 - Base",Grids[0],Grids[1],Vector3D.Transform(pivots[0],global),Vector3D.TransformNormal(axes[0],global)),
                Rig.Rotor("hinge one",Grids[1],Grids[2],Vector3D.Transform(pivots[1],global),Vector3D.TransformNormal(axes[1],global)),
                Rig.Rotor("hinge two",Grids[2],Grids[3],Vector3D.Transform(pivots[2],global),Vector3D.TransformNormal(axes[2],global)),
                Rig.Piston("distal piston",Grids[3],Grids[4],Vector3D.Transform(pivots[3],global),Vector3D.TransformNormal(axes[3],global))};
            foreach(var j in Joints.OfType<IMyMotorStator>())if(!unlimited&&(!rotaryBase||j!=Joints[0])) {RecordProxy.Of(j).Values["LowerLimitRad"]=-.15f;RecordProxy.Of(j).Values["UpperLimitRad"]=.15f;}
            var piston=(IMyPistonBase)Joints[3];RecordProxy.Of(piston).Values["MinLimit"]=4f;RecordProxy.Of(piston).Values["MaxLimit"]=6f;
            Arm=Make("Arm");Put(Arm,"PB",Rig.PB);Put(Arm,"Name","Arm 1");Put(Arm,"Hull",Grids[0].EntityId);Put(Arm,"Fingerprint","PHHP offset prototype");
            Put(Arm,"Rates",new double[4]);Put(Arm,"Caps",Enumerable.Repeat(.1,4).ToArray());
            var scene=(IDictionary)Get(Service,"Scene")!;scene.Clear();
            for(int i=0;i<Grids.Length;i++) {
                var grid=Make("GridShape");Put(grid,"Grid",Grids[i]);Put(grid,"Min",Vector3I.Zero);Put(grid,"Max",Vector3I.Zero);Put(grid,"Ready",true);
                var shape=Make("Shape");Put(shape,"Centre",Vector3D.Zero);Put(shape,"Half",new Vector3D(1.25));((IList)Get(grid,"Shapes")!).Add(shape);scene[Grids[i].EntityId]=grid;
                RecordProxy.Of(Grids[i]).Values["Min"]=RecordProxy.Of(Grids[i]).Values["Max"]=Vector3I.Zero;
                if(i==0)continue;
                ((IDictionary)Get(Arm,"Bodies")!)[Grids[i].EntityId]=(1<<i)-1;
                var group=Make("Group");Put(group,"Rotary",Joints[i-1] is IMyMotorStator);
                var member=Make("Joint");Put(member,"Block",Joints[i-1]);Put(member,"From",Grids[i-1].EntityId);Put(member,"To",Grids[i].EntityId);Put(member,"Sign",1);
                ((IList)Get(group,"Members")!).Add(member);((IList)Get(Arm,"Groups")!).Add(group);
            }
            Relative=Joints.Select(j=>j.TopGrid.WorldMatrix*MatrixD.Invert(j.CubeGrid.WorldMatrix)).ToArray();
            BlockLocal=Joints.Select(j=>j.WorldMatrix*MatrixD.Invert(j.CubeGrid.WorldMatrix)).ToArray();
            Initial=Joints.Select(Q).ToArray();
        }
        internal object Make(string name)=>Activator.CreateInstance(Owner.GetNestedType(PackedProgramChecks.Name(Owner,name),All)!,true)!;
        internal object Invoke(string name,params object[] args)=>Owner.GetMethod(PackedProgramChecks.Name(Owner,name),All)!.Invoke(Service,args)!;
        internal object Grid(int i)=>((IDictionary)Get(Service,"Scene")!)[Grids[i].EntityId]!;
        internal void Prepare(string mode="Shadow") {Put(Service,"SelfPairMode",mode);for(int i=0;i<5;i++)Invoke("PrepareSelfPairs",Arm);Table=Get(Service,"ActiveSelfTable")!;}
        internal bool Skip(int a,int b,double range=.75)=>(bool)Invoke("SkipSelfPair",Arm,Grid(a),Grid(b),range);
        internal object Pair(int a,int b)=>((IDictionary)Get(Table,"Proven")!)[Invoke("SelfKey",Grids[a].EntityId,Grids[b].EntityId)]!;
        static double Q(IMyMechanicalConnectionBlock j)=>j is IMyMotorStator r?r.Angle:((IMyPistonBase)j).CurrentPosition;
        internal void Pose(double[] q)
        {
            for(int i=0;i<Joints.Length;i++) {
                var j=Joints[i];var frame=BlockLocal[i]*j.CubeGrid.WorldMatrix;RecordProxy.Of(j).Values["WorldMatrix"]=frame;
                double delta=q[i]-Initial[i];var child=Relative[i]*j.CubeGrid.WorldMatrix;
                if(j is IMyMotorStator) {
                    RecordProxy.Of(j).Values["Angle"]=(float)q[i];var rotation=MatrixD.CreateFromAxisAngle(-frame.Up,delta);
                    var p=frame.Translation+Vector3D.TransformNormal(child.Translation-frame.Translation,rotation);child=child.GetOrientation()*rotation;child.Translation=p;
                } else {RecordProxy.Of(j).Values["CurrentPosition"]=(float)q[i];child.Translation+=frame.Up*delta;}
                RecordProxy.Of(j.TopGrid).Values["WorldMatrix"]=child;
            }
        }
    }
    internal static void SelfPairCases(Type type)
    {
        var f=new SelfPairRig(type);f.Prepare();
        Check(f.Table!=null&&(bool)Get(f.Table,"Supported")!,"Offset/rotated serial chain was not captured.");
        var pair=f.Pair(1,4)??throw new Exception("Limited offset PHHP did not produce a conservative exclusion.");Check((double)Get(pair,"Gap")! > .8,"Proof did not exceed the influence/slack threshold.");
        Check((long)Get(pair,"Anchor")! ==f.Grids[1].EntityId,"Common ancestor was not removed from relative motion.");
        Check(!f.Skip(1,4)&&(int)Get(f.Service,"SelfWouldSkip")! >0&&(int)Get(f.Service,"SelfSkipped")! ==0,"Shadow mode altered live checks.");
        f.Prepare("On");Check(f.Skip(1,4),"Enabled mode did not apply a valid certificate.");
        Check(!f.Skip(0,4),"Filter exempted hull/other-arm geometry.");
        Check(!f.Skip(1,4,1000),"Certificate ignored a larger live prediction range.");
        var random=new Random(7741);pair=f.Pair(1,4);var first=Get(pair,"First")!;var second=Get(pair,"Second")!;
        for(int trial=0;trial<1200;trial++) {
            f.Pose(new[]{random.NextDouble()*10,random.NextDouble()*.3-.15,random.NextDouble()*.3-.15,4+random.NextDouble()*2});
            var inverse=MatrixD.Invert(f.Grids[1].WorldMatrix);
            foreach(var item in new[]{(Index:1,Ball:first),(Index:4,Ball:second)}) {
                var centre=(Vector3D)Get(item.Ball,"Centre")!;double radius=(double)Get(item.Ball,"Radius")!;
                // Actual occupied cube corners must be inside the proof at
                // arbitrary combined settings, including moving shared piston.
                for(int corner=0;corner<8;corner++) {
                    var local=new Vector3D((corner&1)==0?-1.25:1.25,(corner&2)==0?-1.25:1.25,(corner&4)==0?-1.25:1.25);
                    var p=Vector3D.Transform(Vector3D.Transform(local,f.Grids[item.Index].WorldMatrix),inverse);
                    Check(Vector3D.Distance(p,centre)<=radius+1e-5,"Rotated/non-collinear FK escaped the analytic bound.");
                }
            }
            Check(f.Skip(1,4),"In-range motion invalidated a valid exclusion.");
        }
        f.Pose(f.Initial);f.Invoke("PrepareSelfPairs",f.Arm);Check(ReferenceEquals(f.Table,Get(f.Service,"ActiveSelfTable")),"Normal joint movement rebuilt the cache.");
        var rotating=new SelfPairRig(type,rotaryBase:true);rotating.Prepare("On");var cached=rotating.Table;
        for(int i=0;i<40;i++) {rotating.Pose(new[]{i*Math.PI/20,.1,-.1,5d});rotating.Invoke("PrepareSelfPairs",rotating.Arm);Check(rotating.Skip(1,4)&&ReferenceEquals(cached,Get(rotating.Service,"ActiveSelfTable")),"Shared upstream rotation invalidated separation or rebuilt the cache.");}
        var oldTable=f.Table;RecordProxy.Of(f.Joints[1]).Values["UpperLimitRad"]=1.5f;f.Prepare("On");Check(!ReferenceEquals(oldTable,f.Table),"Changed limits retained a cached proof.");
        var unlimited=new SelfPairRig(type,true);unlimited.Prepare("On");Check(unlimited.Pair(1,4)==null&&!unlimited.Skip(1,4),"PHHP pattern alone exempted a foldable full-rotation chain.");
        var axial=new SelfPairRig(type,true);
        for(int i=1;i<=2;i++) {
            var j=axial.Joints[i];var axis=Vector3D.Normalize(axial.Grids[4].WorldMatrix.Translation-j.GetPosition());
            var forward=Vector3D.Normalize(Vector3D.Cross(axis,Math.Abs(Vector3D.Dot(axis,Vector3D.Up))>.9?Vector3D.Right:Vector3D.Up));
            var axisFrame=MatrixD.CreateWorld(j.GetPosition(),forward,axis);RecordProxy.Of(j).Values["WorldMatrix"]=axisFrame;
            axial.BlockLocal[i]=axisFrame*MatrixD.Invert(j.CubeGrid.WorldMatrix);
        }
        axial.Prepare("On");Check(axial.Pair(1,4)!=null,"Full-turn axes through the segment centre lost their invariant centre proof.");
        var axialBall=Get(axial.Pair(1,4),"Second")!;
        for(int trial=0;trial<120;trial++) {
            axial.Pose(new[]{random.NextDouble()*10,random.NextDouble()*Math.PI*2-Math.PI,random.NextDouble()*Math.PI*2-Math.PI,4+random.NextDouble()*2});
            var inverse=MatrixD.Invert(axial.Grids[1].WorldMatrix);
            for(int corner=0;corner<8;corner++) {
                var local=new Vector3D((corner&1)==0?-1.25:1.25,(corner&2)==0?-1.25:1.25,(corner&4)==0?-1.25:1.25);
                var p=Vector3D.Transform(Vector3D.Transform(local,axial.Grids[4].WorldMatrix),inverse);
                Check(Vector3D.Distance(p,(Vector3D)Get(axialBall,"Centre")!)<=(double)Get(axialBall,"Radius")!+1e-5,"Full-turn axial FK escaped its enclosure.");
            }
            Check(axial.Skip(1,4),"Full-turn invariant-centre motion invalidated the proof.");
        }
        var wrapped=new SelfPairRig(type);RecordProxy.Of(wrapped.Joints[1]).Values["LowerLimitRad"]=1f;RecordProxy.Of(wrapped.Joints[1]).Values["UpperLimitRad"]=2f;wrapped.Prepare("On");Check(wrapped.Pair(1,4)==null,"Ambiguous wrapped angle inferred a narrow interval.");
        var parallel=new SelfPairRig(type);var group=((IList)Get(parallel.Arm,"Groups")!)[1]!;((IList)Get(group,"Members")!).Add(((IList)Get(group,"Members")!)[0]);parallel.Prepare("On");
        Check(parallel.Table==null&&!parallel.Skip(1,4),"Unanalysed parallel closure was incorrectly exempted.");
        var flex=new SelfPairRig(type);flex.Prepare("On");var frame=flex.Grids[4].WorldMatrix;frame.Translation=flex.Grids[1].WorldMatrix.Translation;RecordProxy.Of(flex.Grids[4]).Values["WorldMatrix"]=frame;
        Check(!flex.Skip(1,4)&&flex.Pair(1,4)==null,"Live enclosure escape kept an unsafe exemption.");
        var geometry=new SelfPairRig(type);geometry.Prepare("On");oldTable=geometry.Table;Put(geometry.Service,"SelfGeometryRevision",(long)Get(geometry.Service,"SelfGeometryRevision")!+1);geometry.Prepare("On");Check(!ReferenceEquals(oldTable,geometry.Table),"Rescan did not invalidate certificates.");
        var changed=new SelfPairRig(type);changed.Prepare("On");oldTable=changed.Table;Put(changed.Grid(4),"Max",new Vector3I(20));changed.Prepare("On");Check(!ReferenceEquals(oldTable,changed.Table)&&!changed.Skip(1,4),"Expanded offset equipment retained proof.");
        var detached=new SelfPairRig(type);detached.Prepare("On");RecordProxy.Of(detached.Joints[2]).Values["IsAttached"]=false;detached.Prepare("On");Check(detached.Table==null&&!detached.Skip(1,4),"Detached edge retained exclusion.");
        var reversed=new SelfPairRig(type);var firstGroup=((IList)Get(reversed.Arm,"Groups")!)[0]!;var firstMember=((IList)Get(firstGroup,"Members")!)[0]!;Put(firstMember,"From",reversed.Grids[1].EntityId);Put(firstMember,"To",reversed.Grids[0].EntityId);reversed.Prepare("On");Check(reversed.Table==null,"Reversed mechanical edge used an unsupported proof.");
        var budget=new SelfPairRig(type);RecordProxy.Of(budget.Rig.Runtime).Values["CurrentInstructionCount"]=12000;budget.Prepare("On");Check(budget.Table==null&&!budget.Skip(1,4),"Proof work ignored instruction headroom.");
        var off=new SelfPairRig(type);off.Prepare("Off");Check(off.Table==null&&!off.Skip(1,4),"Off mode applied exclusions.");
        var invalid=new MyIni();invalid.Set("Collision","SelfPairs","AlwaysSafe");bool refused=false;
        try{off.Invoke("ConfigureSelfPairs",invalid);}catch(System.Reflection.TargetInvocationException e){refused=e.InnerException!.Message.Contains("SelfPairs");}
        Check(refused,"Malformed prototype setting was accepted.");
        Console.WriteLine("Self-pair prototype: analytic rotated/offset bounds, randomized FK enclosure oracle, common-ancestor removal, Shadow/On/Off, range guard, geometry/limit/attachment/flex invalidation and conservative fallback PASS ("+Tests.Assertions+" assertions).");
    }
}
