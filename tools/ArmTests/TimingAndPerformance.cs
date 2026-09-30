using Sandbox.ModAPI.Ingame;
using VRageMath;

internal static partial class Scenarios
{
    static void InvokeFrame(object script,Rig rig,string command,UpdateType source,double elapsed)
    {
        RecordProxy.Of(rig.Runtime).Values["TimeSinceLastRun"]=TimeSpan.FromSeconds(elapsed);
        script.GetType().GetMethod("Main")!.Invoke(script,new object[]{command,source});
    }

    static Rig ReportedTenRotaries(out string fingerprint)
    {
        // Exact member and grid identities supplied with the in-game Check/On failure.
        long[] grids={107485094851740926,126598825120858757,137311173712393563,88946880151693191,87194187009079826,127427384420509439,102725068389592867,134535205683930901,79007538104531327,134019949187223466,107902501386217156};
        long[] members={140184492367761716,72638857573754255,87299814172504514,118125174656243008,102391566979940913,134610265194015360,139173715249817659,139492162755914365,76876811995172851,142482281370506520};
        string[] names={"Arm 1 - Base","Hinge","Advanced Rotor","Hinge","Advanced Rotor","Hinge","Advanced Rotor","Hinge","Hinge","Advanced Rotor"};
        var rig=new Rig();var from=rig.Root;RecordProxy.Of(from).Values["EntityId"]=grids[0];
        for(int i=0;i<members.Length;i++)
        {
            var to=rig.Grid();RecordProxy.Of(to).Values["EntityId"]=grids[i+1];
            var joint=rig.Rotor(names[i],from,to,new Vector3D(i%3,0,-2*i),i%2==0?Vector3D.Up:Vector3D.Right);
            RecordProxy.Of(joint).Values["EntityId"]=members[i];from=to;
        }
        rig.Block<IMyShipDrill>("Arm 1 - Head",from,new Vector3D(1,0,-22));rig.Cockpit();
        fingerprint=string.Join("|",members.Select((id,i)=>$"R:{id}+@{grids[i]}>{grids[i+1]}"));
        return rig;
    }

