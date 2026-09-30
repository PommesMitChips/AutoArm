using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

// A plant fixture: terminal pointers survive merge/split, while occupied cells
// and grid identities change. Rotor tops deliberately never enter the GTS.
internal sealed class ToolSwapFixture
{
    internal readonly Rig Rig = new();
    internal readonly IMyMotorStator Base, Mount;
    internal readonly IMyPistonBase Piston;
    internal readonly IMyShipMergeBlock UnrelatedMerge;
    internal readonly IMyCubeGrid Arm;
    internal readonly IMyTerminalBlock[] Markers = new IMyTerminalBlock[2];
    internal readonly IMyMotorRotor[] Tops = new IMyMotorRotor[2];
    internal readonly IMyShipMergeBlock[][] Heads = new IMyShipMergeBlock[2][], Stands = new IMyShipMergeBlock[2][];
    internal readonly List<string> Mutations = new();
    internal readonly List<string> MutationJournals = new();
    internal readonly List<bool> StoppedAtMutation = new();
    internal TestHost? Host;
    readonly Dictionary<IMyCubeGrid, Dictionary<Vector3I, IMySlimBlock>> Cells = new();
    internal IMyMotorRotor? AttachResult;
    internal bool AutoDetach = true, AutoAttach = true;
    internal bool HideNonterminalCells;

