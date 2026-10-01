using System.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;

internal static partial class Scenarios
{
    static void ValidationAndCacheCases(Type type)
    {
        var rig=ReportedTenRotaries(out _); var script=Start(type,rig); Run(script,"On");
        var joints=rig.Blocks.OfType<IMyMechanicalConnectionBlock>().ToArray();
        foreach(var block in joints) RecordProxy.Of(block).Reads.Clear();
        InvokeFrame(script,rig,"",UpdateType.Update1,1d/60);
        var counts=joints.Select(b=>RecordProxy.Of(b).Reads.GetValueOrDefault("IsAttached")).ToArray();
        // Base marker checks need no attachment getter; ordinary owned stages
        // each do one live availability check in Refresh, not three traversals.
        Check(counts.All(n=>n==1),"Active ten-stage arm repeated attachment checks: "+string.Join(",",counts));
        Check(joints.Cast<IMyMotorStator>().All(b=>RecordProxy.Of(b).Reads.GetValueOrDefault("Angle")==1),"Singleton sync loop duplicated Bounds position reads.");
        RecordProxy.Of(joints[^1]).Values["IsAttached"]=false; InvokeFrame(script,rig,"",UpdateType.Update1,1d/60);
        Check(!Enabled(script),"Last-stage detachment escaped same-frame refresh guard."); NoVelocity(rig);
        var parallel=Fixtures.Parallel(true,out _,out var second); var paired=Start(type,parallel); Run(paired,"On");
        RecordProxy.Of(second).Values["Angle"]=(float).02; InvokeFrame(paired,parallel,"",UpdateType.Update1,1d/60); Check(!Enabled(paired),"Parallel sync was deferred on active cheap path."); NoVelocity(parallel);
        var home=Fixtures.Serial(out _,out var hp,out _,out _); var hs=Start(type,home); Run(hs,"SetHome"); RecordProxy.Of(hp).Values["CurrentPosition"]=5.3f; Run(hs,"GoHome");
        RecordProxy.Of(hp).Values["IsAttached"]=false; InvokeFrame(hs,home,"",UpdateType.Update1,1d/60); Check(!Enabled(hs),"Home lost immediate actuator checks."); NoVelocity(home);

        var cacheRig=Fixtures.Serial(out _,out _,out _,out _); var cached=Start(type,cacheRig);
        var parked=cacheRig.Rotor("parked spare rotor",cacheRig.Grid(),cacheRig.Grid()); var destroyed=cacheRig.Rotor("old removed rotor",cacheRig.Grid(),cacheRig.Grid());
        RecordProxy.Of(parked).Values["EntityId"]=-8371L; // Nonzero signed IDs retain the existing config contract.
        var map=(IDictionary)Get(cached,"ToolWeights")!;
        string keep="R:"+parked.EntityId+"-", drop="R:"+destroyed.EntityId+"+";
        map[keep]=new[]{.37,2.5}; map[drop]=new[]{.8,1.7};
        foreach(var g in Groups(cached)) map[(string)Tests.Call(cached,"ToolGroupKey",g)!]=new[]{.7,1.4};
        var saved=new MyIni(); saved.TryParse(((TestHost)cached).Storage); saved.Set("Unrelated","Value","keep me"); ((TestHost)cached).Storage=saved.ToString(); Tests.Call(cached,"WriteToolWeights");
        cacheRig.Blocks.Remove(destroyed); RecordProxy.Of(parked).Values["IsFunctional"]=false;
        RecordProxy.Of(cacheRig.Runtime).Values["CurrentInstructionCount"]=26000; string before=((TestHost)cached).Storage; Tests.Call(cached,"PruneToolWeights");
        Check(map.Contains(drop)&&((TestHost)cached).Storage==before,"Low-budget prune partially committed its removals.");
        RecordProxy.Of(cacheRig.Runtime).Values["CurrentInstructionCount"]=0; Tests.Call(cached,"PruneToolWeights");
        Check(!map.Contains(drop)&&map.Contains(keep)&&((double[])map[keep]!)[0]==.37,"Prune erased surviving parked preferences or retained a removed actuator.");
        saved=new MyIni(); saved.TryParse(((TestHost)cached).Storage); Check(saved.Get("Unrelated","Value").ToString()=="keep me"&&!saved.Get("AutoArm Tool Weights","Rows").ToString().Contains(drop),"Cache pruning damaged unrelated Storage or left a deleted row persisted.");
        // A cold load must prune old identities without needing a successful swap.
        string rows=saved.Get("AutoArm Tool Weights","Rows").ToString(); saved.Set("AutoArm Tool Weights","Rows",rows+"\nR:999999+|0.2|0.9"); cacheRig.Storage=saved.ToString();
        var cold=Start(type,cacheRig); var coldMap=(IDictionary)Get(cold,"ToolWeights")!;
        Check(!coldMap.Contains("R:999999+")&&coldMap.Contains(keep),"Cold successful discovery failed to prune stale cache while preserving parked preferences.");
        // Rename to a second head marker so discovery fails; it must not prune.
        saved=new MyIni(); saved.TryParse(((TestHost)cold).Storage); saved.Set("AutoArm Tool Weights","Rows",rows+"\nR:999999+|0.2|0.9"); cacheRig.Storage=saved.ToString();
        cacheRig.Block<IMyShipDrill>("Arm 1 - Head - duplicate",cacheRig.Grid()); string failedBefore=cacheRig.Storage; var failed=Tests.Create(type,cacheRig); for(int i=0;i<8;i++) InvokeFrame(failed,cacheRig,"",UpdateType.Update10,.1);
        Check(((TestHost)failed).Storage==failedBefore,"Failed discovery pruned persistent preferences.");
        Console.WriteLine("Validation/cache: one active attachment/position check per singleton, immediate final-stage and Home detachment/parallel sync, stale-ID pruning, parked retention, bounded atomic cleanup and failed-discovery preservation.");
    }
}
