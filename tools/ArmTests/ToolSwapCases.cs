using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

#if TOOL_SWAP_FOCUS
// Optional focused entry point, compiled only by an explicit test invocation.
internal static class ToolSwapFocusRunner
{
    static int Main(string[] args)
    {
        try
        {
            System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (_,name) =>
            {
                var file = Path.Combine(Tests.GameBin,name.Name+".dll");
                return File.Exists(file) ? System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(file) : null;
            };
            Tests.Workspace = Path.GetDirectoryName(Path.GetFullPath(args[0]))!;
            Scenarios.ToolSwapCases(Tests.Script(File.ReadAllText(args[0])));
            Console.WriteLine("Tool swap focus PASS: "+Tests.Assertions+" assertions.");
            return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
#endif

internal static partial class Scenarios
{
    static object SwapController(object script) => Get(script,"Tools")!;
    static string SwapPhase(object script) => (string)Get(SwapController(script),"Phase")!;
    static MatrixD SwapWorldGoal(object script,string poseField)
    {
        var tools = SwapController(script);
        string anchorField = poseField=="Dock" || poseField=="Approach" ? "DockAnchor" : poseField=="Retreat" ? "RetreatAnchor" : "AttachAnchor";
        return (MatrixD)Get(tools,poseField)! * ((VRage.Game.ModAPI.Ingame.IMyCubeBlock)Get(tools,anchorField)!).WorldMatrix;
    }
    static void SwapFrame(object script, string command = "", bool timed = true, double dt = 1d/60)
    {
        RecordProxy.Of(((TestHost)script).Runtime).Values["TimeSinceLastRun"] = TimeSpan.FromSeconds(dt);
        script.GetType().GetMethod("Main")!.Invoke(script,new object[]{command,timed ? UpdateType.Update1 : UpdateType.Terminal});
    }
    static object SwapStart(Type type, ToolSwapFixture fixture)
    {
        var script = Tests.Create(type,fixture.Rig);
        fixture.Host = (TestHost)script;
        for(int i=0;i<160 && SwapPhase(script)!="Idle";i++) SwapFrame(script);
        Check(SwapPhase(script)=="Idle" && !(bool)Get(SwapController(script),"Recovery")!, "Fixture scan failed: "+string.Join(" | ",fixture.Rig.Log.TakeLast(5)));
        Check(!Enabled(script),"Tool scan must finish OFF.");
        return script;
    }
    static void SwapReach(object script, ToolSwapFixture fixture, string phase, int ticks = 200)
    {
        for(int i=0;i<ticks && SwapPhase(script)!=phase;i++)
        {
            Check(SwapPhase(script)!="Idle", "Tool operation stopped before "+phase+": "+string.Join(" | ",fixture.Rig.Log.TakeLast(4)));
            SwapFrame(script);
        }
        Check(SwapPhase(script)==phase,"Tool operation did not reach "+phase+"; at "+SwapPhase(script));
    }
    static bool SwapMove(object script,ToolSwapFixture fixture,string phase,string poseField,bool source)
    {
        SwapReach(script,fixture,phase);
        bool drove = false;
        // Exercise the real actuator solver before the plant supplies its next
        // observed pose. Terminal calls at zero time cannot satisfy a settle.
        for(int i=0;i<4 && SwapPhase(script)==phase;i++) { SwapFrame(script); drove |= fixture.AnyDrive; }
        var target = SwapWorldGoal(script,poseField);
        if(source) fixture.MoveSource(target);
        else RecordProxy.Of(fixture.Mount).Values["WorldMatrix"] = target;
        for(int i=0;i<240 && SwapPhase(script)==phase;i++) SwapFrame(script);
        Check(SwapPhase(script)!=phase && SwapPhase(script)!="Idle","Motion phase failed after plant observation: "+phase+" | "+string.Join(" | ",fixture.Rig.Log.TakeLast(4)));
        return drove;
    }
    static void SwapUnchangedMerges(ToolSwapFixture f,string message)
    {
        Check(!f.StandWritten,message+": station merge was written.");
    }
    internal static void ToolSwapCases(Type type)
    {
        OptionalToolPorts(type);
        BareToolPickup(type,false);
        BareToolPickup(type,true);
        GenericToolPark(type);
        AlreadyPartialToolSupport(type);
        ToolStrictFormat(type);
        FullToolSwap(type);
        PickupCancelBoundaries(type);
        ToolSupportWaitAndCancel(type);
        ToolHostileGuards(type);
        ToolFinalAttachGuards(type);
        ToolBareHomeAttachmentGuards(type);
        ToolRejectedStartupStopsOwned(type);
        Console.WriteLine("Tool changes: occupied cell cuts, optional ports, explicit scan overrides, timed pickup/release, wrong-top refusal, partial support, cancellation/restart, full swap OFF and strict Format.");
    }
    static void OptionalToolPorts(Type type)
    {
        var f = new ToolSwapFixture(ports:false);
        var script = SwapStart(type,f);
        SwapFrame(script,"On",false,0); SwapFrame(script);
        Check(Enabled(script),"Tool profiles with no optional parking ports must permit manual On.");
        SwapFrame(script,"Off",false,0);
        SwapFrame(script,"Tool 2;On",false,0);
        for(int i=0;i<120 && SwapPhase(script)!="Idle";i++) SwapFrame(script);
        Check(!Enabled(script),"Tool command and semicolon On resumed after refusal.");
        Check(f.Mutations.Count==0,"Port-less tool change made a mechanical mutation.");
        SwapUnchangedMerges(f,"Optional ports");
    }
    static void BareToolPickup(Type type,bool wrongTop)
    {
        var f = new ToolSwapFixture(explicitOnly:true,headless:true);
        if(wrongTop) f.AttachResult = f.Tops[0];
        var script = SwapStart(type,f);
        var tools = SwapController(script);
        Check(Get(Get(script,"Topology")!,"Head") == f.Mount,"Headless discovery must use configured Mount endpoint.");
        Check(!f.Rig.Blocks.Any(b=>ReferenceEquals(b,f.Tops[1])),"Fixture top accidentally entered terminal inventory.");
        SwapFrame(script,"Tool 2",false,0);
        Check(!Enabled(script),"Tool pickup must start OFF.");
        string phase = SwapPhase(script);
        for(int i=0;i<8;i++) SwapFrame(script,"",true,0);
        Check(SwapPhase(script)==phase && f.Mutations.Count==0,"Zero-time updates advanced tool state or attached.");
        if(!wrongTop)
        {
            SwapReach(script,f,"ApproachTop");
            var before = SwapWorldGoal(script,"TopApproach");
            var shift = new Vector3D(.08,0,0); f.ShiftRegion(1,shift);
            var after = SwapWorldGoal(script,"TopApproach");
            Check(Vector3D.Distance(after.Translation,before.Translation+shift)<1e-9,"Moving support anchor did not update world approach target.");
            SwapFrame(script);
            var d = after.Translation-f.Base.GetPosition(); var frame = f.Base.WorldMatrix;
            var expected = new Vector3D(Vector3D.Dot(d,frame.Forward),Vector3D.Dot(d,frame.Left),Vector3D.Dot(d,frame.Up));
            Check(Vector3D.Distance((Vector3D)Get(script,"TargetP")!,expected)<1e-8,"Real pose control used stale world approach target after support moved.");
        }
        bool drove = SwapMove(script,f,"ApproachTop","TopApproach",false);
        drove |= SwapMove(script,f,"AlignTop","Attach",false);
        Check(drove,"Pickup approach never exercised a nonzero actuator output.");
        SwapReach(script,f,"Attach");
        Check(f.Mutations.SequenceEqual(new[]{"Attach"}),"Pickup must request one Attach without Detach.");
        Check(f.MutationJournals.SequenceEqual(new[]{"Attach"}) && f.StoppedAtMutation.All(x=>x),"Attach was not journaled before mutation with every owned drive stopped.");
        Check(f.Heads[1].All(m=>m.Enabled),"Support released in the Attach request frame.");
        if(wrongTop)
        {
            SwapFrame(script);
            Check(SwapPhase(script)=="Idle" && (bool)Get(tools,"Recovery")!,"Wrong top attachment must cancel into recovery.");
            Check(f.Heads[1].All(m=>m.Enabled),"Wrong top attachment released destination supports.");
            Check(!f.Heads[1].Any(m=>RecordProxy.Of(m).Writes.Any(w=>w.Name=="Enabled" && Equals(w.Value,false))),"Wrong top observed a disable write.");
            Check(!Enabled(script),"Wrong top attachment resumed manual control.");
        }
        else
        {
            SwapFrame(script);
            Check(SwapPhase(script)=="Attach" && f.Heads[1].All(m=>m.Enabled),"One attachment observation released support.");
            SwapFrame(script);
            Check(SwapPhase(script)=="Release" && f.Heads[1].All(m=>!m.Enabled),"Verified attachment did not release all head supports.");
            for(int i=0;i<8;i++) SwapFrame(script);
            Check(SwapPhase(script)=="Release","Connected/support-grid overlap must prevent split discovery.");
            f.SplitDestination();
            SwapReach(script,f,"Idle");
            Check(!(bool)Get(tools,"Recovery")! && !Enabled(script),"Successful pickup did not finish healthy and OFF.");
            Check(Get(Get(script,"Topology")!,"Head") == f.Markers[1],"Completed pickup did not restore full marker endpoint.");
            SwapFrame(script,"On",false,0); SwapFrame(script);
            Check(Enabled(script),"Completed pickup requires and accepts explicit On.");
        }
        SwapUnchangedMerges(f,"Pickup");
    }
    static void ToolSupportWaitAndCancel(Type type)
    {
        var f = new ToolSwapFixture(); var script = SwapStart(type,f);
        SwapFrame(script,"Tool 2",false,0);
        bool drove = SwapMove(script,f,"ApproachDock","Approach",true);
        drove |= SwapMove(script,f,"Dock","Dock",true);
        Check(drove,"Parking approach never drove actual actuators.");
        Check(f.Heads[0].All(m=>m.Enabled),"Settled Dock did not enable head ports.");
        f.LockSource(1);
        for(int i=0;i<20;i++) SwapFrame(script);
        Check(SwapPhase(script)=="Lock" && !f.Mutations.Contains("Detach"),"Partial two-port lock detached the source.");
        SwapFrame(script,"Off",false,0);
        Check(!Enabled(script) && SwapPhase(script)=="Idle" && (bool)Get(SwapController(script),"Recovery")!,"Off during support wait failed to require recovery.");
        Check(f.Heads[0].All(m=>m.Enabled),"Cancel automatically released source support.");
        SwapFrame(script,"On",false,0); SwapFrame(script);
        Check(!Enabled(script),"On resumed a canceled tool operation.");
        f.Rig.Storage = ((TestHost)script).Storage;
        var saved = new MyIni(); Check(saved.TryParse(f.Rig.Storage),"Tool mutation journal is invalid INI.");
        Check(saved.Get("AutoArm Swap","Phase").ToString().StartsWith("Recovery:"),"Cancel did not journal the interrupted phase.");
        var restarted = SwapStartRecovery(type,f);
        SwapFrame(restarted,"On",false,0); SwapFrame(restarted);
        Check(!Enabled(restarted),"Journal restart resumed canceled motion.");
        Check(!f.Mutations.Contains("Detach") && f.Heads[0].All(m=>m.Enabled),"Restart mutated or released partial support.");
        SwapUnchangedMerges(f,"Cancel/restart");
    }
    static object SwapStartRecovery(Type type,ToolSwapFixture f)
    {
        var script = Tests.Create(type,f.Rig);
        f.Host = (TestHost)script;
        for(int i=0;i<160 && SwapPhase(script)!="Idle";i++) SwapFrame(script);
        Check(!Enabled(script) && (bool)Get(SwapController(script),"Recovery")!,"Interrupted journal did not retain recovery OFF on startup.");
        return script;
    }
    static void PickupCancelBoundaries(Type type)
    {
        foreach(string boundary in new[]{"Attach","Release"})
        {
            var f = new ToolSwapFixture(headless:true); var script = SwapStart(type,f);
            SwapFrame(script,"Tool 2",false,0);
            SwapMove(script,f,"ApproachTop","TopApproach",false);
            SwapMove(script,f,"AlignTop","Attach",false);
            SwapReach(script,f,boundary);
            if(boundary=="Release") f.SplitDestination();
            bool[] enabled = f.Heads[1].Select(m=>m.Enabled).ToArray();
            int[] writes = f.Heads[1].Select(m=>RecordProxy.Of(m).Writes.Count).ToArray();
            int mutations = f.Mutations.Count;
            SwapFrame(script,"SwapCancel",false,0);
            Check(SwapPhase(script)=="Idle" && !Enabled(script),"Cancel at "+boundary+" failed to stop.");
            f.Rig.Storage = ((TestHost)script).Storage;
            var saved = new MyIni(); saved.TryParse(f.Rig.Storage);
            Check(saved.Get("AutoArm Swap","Phase").ToString()=="Recovery:"+boundary,"Cancel did not persist mechanical boundary "+boundary);
            var restarted = SwapStartRecovery(type,f);
            SwapFrame(restarted,"On",false,0); SwapFrame(restarted);
            Check(!Enabled(restarted) && f.Mutations.Count==mutations,"Restart at "+boundary+" resumed or repeated a mutation.");
            for(int n=0;n<2;n++) Check(f.Heads[1][n].Enabled==enabled[n] && RecordProxy.Of(f.Heads[1][n]).Writes.Count==writes[n],"Cancel/restart at "+boundary+" wrote destination supports.");
            SwapUnchangedMerges(f,"Boundary recovery "+boundary);
        }
    }
    static void FullToolSwap(Type type)
    {
        var f = new ToolSwapFixture(); var script = SwapStart(type,f);
        var ini = new MyIni(); Check(ini.TryParse(f.Rig.PB.CustomData),"Initial swap config is invalid.");
        var rows = ini.Get("Config","Actuators").ToString().Split('\n');
        for(int n=1;n<rows.Length;n++) { var cells=rows[n].Split('|'); cells[2]=(n*.23).ToString(System.Globalization.CultureInfo.InvariantCulture); cells[3]=(n*.41).ToString(System.Globalization.CultureInfo.InvariantCulture); rows[n]=string.Join("|",cells); }
        ini.Set("Config","Actuators",string.Join("\n",rows)); RecordProxy.Of(f.Rig.PB).Values["CustomData"] = ini.ToString();
        SwapFrame(script,"Reload",false,0); SwapReach(script,f,"Idle");
        for(int n=0;n<Groups(script).Length;n++) Check(Math.Abs(Convert.ToDouble(Get(Groups(script)[n],"MoveW"))-(n+1)*.23)<1e-12,"Reload lost configured physical-group tool weight.");
        var weights = Groups(script).ToDictionary(g=>((IMyMechanicalConnectionBlock)Get(Members(g)[0],"B")!).EntityId,g=>(Move:Convert.ToDouble(Get(g,"MoveW")),Turn:Convert.ToDouble(Get(g,"TurnW"))));
        long oldGrid = f.Markers[1].CubeGrid.EntityId;
        SwapFrame(script,"Tool 2",false,0);
        bool drove = SwapMove(script,f,"ApproachDock","Approach",true);
        drove |= SwapMove(script,f,"Dock","Dock",true);
        f.LockSource();
        SwapFrame(script); Check(!f.Mutations.Contains("Detach"),"Source detached after one support observation.");
        SwapFrame(script); Check(f.Mutations.SequenceEqual(new[]{"Detach"}),"Full source support did not detach exactly once.");
        SwapReach(script,f,"Retreat");
        Check(Get(Get(script,"Topology")!,"Head") == f.Mount,"Detached source did not select bare Mount endpoint.");
        drove |= SwapMove(script,f,"Retreat","Retreat",false);
        drove |= SwapMove(script,f,"ApproachTop","TopApproach",false);
        drove |= SwapMove(script,f,"AlignTop","Attach",false);
        Check(drove,"Complete tool swap never drove actual actuators.");
        SwapReach(script,f,"Release");
        Check(f.Mutations.SequenceEqual(new[]{"Detach","Attach"}),"Swap attach/detach ordering or repetition wrong.");
        Check(f.MutationJournals.SequenceEqual(new[]{"Detach","Attach"}) && f.StoppedAtMutation.All(x=>x),"Swap mechanical mutations were not preceded by a journal and stopped drives.");
        Check(f.Heads[0].All(m=>m.Enabled && m.IsConnected),"Source support was released during incoming pickup.");
        Check(f.Heads[1].All(m=>!m.Enabled),"Incoming support not released after correct attach verification.");
        f.SplitDestination(); SwapReach(script,f,"Idle");
        Check(!Enabled(script) && !(bool)Get(SwapController(script),"Recovery")!,"Completed full swap did not finish healthy OFF.");
        Check(Get(Get(script,"Topology")!,"Head") == f.Markers[1],"Final topology did not rediscover destination marker.");
        Check(Groups(script).Sum(g=>Members(g).Length)==3,"Final topology did not restore complete physical actuator corridor.");
        Check(f.Markers[1].CubeGrid.EntityId!=oldGrid,"Fixture did not exercise incoming grid identity change.");
        foreach(var g in Groups(script))
        {
            var block = (IMyMechanicalConnectionBlock)Get(Members(g)[0],"B")!; var expected = weights[block.EntityId];
            Check(Math.Abs(Convert.ToDouble(Get(g,"MoveW"))-expected.Move)<1e-12 && Math.Abs(Convert.ToDouble(Get(g,"TurnW"))-expected.Turn)<1e-12,"Merge/split identity rewrite lost physical actuator preferences.");
        }
        SwapUnchangedMerges(f,"Full swap");
    }
    static void GenericToolPark(Type type)
    {
        var f = new ToolSwapFixture(); var script = SwapStart(type,f);
        SwapFrame(script,"Park",false,0);
        bool drove = SwapMove(script,f,"ApproachDock","Approach",true);
        drove |= SwapMove(script,f,"Dock","Dock",true); f.LockSource();
        SwapReach(script,f,"Retreat");
        var target = SwapWorldGoal(script,"Retreat");
        SwapFrame(script); drove |= f.AnyDrive;
        RecordProxy.Of(f.Mount).Values["WorldMatrix"] = target;
        SwapReach(script,f,"Idle",300);
        Check(drove && !Enabled(script) && !f.Mount.IsAttached && f.Mount.Top==null,"Generic Park did not drive, detach and finish headless OFF.");
        Check(f.Mutations.SequenceEqual(new[]{"Detach"}) && f.Heads[0].All(m=>m.Enabled && m.IsConnected),"Generic Park attached or released parked support.");
        Check(Get(Get(script,"Topology")!,"Head")==f.Mount,"Generic Park lost bare Mount endpoint.");
        SwapUnchangedMerges(f,"Generic Park");
    }
    static void AlreadyPartialToolSupport(Type type)
    {
        var f = new ToolSwapFixture(); var script = SwapStart(type,f);
        f.LockSource(1); SwapFrame(script,"Park",false,0);
        for(int i=0;i<160 && SwapPhase(script)!="Idle";i++) SwapFrame(script);
        Check(SwapPhase(script)=="Idle" && !f.Mutations.Contains("Detach"),"Already supported source with one of two locks detached.");
        Check(!Enabled(script) && f.Heads[0].All(m=>RecordProxy.Of(m).Writes.All(w=>w.Name!="Enabled")),"Partial support refusal changed merges or resumed motion.");
        SwapUnchangedMerges(f,"Already partial support");
    }
    static void ToolHostileGuards(Type type)
    {
        var startup = new ToolSwapFixture(headless:true);
        RecordProxy.Of(startup.Mount).Values["TargetVelocityRad"] = .37f;
        RecordProxy.Of(startup.Mount).Writes.Clear();
        var journal = new MyIni(); journal.Set("AutoArm Swap","Phase","Attach"); startup.Rig.Storage = journal.ToString();
        var interrupted = Tests.Create(type,startup.Rig);
        Check(startup.Mount.TargetVelocityRad==0 && RecordProxy.Of(startup.Mount).Writes.Any(w=>w.Name=="TargetVelocityRad" && Equals(w.Value,0f)),"Restart did not stop excluded Mount immediately before any timed scan.");
        Check(!Enabled(interrupted) && (bool)Get(SwapController(interrupted),"Recovery")!,"Interrupted startup did not begin recovery OFF.");

        var canceled = new ToolSwapFixture(headless:true); var cancelScript = SwapStart(type,canceled);
        SwapFrame(cancelScript,"SetHome",false,0);
        Check(((string)Get(cancelScript,"HomeFingerprint")!).Length>0,"Hostile GoHome fixture did not capture a matching Home.");
        SwapFrame(cancelScript,"Tool 2",false,0);
        SwapFrame(cancelScript,"SwapCancel; GoHome",false,0); SwapFrame(cancelScript);
        Check(!Enabled(cancelScript) && !(bool)Get(cancelScript,"GoingHome")! && (bool)Get(SwapController(cancelScript),"Recovery")!,"SwapCancel;GoHome bypassed tool recovery/batch lock.");
        SwapFrame(cancelScript,"GoHome",false,0); SwapFrame(cancelScript);
        Check(!Enabled(cancelScript) && !(bool)Get(cancelScript,"GoingHome")! && !canceled.AnyDrive,"Separate GoHome resumed canceled tool motion.");

        foreach(bool known in new[]{false,true})
        {
            var f = new ToolSwapFixture(ports:false,headless:true); var script = SwapStart(type,f);
            SwapFrame(script,"On",false,0);
            var cockpit = f.Rig.Blocks.OfType<IMyShipController>().Single();
            RecordProxy.Of(cockpit).Values["MoveIndicator"] = new Vector3(0,0,-1);
            for(int i=0;i<6;i++) SwapFrame(script);
            Check(Enabled(script) && f.AnyDrive,"External attachment fixture did not first exercise bare manual movement.");
            var top = known ? f.Tops[0] : RecordProxy.Make<IMyMotorRotor>();
            if(!known)
            {
                RecordProxy.Of(top).Values["EntityId"] = 8999L;
                RecordProxy.Of(top).Values["CubeGrid"] = f.Rig.Grid();
                RecordProxy.Of(top).Values["WorldMatrix"] = MatrixD.Identity;
            }
            RecordProxy.Of(top).Values["Base"] = f.Mount;
            var mount = RecordProxy.Of(f.Mount).Values;
            mount["Top"] = top; mount["TopGrid"] = top.CubeGrid; mount["IsAttached"] = true;
            SwapFrame(script);
            Check(!Enabled(script) && !f.AnyDrive,"External "+(known?"known":"unknown")+" attachment left stale bare endpoint manual control active.");
            Check(f.Mutations.Count==0,"External attachment fault requested a mechanical mutation.");
            SwapUnchangedMerges(f,"External attachment");
        }
    }
    static void ToolStrictFormat(Type type)
    {
        var f = new ToolSwapFixture();
        string invalid = f.Rig.PB.CustomData.Replace("Format=3","Format=2").Replace("Format = 3","Format = 2");
        Check(invalid!=f.Rig.PB.CustomData,"Fixture did not replace Format.");
        RecordProxy.Of(f.Rig.PB).Values["CustomData"] = invalid;
        var script = Tests.Create(type,f.Rig); SwapFrame(script,"On",false,0); SwapFrame(script);
        Check(!Enabled(script) && f.Rig.PB.CustomData==invalid,"Invalid tool Format enabled or rewrote source configuration.");
        Check(f.Rig.Log.Any(s=>s.Contains("Format must be 3")),"Invalid tool Format omitted actionable format error.");
        Check(f.Mutations.Count==0,"Invalid tool configuration made mechanical mutations.");
    }
    static void ToolFinalAttachGuards(Type type)
    {
        var f = new ToolSwapFixture(headless:true);
        var ini = new MyIni(); ini.TryParse(f.Rig.PB.CustomData);
        ini.Set("Tools","PositionTolerance",.05); ini.Set("Tools","AngleTolerance",2);
        RecordProxy.Of(f.Rig.PB).Values["CustomData"] = ini.ToString();
        var script = SwapStart(type,f); SwapFrame(script,"Tool 2",false,0);
        SwapMove(script,f,"ApproachTop","TopApproach",false);
        SwapReach(script,f,"AlignTop");
        var goal = SwapWorldGoal(script,"Attach"); var offset = goal; offset.Translation+=Vector3D.Up*.05;
        RecordProxy.Of(f.Mount).Values["WorldMatrix"] = offset;
        for(int i=0;i<120;i++) SwapFrame(script);
        Check(SwapPhase(script)=="AlignTop" && f.Mutations.Count==0 && f.Heads[1].All(m=>m.Enabled),"Loose Tools position tolerance permitted Attach with Mount 5cm off actual shaft alignment.");
        Check(Math.Abs(Convert.ToDouble(Get(script,"LastPositionError"))-.05)<1e-8,"Loose-tolerance attach fixture did not maintain its 5cm physical pose error.");
        RecordProxy.Of(f.Mount).Values["WorldMatrix"] = goal;
        SwapReach(script,f,"Attach",300);
        Check(f.Mutations.SequenceEqual(new[]{"Attach"}),"Correct final shaft alignment did not request one Attach.");
        var shifted = goal; shifted.Translation+=Vector3D.Up*.02;
        RecordProxy.Of(f.Mount).Values["WorldMatrix"] = shifted;
        for(int i=0;i<10;i++) SwapFrame(script);
        Check(SwapPhase(script)=="Attach" && f.Heads[1].All(m=>m.Enabled),"Correct top IDs released support despite actual attached Mount pose being 2cm off alignment.");
        Check(f.Heads[1].All(m=>RecordProxy.Of(m).Writes.All(w=>w.Name!="Enabled" || !Equals(w.Value,false))),"Misaligned attached Mount caused a destination merge disable write.");
        SwapFrame(script,"Off",false,0); SwapUnchangedMerges(f,"Final attach guard");

        var pilot = new ToolSwapFixture(headless:true); var pilotScript = SwapStart(type,pilot);
        SwapFrame(pilotScript,"Speed 0",false,0);
        SwapFrame(pilotScript,"Tool 2",false,0); SwapReach(pilotScript,pilot,"ApproachTop");
        var cockpit = pilot.Rig.Blocks.OfType<IMyShipController>().Single();
        RecordProxy.Of(cockpit).Values["MoveIndicator"] = new Vector3(0,0,-1);
        SwapFrame(pilotScript);
        Check(SwapPhase(pilotScript)=="Idle" && !Enabled(pilotScript) && !pilot.AnyDrive && (bool)Get(SwapController(pilotScript),"Recovery")!,"Raw W at Speed zero failed to cancel automatic pickup into recovery OFF.");
        Check(pilot.Mutations.Count==0 && pilot.Heads[1].All(m=>m.Enabled && m.IsConnected),"Pilot cancellation mutated attachment or released parked support.");
        SwapUnchangedMerges(pilot,"Raw pilot cancellation");
    }
    static void ToolBareHomeAttachmentGuards(Type type)
    {
        foreach(bool known in new[]{false,true})
        {
            var f = new ToolSwapFixture(ports:false,headless:true); var script = SwapStart(type,f);
            SwapFrame(script,"SetHome",false,0);
            RecordProxy.Of(f.Base).Values["Angle"] = .15f;
            RecordProxy.Of(f.Piston).Values["CurrentPosition"] = 6f;
            SwapFrame(script,"GoHome",false,0); SwapFrame(script);
            Check(Enabled(script) && (bool)Get(script,"GoingHome")! && f.AnyDrive,"Bare Home attachment fixture did not first drive a valid Home return.");
            var top = known ? f.Tops[0] : RecordProxy.Make<IMyMotorRotor>();
            if(!known)
            {
                RecordProxy.Of(top).Values["EntityId"] = 8998L;
                RecordProxy.Of(top).Values["CubeGrid"] = f.Rig.Grid();
                RecordProxy.Of(top).Values["WorldMatrix"] = MatrixD.Identity;
            }
            RecordProxy.Of(top).Values["Base"] = f.Mount;
            var mount = RecordProxy.Of(f.Mount).Values;
            mount["Top"] = top; mount["TopGrid"] = top.CubeGrid; mount["IsAttached"] = true;
            SwapFrame(script);
            Check(!Enabled(script) && !(bool)Get(script,"GoingHome")! && !f.AnyDrive,"Home continued through external "+(known?"known":"unknown")+" attachment despite stale bare endpoint.");
            Check(f.Mutations.Count==0,"External attachment Home fault requested a mechanical mutation.");
            SwapUnchangedMerges(f,"External Home attachment");
        }
    }
    static void ToolRejectedStartupStopsOwned(Type type)
    {
        foreach(bool missingMarker in new[]{false,true})
        {
            var f = new ToolSwapFixture();
            var unrelatedRotor = f.Rig.Rotor("Unrelated ship rotor",f.Rig.Root,f.Rig.Grid());
            var unrelatedPiston = f.Rig.Piston("Unrelated ship piston",f.Rig.Root,f.Rig.Grid());
            var original = SwapStart(type,f);
            // Persist exactly what the real controller records for its owned
            // corridor. The startup refusal occurs before fresh discovery.
            f.Rig.Storage = ((TestHost)original).Storage;
            var ini = new MyIni(); ini.TryParse(f.Rig.PB.CustomData);
            if(missingMarker) f.Rig.Blocks.Remove(f.Markers[1]);
            else ini.Set("Config","HeadSpeed","NaN");
            string refused = ini.ToString(); RecordProxy.Of(f.Rig.PB).Values["CustomData"] = refused;
            RecordProxy.Of(f.Base).Values["TargetVelocityRad"] = .19f;
            RecordProxy.Of(f.Piston).Values["Velocity"] = .23f;
            RecordProxy.Of(f.Mount).Values["TargetVelocityRad"] = -.31f;
            RecordProxy.Of(unrelatedRotor).Values["TargetVelocityRad"] = .27f;
            RecordProxy.Of(unrelatedPiston).Values["Velocity"] = .43f;
            foreach(var b in new IMyTerminalBlock[]{f.Base,f.Piston,f.Mount,unrelatedRotor,unrelatedPiston}) RecordProxy.Of(b).Writes.Clear();
            var restarted = Tests.Create(type,f.Rig);
            string reason = missingMarker ? "missing configured tool marker" : "invalid scalar";
            Check(!Enabled(restarted) && f.Base.TargetVelocityRad==0 && f.Piston.Velocity==0 && f.Mount.TargetVelocityRad==0,"Fresh startup with "+reason+" left a previously owned actuator moving before any timed update.");
            Check(unrelatedRotor.TargetVelocityRad==.27f && unrelatedPiston.Velocity==.43f && RecordProxy.Of(unrelatedRotor).Writes.Count==0 && RecordProxy.Of(unrelatedPiston).Writes.Count==0,"Rejected startup with "+reason+" wrote unrelated ship actuators.");
            Check(f.Rig.PB.CustomData==refused && f.Mutations.Count==0,"Rejected startup rewrote user configuration or mutated tool mechanics.");
        }
    }
}