    internal ToolSwapFixture(bool ports = true, bool explicitOnly = false, bool headless = false, bool hideNonterminalCells = false)
    {
        HideNonterminalCells = hideNonterminalCells;
        var shoulder = Rig.Grid(); Arm = Rig.Grid();
        Base = Rig.Rotor("Arm 1 - Base - Rotor", Rig.Root, shoulder);
        Piston = Rig.Piston("Reach piston", shoulder, Arm, new Vector3D(0,0,-2), Vector3D.Forward);
        var first = Grid(new Vector3D(0,0,-10));
        Mount = Rig.Rotor("Tool Mount", Arm, first, new Vector3D(0,-2.9208642244,-10));
        RecordProxy.Of(Mount).Values["BlockDefinition"] = (VRage.ObjectBuilders.SerializableDefinitionId)new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeStator");
        Rig.Cockpit();
        var unrelated = Grid(new Vector3D(25,0,-10));
        UnrelatedMerge = Rig.Block<IMyShipMergeBlock>("Unrelated station merge",unrelated);
        Put(UnrelatedMerge,unrelated,Vector3I.Zero,true);
        RecordProxy.Of(UnrelatedMerge).Values["IsConnected"] = true;
        for(int n=0;n<2;n++)
        {
            var grid = n == 0 ? first : Grid(new Vector3D(5,0,-10));
            Markers[n] = Rig.Block<IMyShipDrill>(n == 0 ? "Arm 1 - Head - Tool A" : "Tool B", grid);
            Put(Markers[n], grid, Vector3I.Zero);
            Tops[n] = RecordProxy.Make<IMyMotorRotor>();
            var top = RecordProxy.Of(Tops[n]);
            top.Values["EntityId"] = 8000L+n; top.Values["Closed"] = false;
            top.Values["BlockDefinition"] = (VRage.ObjectBuilders.SerializableDefinitionId)new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeRotor");
            top.Values["Base"] = n == 0 && !headless ? Mount : null;
            Put(Tops[n], grid, new Vector3I(0,-1,0));
            // The cell at (0,1,0) joins the second parallel merge to the marker.
            var bridge = RecordProxy.Make<IMyCubeBlock>();
            RecordProxy.Of(bridge).Values["EntityId"] = 8100L+n;
            Put(bridge, grid, new Vector3I(0,1,0));
            Heads[n] = new IMyShipMergeBlock[ports ? 2 : 0];
            Stands[n] = new IMyShipMergeBlock[ports ? 2 : 0];
            var standGrid = n == 0 ? Grid(new Vector3D(.3,0,-10)) : grid;
            for(int j=0;j<Heads[n].Length;j++)
            {
                Heads[n][j] = Rig.Block<IMyShipMergeBlock>($"Tool {n+1} head merge {j+1}", grid);
                Stands[n][j] = Rig.Block<IMyShipMergeBlock>($"Arm 1 - Tool {(n==0?"A":"B")} - StandMerge {j+1}", standGrid);
                Put(Heads[n][j], grid, new Vector3I(1,j,0));
                Put(Stands[n][j], standGrid, new Vector3I(2,j,0), true);
                RecordProxy.Of(Heads[n][j]).Values["IsConnected"] = n == 1;
                RecordProxy.Of(Stands[n][j]).Values["IsConnected"] = n == 1;
                RecordProxy.Of(Heads[n][j]).Values["Enabled"] = n == 1;
            }
            if(n == 1 && ports)
            {
                // An eligible unattached top across the opposed merge faces
                // proves the cell flood fill cuts the station side.
                var stationTop = RecordProxy.Make<IMyMotorRotor>();
                RecordProxy.Of(stationTop).Values["EntityId"] = 8200L;
                Put(stationTop, grid, new Vector3I(3,0,0));
            }
        }
        var mp = RecordProxy.Of(Mount);
        mp.Values["Top"] = headless ? null : Tops[0];
        mp.Values["TopGrid"] = headless ? null : first;
        mp.Values["IsAttached"] = !headless;
        var old = mp.Call;
        mp.Call = (method,args) =>
        {
            if(method.Name == "Detach")
            {
                ObserveMutation();
                Mutations.Add("Detach");
                if(AutoDetach) { RecordProxy.Of(Tops[0]).Values["Base"] = null; mp.Values["Top"] = null; mp.Values["TopGrid"] = null; mp.Values["IsAttached"] = false; }
                return null;
            }
            if(method.Name == "Attach")
            {
                ObserveMutation();
                Mutations.Add("Attach");
                if(AutoAttach) { var t = AttachResult ?? Tops[1]; mp.Values["Top"] = t; mp.Values["TopGrid"] = t.CubeGrid; mp.Values["IsAttached"] = true; RecordProxy.Of(t).Values["Base"] = Mount; }
                return null;
            }
            return old!(method,args);
        };
        var ini = new MyIni();
        ini.Set("AutoArm", "Format", 3); ini.Set("AutoArm", "Arm", "Arm 1");
        ini.Set("Tools", "Enabled", true); ini.Set("Tools", "Mount", "Tool Mount");
        ini.Set("Tools", "Heads", string.Join("\n",Markers.Select(m=>m.CustomName)));
        ini.Set("Tools", "PhaseTimeout", 60);
        ini.Set("Tools", "MergeNormal", "Right");
        for(int n=0;n<2;n++) ini.Set($"Tool{n+1:00}","StandPrefix",$"Arm 1 - Tool {(n==0?"A":"B")} - StandMerge ");
        if(explicitOnly)
        {
            ini.Set("Tools","ScanHeadMerges",false); ini.Set("Tools","ScanStandMerges",false); ini.Set("Tools","ScanParkMerges",false);
            for(int n=0;n<2;n++) { ini.Set($"Tool{n+1:00}","HeadMerges",string.Join("\n",Heads[n].Select(m=>m.CustomName))); ini.Set($"Tool{n+1:00}","StandMerges",string.Join("\n",Stands[n].Select(m=>m.CustomName))); }
        }
        ini.Set("Tools","ParkMerges",string.Join("\n",Stands[0].Select(m=>m.CustomName)));
        RecordProxy.Of(Rig.PB).Values["CustomData"] = ini.ToString();
    }
    void ObserveMutation()
    {
        var journal = new MyIni(); journal.TryParse(Host?.Storage ?? "");
        MutationJournals.Add(journal.Get("AutoArm Swap","Phase").ToString());
        StoppedAtMutation.Add(!AnyDrive);
    }
    internal IMyCubeGrid Grid(Vector3D origin)
    {
        var grid = Rig.Grid(); var proxy = RecordProxy.Of(grid);
        var matrix = MatrixD.Identity; matrix.Translation = origin;
        proxy.Values["WorldMatrix"] = matrix; proxy.Values["GridSizeEnum"] = MyCubeSize.Large;
        Cells[grid] = new();
        proxy.Call = (m,a) => m.Name switch
        {
            "GetCubeBlock" => VisibleCell(grid,(Vector3I)a![0]!),
            "CubeExists" => Cells[grid].ContainsKey((Vector3I)a![0]!),
            "GridIntegerToWorld" => Vector3D.Transform((Vector3D)(Vector3I)a![0]! * 2.5, (MatrixD)proxy.Values["WorldMatrix"]!),
            "WorldToGridInteger" => Vector3I.Round(Vector3D.Transform((Vector3D)a![0]!,MatrixD.Invert((MatrixD)proxy.Values["WorldMatrix"]!))/2.5),
            _ => m.ReturnType == typeof(void) || !m.ReturnType.IsValueType ? null : Activator.CreateInstance(m.ReturnType)
        };
        return grid;
    }
    IMySlimBlock? VisibleCell(IMyCubeGrid grid,Vector3I cell)
    {
        var slim = Cells[grid].GetValueOrDefault(cell);
        return HideNonterminalCells && slim?.FatBlock != null && slim.FatBlock is not IMyTerminalBlock ? null : slim;
    }
    internal void RemoveTopCell(int region) => Cells[Markers[region].CubeGrid].Remove(Tops[region].Position);
    internal void AddHiddenStationCell(int region)
    {
        var top = RecordProxy.Make<IMyMotorRotor>();
        RecordProxy.Of(top).Values["EntityId"] = 8300L+region;
        Put(top,Markers[region].CubeGrid,new Vector3I(3,0,0));
    }
    internal void SelectOnlyProfile(int region)
    {
        var ini = new MyIni(); ini.TryParse(Rig.PB.CustomData);
        ini.Set("Tools","Heads",Markers[region].CustomName);
        ini.Set("Tool01","StandPrefix",$"Arm 1 - Tool {(region==0?"A":"B")} - StandMerge ");
        ini.Set("Tool01","HeadMerges",string.Join("\n",Heads[region].Select(m=>m.CustomName)));
        ini.Set("Tool01","StandMerges",string.Join("\n",Stands[region].Select(m=>m.CustomName)));
        RecordProxy.Of(Rig.PB).Values["CustomData"] = ini.ToString();
    }
    void Put(IMyCubeBlock block, IMyCubeGrid grid, Vector3I cell, bool reversed = false)
    {
        var values = RecordProxy.Of(block).Values;
        values["CubeGrid"] = grid; values["Position"] = cell; values["Min"] = cell; values["Max"] = cell;
        values["Orientation"] = new MyBlockOrientation(reversed ? Base6Directions.Direction.Backward : Base6Directions.Direction.Forward, Base6Directions.Direction.Up);
        values["WorldMatrix"] = MatrixD.CreateWorld(grid.GridIntegerToWorld(cell), reversed ? Vector3D.Backward : Vector3D.Forward, Vector3D.Up);
        if(block is not IMyTerminalBlock)
        {
            RecordProxy.Of(block).Call = (m,a) => m.Name=="GetPosition" ? ((MatrixD)values["WorldMatrix"]!).Translation :
                m.ReturnType == typeof(void) || !m.ReturnType.IsValueType ? null : Activator.CreateInstance(m.ReturnType);
        }
        var slim = RecordProxy.Make<IMySlimBlock>(); RecordProxy.Of(slim).Values["FatBlock"] = block;
        Cells[grid][cell] = slim;
    }
    internal void MoveSource(MatrixD pose)
    {
        var transform = MatrixD.Invert(Markers[0].WorldMatrix) * pose;
        var grid = Markers[0].CubeGrid;
        RecordProxy.Of(grid).Values["WorldMatrix"] = grid.WorldMatrix * transform;
        foreach(var block in new IMyCubeBlock[]{Markers[0],Tops[0]}.Concat(Heads[0])) RecordProxy.Of(block).Values["WorldMatrix"] = block.WorldMatrix * transform;
        RecordProxy.Of(Mount).Values["WorldMatrix"] = Mount.WorldMatrix * transform;
    }
    internal void LockSource(int pairCount = 2)
    {
        var grid = Markers[0].CubeGrid;
        foreach(var stand in Stands[0]) Put(stand,grid,stand.Position,true);
        for(int n=0;n<Heads[0].Length;n++) { RecordProxy.Of(Heads[0][n]).Values["IsConnected"] = n < pairCount; RecordProxy.Of(Stands[0][n]).Values["IsConnected"] = n < pairCount; }
        RecordProxy.Of(Mount).Values["TopGrid"] = grid;
    }
    internal void SplitDestination() => SplitRegion(1);
    internal void SplitRegion(int region)
    {
        var origin = Markers[region].WorldMatrix.Translation;
        var oldGrid = Markers[region].CubeGrid;
        var bridge = Cells[oldGrid].GetValueOrDefault(new Vector3I(0,1,0))?.FatBlock;
        var grid = Grid(origin);
        foreach(var b in new IMyCubeBlock[]{Markers[region],Tops[region]}.Concat(Heads[region])) Put(b,grid,b.Position);
        if(bridge!=null) Put(bridge,grid,bridge.Position);
        foreach(var b in Heads[region].Concat(Stands[region])) RecordProxy.Of(b).Values["IsConnected"] = false;
        RecordProxy.Of(Mount).Values["TopGrid"] = grid;
    }
    internal void ShiftRegion(int region,Vector3D delta)
    {
        var grid = Markers[region].CubeGrid; var frame=grid.WorldMatrix; frame.Translation+=delta;
        RecordProxy.Of(grid).Values["WorldMatrix"] = frame;
        foreach(var b in Cells[grid].Values.Select(s=>s.FatBlock).Where(b=>b!=null).Distinct())
        {
            var pose = b!.WorldMatrix; pose.Translation+=delta; RecordProxy.Of(b).Values["WorldMatrix"] = pose;
        }
    }
    internal bool AnyDrive => Math.Abs(Base.TargetVelocityRad)>1e-7 || Math.Abs(Piston.Velocity)>1e-7 || Math.Abs(Mount.TargetVelocityRad)>1e-7;
    internal bool StandWritten => Stands.SelectMany(x=>x).Append(UnrelatedMerge).Any(m=>RecordProxy.Of(m).Writes.Any(w=>w.Name=="Enabled"));
}