    static void TimingAndPerformance(Type type)
    {
        var rig=ReportedTenRotaries(out var fingerprint);var script=Start(type,rig);
        Check((string)Get(Get(script,"Topology")!,"Fingerprint")! == fingerprint,"Reported 10-joint configuration fingerprint changed.");
        InvokeFrame(script,rig,"Check",UpdateType.Terminal,0);
        Check(rig.Log.Last().Contains("CHECK PASS"),"Reported arm did not pass Check.");
        InvokeFrame(script,rig,"On",UpdateType.Terminal,0);
        Check(!Enabled(script) && (bool)Get(script,"PendingOn")!,"Zero-time On must request stopped setup without fabricating observations.");
        Check(Convert.ToInt32(Get(script,"LastSolverPasses"))==0,"A command-only invocation ran a motion solve.");NoVelocity(rig);
        InvokeFrame(script,rig,"",UpdateType.Update1,0);
        Check(!Enabled(script)&&(bool)Get(script,"PendingOn")!&&Convert.ToInt32(Get(script,"LastSolverPasses"))==0,"Same-frame periodic callback faulted or fabricated elapsed time.");
        InvokeFrame(script,rig,"",UpdateType.Update1,1d/60);
        WaitForOn(script); InvokeFrame(script,rig,"",UpdateType.Update1,1d/60);
        Check(Enabled(script)&&Convert.ToInt32(Get(script,"LastSolverPasses"))>0,"Next positive periodic tick failed to control reported arm.");
        InvokeFrame(script,rig,"Hold",UpdateType.Terminal|UpdateType.Update1,1d/60);NoVelocity(rig);
        Check(Enabled(script)&&Convert.ToDouble(Get(script,"ControlElapsed"))==0,"Combined flags did not reset the recaptured timing baseline.");
        InvokeFrame(script,rig,"Stop",UpdateType.Trigger,0);Check(!Enabled(script),"Zero-time toolbar Stop was delayed.");NoVelocity(rig);

        var pendingRig=Fixtures.Serial(out _,out _,out _,out _);var pending=Tests.Create(type,pendingRig);
        for(int i=0;i<5;i++)InvokeFrame(pending,pendingRig,"On",UpdateType.Terminal,0);
        Check(!(bool)Get(Get(pending,"Topology")!,"Ready")!,"Repeated same-frame commands falsely established stable setup.");
        for(int i=0;i<3;i++)
        {
            InvokeFrame(pending,pendingRig,"Info",UpdateType.Terminal,1d/60);
            InvokeFrame(pending,pendingRig,"",UpdateType.Update1,0);
        }
        Check(Enabled(pending),"Manual-first/periodic-zero ordering starved setup or pending On.");

        var observerRig=Fixtures.Serial(out _,out _,out var head,out _);var observer=Start(type,observerRig);
        InvokeFrame(observer,observerRig,"On",UpdateType.Terminal,0);
        RecordProxy.Of(observerRig.Runtime).Values["TimeSinceLastRun"]=TimeSpan.FromSeconds(1d/60); WaitForOn(observer);
        InvokeFrame(observer,observerRig,"Info",UpdateType.Terminal,.02);
        var matrix=head.WorldMatrix;matrix.Translation+=Vector3D.Forward*.01;RecordProxy.Of(head).Values["WorldMatrix"]=matrix;
        InvokeFrame(observer,observerRig,"",UpdateType.Update1,.03);
        Check(Math.Abs(((Vector3D)Get(observer,"FilteredV")!).Length()-.05)<1e-9,"Manual command time was omitted from the measured velocity interval.");
        InvokeFrame(observer,observerRig,"On",UpdateType.Terminal,0);
        RecordProxy.Of(observerRig.Runtime).Values["TimeSinceLastRun"]=TimeSpan.FromSeconds(1d/60); WaitForOn(observer);
        matrix.Translation+=Vector3D.Forward*.02;RecordProxy.Of(head).Values["WorldMatrix"]=matrix;
        InvokeFrame(observer,observerRig,"",UpdateType.Update1,.2);
        Check(Math.Abs(((Vector3D)Get(observer,"FilteredV")!).Length()-(.1*.2/.35))<1e-9,"Observed velocity used the capped integration step instead of actual observation time.");
        InvokeFrame(observer,observerRig,"Info",UpdateType.Terminal,.3);
        InvokeFrame(observer,observerRig,"Info",UpdateType.Terminal,.3);
        Check(!Enabled(observer),"Frequent manual calls masked a stale control interval.");NoVelocity(observerRig);

        var displayRig=Fixtures.Serial(out _,out _,out _,out _);var display=Start(type,displayRig);InvokeFrame(display,displayRig,"On",UpdateType.Terminal,0);
        RecordProxy.Of(displayRig.Runtime).Values["TimeSinceLastRun"]=TimeSpan.FromSeconds(1d/60); WaitForOn(display);
        int inventory=displayRig.InventoryCalls,builds=Convert.ToInt32(Get(display,"RoutineStatusBuilds")),echoes=displayRig.Log.Count;
        for(int i=0;i<60;i++)InvokeFrame(display,displayRig,"",UpdateType.Update1,1d/60);
        int addedScans=displayRig.InventoryCalls-inventory,addedBuilds=Convert.ToInt32(Get(display,"RoutineStatusBuilds"))-builds;
        Check(addedScans==2,"Full topology scan cadence is not 2 per 60 active passes: "+addedScans);
        Check(addedBuilds>=3&&addedBuilds<=5,"Routine strings were not formatted at approximately 4 Hz: "+addedBuilds);
        Check(displayRig.Log.Count-echoes==60&&displayRig.Log.TakeLast(60).All(s=>s.Length>0),"Cached display was not replayed on every invocation.");
        InvokeFrame(display,displayRig,"Info",UpdateType.Terminal,0);string info=displayRig.Log.Last();
        for(int i=0;i<20;i++)InvokeFrame(display,displayRig,"",UpdateType.Update1,1d/60);
        Check(displayRig.Log.Last()==info,"Explicit Info output was immediately erased by routine status.");
        InvokeFrame(display,displayRig,"Status",UpdateType.Terminal,0);InvokeFrame(display,displayRig,"",UpdateType.Update1,1d/60);
        Check(displayRig.Log.Last().Contains("ON | input+feedback"),"Live status did not resume on request.");
        InvokeFrame(display,displayRig,"OnOff Toggle",UpdateType.Terminal,0);
        Check(!Enabled(display)&&displayRig.Log.Last().Contains("Toggle OFF"),"Cached display falsely reported On after OnOff Toggle.");
        InvokeFrame(display,displayRig,"On",UpdateType.Terminal,0);
        RecordProxy.Of(displayRig.Runtime).Values["TimeSinceLastRun"]=TimeSpan.FromSeconds(1d/60); WaitForOn(display);
        InvokeFrame(display,displayRig,"OrientationTolerance 4",UpdateType.Terminal,0);
        Check(!Enabled(display)&&displayRig.Log.Last().Contains("Stopped:"),"Tolerance command hid its stopped state behind a cached value message.");
        InvokeFrame(display,displayRig,"SetHome",UpdateType.Terminal,0);
        InvokeFrame(display,displayRig,"GoHome",UpdateType.Terminal,0);
        Check(Enabled(display)&&displayRig.Log.Last().Contains("GoHome in progress"),"Homing began with stale Off text still displayed.");
        InvokeFrame(display,displayRig,"Stop",UpdateType.Terminal,0);InvokeFrame(display,displayRig,"On",UpdateType.Terminal,0);
        RecordProxy.Of(displayRig.Runtime).Values["TimeSinceLastRun"]=TimeSpan.FromSeconds(1d/60); WaitForOn(display);
        InvokeFrame(display,displayRig,"",UpdateType.Update1,-.01);Check(!Enabled(display),"A negative periodic interval failed to stop control.");NoVelocity(displayRig);
        Console.WriteLine("Startup/performance: reported 10-joint fingerprint; zero-time commands, accumulated timing, mixed flags, 30-pass scans and cached 4 Hz status formatting.");
    }
}
