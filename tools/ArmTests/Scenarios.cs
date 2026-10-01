using System.Collections;
using System.Reflection;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

internal static partial class Scenarios
{
    const BindingFlags All = BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static object? Get(object o,string name)
    {
        o=PackedProgramChecks.Unwrap(o);
        string member=PackedProgramChecks.Name(o.GetType(),name);
        var field=o.GetType().GetField(member,All); if(field!=null) return field.GetValue(o);
        var property=o.GetType().GetProperty(member,All); if(property!=null) return property.GetValue(o);
        var arms=o.GetType().GetField(PackedProgramChecks.Name(o.GetType(),"Arms"),All)?.GetValue(o) as IDictionary;
        var selected=o.GetType().GetField(PackedProgramChecks.Name(o.GetType(),"Selected"),All)?.GetValue(o) as string;
        return arms!=null&&selected!=null&&arms.Contains(selected)?Get(arms[selected]!,name):null;
    }
    static object[] Groups(object script) => ((IEnumerable?)Get(Get(script,"Topology")!,"Groups"))?.Cast<object>().ToArray() ?? Array.Empty<object>();
    static object[] Members(object group) => ((IEnumerable)Get(group,"M")!).Cast<object>().ToArray();
    static bool Enabled(object script) => (bool)Get(script,"Enabled")!;
    static void Run(object script,string command="")
    {
        var main=script.GetType().GetMethod("Main")!;
        if(main.GetParameters().Length==1){main.Invoke(script,new object[]{command});return;}
        if(command.Length==0){main.Invoke(script,new object[]{command,UpdateType.Update1});return;}
        // Terminal/toolbar commands may run in the same frame (dt=0). Observe
        // their motion effects only on the following timed update.
        var runtime=RecordProxy.Of(((TestHost)script).Runtime);
        var interval=runtime.Values["TimeSinceLastRun"];
        try
        {
            runtime.Values["TimeSinceLastRun"]=TimeSpan.Zero;
            main.Invoke(script,new object[]{command,UpdateType.Terminal});
        }
        finally{runtime.Values["TimeSinceLastRun"]=interval;}
        main.Invoke(script,new object[]{"",UpdateType.Update1});
        WaitForOn(script);
    }
    static void WaitForOn(object script)
    {
        bool waiting = (bool)Get(script,"PendingOn")!;
        if(waiting) RecordProxy.Of(((TestHost)script).Runtime).Values["TimeSinceLastRun"]=TimeSpan.FromSeconds(1d/60);
        for(int i=0;i<200 && (bool)Get(script,"PendingOn")!;i++)
            script.GetType().GetMethod("Main")!.Invoke(script,new object[]{"",UpdateType.Update1});
        if(waiting && Enabled(script)) script.GetType().GetMethod("Main")!.Invoke(script,new object[]{"",UpdateType.Update1});
    }
    static object Start(Type type,Rig rig)
    {
        var script=Tests.Create(type,rig);
        RecordProxy.Of(rig.Runtime).Values["TimeSinceLastRun"]=TimeSpan.FromSeconds(.1);
        for(int i=0;i<30;i++)Run(script);
        RecordProxy.Of(rig.Runtime).Values["TimeSinceLastRun"]=TimeSpan.FromSeconds(1d/60);
        return script;
    }
    static void Check(bool condition,string message)=>Tests.Assert(condition,message);
    static void NoVelocity(Rig rig)
    {
        foreach(var block in rig.Blocks)
            if(block is IMyMotorStator rotor)Check(rotor.TargetVelocityRad==0,"Rotor not stopped: "+block.CustomName);
            else if(block is IMyPistonBase piston)Check(piston.Velocity==0,"Piston not stopped: "+block.CustomName);
    }
    internal static void RunAll(Type type)
    {
        Discovery(type);
        Configuration(type);
        Responsive(type);
        Faults(type);
        BoundsAndInvalidState(type);
        Home(type);
        Adversarial(type);
        IdealPistonMotion(type);
        ClaimsAndCompatibility(type);
        TimingAndPerformance(type);
        PivotProbeCases(type);
        FriendlyConfigCases(type);
        GravityHoldCases(type);
        DampingCases(type);
        ViewControlCases(type);
        PoseCommandsCases(type);
        PathCases(type);
        var toolType=Tests.Script(Tests.ToolEngineSource);
        NativeReverseGeometry(toolType);
        DualModuleCases(type,toolType);
        ServiceCases(type,toolType);
        ValidationAndCacheCases(type);
        Console.WriteLine($"Automatic arm integration: PASS ({Tests.Assertions} assertions).");
    }
    static void Discovery(Type type)
    {
        var fixtures=new (Rig Rig,int Groups,int Members)[]{
            (Fixtures.Serial(out _,out _,out _,out _),2,2),
            (Fixtures.Parallel(false,out _,out _),1,2),
            (Fixtures.Parallel(true,out _,out _),1,2),
            (Fixtures.Rails(),3,5),
            (Fixtures.MixedBranches(),4,7),
            (Fixtures.FullMixed(),12,15)};
        foreach(var fixture in fixtures)
        {
            var script=Start(type,fixture.Rig);
            var groups=Groups(script);
            Check(groups.Length==fixture.Groups,$"Detected {groups.Length} groups, expected {fixture.Groups}. Log: {string.Join(" | ",fixture.Rig.Log.TakeLast(3))}");
            Check(groups.Sum(g=>Members(g).Length)==fixture.Members,"Wrong physical actuator count.");
            Check(!Enabled(script),"Discovery must stay Off.");
            Check(fixture.Rig.PB.CustomData.Contains("[AutoArm]"),"No generated Custom Data.");
            Run(script,"On");
            Check(Enabled(script),"Valid arm would not enable: "+string.Join(" | ",fixture.Rig.Log.TakeLast(3)));
            Run(script,"Stop");NoVelocity(fixture.Rig);
        }
        foreach(var rig in new[]{Fixtures.Rails(unequal:true),Fixtures.Rails(rotaryBranch:true),Fixtures.MixedBranches(offsetRotaryAxis:true)})
        {
            var script=Start(type,rig);Run(script,"On");
            Check(!Enabled(script),"Unsupported closed loop enabled.");NoVelocity(rig);
        }
        var parallel=Fixtures.Parallel(true,out var a,out var b);
        var paired=Start(type,parallel);
        var signs=Members(Groups(paired)[0]).Select(m=>Convert.ToDouble(Get(m,"Sign"))).ToArray();
        Check(signs[0]*signs[1]<0,"Antiparallel axes need opposite actuator signs.");
        var accessory=Fixtures.Serial(out var baseRotor,out var piston,out _,out _);
        var side=accessory.Rotor("unrelated accessory",baseRotor.TopGrid,accessory.Grid());
        RecordProxy.Of(side).Values["TargetVelocityRad"]=.17f;
        RecordProxy.Of(side).Writes.Clear();
        var withAccessory=Start(type,accessory);Run(withAccessory,"On");Run(withAccessory,"Stop");
        Check(Groups(withAccessory).Length==2,"Dangling accessory entered the arm corridor.");
        Check(!RecordProxy.Of(side).Writes.Any(w=>w.Name=="TargetVelocityRad"),"Controller wrote to an ignored accessory.");
        var ship=Fixtures.Serial(out _,out _,out _,out _);
        for(int i=0;i<60;i++)ship.Rotor("unrelated ship mechanism "+i,ship.Root,ship.Grid());
        Check(Groups(Start(type,ship)).Length==2,"Whole-ship mechanism count incorrectly limits a small arm.");
        Console.WriteLine("Discovery: serial, signed hinge bundles, piston rails, mixed split/rejoin branches, original 12-group layout; unsupported loops rejected; accessory untouched.");
    }
    static void Configuration(Type type)
    {
        var rig=Fixtures.Serial(out _,out _,out _,out _);RecordProxy.Of(rig.PB).Values["CustomData"]="[AutoArm]\nArm=Arm 1\nFormat=6\n[Unrelated]\nKeep=original\n";
        var script=Start(type,rig);
        var ini=new MyIni();Check(ini.TryParse(rig.PB.CustomData),"Generated INI invalid.");
        Check(ini.Get("Unrelated","Keep").ToString()=="original","Unrelated configuration lost.");
        var rows=ini.Get("Config","Actuators").ToString().Split('\n');
        var row=rows[1].Split('|');row[2]=" 0.25 ";row[3]=" 2 ";rows[1]=string.Join("|",row);
        ini.Set("Config","Actuators",string.Join("\n",rows));
        RecordProxy.Of(rig.PB).Values["CustomData"]=ini.ToString();
        Run(script,"Reload");for(int i=0;i<90;i++)Run(script);
        Check(!Enabled(script),"Reload unexpectedly enabled movement.");
        Check(Math.Abs(Convert.ToDouble(Get(Groups(script)[0],"MoveW"))-.25)<1e-12,"Translation preference not loaded.");
        Check(Math.Abs(Convert.ToDouble(Get(Groups(script)[0],"TurnW"))-2)<1e-12,"Orientation preference not loaded.");
        var text=rig.PB.CustomData;Run(script,"DumpTrace");Check(rig.PB.CustomData==text,"Trace export overwrote configuration.");
        foreach(string bad in new[]{"NaN","-1","11","broken"})
        {
            var invalid=new MyIni();invalid.TryParse(text);
            var invalidRows=invalid.Get("Config","Actuators").ToString().Split('\n');
            var invalidRow=invalidRows[1].Split('|');invalidRow[2]=bad;invalidRows[1]=string.Join("|",invalidRow);
            invalid.Set("Config","Actuators",string.Join("\n",invalidRows));
            string data=invalid.ToString();RecordProxy.Of(rig.PB).Values["CustomData"]=data;
            Run(script,"Reload");Run(script,"On");
            Check(!Enabled(script),"Invalid participation value enabled motion: "+bad);
            Check(rig.PB.CustomData==data,"Invalid participation value silently overwritten: "+bad);
        }
        RecordProxy.Of(rig.PB).Values["CustomData"]=text;Run(script,"Reload");for(int i=0;i<5;i++)Run(script);
        RecordProxy.Of(rig.PB).Values["CustomData"]="non-INI user notes that must survive";
        Run(script,"Reload");
        Check(rig.PB.CustomData=="non-INI user notes that must survive","Malformed Custom Data was overwritten.");
        Check(!Enabled(script),"Invalid configuration did not stay Off.");
        Console.WriteLine("Configuration: defaults, unrelated sections, editable weights, Off-on-reload and non-destructive trace/parse-failure behavior.");
    }
    static void Responsive(Type type)
    {
        foreach(double offset in new[]{.08,.2})
        {
            var rig=Fixtures.Serial(out var rotor,out var piston,out var head,out var cockpit);
            var script=Start(type,rig);Run(script,"On");
            var matrix=(MatrixD)RecordProxy.Of(head).Values["WorldMatrix"]!;matrix.Translation+=Vector3D.Forward*offset;
            RecordProxy.Of(head).Values["WorldMatrix"]=matrix;
            RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-1);
            bool moved=false;
            for(int tick=0;tick<30;tick++)
            {
                Run(script);
                Check(Enabled(script),"Tracking correction disabled manual control.");
                var pilot=(Vector3D)Get(script,"LastPilotLinear")!;var feedback=(Vector3D)Get(script,"LastFeedbackLinear")!;var request=(Vector3D)Get(script,"LastRequestedLinear")!;
                Check(pilot.LengthSquared()>1e-8,"Tracking error muted pilot input.");
                Check(feedback.LengthSquared()>1e-8,"Pose correction disappeared under pilot input: tick="+tick+", offset="+offset+", pose error="+Get(script,"LastPositionError")+", bias="+Get(script,"PositionI")+", feedback="+feedback);
                Check((request-pilot-feedback).Length()<1e-10,"Pilot and feedback are not additive.");
                Check(Convert.ToDouble(Get(script,"LastLinearAdvance"))>0,"Opposing correction froze pilot target advancement.");
                Check(Math.Abs(piston.Velocity)<=.200001,"Piston cap exceeded.");
                Check(Math.Abs(rotor.TargetVelocityRad)<=.5*Math.PI/30+.000001,"Rotary cap exceeded.");
                moved|=Math.Abs(piston.Velocity)>1e-6;
            }
            Check(moved,"Held pilot input never produced piston motion after initial feedback cancellation.");
            Run(script,"Stop");NoVelocity(rig);
        }
        var angularRig=Fixtures.Serial(out var baseRotor,out _,out var tool,out var pilotController);
        var angularScript=Start(type,angularRig);Run(angularScript,"On");
        var facing=Vector3D.Normalize(Vector3D.Forward+.05*Vector3D.Right);
        RecordProxy.Of(tool).Values["WorldMatrix"]=MatrixD.CreateWorld(tool.GetPosition(),facing,Vector3D.Up);
        RecordProxy.Of(pilotController).Values["RollIndicator"]=1f;Run(angularScript);
        var ap=(Vector3D)Get(angularScript,"LastPilotAngular")!;
        var af=(Vector3D)Get(angularScript,"LastFeedbackAngular")!;
        var ar=(Vector3D)Get(angularScript,"LastRequestedAngular")!;
        var frame=baseRotor.WorldMatrix;
        var expected=new Vector3D(Vector3D.Dot(facing,frame.Forward),Vector3D.Dot(facing,frame.Left),Vector3D.Dot(facing,frame.Up))*(5*Math.PI/180);
        Check((ap-expected).Length()<1e-10,"Q/E roll direction differs from the original head controls.");
        Check(af.LengthSquared()>1e-8&&(ar-ap-af).Length()<1e-10,"Angular input is not additive to nonzero orientation correction.");
        Run(angularScript,"Stop");NoVelocity(angularRig);
        Console.WriteLine("Responsiveness: nonzero pilot + feedback at 8 cm and 20 cm error, held over multiple ticks, with drive caps respected.");
    }
    static void Faults(Type type)
    {
        var rig=Fixtures.Serial(out var rotor,out var piston,out _,out var cockpit);var script=Start(type,rig);Run(script,"On");
        RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-1);Run(script);
        RecordProxy.Of(piston).Values["IsAttached"]=false;Run(script);
        Check(!Enabled(script),"Detached owned drive did not stop the arm.");NoVelocity(rig);
        RecordProxy.Of(piston).Values["IsAttached"]=true;for(int i=0;i<30;i++)Run(script);
        Check(!Enabled(script),"Repair automatically restarted a stopped arm.");NoVelocity(rig);
        var duplicate=Fixtures.Serial(out var root,out _,out var head,out _);
        duplicate.Block<IMyShipDrill>("Arm 1 - Head duplicate",head.CubeGrid);
        var rejected=Start(type,duplicate);Run(rejected,"On");Check(!Enabled(rejected),"Ambiguous head marker enabled.");NoVelocity(duplicate);
        var stale=Fixtures.Serial(out _,out _,out _,out _);var stopped=Start(type,stale);Run(stopped,"On");
        RecordProxy.Of(stale.Runtime).Values["TimeSinceLastRun"]=TimeSpan.FromSeconds(2);Run(stopped);
        Check(!Enabled(stopped),"Stale control tick did not stop.");NoVelocity(stale);
        var checkedRig=Fixtures.Serial(out _,out var checkedPiston,out _,out _);var checkedScript=Start(type,checkedRig);Run(checkedScript,"On");
        RecordProxy.Of(checkedPiston).Values["IsAttached"]=false;Run(checkedScript,"Check");
        Check(!Enabled(checkedScript),"A failing explicit Check left control enabled.");
        RecordProxy.Of(checkedPiston).Values["IsAttached"]=true;for(int i=0;i<30;i++)Run(checkedScript);
        Check(!Enabled(checkedScript),"Check failure auto-resumed after repair.");
        Console.WriteLine("Faults: detachment, ambiguous head and stale tick refuse/stop motion.");
    }
    static void BoundsAndInvalidState(Type type)
    {
        var rig=Fixtures.Serial(out var rotor,out var piston,out _,out var cockpit);var script=Start(type,rig);
        RecordProxy.Of(piston).Values["CurrentPosition"]=10f;Run(script,"On");
        RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-1);Run(script);
        Check(piston.Velocity<=0,"Piston commanded beyond upper limit.");
        RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,1);Run(script);
        Check(piston.Velocity<0,"Piston could not reverse away from upper limit.");
        RecordProxy.Of(piston).Values["CurrentPosition"]=float.NaN;Run(script);
        Check(!Enabled(script),"Non-finite piston feedback did not fault.");NoVelocity(rig);
        var angular=Fixtures.Parallel(true,out var a,out var b);var paired=Start(type,angular);Run(paired,"On");
        var pilot=angular.Blocks.OfType<IMyShipController>().Single();
        RecordProxy.Of(pilot).Values["MoveIndicator"]=new Vector3(1,0,0);
        float previous=0;
        for(int i=0;i<12;i++)
        {
            Run(paired);Check(Math.Abs(a.TargetVelocityRad+b.TargetVelocityRad)<1e-6,"Paired antiparallel commands conflict.");
            Check(Math.Abs(a.TargetVelocityRad-previous)<=Math.PI/30/60+1e-6,"Rotary acceleration cap exceeded.");previous=a.TargetVelocityRad;
        }
        RecordProxy.Of(b).Values["RotorLock"]=true;Run(paired);
        Check(a.TargetVelocityRad==0&&b.TargetVelocityRad==0,"Locked parallel member did not zero its group.");
        var nan=Fixtures.Serial(out var nanRotor,out _,out _,out _);var nanScript=Start(type,nan);Run(nanScript,"On");
        RecordProxy.Of(nanRotor).Values["LowerLimitRad"]=float.NaN;Run(nanScript);
        Check(!Enabled(nanScript),"Invalid joint limits did not fault.");NoVelocity(nan);
        Console.WriteLine("Bounds: upper-limit reversal, finite-state guards, signed paired commands, acceleration and locked-group behavior.");
    }
    static void Home(Type type)
    {
        var rig=Fixtures.Serial(out var rotor,out var piston,out _,out _);rig.Storage="legacy home text must survive";
        var script=Start(type,rig);
        var preserved=new MyIni();Check(preserved.TryParse(((TestHost)script).Storage)&&preserved.EndContent==rig.Storage,"Startup lost opaque legacy Storage.");
        Run(script,"SetHome");Check(((TestHost)script).Storage.Contains("AutoArm Home"),"Home was not persisted.");
        RecordProxy.Of(piston).Values["CurrentPosition"]=6f;
        Run(script,"GoHome");Check(Enabled(script),"Valid home did not start.");
        Check(piston.Velocity<0,"Home moved the piston away from its saved target.");
        Run(script,"Hold");Check(Enabled(script)&&!(bool)Get(script,"GoingHome")!,"Hold did not cancel GoHome.");
        Run(script,"Stop");NoVelocity(rig);
        RecordProxy.Of(piston).Values["MinLimit"]=5.5f;
        Run(script,"GoHome");Check(!Enabled(script),"Home outside physical limits was accepted.");NoVelocity(rig);
        var wrap=Fixtures.Parallel(false,out var a,out var b);
        RecordProxy.Of(a).Values["Angle"]=(float)(Math.PI-.01);RecordProxy.Of(b).Values["Angle"]=(float)(Math.PI-.01);
        var wrapped=Start(type,wrap);Run(wrapped,"SetHome");
        RecordProxy.Of(a).Values["Angle"]=(float)(-Math.PI+.01);RecordProxy.Of(b).Values["Angle"]=(float)(-Math.PI+.01);
        Run(wrapped,"GoHome");Check(Enabled(wrapped),"Wrapped rotor home did not start.");
        Check(a.TargetVelocityRad<0&&b.TargetVelocityRad<0,"Rotor home selected the long way across angle wrap.");
        Run(wrapped,"Stop");NoVelocity(wrap);
        foreach(bool flipped in new[]{false,true})
        {
            var halfTurn=Fixtures.Parallel(flipped,out var lead,out var mate);var homeScript=Start(type,halfTurn);Run(homeScript,"SetHome");
            RecordProxy.Of(lead).Values["Angle"]=(float)(Math.PI-.001);
            RecordProxy.Of(mate).Values["Angle"]=(float)((flipped?-1:1)*(Math.PI+.001));
            Run(homeScript,"GoHome");
            Check(Enabled(homeScript),"A synchronized pair chose opposing circular home branches.");
            Check(lead.TargetVelocityRad<0&&Math.Abs(lead.TargetVelocityRad-(flipped?-1:1)*mate.TargetVelocityRad)<1e-6,"Half-turn home commands fight between paired members.");
            Run(homeScript,"Stop");NoVelocity(halfTurn);
        }
        foreach(var homeCase in new[]{(Index:0,Upper:2*Math.PI),(Index:1,Upper:2*Math.PI),(Index:1,Upper:Math.PI+.01)})
        {
            var mixedLimits=Fixtures.Parallel(false,out var lead,out var mate);
            var limited=RecordProxy.Of(homeCase.Index==0?lead:mate);
            limited.Values["LowerLimitRad"]=0f;limited.Values["UpperLimitRad"]=(float)homeCase.Upper;
            var homeScript=Start(type,mixedLimits);Run(homeScript,"SetHome");
            RecordProxy.Of(lead).Values["Angle"]=(float)(Math.PI-.001);RecordProxy.Of(mate).Values["Angle"]=(float)(Math.PI+.001);
            Run(homeScript,"GoHome");
            // At pi+.001 a legal 2pi target is closer than zero; otherwise the limited member must return toward zero.
            int direction=homeCase.Index==1&&homeCase.Upper>=2*Math.PI?1:-1;
            Check(Enabled(homeScript)&&direction*lead.TargetVelocityRad>0&&direction*mate.TargetVelocityRad>0,
                $"Mixed-limit home branch failed (limited index {homeCase.Index}, upper {homeCase.Upper}, expected sign {direction}, rates {lead.TargetVelocityRad}/{mate.TargetVelocityRad}): {string.Join(" | ",mixedLimits.Log.TakeLast(3))}");
            Check(Math.Abs(lead.TargetVelocityRad-mate.TargetVelocityRad)<1e-6,"Mixed-limit peers received incompatible velocities.");
            Run(homeScript,"Stop");NoVelocity(mixedLimits);
        }
        var malformed=Fixtures.Serial(out _,out _,out _,out _);var refused=Start(type,malformed);Run(refused,"SetHome");
        RecordProxy.Of(malformed.PB).Values["CustomData"]="malformed edited configuration";
        Run(refused,"Reload");Run(refused,"GoHome");
        Check(!Enabled(refused),"GoHome bypassed invalid configuration after Reload.");NoVelocity(malformed);
        Console.WriteLine("Home: legacy Storage preservation, capture, bounded return, impossible target refusal and shortest valid rotor wrap.");
    }
    static void Adversarial(Type type)
    {
        var bad=Fixtures.Serial(out _,out _,out _,out var cockpit);
        RecordProxy.Of(bad.PB).Values["CustomData"]="invalid first boot data";
        var repaired=Start(type,bad);Check(!Enabled(repaired),"Invalid first boot enabled.");
        RecordProxy.Of(bad.PB).Values["CustomData"]="";Run(repaired,"Reload");for(int i=0;i<5;i++)Run(repaired);
        Run(repaired,"On");RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-1);Run(repaired);
        Check(Enabled(repaired),"Fixing initial CustomData via Reload leaves unallocated control state: "+string.Join(" | ",bad.Log.TakeLast(3)));
        var pair=Fixtures.Parallel(true,out var first,out var second);var paired=Start(type,pair);Run(paired,"On");
        RecordProxy.Of(first).Values["Angle"]=(float)Math.PI;RecordProxy.Of(second).Values["Angle"]=(float)-Math.PI;
        Run(paired);Check(Enabled(paired),"Synchronized opposite-sign pair falsely faulted at +/-pi.");
        RecordProxy.Of(first).Values["Angle"]=(float)(-Math.PI+.001);RecordProxy.Of(second).Values["Angle"]=(float)(Math.PI-.002);
        Run(paired);Check(Enabled(paired),"Within-tolerance circular pair mismatch falsely faulted near branch cut.");
        RecordProxy.Of(first).Values["Angle"]=(float)(-Math.PI+.05);Run(paired);
        Check(!Enabled(paired),"True pair desynchronization near branch cut was missed.");NoVelocity(pair);
        var linked=Fixtures.Serial(out var root,out var end,out _,out _);var linkedScript=Start(type,linked);Run(linkedScript,"On");
        linked.Piston("new crosslink",root.TopGrid,end.TopGrid,Vector3D.Zero,Vector3D.Forward);
        for(int i=0;i<30&&Enabled(linkedScript);i++)Run(linkedScript);
        Check(!Enabled(linkedScript),"A new parallel closure escaped the periodic 30-pass discovery bound.");
        var rotationRig=Fixtures.Serial(out _,out _,out _,out _);var rotationScript=Start(type,rotationRig);
        var f=new Vector3D(1,0,0);var u=new Vector3D(0,0,1);
        var exact=(Vector3D)Tests.Call(rotationScript,"RotationError",f,u,-f,u)!;
        Check(Math.Abs(Math.Abs(exact.Z)-Math.PI)<1e-8&&Math.Abs(exact.X)+Math.Abs(exact.Y)<1e-8,"Exact Face Back selected the wrong rotation axis.");
        var random=new Random(17907001);
        Func<Vector3D> vector=()=>Vector3D.Normalize(new Vector3D(random.NextDouble()-.5,random.NextDouble()-.5,random.NextDouble()-.5));
        for(int i=0;i<1200;i++)
        {
            var forward=vector();var up=Vector3D.Normalize(Vector3D.Cross(forward,vector()));var axis=vector();
            double angle=i%4==0?Math.PI:i%4==1?Math.PI-1e-7:i%4==2?1e-9:random.NextDouble()*Math.PI;
            var tf=RotateVector(forward,axis*angle);var tu=RotateVector(up,axis*angle);
            var error=(Vector3D)Tests.Call(rotationScript,"RotationError",forward,up,tf,tu)!;
            Check(double.IsFinite(error.Length())&&error.Length()<=Math.PI+1e-7,"Invalid rotation error vector.");
            Check((RotateVector(forward,error)-tf).Length()<2e-6&&(RotateVector(up,error)-tu).Length()<2e-6,"Rotation error does not reconstruct target orientation.");
        }
        Console.WriteLine("Adversarial regressions: invalid-first-boot reload, circular paired rotary sync, bounded crosslink detection, exact Face Back and 1,200 orientation reconstructions.");
    }
    static void IdealPistonMotion(Type type)
    {
        // A deliberately ideal velocity actuator, not a Space Engineers constraint/physics simulation.
        var rig=new Rig();var moving=rig.Grid();
        var piston=rig.Piston("Arm 1 - Base - Piston",rig.Root,moving,Vector3D.Zero,Vector3D.Forward);
        var tool=rig.Block<IMyShipDrill>("Arm 1 - Head - Drill",moving,Vector3D.Forward*5);
        var cockpit=rig.Cockpit();var script=Start(type,rig);Run(script,"On");
        double q=5;float previous=0;
        Action tick=()=>
        {
            Run(script);Check(Enabled(script),"Ideal piston controller unexpectedly disabled.");
            Check(Math.Abs(piston.Velocity-previous)<=.4/60+1e-6,"Ideal piston acceleration limit exceeded.");previous=piston.Velocity;
            q+=piston.Velocity/60d;
            RecordProxy.Of(piston).Values["CurrentPosition"]=(float)q;
            RecordProxy.Of(tool).Values["WorldMatrix"]=MatrixD.CreateWorld(Vector3D.Forward*q,Vector3D.Forward,Vector3D.Up);
        };
        RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-1);
        for(int i=0;i<240;i++)tick();
        Check(q>5.6&&q<5.9,"Ideal actuator did not follow sustained pilot translation.");
        RecordProxy.Of(cockpit).Values["MoveIndicator"]=Vector3.Zero;
        for(int i=0;i<240;i++)tick();
        Check(Convert.ToDouble(Get(script,"LastPositionError"))<.01,"Ideal actuator did not settle in hold.");
        q+=.2;RecordProxy.Of(piston).Values["CurrentPosition"]=(float)q;
        RecordProxy.Of(tool).Values["WorldMatrix"]=MatrixD.CreateWorld(Vector3D.Forward*q,Vector3D.Forward,Vector3D.Up);
        RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,1);tick();
        var pilot=(Vector3D)Get(script,"LastPilotLinear")!;var feedback=(Vector3D)Get(script,"LastFeedbackLinear")!;
        Check(Vector3D.Dot(pilot,feedback)>0,"Disturbance fixture did not align pilot and correction.");
        Check(piston.Velocity<0,"Aligned pilot+correction did not immediately reverse the resting actuator.");
        Run(script,"Stop");NoVelocity(rig);
        var budget=Fixtures.Serial(out _,out _,out _,out _);var budgetScript=Start(type,budget);Run(budgetScript,"On");
        RecordProxy.Of(budget.Runtime).Values["CurrentInstructionCount"]=(int)(budget.Runtime.MaxInstructionCount*.90);Run(budgetScript);
        Check(!Enabled(budgetScript),"Low remaining instruction budget left control enabled.");NoVelocity(budget);
        Console.WriteLine("Ideal piston motion: sustained input, acceleration-limited release/hold, disturbance plus input; budget-exhaustion stop branch (not in-game cost profiling).");
    }
    static Vector3D RotateVector(Vector3D value,Vector3D rotation)
    {
        double angle=rotation.Length();if(angle<1e-14)return value;
        var axis=rotation/angle;double cosine=Math.Cos(angle),sine=Math.Sin(angle);
        return value*cosine+Vector3D.Cross(axis,value)*sine+axis*Vector3D.Dot(axis,value)*(1-cosine);
    }
}
