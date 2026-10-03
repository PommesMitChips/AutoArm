using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;
using System.Reflection;

internal static partial class Scenarios
{
    // Import a read-only survey into proxy grids so the actual topology and
    // clearance implementations can consume its geometry. No game is touched.
    internal static void SurveyGeometryCases(Type armType,Type collisionType,string path,bool profile=false,bool encounters=false,string? selfMode=null,bool armProfile=false,string? posture=null,Type? plannerType=null)
    {
        string[] lines=File.ReadAllLines(path);
        Check(lines.Any(l=>l.StartsWith("RESULT STABLE ENDPOINTS")),"Survey endpoints were not stable.");
        var rig=new Rig();rig.Blocks.Clear();
        var grids=new Dictionary<long,IMyCubeGrid>();
        MatrixD ParseFrame(string text)=>PCFrame(text.Replace("|"," "));
        Vector3I ParseCell(string text){var v=text.Trim().Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();return new(v[0],v[1],v[2]);}
        foreach(var line in lines.Where(l=>l.StartsWith("GRID ")&&!l.StartsWith("GRID END")))
        {
            var p=line.Split('|').Select(x=>x.Trim()).ToArray();long id=long.Parse(p[0][5..]);var g=rig.Grid();grids[id]=g;
            var cells=new HashSet<Vector3I>();
            foreach(var row in lines.Where(l=>l.StartsWith("CELLS "+id+" |")))
            {var values=row.Split('|')[1].Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();for(int x=values[0];x<=values[1];x++)cells.Add(new(x,values[2],values[3]));}
            GridCells(g,cells,Vector3D.Zero);var v=RecordProxy.Of(g).Values;
            v["EntityId"]=id;v["Closed"]=false;v["GridSize"]=float.Parse(p[1][5..],System.Globalization.CultureInfo.InvariantCulture);v["WorldMatrix"]=ParseFrame(string.Join(" | ",p.Skip(3)).Substring(6));
        }
        var configs=new Dictionary<long,string>();
        for(int i=0;i<lines.Length;i++)if(lines[i].StartsWith("CUSTOM DATA ")&&lines[i].EndsWith(" BEGIN"))
        {long id=long.Parse(lines[i].Split(' ')[2]);var text=new List<string>();while(++i<lines.Length&&lines[i]!="CUSTOM DATA "+id+" END")text.Add(lines[i]);configs[id]=string.Join("\n",text);}
        var blocks=new Dictionary<long,IMyTerminalBlock>();
        long ownerId=long.Parse(lines.Single(l=>l.StartsWith("OWNER ")).Split(' ')[1]);
        foreach(var line in lines.Where(l=>l.StartsWith("BLOCK ")))
        {
            var p=line.Split('|').Select(x=>x.Trim()).ToArray();long id=long.Parse(p[0][6..]),gid=long.Parse(p[3][5..]);
            var frame=ParseFrame(string.Join(" | ",p.Skip(6)).Substring(6));string name=p[1];IMyTerminalBlock b;
            if(id==ownerId){b=rig.PB;rig.Blocks.Add(b);}
            else if(p[2].Contains("ProgrammableBlock"))b=rig.Block<IMyProgrammableBlock>(name,grids[gid]);
            else if(p[2].Contains("Stator"))b=rig.Block<IMyMotorStator>(name,grids[gid]);
            else if(p[2].Contains("PistonBase"))b=rig.Block<IMyPistonBase>(name,grids[gid]);
            else if(p[2].Contains("Drill"))b=rig.Block<IMyShipDrill>(name,grids[gid]);
            else b=rig.Block<IMyTerminalBlock>(name,grids[gid]);
            var proxy=RecordProxy.Of(b);var v=proxy.Values;v["EntityId"]=id;v["CustomName"]=name;v["CubeGrid"]=grids[gid];v["WorldMatrix"]=frame;v["Position"]=ParseCell(p[4][5..]);
            string[] bounds=p[5][7..].Split(':');v["Min"]=ParseCell(bounds[0]);v["Max"]=ParseCell(bounds[1]);
            v["CustomData"]=configs.GetValueOrDefault(id,"");v["BlockDefinition"]=ToolSwapFixture.Definition(p[2].Split('/')[1]);
            proxy.Call=(m,a)=>m.Name=="GetPosition"?((MatrixD)v["WorldMatrix"]!).Translation:m.Name is "IsSameConstructAs" or "IsSameGridAs"?true:m.ReturnType.IsValueType?Activator.CreateInstance(m.ReturnType):null;
            blocks[id]=b;
        }
        foreach(var line in lines.Where(l=>l.StartsWith("JOINT ")&&!l.StartsWith("JOINT END")))
        {
            var p=line.Split('|').Select(x=>x.Trim()).ToArray();long id=long.Parse(p[0][6..]);var ids=p[1].Split('/');long gid=long.Parse(ids[1]),topId=long.Parse(ids[2]);
            var joint=(IMyMechanicalConnectionBlock)blocks[id];IMyCubeBlock top=joint is IMyMotorStator?RecordProxy.Make<IMyMotorRotor>():RecordProxy.Make<IMyPistonTop>();
            var topRow=lines.Single(l=>l.StartsWith("TOP "+topId+" |" )).Split('|').Select(x=>x.Trim()).ToArray();var tv=RecordProxy.Of(top).Values;
            tv["EntityId"]=topId;tv["CubeGrid"]=grids[gid];tv["Position"]=ParseCell(topRow[2][5..]);tv["WorldMatrix"]=ParseFrame(string.Join(" | ",topRow.Skip(3)).Substring(6));
            tv["Min"]=tv["Max"]=tv["Position"];tv["BlockDefinition"]=ToolSwapFixture.Definition(joint is IMyMotorStator?(blocks[id].BlockDefinition.SubtypeName.Contains("Hinge")?"LargeHingeHead":"LargeAdvancedRotor"):(blocks[id].BlockDefinition.SubtypeName.Contains("Reskin")?"LargePistonTopReskin":"LargePistonTop"));
            RecordProxy.Of(top).Call=(m,a)=>m.Name=="GetPosition"?((MatrixD)tv["WorldMatrix"]!).Translation:m.ReturnType.IsValueType?Activator.CreateInstance(m.ReturnType):null;
            var v=RecordProxy.Of(joint).Values;v["Top"]=top;v["TopGrid"]=grids[gid];v["IsAttached"]=bool.Parse(ids[3]);v["PendingAttachment"]=bool.Parse(ids[4]);
            float q=float.Parse(p[2][2..],System.Globalization.CultureInfo.InvariantCulture);
            if(joint is IMyMotorStator)
            {v["Angle"]=q;var limits=lines.Single(l=>l.StartsWith("ROTARY "+id+" |")).Split('|')[1].Trim()[7..].Split(' ');v["LowerLimitRad"]=float.Parse(limits[0],System.Globalization.CultureInfo.InvariantCulture);v["UpperLimitRad"]=float.Parse(limits[1],System.Globalization.CultureInfo.InvariantCulture);}
            else{v["CurrentPosition"]=q;v["MinLimit"]=0f;v["MaxLimit"]=10f;v["MaxVelocity"]=5f;}
        }
        if(posture!=null) {
            // Retain the original occupied cells and block-local geometry, then
            // apply the newly reported coordinates through ideal rigid joints.
            // This reconstructs posture; it does not reproduce measured flex.
            var coordinates=System.Text.Json.JsonSerializer.Deserialize<Dictionary<long,double>>(File.ReadAllText(posture))!;
            var links=blocks.Values.OfType<IMyMechanicalConnectionBlock>().ToArray();
            Check(coordinates.Count==links.Length&&links.All(j=>coordinates.ContainsKey(j.EntityId)),"Posture must cover each imported joint exactly.");
            var original=grids.ToDictionary(g=>g.Key,g=>g.Value.WorldMatrix);
            var localBlocks=blocks.Values.ToDictionary(b=>b.EntityId,b=>b.WorldMatrix*MatrixD.Invert(original[b.CubeGrid.EntityId]));
            var tops=links.ToDictionary(j=>j.EntityId,j=>j.Top.WorldMatrix*MatrixD.Invert(original[j.TopGrid.EntityId]));
            var pending=links.ToList();var done=new HashSet<long>{rig.PB.CubeGrid.EntityId};
            while(pending.Count>0) {
                var j=pending.First(k=>done.Contains(k.CubeGrid.EntityId));pending.Remove(j);
                var frame=localBlocks[j.EntityId]*j.CubeGrid.WorldMatrix;
                var child=original[j.TopGrid.EntityId]*MatrixD.Invert(original[j.CubeGrid.EntityId])*j.CubeGrid.WorldMatrix;
                double old=j is IMyMotorStator r?r.Angle:((IMyPistonBase)j).CurrentPosition,delta=coordinates[j.EntityId]-old;
                if(j is IMyMotorStator) {var rotate=MatrixD.CreateFromAxisAngle(-frame.Up,delta);var position=frame.Translation+Vector3D.TransformNormal(child.Translation-frame.Translation,rotate);child=child.GetOrientation()*rotate;child.Translation=position;}
                else child.Translation+=frame.Up*delta;
                RecordProxy.Of(j).Values[j is IMyMotorStator?"Angle":"CurrentPosition"]=(float)coordinates[j.EntityId];
                RecordProxy.Of(j.TopGrid).Values["WorldMatrix"]=child;done.Add(j.TopGrid.EntityId);
            }
            foreach(var b in blocks.Values)RecordProxy.Of(b).Values["WorldMatrix"]=localBlocks[b.EntityId]*b.CubeGrid.WorldMatrix;
            foreach(var j in links)RecordProxy.Of(j.Top).Values["WorldMatrix"]=tops[j.EntityId]*j.TopGrid.WorldMatrix;
            Console.WriteLine("POSTURE reconstructed from reported joint coordinates, original occupancy and ideal rigid transforms.");
        }
        // Native AABBs were not in the original report. Emulate them from the
        // installed MWM assets; this is explicit model evidence, not live API proof.
        foreach(var block in blocks.Values) SetModelAabb(block);
        foreach(var joint in blocks.Values.OfType<IMyMechanicalConnectionBlock>())if(joint.Top!=null)SetModelAabb(joint.Top);
        var collisionPB=(IMyProgrammableBlock)blocks.Values.Single(b=>b.CustomName=="Collision PB");
        if(selfMode!=null) {
            var prototypeConfig=new MyIni();prototypeConfig.TryParse(collisionPB.CustomData);prototypeConfig.Set("Collision","SelfPairs",selfMode);
            RecordProxy.Of(collisionPB).Values["CustomData"]=prototypeConfig.ToString();
        }
        var collisionRig=new Rig(rig,"Collision import host");rig.Blocks.Remove(collisionRig.PB);
        object? counter=null;int peak=0;var costs=new List<int>();
        var injector=profile||armProfile?Assembly.LoadFrom(Path.Combine(Tests.GameBin,"VRage.Library.dll")).GetType("VRage.Library.Compiler.IlInjector",true):null;
        object CreateCollision()
        {
            if(!profile)return Tests.Create(collisionType,collisionRig);
            counter=injector!.GetMethod("BeginRunBlock")!.Invoke(null,new object[]{50000,10000,false})!;
            try{return Tests.Create(collisionType,collisionRig);}finally{((IDisposable)counter).Dispose();counter=null;}
        }
        void CollisionFrame(object host,string command="")
        {
            if(!profile){HostFrame(host,collisionRig,command);return;}
            counter=injector!.GetMethod("BeginRunBlock")!.Invoke(null,new object[]{50000,10000,false})!;
            try{HostFrame(host,collisionRig,command);}
            finally{int cost=(int)counter.GetType().GetProperty("InstructionCount")!.GetValue(counter)!;peak=Math.Max(peak,cost);costs.Add(cost);((IDisposable)counter).Dispose();counter=null;}
        }
        if(profile)
        {
            var rp=RecordProxy.Of(collisionRig.Runtime);rp.Values.Remove("CurrentInstructionCount");
            rp.Call=(m,args)=>m.Name=="get_CurrentInstructionCount"?(counter==null?0:(int)counter.GetType().GetProperty("InstructionCount")!.GetValue(counter)!):m.ReturnType.IsValueType?Activator.CreateInstance(m.ReturnType):null;
        }
        // The proxy host exposes a settable Me, so use the exact imported PB.
        var collision=CreateCollision();((TestHost)collision).Me=collisionPB;
        // Bind an IGC endpoint under the imported ID while preserving the shared construct.
        RecordProxy.Of(collisionRig.PB).Values["EntityId"]=collisionPB.EntityId;RecordProxy.Of(collisionRig.PB).Values["CustomName"]="Collision PB";
        RecordProxy.Of(collisionRig.PB).Values["CustomData"]=collisionPB.CustomData;RecordProxy.Of(collisionRig.PB).Values["CubeGrid"]=collisionPB.CubeGrid;
        var bus=new ModuleBus();bus.Bind(rig);bus.Bind(collisionRig);((TestHost)collision).IGC=collisionRig.IGC;
        if(armProfile) {
            var runtime=RecordProxy.Of(rig.Runtime);runtime.Values.Remove("CurrentInstructionCount");
            runtime.Call=(m,a)=>m.Name=="get_CurrentInstructionCount"?(counter==null?0:(int)counter.GetType().GetProperty("InstructionCount")!.GetValue(counter)!):m.ReturnType.IsValueType?Activator.CreateInstance(m.ReturnType):null;
        }
        object arm;
        if(armProfile) {counter=injector!.GetMethod("BeginRunBlock")!.Invoke(null,new object[]{50000,10000,false})!;try{arm=Tests.Create(armType,rig);}finally{((IDisposable)counter).Dispose();counter=null;}}
        else arm=Tests.Create(armType,rig);
        void ArmFrame(string command="") {
            if(!armProfile){HostFrame(arm,rig,command);return;}
            counter=injector!.GetMethod("BeginRunBlock")!.Invoke(null,new object[]{50000,10000,false})!;
            try{HostFrame(arm,rig,command);}finally{int cost=(int)counter.GetType().GetProperty("InstructionCount")!.GetValue(counter)!;peak=Math.Max(peak,cost);costs.Add(cost);((IDisposable)counter).Dispose();counter=null;}
        }
        void ArmReady(string name) {ArmFrame("On("+name+")");for(int i=0;i<100&&!Enabled(Core(arm,name));i++)ArmFrame();Check(Enabled(Core(arm,name)),"Counted On failed for "+name);}
        ArmReady("Arm 1");ArmReady("Arm 2");
        // Reload after substituting Me to ensure the imported config is authoritative.
        CollisionFrame(collision,"Reload");
        for(int i=0;i<150;i++){ArmFrame();CollisionFrame(collision);}
        if(posture!=null) {CollisionFrame(collision,"Info");Console.WriteLine(collisionRig.Log.Last());}
        if(profile)Console.WriteLine("Stationary counted runs: peak="+peak+" / 50000; last="+collisionRig.Log.Last());
        if(selfMode!=null) {
            var tables=(System.Collections.IDictionary)Get(collision,"SelfTables")!;
            foreach(System.Collections.DictionaryEntry entry in tables)Console.WriteLine("Self-pair table "+entry.Key+": "+((System.Collections.IDictionary)Get(entry.Value!,"Proven")!).Count+" / "+((System.Collections.IList)Get(entry.Value!,"Pairs")!).Count+" proven; processed "+Get(entry.Value!,"Cursor"));
        }
        foreach(string name in new[]{"Arm 1","Arm 2"})
        {
            var core=Core(arm,name);Check(Enabled(core),"Imported arm stopped: "+name);
            var safety=Get(Get(core,"Services")!,"Safety")!;var ini=new MyIni();
            var receiver=PackedProgramChecks.Unwrap(safety);receiver.GetType().GetMethod(PackedProgramChecks.Name(receiver.GetType(),"WriteStatus"),All)!.Invoke(receiver,new object[]{ini});
            Console.WriteLine(name+" imported groups="+Groups(core).Length+" | "+ini.ToString().Replace('\n',' '));
            Check(ini.Get("Link","SafetyReady").ToBoolean()&&!ini.Get("Link","SafetyHold").ToBoolean(),"Imported vanilla interfaces still held "+name+": "+ini);
        }
        var joints=blocks.Values.OfType<IMyMechanicalConnectionBlock>().ToArray();
        var local=blocks.Values.ToDictionary(b=>b.EntityId,b=>b.WorldMatrix*MatrixD.Invert(b.CubeGrid.WorldMatrix));
        var topLocal=joints.ToDictionary(j=>j.Top.EntityId,j=>j.Top.WorldMatrix*MatrixD.Invert(j.TopGrid.WorldMatrix));
        var relative=joints.ToDictionary(j=>j.EntityId,j=>j.TopGrid.WorldMatrix*MatrixD.Invert(j.CubeGrid.WorldMatrix));
        double Q(IMyMechanicalConnectionBlock j)=>j is IMyMotorStator r?r.Angle:((IMyPistonBase)j).CurrentPosition;
        var initial=joints.ToDictionary(j=>j.EntityId,Q);
        void Kinematics()
        {
            foreach(var j in joints)
            {
                var v=RecordProxy.Of(j).Values;
                if(j is IMyMotorStator r)v["Angle"]=(float)(r.Angle+r.TargetVelocityRad/60);
                else {var p=(IMyPistonBase)j;v["CurrentPosition"]=(float)Math.Clamp(p.CurrentPosition+p.Velocity/60,p.MinLimit,p.MaxLimit);}
            }
            var pending=joints.ToList();var done=new HashSet<long>{rig.PB.CubeGrid.EntityId};
            while(pending.Count>0)
            {
                var j=pending.First(n=>done.Contains(n.CubeGrid.EntityId));pending.Remove(j);
                var baseFrame=local[j.EntityId]*j.CubeGrid.WorldMatrix;RecordProxy.Of(j).Values["WorldMatrix"]=baseFrame;
                var child=relative[j.EntityId]*j.CubeGrid.WorldMatrix;double delta=Q(j)-initial[j.EntityId];
                if(j is IMyMotorStator)
                {var rotation=MatrixD.CreateFromAxisAngle(-baseFrame.Up,delta);var p=baseFrame.Translation+Vector3D.TransformNormal(child.Translation-baseFrame.Translation,rotation);child=child.GetOrientation()*rotation;child.Translation=p;}
                else child.Translation+=baseFrame.Up*delta;
                RecordProxy.Of(j.TopGrid).Values["WorldMatrix"]=child;done.Add(j.TopGrid.EntityId);
            }
            foreach(var b in blocks.Values){RecordProxy.Of(b).Values["WorldMatrix"]=local[b.EntityId]*b.CubeGrid.WorldMatrix;SetModelAabb(b);}
            foreach(var j in joints){RecordProxy.Of(j.Top).Values["WorldMatrix"]=topLocal[j.Top.EntityId]*j.TopGrid.WorldMatrix;SetModelAabb(j.Top);}
        }
        if(plannerType!=null){
            var plannerRig=new Rig(rig,"Planner PB");var pv=RecordProxy.Of(plannerRig.PB).Values;pv["CubeGrid"]=rig.PB.CubeGrid;pv["Position"]=pv["Min"]=pv["Max"]=new Vector3I(-30,0,0);bus.Bind(plannerRig);
            var pc=new MyIni();pc.Set("Planner","Format",1);pc.Set("Planner","ArmPB","@"+rig.PB.EntityId);pc.Set("Planner","Arm","Arm 1");pc.Set("Planner","NodeLimit",1024);pv["CustomData"]=pc.ToString();
            var ac=new MyIni();ac.TryParse(rig.PB.CustomData);ac.Set("global","Peers",ac.Get("global","Peers").ToString()+"\nPlanner PB | Plan");RecordProxy.Of(rig.PB).Values["CustomData"]=ac.ToString();ArmReady("Arm 1");ArmReady("Arm 2");
            for(int i=0;i<150;i++){ArmFrame();CollisionFrame(collision);}var planner=JointCreate(plannerType,plannerRig);
            foreach(string name in new[]{"Arm 1","Arm 2"}){
                // This stage deliberately plans one arm against a stationary peer.
                // A second ON controller can change its posture during idle hold.
                ArmFrame("Stop("+(name=="Arm 1"?"Arm 2":"Arm 1")+")");Kinematics();ArmReady(name);for(int i=0;i<150;i++){ArmFrame();CollisionFrame(collision);}
                JointFrame(planner,plannerRig,"Select "+name);var end=(MatrixD)Get(Get(Core(arm,name),"Topology")!,"EndPose")!;var baseBlock=rig.Blocks.Single(b=>b.CustomName==name+" - Base");
                foreach(var goal in new[]{end.Translation+baseBlock.WorldMatrix.Up*.1,end.Translation}){var relativeGoal=goal-baseBlock.GetPosition();
                string command="PreviewTo "+NumberForTest(Vector3D.Dot(relativeGoal,baseBlock.WorldMatrix.Forward))+" "+NumberForTest(Vector3D.Dot(relativeGoal,baseBlock.WorldMatrix.Left))+" "+NumberForTest(Vector3D.Dot(relativeGoal,baseBlock.WorldMatrix.Up));JointFrame(planner,plannerRig,command);
                int ticks=0;for(;ticks<3000&&(string)Get(planner,"Phase")! !="Idle"&&(string)Get(planner,"Phase")! !="Preview";ticks++){ArmFrame();CollisionFrame(collision);JointFrame(planner,plannerRig);}
                Console.WriteLine("SURVEY joint planning "+name+" | ticks "+ticks+" | "+plannerRig.Log.Last());
                Check((string)Get(planner,"Phase")! =="Preview","Survey planner failed its smoke preview.");
                int paths=bus.Sent.Count(m=>m.Source==plannerRig.PB.EntityId&&m.Data.Contains("Operation=PATH"));
                JointFrame(planner,plannerRig,command.Replace("PreviewTo","MoveTo"));
                for(ticks=0;ticks<15000&&(string)Get(planner,"Phase")! !="Complete"&&(string)Get(planner,"Phase")! !="Idle";ticks++){Kinematics();ArmFrame();CollisionFrame(collision);JointFrame(planner,plannerRig);}
                Console.WriteLine("SURVEY joint execution "+name+" | ticks "+ticks+" | "+plannerRig.Log.Last());
                if((string)Get(planner,"Phase")! !="Complete"){
                    var core=Core(arm,name);var clients=(System.Collections.IDictionary)Get(Get(core,"Services")!,"Peers")!;var client=clients[plannerRig.PB.EntityId]!;var failed=Get(client,"Path");
                    if(failed!=null){var targets=(double[][])Get(failed,"Joints")!;int step=(int)Get(failed,"Completed")!;int column=0;foreach(var group in Groups(core))foreach(var member in ((System.Collections.IEnumerable)Get(group,"M")!).Cast<object>()){var block=(IMyMechanicalConnectionBlock)Get(member,"B")!;Console.WriteLine("GUIDE DEBUG "+block.EntityId+" q="+Q(block)+" target="+targets[step][column++]+" rate="+(block is IMyMotorStator rr?rr.TargetVelocityRad:((IMyPistonBase)block).Velocity));}}
                    Console.WriteLine("GUIDE DEBUG head error "+Get(core,"LastPositionError")+" / "+Get(core,"LastOrientationErrorDeg"));
                }
                Check((string)Get(planner,"Phase")! =="Complete","Survey joint execution failed: "+plannerRig.Log.Last()+" / "+rig.Log.Last());
                Check(bus.Sent.Count(m=>m.Source==plannerRig.PB.EntityId&&m.Data.Contains("Operation=PATH"))>paths,"Survey execution published no checked route.");
                var reached=(MatrixD)Get(Get(Core(arm,name),"Topology")!,"EndPose")!;Check(Vector3D.Distance(reached.Translation,goal)<.005,"Survey route missed actual head goal.");
                }
                Console.WriteLine("SURVEY round trip "+name+": PASS; live Collision, actual commanded joint-rate plant, 5 mm head tolerance.");
            }
            ArmFrame("StopAll");NoVelocity(rig);return;
        }
        var benchRig=new Rig(rig,"Arm Bench");RecordProxy.Of(benchRig.PB).Values["CubeGrid"]=rig.PB.CubeGrid;bus.Bind(benchRig);
        var config=new MyIni();config.TryParse(rig.PB.CustomData);config.Set("global","Peers",config.Get("global","Peers").ToString()+"\nArm Bench | Plan");RecordProxy.Of(rig.PB).Values["CustomData"]=config.ToString();
        ArmReady("Arm 1");ArmReady("Arm 2");
        for(int i=0;i<100;i++){ArmFrame();CollisionFrame(collision);}
        var bench=Tests.Create(Tests.Script(File.ReadAllText(Path.Combine(Tests.Workspace,"AutoArm_Bench_Source.txt"))),benchRig);
        if(posture!=null) {
            HostFrame(bench,benchRig,"Rebase");
            for(int i=0;i<900&&(int)Get(bench,"Phase")! !=0;i++) {Kinematics();ArmFrame();CollisionFrame(collision);HostFrame(bench,benchRig);}
            Check(benchRig.Log.Last().StartsWith("REBASE"),"Posture replay did not rebase: "+benchRig.Log.Last());
        }
        foreach(string command in encounters?new[]{"Cross","Bases"}:new[]{"Smoke","Run"})
        {
            if(encounters) {
                ArmFrame("StopAll");
                foreach(var j in joints)RecordProxy.Of(j).Values[j is IMyMotorStator?"Angle":"CurrentPosition"]=(float)initial[j.EntityId];
                Kinematics();ArmReady("Arm 1");ArmReady("Arm 2");CollisionFrame(collision,"Reload");
                for(int i=0;i<150;i++){ArmFrame();CollisionFrame(collision);}
                benchRig.Storage=((TestHost)bench).Storage;
                bench=Tests.Create(bench.GetType(),benchRig);
            }
            HostFrame(bench,benchRig,command);
            for(int i=0;i<(encounters?216100:22000)&&(int)Get(bench,"Phase")! !=0;i++) {Kinematics();ArmFrame();CollisionFrame(collision);HostFrame(bench,benchRig);}
            Console.WriteLine("Imported ideal kinematic "+command+": "+benchRig.Log.Last());
            if(posture!=null) {var report=new MyIni();report.TryParse(benchRig.PB.CustomData);File.WriteAllText(Path.Combine(Tests.Workspace,".build/posture-"+command.ToLowerInvariant()+".txt"),report.EndContent);CollisionFrame(collision,"Info");Console.WriteLine(collisionRig.Log.Last());}
            if(!benchRig.Log.Last().StartsWith("PASS:"))
            {
                foreach(var message in bus.Sent.Where(s=>s.Source==collisionPB.EntityId).TakeLast(2))Console.WriteLine(message.Data);
            }
            if(command=="Smoke"&&posture==null)Check(benchRig.Log.Last().StartsWith("PASS:"),"Imported smoke path failed: "+benchRig.Log.Last()+" / "+string.Join(" | ",rig.Log.TakeLast(3)));
            else {
                bool passed=benchRig.Log.Last().StartsWith("PASS:");
                Check(passed||benchRig.Log.Last().StartsWith("ABORT:"),"Full run neither completed nor aborted within its bound.");
                if(!passed) {
                    Check(Get(Core(arm,"Arm 1"),"LocalPath")==null&&Get(Core(arm,"Arm 2"),"LocalPath")==null,"Full-run abort left a path active.");
                    Console.WriteLine("KNOWN LIMIT: "+command+" remains conservative/uncompleted in the ideal plant; this is NOT a passing avoidance/completion test.");
                }
            }
            if(encounters) {
                File.WriteAllText(Path.Combine(Tests.Workspace,".build/encounter-"+command.ToLowerInvariant()+".txt"),(string)Get(bench,"SavedReport")!);
                Console.WriteLine("Encounter report recorded for "+command+"; model results do not establish live physics clearance.");
            }
        }
        Console.WriteLine("Imported survey geometry consumed by actual topology/collision scripts; stationary proxy frames, not physics.");
        int peakRows=0,capacityHolds=0;
        foreach(var message in bus.Sent.Where(s=>s.Source==collisionPB.EntityId)) {
            var guide=new MyIni();guide.TryParse(message.Data);if(guide.Get("Link","Operation").ToString()!="GUIDE")continue;
            peakRows=Math.Max(peakRows,guide.Get("Link","Rows").ToString().Split('\n').Count(s=>s.Length>0));
            if(guide.Get("Link","Hold").ToBoolean()&&guide.Get("Link","Reason").ToString().Contains("constraint capacity"))capacityHolds++;
        }
        Console.WriteLine("Replay constraint rows: peak "+peakRows+"; capacity holds "+capacityHolds);
        int reallocations=0,repairPasses=0,repairBudgets=0;
        foreach(var item in bus.Sent.Where(s=>s.Source==rig.PB.EntityId)) {var state=new MyIni();state.TryParse(item.Data);if(state.Get("Link","Operation").ToString()!="STATE")continue;if(state.Get("Link","SafetyReallocated").ToBoolean())reallocations++;repairPasses=Math.Max(repairPasses,state.Get("Link","SafetyRepairPasses").ToInt32());if(state.Get("Link","SafetyRepairBudget").ToBoolean())repairBudgets++;}
        Console.WriteLine("Repair telemetry: "+reallocations+" reallocated states; peak passes "+repairPasses+"; budget states "+repairBudgets);
        if(profile||armProfile)Console.WriteLine("Installed resource-monitoring rewrite ("+(armProfile?"Arm":"Collision")+"): "+costs.Count+" runs, peak "+peak+" / 50000 instructions; median "+costs.Order().ElementAt(costs.Count/2)+".");
    }
}
