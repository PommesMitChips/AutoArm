using System.Collections;
using System.Reflection;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

// Test-only proxies implement the installed SE interfaces. No game process or save is touched.
public class RecordProxy : DispatchProxy
{
    public static string Actor = "";
    public readonly List<(string Actor,string Name,object? Value)> ActorWrites = new();
    public readonly Dictionary<string, object?> Values = new();
    public readonly Dictionary<string,int> Reads = new();
    public readonly List<(string Name, object? Value)> Writes = new();
    public Func<MethodInfo, object?[]?, object?>? Call;
    protected override object? Invoke(MethodInfo? target, object?[]? args)
    {
        var method = target!;
        if (method.Name.StartsWith("get_"))
        {
            string read=method.Name[4..]; Reads.TryGetValue(read,out int count); Reads[read]=count+1;
            if (Values.TryGetValue(method.Name[4..], out var value)) return value;
        }
        if (method.Name.StartsWith("set_"))
        {
            string key = method.Name[4..];
            Values[key] = args![0]; Writes.Add((key, args[0])); ActorWrites.Add((Actor,key,args[0])); return null;
        }
        if (Call != null) return Call(method, args);
        return method.ReturnType == typeof(void) || !method.ReturnType.IsValueType ? null : Activator.CreateInstance(method.ReturnType);
    }
    public static T Make<T>() where T : class => Create<T, RecordProxy>();
    public static RecordProxy Of(object o) => (RecordProxy)o;
}

public class TestHost
{
    public static Rig Next = null!;
    protected TestHost()
    {
        Me = Next.PB; GridTerminalSystem = Next.GTS; Runtime = Next.Runtime;
        Storage = Next.Storage; Echo = Next.Log.Add;
        IGC = Next.IGC;
    }
    public IMyProgrammableBlock Me { get; set; }
    public IMyGridTerminalSystem GridTerminalSystem { get; set; }
    public IMyGridProgramRuntimeInfo Runtime { get; set; }
    public string Storage { get; set; }
    public Action<string> Echo { get; set; }
    public IMyIntergridCommunicationSystem IGC { get; set; }
}

public class Rig
{
    long sequence = 100;
    public readonly List<IMyTerminalBlock> Blocks = new();
    public readonly List<string> Log = new();
    public int InventoryCalls;
    public int TypedInventoryCalls;
    public readonly IMyCubeGrid Root;
    public readonly IMyProgrammableBlock PB;
    public readonly IMyGridTerminalSystem GTS;
    public readonly IMyGridProgramRuntimeInfo Runtime;
    public string Storage = "";
    public IMyIntergridCommunicationSystem IGC;

