using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

internal static partial class Scenarios
{
    // Import a read-only survey into proxy grids so the actual topology and
    // clearance implementations can consume its geometry. No game is touched.
    internal static void SurveyGeometryCases(Type armType,Type collisionType,string path)
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
            v["EntityId"]=id;v["GridSize"]=float.Parse(p[1][5..],System.Globalization.CultureInfo.InvariantCulture);v["WorldMatrix"]=ParseFrame(string.Join(" | ",p.Skip(3)).Substring(6));
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
        // Native AABBs were not in the original report. Emulate them from the
        // installed MWM assets; this is explicit model evidence, not live API proof.
        foreach(var block in blocks.Values) SetModelAabb(block);
        foreach(var joint in blocks.Values.OfType<IMyMechanicalConnectionBlock>())if(joint.Top!=null)SetModelAabb(joint.Top);
        var collisionPB=(IMyProgrammableBlock)blocks.Values.Single(b=>b.CustomName=="Collision PB");
        var collisionRig=new Rig(rig,"Collision import host");rig.Blocks.Remove(collisionRig.PB);
        // The proxy host exposes a settable Me, so use the exact imported PB.
        var collision=Tests.Create(collisionType,collisionRig);((TestHost)collision).Me=collisionPB;
        // Bind an IGC endpoint under the imported ID while preserving the shared construct.
        RecordProxy.Of(collisionRig.PB).Values["EntityId"]=collisionPB.EntityId;RecordProxy.Of(collisionRig.PB).Values["CustomName"]="Collision PB";
        RecordProxy.Of(collisionRig.PB).Values["CustomData"]=collisionPB.CustomData;RecordProxy.Of(collisionRig.PB).Values["CubeGrid"]=collisionPB.CubeGrid;
        var bus=new ModuleBus();bus.Bind(rig);bus.Bind(collisionRig);((TestHost)collision).IGC=collisionRig.IGC;
        var arm=Tests.Create(armType,rig);HostReady(arm,rig,"Arm 1");HostReady(arm,rig,"Arm 2");
        // Reload after substituting Me to ensure the imported config is authoritative.
        HostFrame(collision,collisionRig,"Reload");
        for(int i=0;i<150;i++){HostFrame(arm,rig);HostFrame(collision,collisionRig);}
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
        var benchRig=new Rig(rig,"Arm Bench");RecordProxy.Of(benchRig.PB).Values["CubeGrid"]=rig.PB.CubeGrid;bus.Bind(benchRig);
        var config=new MyIni();config.TryParse(rig.PB.CustomData);config.Set("global","Peers",config.Get("global","Peers").ToString()+"\nArm Bench | Plan");RecordProxy.Of(rig.PB).Values["CustomData"]=config.ToString();
        HostReady(arm,rig,"Arm 1");HostReady(arm,rig,"Arm 2");
        for(int i=0;i<100;i++){HostFrame(arm,rig);HostFrame(collision,collisionRig);}
        var bench=Tests.Create(Tests.Script(File.ReadAllText(Path.Combine(Tests.Workspace,"AutoArm_Bench.txt"))),benchRig);
        foreach(string command in new[]{"Smoke","Run"})
        {
            HostFrame(bench,benchRig,command);
            for(int i=0;i<22000&&(int)Get(bench,"Phase")! !=0;i++) {Kinematics();HostFrame(arm,rig);HostFrame(collision,collisionRig);HostFrame(bench,benchRig);}
            Console.WriteLine("Imported ideal kinematic "+command+": "+benchRig.Log.Last());
            if(!benchRig.Log.Last().StartsWith("PASS:"))
            {
                foreach(var message in bus.Sent.Where(s=>s.Source==collisionPB.EntityId).TakeLast(2))Console.WriteLine(message.Data);
            }
            if(command=="Smoke")Check(benchRig.Log.Last().StartsWith("PASS:"),"Imported smoke path failed: "+benchRig.Log.Last()+" / "+string.Join(" | ",rig.Log.TakeLast(3)));
            else {
                bool passed=benchRig.Log.Last().StartsWith("PASS:");
                Check(passed||benchRig.Log.Last().StartsWith("ABORT:"),"Full run neither completed nor aborted within its bound.");
                if(!passed) {
                    Check(Get(Core(arm,"Arm 1"),"LocalPath")==null&&Get(Core(arm,"Arm 2"),"LocalPath")==null,"Full-run abort left a path active.");
                    Console.WriteLine("KNOWN LIMIT: full shared-plane Run remains conservative/uncompleted in the ideal plant; this is NOT a passing full motion test.");
                }
            }
        }
        Console.WriteLine("Imported survey geometry consumed by actual topology/collision scripts; stationary proxy frames, not physics.");
    }
}
