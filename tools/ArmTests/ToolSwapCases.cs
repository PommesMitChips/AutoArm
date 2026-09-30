using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;
#if TOOL_SWAP_FOCUS
internal static class ToolSwapFocusRunner
{
    static int Main(string[] args)
    {
        try
        {
            System.Runtime.Loader.AssemblyLoadContext.Default.Resolving+=(_,name)=>
            {
                var file=Path.Combine(Tests.GameBin,name.Name+".dll");
                return File.Exists(file)?System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(file):null;
            };
            Tests.Workspace=Path.GetDirectoryName(Path.GetFullPath(args[0]))!;
            Scenarios.ToolSwapCases(Tests.Script(File.ReadAllText(args[0])));
            Console.WriteLine("Reverse tool swap focus PASS: "+Tests.Assertions+" assertions."); return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
#endif
internal static partial class Scenarios
{
    static object SwapController(object script)=>Get(script,"Tools")!;
    static string SwapPhase(object script)=>(string)Get(SwapController(script),"Phase")!;
    static void SwapFrame(object script,string command="",bool timed=true,double dt=1d/60)
    {
        RecordProxy.Of(((TestHost)script).Runtime).Values["TimeSinceLastRun"]=TimeSpan.FromSeconds(dt);
        script.GetType().GetMethod("Main")!.Invoke(script,new object[]{command,timed?UpdateType.Update1:UpdateType.Terminal});
    }
    static MatrixD SwapGoal(object script,string field)
    {
        var tools=SwapController(script); string anchor=field=="Dock" || field=="Approach"?"DockAnchor":field=="Retreat"?"RetreatAnchor":"AttachAnchor";
        return (MatrixD)Get(tools,field)! * (MatrixD)Get(Get(tools,anchor)!,"WorldMatrix")!;
    }
    static object SwapStart(Type type,ToolSwapFixture f,bool recovery=false)
    {
        var script=Tests.Create(type,f.Rig); f.Host=(TestHost)script;
        for(int i=0;i<200 && SwapPhase(script)!="Idle";i++) SwapFrame(script);
        Check(SwapPhase(script)=="Idle" && !Enabled(script) && (bool)Get(SwapController(script),"Recovery")! == recovery,"Reverse fixture startup scan failed: "+string.Join(" | ",f.Rig.Log.TakeLast(4)));
        return script;
    }
    static bool SwapPlant(object script,ToolSwapFixture f,string until,bool autoLock=true,bool autoSplit=true,int maximum=5000)
    {
        bool drove=false; string previous="";
        for(int i=0;i<maximum && SwapPhase(script)!=until;i++)
        {
            string phase=SwapPhase(script);
            Check(phase!="Idle","Operation stopped before "+until+": "+string.Join(" | ",f.Rig.Log.TakeLast(4)));
            string? field=phase=="ApproachDock"?"Approach":phase=="Dock"?"Dock":phase=="Retreat"?"Retreat":phase=="ApproachTop"?"TopApproach":phase=="AlignTop"?"Attach":null;
            if(field!=null && previous!=phase)
            {
                SwapFrame(script,dt:.05); drove|=f.AnyDrive;
                if(SwapPhase(script)!=phase) { previous=phase; continue; }
            }
            if(field!=null)
            {
                var goal=SwapGoal(script,field);
                if(phase=="ApproachDock" || phase=="Dock") f.MoveSource(goal); else f.MoveArm(goal);
            }
            else if(phase=="Lock" && autoLock) f.LockSource();
            else if(phase=="Release" && autoSplit) f.SplitTool(Array.IndexOf(f.Markers,(IMyTerminalBlock)Get(Get(SwapController(script),"Destination")!,"Marker")!));
            SwapFrame(script,dt:.05); drove|=f.AnyDrive; previous=phase;
        }
        Check(SwapPhase(script)==until,"Reverse operation did not reach "+until+"; at "+SwapPhase(script)); return drove;
    }
    static void SwapStationUntouched(ToolSwapFixture f)=>Check(!f.StationWritten,"Controller wrote selected or unrelated station merge blocks.");
    static void SwapReverseMember(object script,IMyMotorStator coupler)
    {
        var member=Groups(script).SelectMany(Members).Single(m=>((IMyMechanicalConnectionBlock)Get(m,"B")!).EntityId==coupler.EntityId);
        Check(Convert.ToInt64(Get(member,"U"))==coupler.TopGrid.EntityId && Convert.ToInt64(Get(member,"V"))==coupler.CubeGrid.EntityId,"Tool-side stator corridor must traverse the physical connection from its top grid toward its base grid.");
    }
    internal static void ToolSwapCases(Type type)
    {
        ReverseOptionalAndConfiguration(type);
        ReverseFreshSwap(type,0,false);
        ReverseFreshSwap(type,.4,true);
        ReverseFreshSwap(type,-.4,false);
        RejectHingeToolCouplers(type);
        FreshHeadlessTipCases(type);
        NativeReverseGeometry(type);
        ReverseWeightRetention(type);
        ReverseParkRestart(type);
        ReverseWrongPartAndTiming(type);
        ReversePartialAndRecovery(type);
        ReverseRuntimeGuards(type);
        ReverseSetupAndCacheScope(type);
        ReverseStartupStop(type);
        Console.WriteLine("Reverse tool changes: visible tool-side bases, hidden shared arm tip, first-use/restart swaps without teaching, reverse signs, support/identity/pose guards, recovery and startup ownership.");
    }
    static void ReverseOptionalAndConfiguration(Type type)
    {
        var f=new ToolSwapFixture(ports:false); var script=SwapStart(type,f); SwapReverseMember(script,f.Couplers[0]);
        SwapFrame(script,"On",false,0); SwapFrame(script); Check(Enabled(script),"Optional absent merge ports blocked reverse-layout manual On.");
        SwapFrame(script,"Off",false,0); SwapFrame(script,"Tool 2;On",false,0);
        for(int i=0;i<160 && SwapPhase(script)!="Idle";i++) SwapFrame(script);
        Check(!Enabled(script) && f.Mutations.Count==0,"Missing optional ports/semicolon On permitted mechanical change or resumed motion."); SwapStationUntouched(f);
        foreach(string fault in new[]{"Missing","WrongType","ArmGrid","Duplicate"})
        {
            var invalid=new ToolSwapFixture(); var ini=new MyIni(); ini.TryParse(invalid.Rig.PB.CustomData);
            if(fault=="Missing") ini.Delete("Tool02","Mount");
            else if(fault=="WrongType") ini.Set("Tool02","Mount",invalid.Markers[1].CustomName);
            else if(fault=="ArmGrid") ini.Set("Tool02","Mount",invalid.Base.CustomName);
            else ini.Set("Tool02","Mount",invalid.Couplers[0].CustomName);
            string text=ini.ToString(); RecordProxy.Of(invalid.Rig.PB).Values["CustomData"]=text;
            var refused=Tests.Create(type,invalid.Rig); SwapFrame(refused,"On",false,0); SwapFrame(refused);
            Check(!Enabled(refused) && invalid.Rig.PB.CustomData==text && invalid.Mutations.Count==0,"Invalid per-tool Mount "+fault+" enabled, rewrote, or mutated reverse setup.");
        }
    }
    static void ReverseFreshSwap(Type type,double angle,bool explicitOnly)
    {
        var f=new ToolSwapFixture(explicitOnly:explicitOnly,destinationAngle:angle);
        Check(f.Rig.Storage=="" && f.ArmGrid.GetCubeBlock(f.ArmTip.Position)==null && f.ArmGrid.CubeExists(f.ArmTip.Position),"Fresh fixture must have no Storage and genuinely hidden occupied arm tip.");
        var script=SwapStart(type,f); SwapReverseMember(script,f.Couplers[0]);
        Check(!f.Rig.Blocks.Any(b=>ReferenceEquals(b,f.ArmTip)),"Arm tip accidentally entered terminal inventory.");
        var saved=new MyIni(); saved.TryParse(((TestHost)script).Storage);
        Check(saved.Get("AutoArm Arm Tip","E"+f.ArmRef.EntityId).ToString().StartsWith(f.ArmTip.EntityId+"|"),"Initial mounted Head1 did not automatically persist arm-reference geometry.");
        SwapFrame(script,"Tool 2",false,0);
        string first=SwapPhase(script); for(int i=0;i<8;i++) SwapFrame(script,timed:true,dt:0);
        Check(SwapPhase(script)==first && f.Mutations.Count==0,"Zero-time updates advanced reverse change or mutated attachments.");
        bool drove=SwapPlant(script,f,"ApproachTop");
        var before=SwapGoal(script,"Attach"); f.ShiftIncoming(new Vector3D(.08,0,0)); var after=SwapGoal(script,"Attach");
        Check(Vector3D.Distance(after.Translation,before.Translation+new Vector3D(.08,0,0))<1e-9,"Moving incoming tool anchor did not carry desired arm-reference target.");
        drove|=SwapPlant(script,f,"Release");
        Check(drove && f.Mutations.Select(m=>(m.Tool,m.Kind)).SequenceEqual(new[]{(0,"Detach"),(1,"Attach")}),"Fresh reverse swap did not drive and detach Source before attaching Destination once.");
        Check(f.Mutations.All(m=>m.Stopped && m.Supported) && f.Mutations.Select(m=>m.Journal).SequenceEqual(new[]{"Detach","Attach"}),"Reverse mutations lacked stopped drives, proven support, or prior journal.");
        Check(f.Couplers[1].Top?.EntityId==f.ArmTip.EntityId && f.ArmTip.Base?.EntityId==f.Couplers[1].EntityId && f.ArmTip.CubeGrid==f.ArmRef.CubeGrid,"Incoming tool base did not acquire actual shared arm tip reciprocally on ARM grid.");
        var tipForward=f.ArmTip.WorldMatrix.Forward; var up=f.ArmTip.WorldMatrix.Up;
        var baseForward=tipForward*Math.Cos(f.Couplers[1].Angle)+Vector3D.Cross(up,tipForward)*Math.Sin(f.Couplers[1].Angle);
        Check(Vector3D.Dot(baseForward,f.Couplers[1].WorldMatrix.Forward)>.999999,"Reverse pose failed retained signed tool-angle roundtrip.");
        Check(f.Heads[0].All(m=>m.Enabled && m.IsConnected) && f.Heads[1].All(m=>!m.Enabled),"Reverse release changed parked source or failed incoming head-side release.");
        for(int i=0;i<8;i++) SwapFrame(script);
        Check(SwapPhase(script)=="Release","Connected tool/stand overlap permitted premature full rediscovery.");
        long oldGrid=f.Markers[1].CubeGrid.EntityId; f.SplitTool(1); SwapPlant(script,f,"Idle");
        Check(!Enabled(script) && !(bool)Get(SwapController(script),"Recovery")! && f.Markers[1].CubeGrid.EntityId!=oldGrid,"Successful reverse swap did not finish healthy OFF after tool-grid split.");
        Check(Get(Get(script,"Topology")!,"Head")==f.Markers[1] && Groups(script).Sum(g=>Members(g).Length)==3,"Reverse incoming full topology/marker endpoint was not restored.");
        SwapReverseMember(script,f.Couplers[1]); SwapFrame(script,"On",false,0); SwapFrame(script);
        Check(Enabled(script),"Completed reverse swap failed fresh explicit manual On."); SwapStationUntouched(f);
    }
    static void ReverseParkRestart(Type type)
    {
        var f=new ToolSwapFixture(); var script=SwapStart(type,f); SwapFrame(script,"Park",false,0);
        bool drove=SwapPlant(script,f,"Idle");
        Check(drove && !Enabled(script) && f.ArmTip.Base==null && f.Couplers[0].Top==null && Get(Get(script,"Topology")!,"Head")==f.ArmRef,"Generic Park did not drive, detach tool base, and finish bare-reference OFF.");
        f.Rig.Storage=((TestHost)script).Storage; int before=f.Mutations.Count;
        var restarted=SwapStart(type,f); SwapFrame(restarted,"Tool 2",false,0); SwapPlant(restarted,f,"Idle");
        Check(!Enabled(restarted) && f.Couplers[1].Top?.EntityId==f.ArmTip.EntityId && f.Mutations.Count==before+1,"Saved bare-arm restart required per-tool learning or failed automatic incoming attachment."); SwapStationUntouched(f);
    }
    static void ReverseWeightRetention(Type type)
    {
        var f=new ToolSwapFixture(); var script=SwapStart(type,f); var ini=new MyIni(); ini.TryParse(f.Rig.PB.CustomData);
        var rows=ini.Get("Config","Actuators").ToString().Split('\n');
        for(int n=1;n<rows.Length;n++) { var cells=rows[n].Split('|'); cells[2]=(n*.23).ToString(System.Globalization.CultureInfo.InvariantCulture); cells[3]=(n*.41).ToString(System.Globalization.CultureInfo.InvariantCulture); rows[n]=string.Join("|",cells); }
        ini.Set("Config","Actuators",string.Join("\n",rows)); RecordProxy.Of(f.Rig.PB).Values["CustomData"]=ini.ToString();
        SwapFrame(script,"Reload",false,0); for(int i=0;i<200 && SwapPhase(script)!="Idle";i++) SwapFrame(script);
        foreach(var g in Groups(script))
        {
            var block=(IMyMechanicalConnectionBlock)Get(Members(g)[0],"B")!; int n=block.EntityId==f.Base.EntityId?1:block.EntityId==f.Piston.EntityId?2:3;
            Check(Math.Abs(Convert.ToDouble(Get(g,"MoveW"))-n*.23)<1e-12 && Math.Abs(Convert.ToDouble(Get(g,"TurnW"))-n*.41)<1e-12,"Reverse Reload lost configured physical actuator preference.");
        }
        SwapFrame(script,"Tool 2",false,0); SwapPlant(script,f,"Idle");
        foreach(var g in Groups(script))
        {
            var block=(IMyMechanicalConnectionBlock)Get(Members(g)[0],"B")!; int n=block.EntityId==f.Base.EntityId?1:block.EntityId==f.Piston.EntityId?2:0;
            if(n>0) Check(Math.Abs(Convert.ToDouble(Get(g,"MoveW"))-n*.23)<1e-12 && Math.Abs(Convert.ToDouble(Get(g,"TurnW"))-n*.41)<1e-12,"Reverse bare/full topology rebuilding lost stable upstream actuator preferences.");
            else Check(Math.Abs(Convert.ToDouble(Get(g,"MoveW"))-1)<1e-12 && Math.Abs(Convert.ToDouble(Get(g,"TurnW"))-1)<1e-12,"New physical incoming actuator inherited another tool's editable preferences.");
        }
    }
    static void RejectHingeToolCouplers(Type type)
    {
        foreach(string subtype in new[]{"LargeHinge","MediumHinge","SmallHinge"})
        {
            var f=new ToolSwapFixture(); foreach(var c in f.Couplers) RecordProxy.Of(c).Values["BlockDefinition"]=ToolSwapFixture.Definition(subtype);
            string original=f.Rig.PB.CustomData; var script=Tests.Create(type,f.Rig);
            SwapFrame(script,"On",false,0); SwapFrame(script,"Tool 2",false,0); SwapFrame(script);
            Check(!Enabled(script) && !f.AnyDrive && f.Mutations.Count==0 && f.Rig.PB.CustomData==original,"Hinge tool coupler must refuse before drive/config or connection mutations.");
        }
    }
    static void NativeReverseGeometry(Type type)
    {
        string[] bases={"LargeStator","SmallStator","LargeAdvancedStator","SmallAdvancedStator","SmallAdvancedStatorSmall"};
        var dummies=new[]{new Vector3D(1.033530949712258e-7,.4208642244338989,1.3662192088759184e-7),new Vector3D(2.17650750755638e-7,.036039892584085464,3.7548051068370114e-7),new Vector3D(1.033530949712258e-7,.19979000091552734,1.2796517978586053e-7),Vector3D.Zero,new Vector3D(2.553320221920785e-8,.047984808683395386,5.927423671892029e-7)};
        var geometry=type.GetNestedType("ToolGeometry",All)!; var info=type.GetNestedType("ToolTopInfo",All)!;
        var capture=info.GetMethod("Capture",All)!; var inverse=geometry.GetMethod("TryTopPose",All)!;
        for(int index=0;index<bases.Length;index++) foreach(bool large in new[]{false,true})
        {
            bool hinge=index>=5;
            string[] heads=hinge?(large?new[]{"LargeHingeHead"}:new[]{"MediumHingeHead","SmallHingeHead"}):(large?new[]{"LargeRotor","LargeAdvancedRotor"}:new[]{"SmallRotor","SmallAdvancedRotor","SmallAdvancedRotorSmall"});
            foreach(string head in heads) foreach(double q in new[]{-.4,.4})
            {
                var f=new ToolSwapFixture(); var c=f.Couplers[1];
                RecordProxy.Of(c).Values["BlockDefinition"]=ToolSwapFixture.Definition(bases[index]); RecordProxy.Of(c).Values["Angle"]=(float)q;
                RecordProxy.Of(f.ArmTip).Values["BlockDefinition"]=ToolSwapFixture.Definition(head);
                RecordProxy.Of(f.ArmGrid).Values["GridSize"]=large?2.5f:.5f; RecordProxy.Of(f.ArmGrid).Values["GridSizeEnum"]=large?VRage.Game.MyCubeSize.Large:VRage.Game.MyCubeSize.Small;
                var tipPose=f.ArmTip.WorldMatrix; tipPose.Translation=f.ArmGrid.GridIntegerToWorld(f.ArmTip.Position); RecordProxy.Of(f.ArmTip).Values["WorldMatrix"]=tipPose;
                var refPose=f.ArmRef.WorldMatrix; refPose.Translation=f.ArmGrid.GridIntegerToWorld(f.ArmRef.Position); RecordProxy.Of(f.ArmRef).Values["WorldMatrix"]=refPose;
                var top=capture.Invoke(null,new object[]{f.ArmRef,f.ArmTip,f.Rig.Blocks})!; var local=(MatrixD)Get(top,"Local")!;
                double lo=hinge?0:index==3?-.02:(index==0 || index==2) && large?-.4:-.11;
                double hi=hinge?0:(index==0 || index==2) && large?.2:.11;
                foreach(double displacement in new[]{lo,0,hi}.Distinct())
                {
                    RecordProxy.Of(c).Values["Displacement"]=(float)displacement;
                    var args=new object?[]{c,top,MatrixD.Identity,""}; bool ok=(bool)inverse.Invoke(null,args)!;
                    Check(ok,"Native inverse refused supported "+bases[index]+"/"+head+" displacement "+displacement+": "+args[3]);
                    var desiredTip=local*(MatrixD)args[2]!;
                    var dummy=hinge?Vector3D.Zero:dummies[index]; double offset=index==1 || index==4?.045:index==3?.11:0;
                    var expected=c.GetPosition()+Vector3D.TransformNormal(dummy+Vector3D.Up*(c.Displacement-offset),c.WorldMatrix);
                    Check(Vector3D.Distance(desiredTip.Translation,expected)<1e-7,"Reverse coupling pivot does not match native stator dummy and displacement.");
                    var forward=desiredTip.Forward*Math.Cos(c.Angle)+Vector3D.Cross(desiredTip.Up,desiredTip.Forward)*Math.Sin(c.Angle);
                    Check(Vector3D.Dot(forward,c.WorldMatrix.Forward)>1-1e-8 && Vector3D.Dot(desiredTip.Up,c.WorldMatrix.Up)>1-1e-8,"Signed retained angle inverse failed native orientation roundtrip.");
                }
                foreach(double bad in new[]{lo-.001,hi+.001,double.NaN})
                {
                    RecordProxy.Of(c).Values["Displacement"]=(float)bad; var args=new object?[]{c,top,MatrixD.Identity,""};
                    Check(!(bool)inverse.Invoke(null,args)!,"Native inverse accepted clamping/nonfinite displacement for "+bases[index]+"/"+head);
                }
                RecordProxy.Of(c).Values["Displacement"]=0f; RecordProxy.Of(c).Values["LowerLimitRad"]=.6f; RecordProxy.Of(c).Values["UpperLimitRad"]=.8f;
                var limits=new object?[]{c,top,MatrixD.Identity,""}; Check(!(bool)inverse.Invoke(null,limits)!,"Native inverse accepted retained angle outside real tool mount limits.");
            }
        }
    }
    static void ReverseWrongPartAndTiming(Type type)
    {
        var f=new ToolSwapFixture(); f.AttachResult=f.WrongArmPart(); var script=SwapStart(type,f);
        SwapFrame(script,"Tool 2",false,0); SwapPlant(script,f,"Attach");
        Check(f.Heads[1].All(m=>m.Enabled),"Reverse Attach request released support before observations."); SwapFrame(script);
        Check(SwapPhase(script)=="Idle" && !Enabled(script) && (bool)Get(SwapController(script),"Recovery")! && f.Heads[1].All(m=>m.Enabled),"Wrong rotor-part identity on correct ARM grid released support.");
        Check(!f.Heads[1].Any(m=>RecordProxy.Of(m).Writes.Any(w=>w.Name=="Enabled" && Equals(w.Value,false))),"Wrong arm-part ID produced a head merge disable write."); SwapStationUntouched(f);
        var pose=new ToolSwapFixture(); var good=SwapStart(type,pose); SwapFrame(good,"Tool 2",false,0); SwapPlant(good,pose,"Attach");
        SwapFrame(good); Check(SwapPhase(good)=="Attach" && pose.Heads[1].All(m=>m.Enabled),"Single correct attachment observation released support.");
        var shifted=pose.ArmRef.WorldMatrix; shifted.Translation+=Vector3D.Up*.02; pose.MoveArm(shifted);
        for(int i=0;i<8;i++) SwapFrame(good);
        Check(SwapPhase(good)=="Attach" && pose.Heads[1].All(m=>m.Enabled),"Correct ID but actual arm pose 2cm off alignment released support.");
        SwapFrame(good,"SwapCancel",false,0); SwapStationUntouched(pose);
    }
    static void ReversePartialAndRecovery(Type type)
    {
        var f=new ToolSwapFixture(); var script=SwapStart(type,f); SwapFrame(script,"Tool 2",false,0); SwapPlant(script,f,"Lock",autoLock:false);
        f.LockSource(1); for(int i=0;i<20;i++) SwapFrame(script);
        Check(SwapPhase(script)=="Lock" && f.Mutations.Count==0,"Partial support detached reversed source stator.");
        SwapFrame(script,"Off",false,0);
        Check(SwapPhase(script)=="Idle" && !Enabled(script) && (bool)Get(SwapController(script),"Recovery")! && f.Heads[0].All(m=>m.Enabled),"Off during support wait failed recovery/support retention.");
        f.Rig.Storage=((TestHost)script).Storage; var restarted=SwapStart(type,f,recovery:true);
        SwapFrame(restarted,"On;GoHome",false,0); SwapFrame(restarted);
        Check(!Enabled(restarted) && f.Mutations.Count==0,"Interrupted reverse journal resumed attachments/manual/Home movement."); SwapStationUntouched(f);
        foreach(string boundary in new[]{"Attach","Release"})
        {
            var x=new ToolSwapFixture(); var active=SwapStart(type,x); SwapFrame(active,"Tool 2",false,0); SwapPlant(active,x,boundary);
            if(boundary=="Release") x.SplitTool(1);
            bool[] enabled=x.Heads[1].Select(m=>m.Enabled).ToArray(); int mutations=x.Mutations.Count;
            SwapFrame(active,"SwapCancel",false,0); x.Rig.Storage=((TestHost)active).Storage;
            var back=SwapStart(type,x,recovery:true); SwapFrame(back,"On",false,0); SwapFrame(back);
            Check(!Enabled(back) && x.Mutations.Count==mutations && x.Heads[1].Select(m=>m.Enabled).SequenceEqual(enabled),"Reverse restart at "+boundary+" resumed mutation or changed supports."); SwapStationUntouched(x);
        }
    }
    static void ReverseRuntimeGuards(Type type)
    {
        var split=new ToolSwapFixture(ports:false); var manual=SwapStart(type,split); SwapFrame(manual,"On",false,0); SwapFrame(manual);
        split.SplitCouplerFromMarker(0); SwapFrame(manual);
        Check(!Enabled(manual) && !split.AnyDrive,"Coupler/marker split kept stale reverse manual topology active.");
        foreach(bool partition in new[]{false,true})
        {
            var x=new ToolSwapFixture(); var active=SwapStart(type,x); SwapFrame(active,"Tool 2",false,0); SwapPlant(active,x,"Lock",autoLock:false);
            x.LockSource();
            if(partition) x.SeparateMarkerAndCouplerFromSupport(0); else x.SplitCouplerFromMarker(0);
            SwapFrame(active);
            Check(SwapPhase(active)=="Idle" && !Enabled(active) && x.Mutations.Count==0 && x.Heads[0].All(m=>m.Enabled),"Support/marker/coupler partition detached an unsupported tool despite still-locked merge pair.");
            SwapStationUntouched(x);
        }
        foreach(bool home in new[]{false,true})
        {
            var f=new ToolSwapFixture(); var owner=f.OtherOwner(); var script=SwapStart(type,f); SwapFrame(script,"Park",false,0); SwapPlant(script,f,"Idle");
            if(home) { SwapFrame(script,"SetHome",false,0); RecordProxy.Of(f.Piston).Values["CurrentPosition"]=6f; SwapFrame(script,"GoHome",false,0); }
            else { SwapFrame(script,"On",false,0); RecordProxy.Of(f.Rig.Blocks.OfType<IMyShipController>().Single()).Values["MoveIndicator"]=new Vector3(0,0,-1); }
            SwapFrame(script); Check(Enabled(script) && f.AnyDrive,"Foreign-owner fixture did not first drive the bare arm.");
            var v=RecordProxy.Of(owner).Values; v["IsAttached"]=true; v["Top"]=f.ArmTip; v["TopGrid"]=f.ArmGrid; RecordProxy.Of(f.ArmTip).Values["Base"]=owner;
            SwapFrame(script);
            Check(!Enabled(script) && !(bool)Get(script,"GoingHome")! && !f.AnyDrive,"Unconfigured tip owner did not stop "+(home?"Home":"manual")+" control.");
            SwapStationUntouched(f);
        }
        var restart=new ToolSwapFixture(); var foreign=restart.OtherOwner(); var parked=SwapStart(type,restart); SwapFrame(parked,"Park",false,0); SwapPlant(parked,restart,"Idle");
        restart.Rig.Storage=((TestHost)parked).Storage;
        var foreignValues=RecordProxy.Of(foreign).Values; foreignValues["IsAttached"]=true; foreignValues["Top"]=restart.ArmTip; foreignValues["TopGrid"]=restart.ArmGrid;
        RecordProxy.Of(restart.ArmTip).Values["Base"]=foreign; RecordProxy.Of(foreign).Writes.Clear(); int before=restart.Mutations.Count;
        var refused=Tests.Create(type,restart.Rig); for(int i=0;i<200 && SwapPhase(refused)!="Idle";i++) SwapFrame(refused);
        SwapFrame(refused,"Tool 2",false,0); for(int i=0;i<160 && SwapPhase(refused)!="Idle";i++) SwapFrame(refused);
        Check(!Enabled(refused) && !restart.AnyDrive && restart.Mutations.Count==before,"Cached bare restart with foreign arm-tip owner authorized pickup/motion.");
        Check(RecordProxy.Of(foreign).Writes.Count==0,"Cached bare restart wrote an unconfigured tip-owning stator.");
        var pilot=new ToolSwapFixture(); var changing=SwapStart(type,pilot); SwapFrame(changing,"Tool 2",false,0); SwapPlant(changing,pilot,"ApproachTop");
        RecordProxy.Of(pilot.Rig.Blocks.OfType<IMyShipController>().Single()).Values["MoveIndicator"]=new Vector3(0,0,-1); SwapFrame(changing);
        Check(SwapPhase(changing)=="Idle" && !Enabled(changing) && !pilot.AnyDrive && pilot.Heads[1].All(m=>m.Enabled),"Raw pilot did not stop reversed automatic pickup while retaining support."); SwapStationUntouched(pilot);
    }
    static void ReverseSetupAndCacheScope(Type type)
    {
        var fresh=new ToolSwapFixture(headless:true); var script=SwapStart(type,fresh); SwapFrame(script,"Tool 2",false,0);
        for(int i=0;i<160 && SwapPhase(script)!="Idle";i++) SwapFrame(script);
        Check(!Enabled(script) && fresh.Mutations.Count==0 && fresh.Rig.Log.Any(s=>s.Contains("Tools.ArmTip",StringComparison.OrdinalIgnoreCase)),"Fresh bare installation without tip frame did not refuse with explicit arm-tip configuration status.");
        var f=new ToolSwapFixture(); var learned=SwapStart(type,f); SwapFrame(learned,"Park",false,0); SwapPlant(learned,f,"Idle"); string storage=((TestHost)learned).Storage;
        foreach(string fault in new[]{"PB","Arm","Marker","Axes"})
        {
            var ini=new MyIni(); ini.TryParse(storage); const string section="AutoArm Arm Tip"; string key="E"+f.ArmRef.EntityId;
            if(fault=="PB") ini.Set(section,"PB",f.Rig.PB.EntityId+1);
            else if(fault=="Arm") ini.Set(section,"Arm","Other arm");
            else if(fault=="Marker") ini.Delete(section,key);
            else { var cells=ini.Get(section,key).ToString().Split('|'); cells[5]="2"; ini.Set(section,key,string.Join("|",cells)); }
            f.Rig.Storage=ini.ToString(); int mutations=f.Mutations.Count; var refused=Tests.Create(type,f.Rig);
            for(int i=0;i<160 && SwapPhase(refused)!="Idle";i++) SwapFrame(refused);
            SwapFrame(refused,"Tool 2",false,0); for(int i=0;i<160 && SwapPhase(refused)!="Idle";i++) SwapFrame(refused);
            Check(SwapPhase(refused)=="Idle" && !Enabled(refused) && !f.AnyDrive && f.Mutations.Count==mutations,"Invalid arm-tip cache "+fault+" authorized automatic movement/attachment.");
        }
    }
    static void ReverseStartupStop(Type type)
    {
        foreach(bool missing in new[]{false,true})
        {
            var f=new ToolSwapFixture(); var other=f.Rig.Rotor("Unrelated ship rotor",f.Rig.Root,f.Rig.Grid()); var original=SwapStart(type,f); f.Rig.Storage=((TestHost)original).Storage;
            var ini=new MyIni(); ini.TryParse(f.Rig.PB.CustomData); if(missing) f.Rig.Blocks.Remove(f.Markers[1]); else ini.Set("Config","HeadSpeed","NaN");
            string bad=ini.ToString(); RecordProxy.Of(f.Rig.PB).Values["CustomData"]=bad;
            RecordProxy.Of(f.Base).Values["TargetVelocityRad"]=.2f; RecordProxy.Of(f.Piston).Values["Velocity"]=.3f; foreach(var c in f.Couplers) RecordProxy.Of(c).Values["TargetVelocityRad"]=-.4f;
            RecordProxy.Of(other).Values["TargetVelocityRad"]=.27f; RecordProxy.Of(other).Writes.Clear(); var refused=Tests.Create(type,f.Rig);
            Check(!Enabled(refused) && !f.AnyDrive,"Rejected reversed startup left owned/configured actuator moving before timed scan.");
            Check(other.TargetVelocityRad==.27f && RecordProxy.Of(other).Writes.Count==0 && f.Rig.PB.CustomData==bad,"Rejected reversed startup wrote unrelated drives or invalid configuration.");
        }
    }
}
