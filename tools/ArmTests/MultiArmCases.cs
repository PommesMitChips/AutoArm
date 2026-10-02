using System.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

internal static partial class Scenarios
{
    internal static void RunHosts(Type armType,Type toolType) { StartupSavedDriveCases(armType); ConfigurationViewCases(armType,toolType); MultiArmCases(armType,toolType); PilotPairCases(armType); MultiToolPilotCases(armType,toolType); Console.WriteLine($"Full host integration: PASS ({Tests.Assertions} assertions)."); }
    static object Core(object host,string name)=>((IDictionary)Get(host,"Arms")!)[name]!;
    static void HostFrame(object host,Rig rig,string command="",double dt=1d/60,UpdateType? kind=null)
    { InvokeFrame(host,rig,command,kind??(command.Length==0?UpdateType.Update1:UpdateType.Terminal),command.Length==0?dt:0); }
    static void HostReady(object host,Rig rig,string name)
    {
        HostFrame(host,rig,"On("+name+")"); for(int i=0;i<100&&!Enabled(Core(host,name));i++) HostFrame(host,rig);
        Check(Enabled(Core(host,name)),"Host On failed for "+name+": "+string.Join(" | ",rig.Log.TakeLast(2)));
    }
    static Rig MultiFixture(out IMyPistonBase a,out IMyPistonBase b,out IMyShipController pilot,bool paired=false,bool wrongKind=false)
    {
        var rig=Fixtures.Serial(out _,out a,out _,out pilot); var first=rig.Grid(); var last=rig.Grid();
        rig.Rotor("Arm 2 - Base - Rotor",rig.Root,first,new Vector3D(20,0,0));
        b=rig.Piston("Arm 2 piston",first,last,new Vector3D(20,0,-5),Vector3D.Forward);
        if(wrongKind) { rig.Blocks.Remove(b); rig.Rotor("Arm 2 wrist",first,last,new Vector3D(20,0,-5)); }
        if(paired) rig.Piston("Arm 2 parallel piston",first,last,new Vector3D(22,0,-5),Vector3D.Forward);
        rig.Block<IMyShipDrill>("Arm 2 - Head - Drill",last,new Vector3D(20,0,-18));
        var data=new MyIni(); data.Set("global","Format",7); data.Set("global","HeadSpeed",.7);
        data.Set("global","Actuators","Stage | Kind | Translation | Orientation\n01 | Rotor x1 | 0.4 | 2\n02 | Piston x1 | 1.7 | 0.2");
        data.Set("Arm 1","ToolSwapPB",""); data.Set("Arm 2","HeadSpeed",.35); RecordProxy.Of(rig.PB).Values["CustomData"]=data.ToString(); return rig;
    }
    static void MultiArmCases(Type armType,Type toolType)
    {
        var rig=MultiFixture(out var a,out var b,out var pilot); var host=Tests.Create(armType,rig);
        for(int i=0;i<30;i++) HostFrame(host,rig); HostReady(host,rig,"Arm 1"); HostReady(host,rig,"Arm 2");
        var first=Core(host,"Arm 1"); var second=Core(host,"Arm 2");
        Check((double)Get(first,"MoveMps")! ==.7&&(double)Get(second,"MoveMps")! ==.35,"Per-arm override did not inherit global settings.");
        Check(Groups(first)[0]!=Groups(second)[0]&&(double)Get(Groups(first)[0],"MoveW")! ==.4&&(double)Get(Groups(second)[1],"MoveW")! ==1.7,"Global stage weights were not independently applied.");
        Check((string)Get(first,"Shape")! ==(string)Get(second,"Shape")!,"Different physical lengths invalidated equivalent joint kinds/layout.");
        RecordProxy.Of(pilot).Values["MoveIndicator"]=new Vector3(0,0,-1); for(int i=0;i<10;i++) HostFrame(host,rig);
        Check(a.Velocity!=0&&b.Velocity==0,"Cockpit input reached more than the selected arm.");
        HostFrame(host,rig,"Select Arm 2"); for(int i=0;i<10;i++) HostFrame(host,rig);
        Check(a.Velocity==0&&b.Velocity!=0,"Selection did not transfer live input and hold the previous arm."); RecordProxy.Of(pilot).Values["MoveIndicator"]=Vector3.Zero;
        HostFrame(host,rig,"Speed(0.8,Arm 1)"); Check((double)Get(first,"MoveMps")! ==.8&&(double)Get(second,"MoveMps")! ==.35,"Explicit trailing arm argument changed another context.");
        HostFrame(host,rig,"Speed 0.45"); Check((double)Get(second,"MoveMps")! ==.45,"Omitted target did not use selected arm.");
        HostFrame(host,rig,"Stop(Arm 1)"); Check(!Enabled(first)&&Enabled(second),"Scoped Stop affected another arm.");
        foreach(string bad in new[]{"Stop(Unknown)","MoveTo(1,2,3,Unknown)","Tool(Head 2,Unknown)","Stop, Unknown"}) { HostFrame(host,rig,bad); Check(Enabled(second),"Unknown target fell back to selected arm: "+bad); }
        HostFrame(host,rig,"SetHome, Arm 1"); HostFrame(host,rig,"SetHome, Arm 2"); HostReady(host,rig,"Arm 2");
        Check(((IDictionary)Get(first,"Home")!).Count==2&&((IDictionary)Get(second,"Home")!).Count==2,"Home was not isolated per arm.");
        rig.Storage=((TestHost)host).Storage; var cold=Tests.Create(armType,rig); Check((string)Get(cold,"Selected")! =="Arm 2"&&!Enabled(Core(cold,"Arm 1"))&&!Enabled(Core(cold,"Arm 2")),"Cold restart lost selection or resumed motion."); NoVelocity(rig);
        Check(((IDictionary)Get(Core(cold,"Arm 1"),"Home")!).Keys.Cast<long>().Intersect(((IDictionary)Get(Core(cold,"Arm 2"),"Home")!).Keys.Cast<long>()).Count()==0,"Saved arm Home identities overlapped.");
        var state=new MyIni(); Check(state.TryParse(((TestHost)cold).Storage)&&state.ContainsSection("State Arm 1")&&state.ContainsSection("State Arm 2"),"Arm state was not persisted separately.");
        foreach(bool differentKind in new[]{false,true})
        {
            var invalid=MultiFixture(out _,out _,out _,paired:!differentKind,wrongKind:differentKind); var scoped=Tests.Create(armType,invalid); for(int i=0;i<20;i++) HostFrame(scoped,invalid); HostReady(scoped,invalid,"Arm 1"); HostFrame(scoped,invalid,"On(Arm 2)"); for(int i=0;i<30;i++) HostFrame(scoped,invalid);
            Check(Enabled(Core(scoped,"Arm 1"))&&!Enabled(Core(scoped,"Arm 2")),"Nonconforming shared layout silently enabled or stopped a valid neighbor.");
        }
        HostStartupInput(armType); MultiHostInput(armType); HostServices(armType); MultiToolHost(armType,toolType); MultiToolHost(armType,toolType,true); MultiToolHost(armType,toolType,true,true); MultiToolHost(armType,toolType,true,false,true);
        File.WriteAllText(Path.Combine(Tests.Workspace,"examples/CustomData.ini"),rig.PB.CustomData);
        Console.WriteLine("Multi-arm hosts: selected-only input, scoped/implicit commands, global inheritance and overrides, joint-kind/parallel rejection, independent Home/restart, cross-PB input selection and shared ToolSwap routing.");
    }
    static void HostStartupInput(Type type)
    {
        var rig=MultiFixture(out _,out _,out var cockpit); string correct=rig.PB.CustomData;
        var invalid=new MyIni(); invalid.Set("global","Format",0); RecordProxy.Of(rig.PB).Values["CustomData"]=invalid.ToString();
        var host=Tests.Create(type,rig); Check((long)Get(host,"InputOwner")! ==0,"Invalid startup fixture unexpectedly selected an input owner.");
        RecordProxy.Of(rig.PB).Values["CustomData"]=correct; HostReady(host,rig,"Arm 1");
        RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-1);
        for(int i=0;i<12;i++) HostFrame(host,rig);
        Check((bool)Get(Core(host,"Arm 1"),"PilotSelected")!&&((Vector3D)Get(Core(host,"Arm 1"),"LastPilotLinear")!).Length()>.01,
            "On after repairing startup configuration enabled motion without a cockpit input owner.");
        HostFrame(host,rig,"StopAll"); NoVelocity(rig);
    }
    static void MultiHostInput(Type type)
    {
        var rig=MultiFixture(out _,out _,out _); var peerRig=new Rig(rig,"Second arm PB");
        var original=new MyIni(); original.TryParse(rig.PB.CustomData); original.DeleteSection("Arm 2"); RecordProxy.Of(rig.PB).Values["CustomData"]=original.ToString();
        var peerData=new MyIni(); peerData.Set("global","Format",7); peerData.Set("Arm 2","ToolSwapPB",""); RecordProxy.Of(peerRig.PB).Values["CustomData"]=peerData.ToString();
        var bus=new ModuleBus(); bus.Bind(rig); bus.Bind(peerRig); var a=Tests.Create(type,rig); var b=Tests.Create(type,peerRig);
        for(int i=0;i<20;i++) { HostFrame(a,rig); HostFrame(b,peerRig); }
        Check(!(bool)Get(Core(a,"Arm 1"),"PilotSelected")!&&(bool)Get(Core(b,"Arm 2"),"PilotSelected")!,"Two Arm PBs retained direct cockpit ownership.");
        Check(rig.Log.Last().Contains("Cockpit input inactive here"),"An unselected host falsely advertised active cockpit input.");
        HostFrame(a,rig,"On(Arm 1)"); for(int i=0;i<20;i++) { HostFrame(a,rig); HostFrame(b,peerRig); }
        Check(!(bool)Get(Core(a,"Arm 1"),"PilotSelected")!&&(bool)Get(Core(b,"Arm 2"),"PilotSelected")!,"On stole another Arm PB's explicit input ownership.");
        HostFrame(a,rig,"Select Arm 1"); for(int i=0;i<10;i++) { HostFrame(a,rig); HostFrame(b,peerRig); }
        Check((bool)Get(Core(a,"Arm 1"),"PilotSelected")!&&!(bool)Get(Core(b,"Arm 2"),"PilotSelected")!,"Construct-wide selection did not transfer input ownership.");
    }
    internal static void MultiToolPilotCases(Type armType, Type toolType)
    {
        foreach(bool split in new[]{false,true}) foreach(string channel in new[]{"keyboard","mouse","roll"})
            MultiToolHost(armType,toolType,split:split,pilotChannel:channel);
        Console.WriteLine("ToolSwap pilot isolation: paired neutral owner, moving neighbor, keyboard/mouse/roll and owner cancellation across shared/split Arm PBs PASS.");
    }
    static void MultiToolHost(Type armType,Type toolType,bool automatic=false,bool split=false,bool manual=false,string pilotChannel="")
    {
        var f=new ToolSwapFixture(headless:automatic); f.NoNamedArmReference(); var g=new ToolSwapFixture(headless:automatic); g.NoNamedArmReference();
        // Pair seats after setup when exercising independent operators.
        foreach(var cockpit in g.Rig.Blocks.OfType<IMyShipController>())RecordProxy.Of(cockpit).Values["IsUnderControl"]=false;
        if(automatic) { f.LockSource(); g.LockSource(); }
        var grids=new HashSet<object>(ReferenceEqualityComparer.Instance); var blocks=new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach(var terminal in g.Rig.Blocks)
        {
            RecordProxy.Of(terminal).Values["CustomName"]=terminal.CustomName.Replace("Arm 1","Arm 2");
            foreach(var block in new VRage.Game.ModAPI.Ingame.IMyCubeBlock?[]{terminal,(terminal as IMyMechanicalConnectionBlock)?.Top}) if(block!=null)
            {
                if(blocks.Add(block)) RecordProxy.Of(block).Values["EntityId"]=block.EntityId+100000;
                if(grids.Add(block.CubeGrid)) RecordProxy.Of(block.CubeGrid).Values["EntityId"]=block.CubeGrid.EntityId+100000;
            }
        }
        var fData=new MyIni(); fData.TryParse(f.Rig.PB.CustomData); var gData=new MyIni(); gData.TryParse(g.Rig.PB.CustomData.Replace("Arm 1","Arm 2"));
        for(int i=0;i<f.Couplers.Length;i++)
        {
            RecordProxy.Of(f.Couplers[i]).Values["CustomName"]="Arm 1 - Head "+(i+1)+" - Mount";
            RecordProxy.Of(g.Couplers[i]).Values["CustomName"]="Arm 2 - Head "+(i+1)+" - Mount";
            fData.Set("Tool"+(i+1).ToString("00"),"Mount",f.Couplers[i].CustomName); gData.Set("Tool"+(i+1).ToString("00"),"Mount",g.Couplers[i].CustomName);
        }
        NameFixture(f,"Arm 1",fData); NameFixture(g,"Arm 2",gData);
        RecordProxy.Of(f.Rig.PB).Values["CustomName"]="Arm PB"; var toolRig=new Rig(f.Rig,"ToolSwap PB");
        var inventory=f.Rig.Blocks.Concat(g.Rig.Blocks.Where(x=>split||x!=g.Rig.PB)).ToList(); f.Rig.ExternalInventory=toolRig.ExternalInventory=inventory; g.Rig.ExternalInventory=inventory;
        var config=new MyIni(); config.Set("global","Format",7); config.Set("global","ToolSwapPB","ToolSwap PB"); config.Set("Arm 1","HeadSpeed",.3); config.Set("Arm 2","HeadSpeed",.4); if(pilotChannel.Length>0)config.Set("global","ReadMouse",true); RecordProxy.Of(f.Rig.PB).Values["CustomData"]=config.ToString();
        var toolData=new MyIni(); toolData.Set("global","Format",2); toolData.Set("global","Link.ArmPB","Arm PB");
        foreach(var tuple in new[]{(Name:"Arm 1",Data:fData),(Name:"Arm 2",Data:gData)})
        {
            var keys=new List<MyIniKey>(); tuple.Data.GetKeys(keys);
            foreach(var key in keys) if(key.Section!="AutoArm"&&key.Section!="Link") toolData.Set(tuple.Name,key.Section+"."+key.Name,tuple.Data.Get(key).ToString());
        }
        RecordProxy.Of(toolRig.PB).Values["CustomData"]=toolData.ToString();
        if(automatic)
        {
            foreach(var tuple in new[]{(Name:"Arm 1",Fixture:f),(Name:"Arm 2",Fixture:g)})
            {
                for(int i=0;i<2;i++)
                {
                    RecordProxy.Of(tuple.Fixture.Markers[i]).Values["CustomName"]=tuple.Name+" - Head "+(i+1)+" - "+(i==0?"Drill":"Welder");
                    RecordProxy.Of(tuple.Fixture.Couplers[i]).Values["CustomName"]="Advanced Rotor";
                    foreach(var merge in tuple.Fixture.Heads[i].Concat(tuple.Fixture.Stands[i]))
                    { RecordProxy.Of(merge).Values["CustomName"]="Merge Block"; RecordProxy.Of(merge).Values["BlockDefinition"]=ToolSwapFixture.Definition("LargeShipMergeBlock"); }
                }
            }
            RecordProxy.Of(f.Rig.PB).Values["CustomName"]="Programmable Block"; RecordProxy.Of(toolRig.PB).Values["CustomName"]="Programmable Block";
            RecordProxy.Of(f.Rig.PB).Values["CustomData"]=""; RecordProxy.Of(toolRig.PB).Values["CustomData"]="";
            RecordProxy.Of(f.Markers[1]).Values["CustomData"]="Original opaque tool notes";
        }
        if(split)
        {
            var owner=new MyIni(); owner.Set("global","Format",7); owner.Set("Arm 1","ToolSwapPB","@"+toolRig.PB.EntityId); if(pilotChannel.Length>0)owner.Set("global","ReadMouse",true); RecordProxy.Of(f.Rig.PB).Values["CustomData"]=owner.ToString();
            var other=new MyIni(); other.Set("global","Format",7); other.Set("Arm 2","ToolSwapPB","@"+toolRig.PB.EntityId); if(pilotChannel.Length>0)other.Set("global","ReadMouse",true); RecordProxy.Of(g.Rig.PB).Values["CustomData"]=other.ToString();
            if(automatic)RecordProxy.Of(toolRig.PB).Values["CustomData"]="";
            else
            {
                toolData.Set("Arm 1","Link.ArmPB","@"+f.Rig.PB.EntityId);toolData.Set("Arm 2","Link.ArmPB","@"+g.Rig.PB.EntityId);
                RecordProxy.Of(toolRig.PB).Values["CustomData"]=toolData.ToString();
            }
        }
        var bus=new ModuleBus{ArmId=f.Rig.PB.EntityId,ToolId=toolRig.PB.EntityId}; bus.Bind(f.Rig); bus.Bind(toolRig);
        if(split) bus.Bind(g.Rig);
        var arm=Tests.Create(armType,f.Rig); var tools=Tests.Create(toolType,toolRig); f.Host=g.Host=(TestHost)tools;
        object otherArm=split?Tests.Create(armType,g.Rig):arm;
        object Owner(string name)=>name=="Arm 2"?otherArm:arm;
        Rig OwnerRig(string name)=>split&&name=="Arm 2"?g.Rig:f.Rig;
        void Tick()
        {
            RecordProxy.Actor="Arm"; HostFrame(arm,f.Rig); if(split) HostFrame(otherArm,g.Rig); RecordProxy.Actor="ToolSwap"; HostFrame(tools,toolRig); RecordProxy.Actor="";
        }
        HostFrame(arm,f.Rig,"On(Arm 1)"); HostFrame(Owner("Arm 2"),OwnerRig("Arm 2"),"On(Arm 2)"); for(int i=0;i<600&&(!Enabled(Core(arm,"Arm 1"))||!Enabled(Core(otherArm,"Arm 2")));i++) Tick();
        Check(Enabled(Core(arm,"Arm 1"))&&Enabled(Core(otherArm,"Arm 2")),"Shared ToolSwap startup failed: "+string.Join(" | ",f.Rig.Log.TakeLast(2))+" / "+string.Join(" | ",toolRig.Log.TakeLast(2)));
        if(automatic)
        {
            var armSettings=new MyIni(); Check(armSettings.TryParse(f.Rig.PB.CustomData)&&armSettings.ContainsKey("global","ReadKeyboard")&&
                armSettings.Get("global","PositionIntegralGain").ToDouble()==.5,"Headless ToolSwap startup did not publish arm defaults.");
            var toolSettings=new MyIni(); Check(toolSettings.TryParse(toolRig.PB.CustomData)&&toolSettings.ContainsKey("global","Tools.MoveSpeed")&&
                !toolSettings.ContainsKey("Arm 1","Tools.MoveSpeed"),"Tool defaults shadowed global settings with generated arm overrides.");
            toolSettings.Set("global","Tools.MoveSpeed",.03); toolSettings.Set("Arm 1","Tools.MoveSpeed",.02);
            RecordProxy.Of(toolRig.PB).Values["CustomData"]=toolSettings.ToString();
            armSettings.Set("global","PositionIntegralGain",.4); armSettings.Set("Arm 1","PositionIntegralGain",.3);
            RecordProxy.Of(f.Rig.PB).Values["CustomData"]=armSettings.ToString();
            HostFrame(arm,f.Rig,"On(Arm 1)"); HostFrame(Owner("Arm 2"),OwnerRig("Arm 2"),"On(Arm 2)");
            for(int i=0;i<600&&(!Enabled(Core(arm,"Arm 1"))||!Enabled(Core(otherArm,"Arm 2")));i++) Tick();
            Check((double)Get(Core(arm,"Arm 1"),"PositionKi")! ==.3&&(!split?(double)Get(Core(arm,"Arm 2"),"PositionKi")! ==.4:true),"Generated arm defaults broke local/global priority.");
            Check((double)Get(SwapController(Core(tools,"Arm 1")),"Move")! ==.02&&
                (double)Get(SwapController(Core(tools,"Arm 2")),"Move")! ==.03,"Tool local/global settings did not override generated defaults.");
            foreach(var fixture in new[]{f,g}) foreach(var marker in fixture.Markers)
            {
                var data=new MyIni(); Check(data.TryParse(marker.CustomData)&&data.Get("AutoArm Tool","Mount").ToInt64()!=0&&data.Get("AutoArm Tool","HeadMerges").ToString().Length>0&&data.Get("AutoArm Tool","ParkPose").ToString().Length>0,"Quick-start discovery did not persist hardware/park pose on its named head.");
            }
            Check(f.Markers[1].CustomData.Contains("Original opaque tool notes"),"Head discovery overwrote unrelated Custom Data.");
            Check(!toolRig.PB.CustomData.Contains("Tool01.Mount")&&!toolRig.PB.CustomData.Contains("Tools.Heads"),"Automatic setup filled PB configuration with hardware-name requirements.");
        }
        var firstSeat=f.Rig.Blocks.OfType<IMyShipController>().First(); var secondSeat=g.Rig.Blocks.OfType<IMyShipController>().First();
        void Input(IMyShipController seat,bool active)
        {
            var values=RecordProxy.Of(seat).Values;
            values["MoveIndicator"]=active&&pilotChannel=="keyboard"?new Vector3(0,0,-1):Vector3.Zero;
            values["RotationIndicator"]=active&&pilotChannel=="mouse"?new Vector2(1,0):Vector2.Zero;
            values["RollIndicator"]=active&&pilotChannel=="roll"?1f:0f;
        }
        if(pilotChannel.Length>0)
        {
            HostFrame(arm,f.Rig,"Select(HRZ,Arm 1)");
            RecordProxy.Of(firstSeat).Values["IsUnderControl"]=false; RecordProxy.Of(secondSeat).Values["IsUnderControl"]=true;
            HostFrame(Owner("Arm 2"),OwnerRig("Arm 2"),"Select(VRT,Arm 2)"); RecordProxy.Of(firstSeat).Values["IsUnderControl"]=true;
            for(int i=0;i<12;i++)Tick();
            Check((long)Get(Core(arm,"Arm 1"),"PilotSeatId")! ==firstSeat.EntityId&&(long)Get(Core(otherArm,"Arm 2"),"PilotSeatId")! ==secondSeat.EntityId,"ToolSwap two-player pairing failed.");
        }
        foreach(var target in new[]{(Name:"Arm 1",Fixture:f),(Name:"Arm 2",Fixture:g)})
        {
            if(pilotChannel.Length>0) Input(target.Name=="Arm 1"?secondSeat:firstSeat,true);
            int chosen=automatic&&target.Name=="Arm 1"?1:2;
            HostFrame(Owner(target.Name),OwnerRig(target.Name),"Tool("+chosen+","+target.Name+")"); string previous=""; int phaseTicks=0;
            for(int i=0;i<7000;i++)
            {
                var core=Core(tools,target.Name); string phase=SwapPhase(core); phaseTicks=phase==previous?phaseTicks+1:0;
                if(i>30&&phase=="Idle"&&Enabled(Core(Owner(target.Name),target.Name))) break;
                string? field=phase=="ApproachDock"?"Approach":phase=="Dock"?"Dock":phase=="Retreat"?"Retreat":phase=="ApproachTop"?"TopApproach":phase=="AlignTop"?"Attach":null;
                int source=Get(SwapController(core),"Source") is object s?Array.IndexOf(target.Fixture.Markers,(IMyTerminalBlock)Get(s,"Marker")!):0;
                if(field!=null&&phaseTicks>8) { var pose=SwapMotionGoal(core,field); if(phase=="ApproachDock"||phase=="Dock") target.Fixture.MoveSource(pose,source); else target.Fixture.MoveArm(pose); }
                else if(phase=="Lock")
                {
                    target.Fixture.LockSource(2,source);
                    if(phaseTicks<4)SwapFixturePointer(target.Fixture,source,true);
                    else if(phaseTicks==4)SwapFixturePointer(target.Fixture,source,false);
                }
                else if(phase=="Release")
                {
                    int destination=Array.IndexOf(target.Fixture.Markers,(IMyTerminalBlock)Get(Get(SwapController(core),"Destination")!,"Marker")!);
                    target.Fixture.SplitTool(destination);
                    if(phaseTicks<4)SwapFixturePointer(target.Fixture,destination,true);
                    else if(phaseTicks==4)SwapFixturePointer(target.Fixture,destination,false);
                }
                if(phase=="ApproachDock"||phase=="Dock")target.Fixture.CaptureSource(source);
                Tick(); previous=phase;
                if(pilotChannel.Length>0)Check(!((string)Get(SwapController(core),"Last")!).Contains("Pilot canceled")&&!((bool)Get(SwapController(core),"Recovery")!),"Another cockpit canceled "+target.Name+" swap via "+pilotChannel+" input.");
            }
            Check(Enabled(Core(Owner(target.Name),target.Name))&&target.Fixture.Couplers[chosen-1].Top==target.Fixture.ArmTip,"Shared ToolSwap failed to mount/resume "+target.Name+": "+string.Join(" | ",toolRig.Log.TakeLast(3)));
            string neighbor=target.Name=="Arm 1"?"Arm 2":"Arm 1"; Check(Enabled(Core(Owner(neighbor),neighbor)),"Scoped tool change stopped the other hosted arm.");
            if(pilotChannel.Length>0){Input(firstSeat,false);Input(secondSeat,false);}
            if(manual&&target.Name=="Arm 1")
            {
                target.Fixture.LockSource(source:chosen-1); target.Fixture.Couplers[chosen-1].Detach();
                for(int i=0;i<400;i++) Tick();
                Check(Enabled(Core(arm,"Arm 1"))&&((VRage.Game.ModAPI.Ingame.IMyCubeBlock)Get(Get(Core(arm,"Arm 1"),"Topology")!,"End")!).CubeGrid==f.ArmGrid,
                    "Actual multi-arm host did not rediscover and resume a manually detached bare arm.");
                Check(Enabled(Core(arm,"Arm 2"))&&g.ArmTip.Base==null,"Bare recovery disturbed another arm or resumed the old pickup.");
            }
            if(!split&&target.Name=="Arm 1")
            {
                var cockpit=f.Rig.Blocks.OfType<IMyShipController>().First();
                RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-1);
                for(int i=0;i<12;i++) Tick();
                Check((bool)Get(Core(arm,"Arm 1"),"PilotSelected")!&&((Vector3D)Get(Core(arm,"Arm 1"),"LastPilotLinear")!).Length()>.01,
                    "Tool pickup resumed ON without restoring live selected-arm cockpit input.");
                RecordProxy.Of(cockpit).Values["MoveIndicator"]=Vector3.Zero;
                HostFrame(arm,f.Rig,"On(Arm 1)"); for(int i=0;i<600&&!Enabled(Core(arm,"Arm 1"));i++) Tick();
                RecordProxy.Of(cockpit).Values["MoveIndicator"]=new Vector3(0,0,-1);
                for(int i=0;i<12;i++) Tick();
                Check(Enabled(Core(arm,"Arm 1"))&&((Vector3D)Get(Core(arm,"Arm 1"),"LastPilotLinear")!).Length()>.01,
                    "Repeated On after tool pickup lost cockpit input.");
                RecordProxy.Of(cockpit).Values["MoveIndicator"]=Vector3.Zero;
            }
        }
        if(pilotChannel.Length>0)
        {
            HostFrame(arm,f.Rig,"Park(Arm 1)");
            for(int i=0;i<100&&!(bool)Get(SwapController(Core(tools,"Arm 1")),"Automatic")!;i++)Tick();
            Check((bool)Get(SwapController(Core(tools,"Arm 1")),"Automatic")!,"Owner cancellation setup did not acquire a swap.");
            Input(firstSeat,true);for(int i=0;i<20;i++)Tick();
            Check(!(bool)Get(Get(Core(arm,"Arm 1"),"Tools")!,"Busy")!&&SwapPhase(Core(tools,"Arm 1"))=="Idle"&&!Enabled(Core(arm,"Arm 1")),"Paired operator failed to cancel its own swap via "+pilotChannel+".");
            Check(Enabled(Core(otherArm,"Arm 2")),"Owner cancellation stopped its neighbor.");Input(firstSeat,false);NoVelocity(f.Rig);
        }
        Check(inventory.All(x=>!RecordProxy.Of(x).ActorWrites.Any(w=>w.Actor=="ToolSwap"&&w.Name is "Velocity" or "TargetVelocityRad")),"Multi-arm ToolSwap wrote joint velocities.");
        if(automatic)
        {
            // Head configuration survives replacement/restart of the ToolSwap
            // script even when its own PB Storage is completely removed.
            HostFrame(arm,f.Rig,"StopAll"); if(split) HostFrame(otherArm,g.Rig,"StopAll"); toolRig.Storage=""; tools=Tests.Create(toolType,toolRig); f.Host=g.Host=(TestHost)tools;
            HostFrame(arm,f.Rig,"On(Arm 1)"); HostFrame(Owner("Arm 2"),OwnerRig("Arm 2"),"On(Arm 2)"); for(int i=0;i<600&&(!Enabled(Core(arm,"Arm 1"))||!Enabled(Core(otherArm,"Arm 2")));i++) Tick();
            Check(Enabled(Core(arm,"Arm 1"))&&Enabled(Core(otherArm,"Arm 2")),"Durable mounted tool hardware required teaching after ToolSwap PB Storage reset.");
            if(!split)
            {
                var corrupt=new MyIni(); corrupt.TryParse(f.Markers[0].CustomData); corrupt.Set("AutoArm Tool","Format",2); string rejected=corrupt.ToString(); RecordProxy.Of(f.Markers[0]).Values["CustomData"]=rejected;
                HostFrame(arm,f.Rig,"On(Arm 1)"); for(int i=0;i<80;i++) Tick();
                Check(!Enabled(Core(arm,"Arm 1"))&&Enabled(Core(otherArm,"Arm 2"))&&f.Markers[0].CustomData==rejected,"Unsupported head format mutated data or stopped another arm.");
            }
        }
        HostFrame(arm,f.Rig,"StopAll"); if(split) HostFrame(otherArm,g.Rig,"StopAll"); Tick(); NoVelocity(f.Rig); NoVelocity(g.Rig);
    }
    static void NameFixture(ToolSwapFixture fixture,string arm,MyIni data)
    {
        var names=new Dictionary<string,string>();
        foreach(var block in fixture.Rig.Blocks) if(!block.CustomName.StartsWith(arm+" - ",StringComparison.Ordinal))
        { string old=block.CustomName; string next=arm+" - "+old; names[old]=next; RecordProxy.Of(block).Values["CustomName"]=next; }
        var keys=new List<MyIniKey>(); data.GetKeys(keys);
        foreach(var key in keys)
        {
            string value=data.Get(key).ToString(); var lines=value.Split('\n');
            for(int i=0;i<lines.Length;i++) if(names.TryGetValue(lines[i].Trim(),out var name)) lines[i]=name;
            data.Set(key.Section,key.Name,string.Join("\n",lines));
        }
    }
    static void HostServices(Type type)
    {
        var rig=MultiFixture(out _,out _,out var pilot); var helper=new Rig(rig,"Planner"); var bus=new ModuleBus(); bus.Bind(rig); bus.Bind(helper);
        var data=new MyIni(); data.TryParse(rig.PB.CustomData); data.Set("global","Peers","Planner | Plan"); RecordProxy.Of(rig.PB).Values["CustomData"]=data.ToString();
        var host=Tests.Create(type,rig); HostReady(host,rig,"Arm 1"); HostReady(host,rig,"Arm 2");
        var a=new ServicePeer(helper,bus,host,rig){Protocol=5}; var b=new ServicePeer(helper,bus,host,rig){Protocol=5,ArmName="Arm 2"}; a.Send("HELLO"); b.Send("HELLO");
        var target=rig.Blocks.Single(x=>x.CustomName=="Arm 2 - Head - Drill").WorldMatrix; target.Translation+=Vector3D.Forward*.1;
        var path=b.Packet("PATH"); path.Set("Link","Waypoints",PCRow(target)); b.Send(path);
        Check(Get(Core(host,"Arm 2"),"LocalPath")!=null&&Get(Core(host,"Arm 1"),"LocalPath")==null,"Shared planner packet crossed arm context.");
        RecordProxy.Of(pilot).Values["MoveIndicator"]=new Vector3(0,0,-1); for(int i=0;i<5;i++) HostFrame(host,rig);
        Check(Get(Core(host,"Arm 2"),"LocalPath")!=null,"Pilot input canceled the nonselected arm's planner path.");
        HostFrame(host,rig,"Stop(Arm 1)"); b.Send("STATUS"); Check(b.Last.Get("Link","Owner").ToInt64()==helper.PB.EntityId,"Stopping one arm revoked another arm's movement session.");
        b.Send("STOP"); Check(Get(Core(host,"Arm 2"),"LocalPath")==null&&!Enabled(Core(host,"Arm 2")),"Planner failed to cancel its own scoped path.");
    }
}
