using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

// Each tool carries a visible base. A shared hidden rotor part stays on the arm.
internal sealed class ToolSwapFixture
{
    internal readonly Rig Rig = new();
    internal readonly IMyMotorStator Base;
    internal readonly IMyPistonBase Piston;
    internal readonly IMyTerminalBlock ArmRef;
    internal readonly IMyMotorRotor ArmTip;
    internal readonly IMyCubeGrid ArmGrid;
    internal readonly IMyTerminalBlock[] Markers = new IMyTerminalBlock[2];
    internal readonly IMyMotorStator[] Couplers = new IMyMotorStator[2];
    internal readonly IMyShipMergeBlock[][] Heads = new IMyShipMergeBlock[2][], Stands = new IMyShipMergeBlock[2][];
    internal readonly IMyShipMergeBlock UnrelatedMerge;
    internal readonly List<(int Tool,string Kind,string Journal,bool Stopped,bool Supported)> Mutations = new();
    internal TestHost? Host;
    internal IMyMotorRotor? AttachResult;
    internal bool AutoAttach = true, AutoDetach = true;
    readonly Dictionary<IMyCubeGrid,Dictionary<Vector3I,IMySlimBlock>> Cells = new();
    internal ToolSwapFixture(bool ports=true,bool explicitOnly=false,bool headless=false,double destinationAngle=0)
    {
        var shoulder=Rig.Grid(); ArmGrid=Grid(new Vector3D(0,.4208642244338989,-10));
        Base=Rig.Rotor("Arm 1 - Base - Rotor",Rig.Root,shoulder);
        Piston=Rig.Piston("Reach piston",shoulder,ArmGrid,new Vector3D(0,0,-2),Vector3D.Forward);
        ArmRef=Rig.Block<IMyShipDrill>("Arm reference",ArmGrid);
        Put(ArmRef,ArmGrid,new Vector3I(1,0,0));
        RecordProxy.Of(ArmRef).Values["WorldMatrix"]=MatrixD.CreateWorld(ArmGrid.GridIntegerToWorld(ArmRef.Position),Vector3D.Left,Vector3D.Up);
        ArmTip=RecordProxy.Make<IMyMotorRotor>(); RecordProxy.Of(ArmTip).Values["EntityId"]=8000L;
        RecordProxy.Of(ArmTip).Values["Closed"]=false; RecordProxy.Of(ArmTip).Values["BlockDefinition"]=Definition("LargeRotor");
        Put(ArmTip,ArmGrid,new Vector3I(0,-1,0)); Rig.Cockpit();
        for(int n=0;n<2;n++)
        {
            var grid=Grid(new Vector3D(n==0?0:5,0,-10));
            Markers[n]=Rig.Block<IMyShipDrill>(n==0?"Arm 1 - Head - Tool A":"Tool B",grid); Put(Markers[n],grid,Vector3I.Zero);
            Couplers[n]=Rig.Rotor(n==0?"Tool A coupler":"Tool B coupler",grid,ArmGrid); Put(Couplers[n],grid,new Vector3I(0,-1,0));
            var v=RecordProxy.Of(Couplers[n]).Values;
            v["BlockDefinition"]=Definition("LargeStator"); v["Angle"]=n==1?(float)destinationAngle:0f;
            v["Top"]=n==0 && !headless?ArmTip:null; v["TopGrid"]=n==0 && !headless?ArmGrid:null;
            v["IsAttached"]=n==0 && !headless; v["PendingAttachment"]=false;
            Armor(grid,new Vector3I(0,1,0));
            Heads[n]=new IMyShipMergeBlock[ports?2:0]; Stands[n]=new IMyShipMergeBlock[ports?2:0];
            var standGrid=n==0?Grid(new Vector3D(.3,0,-10)):grid;
            for(int j=0;j<Heads[n].Length;j++)
            {
                Heads[n][j]=Rig.Block<IMyShipMergeBlock>($"Tool {n+1} head merge {j+1}",grid);
                Stands[n][j]=Rig.Block<IMyShipMergeBlock>($"Arm 1 - Tool {(n==0?"A":"B")} - StandMerge {j+1}",standGrid);
                Put(Heads[n][j],grid,new Vector3I(1,j,0)); Put(Stands[n][j],standGrid,new Vector3I(2,j,0),true);
                RecordProxy.Of(Heads[n][j]).Values["Enabled"]=n==1;
                RecordProxy.Of(Heads[n][j]).Values["IsConnected"]=n==1; RecordProxy.Of(Stands[n][j]).Values["IsConnected"]=n==1;
            }
            int index=n; var proxy=RecordProxy.Of(Couplers[n]); var old=proxy.Call;
            proxy.Call=(method,args)=>
            {
                if(method.Name=="Detach")
                {
                    Record(index,"Detach");
                    if(AutoDetach)
                    {
                        var top=proxy.Values["Top"] as IMyMotorRotor;
                        if(top!=null) RecordProxy.Of(top).Values["Base"]=null;
                        proxy.Values["Top"]=null; proxy.Values["TopGrid"]=null;
                        proxy.Values["IsAttached"]=false; proxy.Values["PendingAttachment"]=false;
                    }
                    return null;
                }
                if(method.Name=="Attach")
                {
                    Record(index,"Attach");
                    if(Couplers[index].IsAttached || Couplers[index].PendingAttachment) return null;
                    if(AutoAttach)
                    {
                        var top=AttachResult ?? ArmTip; proxy.Values["Top"]=top; proxy.Values["TopGrid"]=top.CubeGrid;
                        proxy.Values["IsAttached"]=true; proxy.Values["PendingAttachment"]=false; RecordProxy.Of(top).Values["Base"]=Couplers[index];
                    }
                    else proxy.Values["PendingAttachment"]=true;
                    return null;
                }
                return old!(method,args);
            };
        }
        RecordProxy.Of(ArmTip).Values["Base"]=headless?null:Couplers[0];
        var other=Grid(new Vector3D(20,0,-10)); UnrelatedMerge=Rig.Block<IMyShipMergeBlock>("Unrelated station merge",other);
        Put(UnrelatedMerge,other,Vector3I.Zero,true); RecordProxy.Of(UnrelatedMerge).Values["IsConnected"]=true;
        var ini=new MyIni(); ini.Set("AutoArm","Format",4); ini.Set("AutoArm","Arm","Arm 1");
        ini.Set("Tools","Enabled",true); ini.Set("Tools","Mount",ArmRef.CustomName);
        ini.Set("Tools","Heads",string.Join("\n",Markers.Select(m=>m.CustomName)));
        ini.Set("Tools","MergeNormal","Right"); ini.Set("Tools","PhaseTimeout",60);
        ini.Set("Tools","ParkMerges",string.Join("\n",Stands[0].Select(m=>m.CustomName)));
        if(explicitOnly) { ini.Set("Tools","ScanHeadMerges",false); ini.Set("Tools","ScanStandMerges",false); ini.Set("Tools","ScanParkMerges",false); }
        for(int n=0;n<2;n++)
        {
            string s=$"Tool{n+1:00}"; ini.Set(s,"Mount",Couplers[n].CustomName); ini.Set(s,"Name",n==0?"A":"B");
            ini.Set(s,"StandPrefix",$"Arm 1 - Tool {(n==0?"A":"B")} - StandMerge ");
            if(explicitOnly) { ini.Set(s,"HeadMerges",string.Join("\n",Heads[n].Select(m=>m.CustomName))); ini.Set(s,"StandMerges",string.Join("\n",Stands[n].Select(m=>m.CustomName))); }
        }
        RecordProxy.Of(Rig.PB).Values["CustomData"]=ini.ToString();
    }
    internal static VRage.ObjectBuilders.SerializableDefinitionId Definition(string subtype) => (VRage.ObjectBuilders.SerializableDefinitionId)new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock),subtype);
    void Record(int tool,string kind)
    {
        var ini=new MyIni(); ini.TryParse(Host?.Storage ?? "");
        Mutations.Add((tool,kind,ini.Get("AutoArm Swap","Phase").ToString(),!AnyDrive,Heads[tool].Length>0 && Heads[tool].All(m=>m.IsConnected && m.Enabled) && Stands[tool].All(m=>m.IsConnected)));
    }
    internal IMyCubeGrid Grid(Vector3D origin)
    {
        var grid=Rig.Grid(); var p=RecordProxy.Of(grid); var frame=MatrixD.Identity; frame.Translation=origin;
        p.Values["WorldMatrix"]=frame; p.Values["GridSizeEnum"]=MyCubeSize.Large; Cells[grid]=new();
        p.Call=(m,a)=>m.Name switch
        {
            "GetCubeBlock" => Visible(grid,(Vector3I)a![0]!),
            "CubeExists" => Cells[grid].ContainsKey((Vector3I)a![0]!),
            "GridIntegerToWorld" => Vector3D.Transform((Vector3D)(Vector3I)a![0]! * grid.GridSize,grid.WorldMatrix),
            "WorldToGridInteger" => Vector3I.Round(Vector3D.Transform((Vector3D)a![0]!,MatrixD.Invert(grid.WorldMatrix))/grid.GridSize),
            _ => m.ReturnType==typeof(void) || !m.ReturnType.IsValueType?null:Activator.CreateInstance(m.ReturnType)
        };
        return grid;
    }
    IMySlimBlock? Visible(IMyCubeGrid grid,Vector3I cell)
    {
        var slim=Cells[grid].GetValueOrDefault(cell);
        return slim?.FatBlock!=null && slim.FatBlock is not IMyTerminalBlock?null:slim;
    }
    void Armor(IMyCubeGrid grid,Vector3I cell) => Cells[grid][cell]=RecordProxy.Make<IMySlimBlock>();
    void Put(IMyCubeBlock block,IMyCubeGrid grid,Vector3I cell,bool reverse=false)
    {
        var v=RecordProxy.Of(block).Values; v["CubeGrid"]=grid; v["Position"]=cell; v["Min"]=cell; v["Max"]=cell;
        v["Orientation"]=new MyBlockOrientation(reverse?Base6Directions.Direction.Backward:Base6Directions.Direction.Forward,Base6Directions.Direction.Up);
        v["WorldMatrix"]=MatrixD.CreateWorld(grid.GridIntegerToWorld(cell),reverse?Vector3D.Backward:Vector3D.Forward,Vector3D.Up);
        if(block is not IMyTerminalBlock) RecordProxy.Of(block).Call=(m,a)=>m.Name=="GetPosition"?((MatrixD)v["WorldMatrix"]!).Translation:m.ReturnType==typeof(void) || !m.ReturnType.IsValueType?null:Activator.CreateInstance(m.ReturnType);
        var slim=RecordProxy.Make<IMySlimBlock>(); RecordProxy.Of(slim).Values["FatBlock"]=block; Cells[grid][cell]=slim;
    }
    void TransformGrid(IMyCubeGrid grid,MatrixD delta)
    {
        RecordProxy.Of(grid).Values["WorldMatrix"]=grid.WorldMatrix*delta;
        foreach(var b in Cells[grid].Values.Select(s=>s.FatBlock).Where(b=>b!=null).Distinct()) RecordProxy.Of(b!).Values["WorldMatrix"]=b!.WorldMatrix*delta;
    }
    internal void MoveSource(MatrixD target)
    {
        var delta=MatrixD.Invert(Markers[0].WorldMatrix)*target;
        TransformGrid(Markers[0].CubeGrid,delta); TransformGrid(ArmGrid,delta);
    }
    internal void MoveArm(MatrixD target) => TransformGrid(ArmGrid,MatrixD.Invert(ArmRef.WorldMatrix)*target);
    internal void ShiftIncoming(Vector3D shift) { var delta=MatrixD.Identity; delta.Translation=shift; TransformGrid(Markers[1].CubeGrid,delta); }
    internal void LockSource(int count=2)
    {
        foreach(var stand in Stands[0]) { var pose=stand.WorldMatrix; Put(stand,Markers[0].CubeGrid,stand.Position,true); RecordProxy.Of(stand).Values["WorldMatrix"]=pose; }
        for(int j=0;j<Heads[0].Length;j++) { RecordProxy.Of(Heads[0][j]).Values["IsConnected"]=j<count; RecordProxy.Of(Stands[0][j]).Values["IsConnected"]=j<count; }
    }
    internal void SplitTool(int n)
    {
        var grid=Grid(Markers[n].WorldMatrix.Translation);
        foreach(var b in new IMyCubeBlock[]{Markers[n],Couplers[n]}.Concat(Heads[n])) { var pose=b.WorldMatrix; Put(b,grid,b.Position); RecordProxy.Of(b).Values["WorldMatrix"]=pose; }
        Armor(grid,new Vector3I(0,1,0)); foreach(var b in Heads[n].Concat(Stands[n])) RecordProxy.Of(b).Values["IsConnected"]=false;
    }
    internal IMyMotorRotor WrongArmPart()
    {
        var top=RecordProxy.Make<IMyMotorRotor>(); RecordProxy.Of(top).Values["EntityId"]=8999L; RecordProxy.Of(top).Values["Closed"]=false;
        RecordProxy.Of(top).Values["BlockDefinition"]=Definition("LargeRotor"); Put(top,ArmGrid,new Vector3I(0,-2,0)); return top;
    }
    internal void SplitCouplerFromMarker(int n)
    {
        var block=Couplers[n]; Cells[block.CubeGrid].Remove(block.Position); var pose=block.WorldMatrix;
        Put(block,Grid(new Vector3D(30,0,-10)),block.Position); RecordProxy.Of(block).Values["WorldMatrix"]=pose;
    }
    internal void SeparateMarkerAndCouplerFromSupport(int n)
    {
        var grid=Grid(Markers[n].WorldMatrix.Translation);
        foreach(var b in new IMyCubeBlock[]{Markers[n],Couplers[n]}) { var pose=b.WorldMatrix; Put(b,grid,b.Position); RecordProxy.Of(b).Values["WorldMatrix"]=pose; }
    }
    internal IMyMotorStator OtherOwner()
    {
        var other=Rig.Rotor("Unconfigured rotor owner",Rig.Root,ArmGrid);
        var v=RecordProxy.Of(other).Values; v["IsAttached"]=false; v["Top"]=null; v["TopGrid"]=null; return other;
    }
    internal bool AnyDrive => Math.Abs(Base.TargetVelocityRad)>1e-7 || Math.Abs(Piston.Velocity)>1e-7 || Couplers.Any(m=>Math.Abs(m.TargetVelocityRad)>1e-7);
    internal bool AnyDriveWritten => new IMyTerminalBlock[]{Base,Piston,Couplers[0],Couplers[1]}.Any(b=>RecordProxy.Of(b).Writes.Any(w=>(w.Name=="Velocity" || w.Name=="TargetVelocityRad") && Math.Abs(Convert.ToDouble(w.Value))>1e-7));
    internal bool StationWritten => Stands.SelectMany(x=>x).Append(UnrelatedMerge).Any(b=>RecordProxy.Of(b).Writes.Any(w=>w.Name=="Enabled"));
}