    public Rig(Rig? shared=null,string pbName="Controller")
    {
        IGC = RecordProxy.Make<IMyIntergridCommunicationSystem>();
        RecordProxy.Of(IGC).Values["UnicastListener"] = RecordProxy.Make<IMyUnicastListener>();
        if(shared!=null) Blocks=shared.Blocks;
        Root = shared?.Root ?? Grid();
        PB = shared==null?Block<IMyProgrammableBlock>(pbName,Root):shared.Block<IMyProgrammableBlock>(pbName,Root);
        RecordProxy.Of(PB).Values["CustomData"] = "";
        GTS = RecordProxy.Make<IMyGridTerminalSystem>();
        RecordProxy.Of(GTS).Call = (m, a) =>
        {
            if (m.Name is "GetBlocks" or "GetBlocksOfType" or "SearchBlocksOfName")
            {
                if(m.Name=="GetBlocks")InventoryCalls++;
                if(m.Name=="GetBlocksOfType")TypedInventoryCalls++;
                int listIndex = m.Name == "SearchBlocksOfName" ? 1 : 0;
                var list = (IList)a![listIndex]!; list.Clear();
                var type = m.IsGenericMethod ? m.GetGenericArguments()[0] : typeof(IMyTerminalBlock);
                var filter = a.Length > listIndex + 1 ? a[listIndex + 1] as Delegate : null;
                foreach (var block in Blocks)
                    if (type.IsInstanceOfType(block) && (m.Name != "SearchBlocksOfName" || block.CustomName.Contains((string)a[0]!)) &&
                        (filter == null || (bool)filter.DynamicInvoke(block)!)) list.Add(block);
                return null;
            }
            if (m.Name == "GetBlockWithName") return Blocks.FirstOrDefault(b => b.CustomName == (string)a![0]!);
            if (m.Name == "GetBlockWithId") return Blocks.FirstOrDefault(b => b.EntityId == (long)a![0]!);
            return null;
        };
        Runtime = RecordProxy.Make<IMyGridProgramRuntimeInfo>();
        var runtime = RecordProxy.Of(Runtime).Values;
        runtime["TimeSinceLastRun"] = TimeSpan.FromSeconds(1d / 60);
        runtime["MaxInstructionCount"] = 50000;
        runtime["CurrentInstructionCount"] = 0;
    }
    public IMyCubeGrid Grid()
    {
        var grid = RecordProxy.Make<IMyCubeGrid>();
        var values = RecordProxy.Of(grid).Values;
        values["EntityId"] = ++sequence;
        values["WorldMatrix"] = MatrixD.Identity;
        values["GridSize"] = 2.5f;
        return grid;
    }
    public T Block<T>(string name, IMyCubeGrid grid, Vector3D? position = null, Vector3D? axis = null) where T : class, IMyTerminalBlock
    {
        var block = RecordProxy.Make<T>();
        var proxy = RecordProxy.Of(block);
        var values = proxy.Values;
        values["EntityId"] = ++sequence; values["CustomName"] = name; values["CubeGrid"] = grid;
        values["Closed"] = false; values["Enabled"] = true; values["IsWorking"] = true; values["IsFunctional"] = true;
        values["CustomData"] = "";
        var up = axis ?? Vector3D.Up;
        var forward = Math.Abs(Vector3D.Dot(up, Vector3D.Forward)) < .9 ? Vector3D.Forward : Vector3D.Right;
        var matrix = MatrixD.CreateWorld(position ?? Vector3D.Zero, forward, up);
        values["WorldMatrix"] = matrix;
        proxy.Call = (m, a) =>
        {
            if (m.Name == "GetPosition") return ((MatrixD)values["WorldMatrix"]!).Translation;
            if (m.Name is "IsSameConstructAs" or "IsSameGridAs") return m.Name == "IsSameConstructAs" || ((IMyTerminalBlock)a![0]!).CubeGrid == grid;
            return m.ReturnType == typeof(void) || !m.ReturnType.IsValueType ? null : Activator.CreateInstance(m.ReturnType);
        };
        Blocks.Add(block);
        return block;
    }
    public IMyMotorStator Rotor(string name, IMyCubeGrid from, IMyCubeGrid to, Vector3D? position = null, Vector3D? axis = null)
    {
        var rotor = Block<IMyMotorStator>(name, from, position, axis);
        var values = RecordProxy.Of(rotor).Values;
        values["TopGrid"] = to; values["Top"] = Top<IMyMotorRotor>(to);
        values["IsAttached"] = true; values["RotorLock"] = false;
        values["Angle"] = 0f; values["LowerLimitRad"] = float.MinValue; values["UpperLimitRad"] = float.MaxValue;
        values["TargetVelocityRad"] = 0f;
        return rotor;
    }
    public IMyPistonBase Piston(string name, IMyCubeGrid from, IMyCubeGrid to, Vector3D? position = null, Vector3D? axis = null)
    {
        var piston = Block<IMyPistonBase>(name, from, position, axis);
        var values = RecordProxy.Of(piston).Values;
        values["TopGrid"] = to; values["Top"] = Top<IMyPistonTop>(to);
        values["IsAttached"] = true; values["CurrentPosition"] = 5f;
        values["MinLimit"] = 0f; values["MaxLimit"] = 10f; values["MaxVelocity"] = 5f; values["Velocity"] = 0f;
        return piston;
    }
    public IMyShipController Cockpit()
    {
        var cockpit = Block<IMyShipController>("Cockpit", Root);
        var values = RecordProxy.Of(cockpit).Values;
        values["IsUnderControl"] = true; values["CanControlShip"] = true;
        values["MoveIndicator"] = Vector3.Zero; values["RotationIndicator"] = Vector2.Zero; values["RollIndicator"] = 0f;
        return cockpit;
    }
    T Top<T>(IMyCubeGrid grid) where T:class
    {
        var top=RecordProxy.Make<T>();
        RecordProxy.Of(top).Values["EntityId"]=++sequence;
        RecordProxy.Of(top).Values["CubeGrid"]=grid;
        RecordProxy.Of(top).Values["Closed"]=false;
        return top;
    }
}
