using System.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;
internal static partial class Scenarios
{
    internal static void PassingCases(Type armType,Type collisionType)
    {
        CollisionCases(armType,collisionType);ConstraintRepairCases(armType);
        var rig=new Rig();var aGrid=rig.Grid();var bGrid=rig.Grid();
        RecordProxy.Of(aGrid).Values["WorldMatrix"]=MatrixD.CreateTranslation(5,0,0);RecordProxy.Of(bGrid).Values["WorldMatrix"]=MatrixD.CreateTranslation(-5,0,0);
        var service=PackedProgramChecks.Unwrap(Tests.Create(collisionType,rig));var owner=service.GetType();
        object Make(string name)=>Activator.CreateInstance(owner.GetNestedType(PackedProgramChecks.Name(owner,name),All)!,true)!;
        object Invoke(string name,params object[] args)=>owner.GetMethod(PackedProgramChecks.Name(owner,name),All)!.Invoke(service,args)!;
        object Grid(IMyCubeGrid native,bool box) {
            var g=Make("GridShape");Put(g,"Grid",native);Put(g,"Ready",true);
            if(box) {var shape=Make("Shape");Put(shape,"Centre",Vector3D.Zero);Put(shape,"Half",new Vector3D(.25));((IList)Get(g,"Shapes")!).Add(shape);var tiles=(IDictionary)Get(g,"Tiles")!;var list=Activator.CreateInstance(typeof(List<>).MakeGenericType(shape.GetType()))!;((IList)list).Add(shape);tiles[Vector3I.Zero]=list;}
            return g;
        }
        var scene=(IDictionary)Get(service,"Scene")!;scene.Clear();scene[rig.Root.EntityId]=Grid(rig.Root,false);scene[aGrid.EntityId]=Grid(aGrid,true);scene[bGrid.EntityId]=Grid(bGrid,true);
        object Arm(string name,IMyCubeGrid grid,double head,double goal) {
            var a=Make("Arm");Put(a,"PB",rig.PB);Put(a,"Name",name);Put(a,"Hull",rig.Root.EntityId);Put(a,"Tool",grid.EntityId);Put(a,"NavAllowed",true);Put(a,"NavKey","2/0");Put(a,"NavHead",new Vector3D(head,0,0));Put(a,"NavGoal",new Vector3D(goal,0,0));Put(a,"NavSpeed",.2);Put(a,"Caps",new[]{.2,.2,.2});((IDictionary)Get(a,"Bodies")!)[grid.EntityId]=0;
            foreach(var axis in new[]{Vector3D.Right,Vector3D.Up,Vector3D.Backward}) {
                var group=Make("Group");var member=Make("Joint");var piston=rig.Piston("axis",rig.Root,grid,axis:axis);Put(member,"Block",piston);Put(member,"From",rig.Root.EntityId);Put(member,"To",grid.EntityId);Put(member,"Sign",1);((IList)Get(group,"Members")!).Add(member);((IList)Get(a,"Groups")!).Add(group);
            }
            return a;
        }
        var yield=Arm("Arm 2",aGrid,5,-6);var winner=Arm("Arm 1",bGrid,-5,6);var arms=(IDictionary)Get(service,"Arms")!;arms.Clear();arms[rig.PB.EntityId+"/Arm 2"]=yield;arms[rig.PB.EntityId+"/Arm 1"]=winner;
        MyIni GuideFor(object arm) {var ini=new MyIni();ini.Set("Link","Hold",false);Invoke("PassingGuidance",arm,ini);return ini;}
        Check(!GuideFor(winner).Get("Link","NavActive").ToBoolean(),"Priority winner received an unnecessary yielding route.");
        var first=GuideFor(yield);Check(first.Get("Link","NavActive").ToBoolean()&&!first.Get("Link","NavWait").ToBoolean(),"Free-space yielding route was not generated.");
        var target=VecForTest(first.Get("Link","NavPoint").ToString());Check(Math.Abs(target.Y)>1&&Math.Abs(target.X-5)<1e-9&&target.Z==0,"Passing lane did not move sideways off the conflicting head line.");
        for(int tick=0;tick<400;tick++) {
            var guide=GuideFor(yield);if(!guide.Get("Link","NavActive").ToBoolean())break;var point=VecForTest(guide.Get("Link","NavPoint").ToString());var position=(Vector3D)Get(yield,"NavHead")!;var delta=point-position;
            if(delta.Length()>.01)position+=delta*(Math.Min(.1,delta.Length())/delta.Length());Put(yield,"NavHead",position);RecordProxy.Of(aGrid).Values["WorldMatrix"]=MatrixD.CreateTranslation(position);
        }
        Check(Vector3D.Distance((Vector3D)Get(yield,"NavHead")!,new Vector3D(-6,0,0))<.01,"Geometric passing-lane stages did not rejoin the original goal.");
        Check((Vector3D)Get(yield,"NavGoal")! ==new Vector3D(-6,0,0),"Passing planner rewrote the submitted original destination.");
        Put(yield,"NavAllowed",false);Check(!GuideFor(yield).Get("Link","NavActive").ToBoolean(),"Manual/linear motion without consent received a route.");Put(yield,"NavAllowed",true);
        Put(yield,"NavHead",new Vector3D(5,0,0));RecordProxy.Of(aGrid).Values["WorldMatrix"]=MatrixD.CreateTranslation(5,0,0);
        Put(service,"Clock",1d);var stale=GuideFor(yield);Check(stale.Get("Link","Hold").ToBoolean(),"Expired passing peer did not cause a hold.");Put(service,"Clock",0d);
        Put(yield,"Lane",null!);RecordProxy.Of(rig.Runtime).Values["CurrentInstructionCount"]=40000;Put(yield,"NavHead",new Vector3D(5,0,0));RecordProxy.Of(aGrid).Values["WorldMatrix"]=MatrixD.CreateTranslation(5,0,0);
        var budget=GuideFor(yield);Check(budget.Get("Link","NavWait").ToBoolean()&&VecForTest(budget.Get("Link","NavPoint").ToString())==new Vector3D(5,0,0),"Exhausted planner budget published unverified movement.");
        Console.WriteLine("Passing lane unit geometry: deterministic yielding, original-goal rejoin, consent, stale-peer and budget refusal PASS ("+Tests.Assertions+" assertions). Full articulated Cross remains a separate completion test.");
    }
    static Vector3D VecForTest(string text) {var values=text.Split(' ').Select(v=>double.Parse(v,System.Globalization.CultureInfo.InvariantCulture)).ToArray();return new Vector3D(values[0],values[1],values[2]);}
}
