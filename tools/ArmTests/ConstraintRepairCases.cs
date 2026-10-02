using System.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;
internal static partial class Scenarios
{
    internal static void ConstraintRepairCases(Type type)
    {
        var rig=new Rig();var middle=rig.Grid();var end=rig.Grid();
        rig.Piston("Arm 1 - Base",rig.Root,middle,axis:Vector3D.Right);rig.Piston("second",middle,end,new Vector3D(5,0,0),Vector3D.Right);
        rig.Block<IMyShipDrill>("Arm 1 - Head",end,new Vector3D(10,0,0));var peer=new Rig(rig,"Collision PB");var bus=new ModuleBus();bus.Bind(rig);bus.Bind(peer);
        var data=new MyIni();data.Set("global","Format",7);data.Set("Arm 1","Peers","Collision PB | Safety");RecordProxy.Of(rig.PB).Values["CustomData"]=data.ToString();
        var host=Tests.Create(type,rig);HostReady(host,rig,"Arm 1");for(int i=0;i<8;i++)HostFrame(host,rig);
        SendGuide(host,rig,peer,bus,Guide(LastCheck(bus,rig.PB.EntityId,peer.PB.EntityId),1,"0 0"));for(int i=0;i<8;i++)HostFrame(host,rig);
        SendGuide(host,rig,peer,bus,Guide(LastCheck(bus,rig.PB.EntityId,peer.PB.EntityId),2,"0 0","-1 0 0"));
        var core=Core(host,"Arm 1");var safety=PackedProgramChecks.Unwrap(Get(Get(core,"Services")!,"Safety")!);
        var b=new double[6,2];b[0,0]=b[0,1]=1;
        Put(core,"B",b);Put(core,"Scale",new[]{1d,1d});Put(core,"Lo",new[]{-1d,-1d});Put(core,"Hi",new[]{1d,1d});Put(core,"Bias",new double[2]);Put(core,"Task",new[]{.1,0,0,0,0,0});Put(core,"ToolDriving",true);Put(core,"LastSolverConverged",true);Put(core,"LastSolverBudgetLimited",false);
        void Constrain(double[] rates)=>safety.GetType().GetMethod(PackedProgramChecks.Name(safety.GetType(),"Constrain"),All)!.Invoke(safety,new object[]{rates});
        var q=new[]{.05,.05};Constrain(q);
        Check((bool)Get(safety,"Reallocated")!&&q[0]<=1e-10&&q[1]>.09&&q.Sum()>.09,"Repair did not preserve task motion through the redundant safe piston.");
        var rows=(IList)Get(safety,"Rows")!;var random=new Random(518);
        for(int trial=0;trial<160;trial++) {
            double ceiling=random.NextDouble()*.045;rows.Clear();rows.Add(new[]{-1d,0d,-ceiling});q=new[]{.05,.05};Constrain(q);
            Check(q[0]<=ceiling+1e-10&&q[0]>=-1&&q[1]>=-1&&q[1]<=1,"Random bounded repair violated a received half-space or joint bound.");
            Check(q.Sum()>.09,"Random redundant repair lost achievable head movement.");
        }
        rows.Clear();rows.Add(new[]{-1d,0d,0d});rows.Add(new[]{0d,-1d,0d});q=new[]{.05,.05};Constrain(q);
        Check(q.All(x=>x<=1e-10),"Opposed constraints admitted unsafe joint motion.");
        rows.Clear();rows.Add(new[]{-1d,0d,0d});Put(core,"Hi",new[]{1d,.02});q=new[]{.05,.05};Constrain(q);
        Check(q[0]<=1e-10&&q[1]<=.02+1e-10,"Repair violated a joint/acceleration upper bound.");
        Put(core,"ToolDriving",false);Put(core,"Hi",new[]{1d,1d});q=new[]{.05,.05};Constrain(q);Check(q.All(x=>x==0)&&!(bool)Get(safety,"Reallocated")!,"Experimental repair changed manual control.");
        Put(core,"ToolDriving",true);RecordProxy.Of(rig.Runtime).Values["CurrentInstructionCount"]=40000;q=new[]{.05,.05};Constrain(q);Check(q.All(x=>x==0)&&(bool)Get(safety,"RepairBudget")!,"Repair budget exhaustion bypassed the original veto.");
        RecordProxy.Of(rig.Runtime).Values["CurrentInstructionCount"]=0;Put(safety,"Age",1d);q=new[]{.05,.05};Constrain(q);Check(q.All(x=>x==0),"Stale guidance admitted repair motion.");
        HostFrame(host,rig,"StopAll");Console.WriteLine("Constraint repair: task-preserving redundancy, all-row bounds, manual/stale/budget fallback PASS ("+Tests.Assertions+" assertions).");
    }
}
