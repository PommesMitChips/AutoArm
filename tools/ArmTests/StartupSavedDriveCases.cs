using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;

internal static partial class Scenarios
{
    // Invoke from the host suite after Workspace has been assigned.
    internal static void StartupSavedDriveCases(Type armType)
    {
        string helper = File.ReadAllText(Path.Combine(Tests.Workspace, "src/AutoArm.SavedDrives.cs.txt"));
        var probe = Tests.Script(helper + "\npublic void Recover(string text,string arm){var state=new MyIni();if(state.TryParse(text))SavedDrives.Stop(state,arm,Me,GridTerminalSystem);}");
        var rig = new Rig();
        var current = rig.Block<IMyPistonBase>("current", rig.Root);
        var parked = rig.Block<IMyMotorStator>("parked", rig.Root);
        var closed = rig.Block<IMyPistonBase>("closed", rig.Root);
        var foreign = rig.Block<IMyPistonBase>("foreign", rig.Root);
        var suspension = rig.Block<StartupSuspension>("suspension", rig.Root);
        var unrelated = rig.Block<IMyShipDrill>("unrelated", rig.Root);
        var malformed = rig.Block<IMyPistonBase>("malformed", rig.Root);
        var failed = rig.Block<IMyPistonBase>("failed lookup", rig.Root);
        var failedConstruct = rig.Block<IMyPistonBase>("failed construct check", rig.Root);
        var failedWrite = System.Reflection.DispatchProxy.Create<IMyPistonBase, StartupWriteFailureProxy>();
        RecordProxy.Of(failedWrite).Values["EntityId"] = 987654321L; RecordProxy.Of(failedWrite).Values["Closed"] = false;
        RecordProxy.Of(failedWrite).Call = (m,a) => m.Name == "IsSameConstructAs" ? true : null;
        rig.Blocks.Add(failedWrite);
        var negative = rig.Block<IMyPistonBase>("negative ID", rig.Root);
        RecordProxy.Of(negative).Values["EntityId"] = -negative.EntityId;
        RecordProxy.Of(closed).Values["Closed"] = true;
        RecordProxy.Of(foreign).Call = (m,a) => m.Name == "IsSameConstructAs" ? false : null;
        RecordProxy.Of(failedConstruct).Call = (m,a) => throw new Exception("Construct check failed");
        int lookups = 0;
        var lookup = RecordProxy.Of(rig.GTS).Call;
        RecordProxy.Of(rig.GTS).Call = (m,a) =>
        {
            if (m.Name == "GetBlockWithId")
            {
                lookups++;
                if ((long)a![0]! == failed.EntityId) throw new Exception("Lookup failed");
            }
            return lookup!(m,a);
        };
        var state = StartupState(rig, "Arm 1", "01|P:" + current.EntityId + "+@1>2:" + current.EntityId + "-@1>2\n02|P:" + failed.EntityId + "+@1>2:" + failedConstruct.EntityId + "+@1>2:" + closed.EntityId + "+@1>2:" + foreign.EntityId + "+@1>2:" + suspension.EntityId + "+@1>2:" + unrelated.EntityId + "+@1>2:" + negative.EntityId + "-@1>2\n03|P:" + malformed.EntityId + "x@1>2\nwrong|columns|are|ignored");
        state.Set("AutoArm Tool Weights", "Rows", "R:" + parked.EntityId + "-@2>3|1|1");
        state.Set("AutoArm Config", "Rows", "failed|P:" + failedWrite.EntityId + "+@1>2\n" + state.Get("AutoArm Config", "Rows").ToString());
        var instance = Tests.Create(probe, rig);
        Action reset = () =>
        {
            foreach (var block in new IMyTerminalBlock[] { current, closed, foreign, malformed, failed, failedConstruct, negative }) RecordProxy.Of(block).Values["Velocity"] = 3f;
            RecordProxy.Of(parked).Values["TargetVelocityRad"] = 3f;
            RecordProxy.Of(suspension).Values["TargetVelocityRad"] = 3f;
        };
        foreach (string gate in new[] { "Format", "PB", "Arm" })
        {
            reset(); var bad = new MyIni(); bad.TryParse(state.ToString());
            bad.Set("AutoArm Config", gate, gate == "Arm" ? "Other arm" : gate == "PB" ? "-777" : "2");
            Tests.Call(instance, "Recover", bad.ToString(), "Arm 1");
            Check(current.Velocity == 3 && parked.TargetVelocityRad == 3 && lookups == 0, "Startup recovery ignored saved identity " + gate + " scope.");
        }
        reset(); Tests.Call(instance, "Recover", state.ToString(), "Arm 1");
        Check(current.Velocity == 0 && parked.TargetVelocityRad == 0 && negative.Velocity == 0, "Startup recovery omitted current, parked or signed long identities.");
        Check(closed.Velocity == 3 && foreign.Velocity == 3 && suspension.TargetVelocityRad == 3 && malformed.Velocity == 3, "Startup recovery stopped an excluded or unsigned identity.");
        Check(failed.Velocity == 3 && failedConstruct.Velocity == 3 && lookups == 10, "Startup recovery did not isolate exceptions or deduplicate actuator identities.");
        Check(rig.InventoryCalls == 0 && rig.TypedInventoryCalls == 0, "Startup recovery enumerated the terminal inventory.");
        StartupHostTiming(armType);
        Console.WriteLine("Saved actuator recovery: PASS (identity scope, signed IDs, parked drives, exclusions, isolated failures, direct lookup and both startup timings).");
    }

    static MyIni StartupState(Rig rig, string arm, string rows)
    {
        var state = new MyIni(); state.Set("AutoArm Config", "Format", 1); state.Set("AutoArm Config", "PB", rig.PB.EntityId);
        state.Set("AutoArm Config", "Arm", arm); state.Set("AutoArm Config", "Rows", rows); return state;
    }

    static void StartupHostTiming(Type armType)
    {
        var rig = new Rig(); var a = rig.Block<IMyPistonBase>("saved A", rig.Root); var b = rig.Block<IMyPistonBase>("saved B", rig.Root);
        var storage = new MyIni(); storage.Set("State Removed arm", "Data", StartupState(rig, "Removed arm", "01|P:" + a.EntityId + "+@1>2").ToString());
        storage.Set("State Parked arm", "Data", StartupState(rig, "Parked arm", "01|P:" + b.EntityId + "-@1>2").ToString());
        rig.Storage = storage.ToString(); RecordProxy.Of(rig.PB).Values["CustomData"] = "[global]\nFormat=0";
        RecordProxy.Of(a).Values["Velocity"] = 3f; RecordProxy.Of(b).Values["Velocity"] = 3f;
        Tests.Create(armType, rig);
        Check(a.Velocity == 0 && b.Velocity == 0, "Host failed configuration before stopping all previously saved arms.");

        rig = MultiFixture(out _, out b, out _);
        var config = new MyIni(); config.TryParse(rig.PB.CustomData); config.DeleteSection("Arm 2"); RecordProxy.Of(rig.PB).Values["CustomData"] = config.ToString();
        storage = new MyIni(); storage.Set("AutoArm Host", "PB", rig.PB.EntityId);
        storage.Set("State Arm 2", "Data", StartupState(rig, "Arm 2", "01|P:" + b.EntityId + "+@1>2").ToString()); rig.Storage = storage.ToString();
        var host = Tests.Create(armType, rig);
        RecordProxy.Of(b).Values["Velocity"] = 3f; config.Set("Arm 2", "HeadSpeed", .35); RecordProxy.Of(rig.PB).Values["CustomData"] = config.ToString();
        var entry = PackedProgramChecks.Unwrap(host);
        Tests.Call(entry, PackedProgramChecks.Name(entry.GetType(), "LoadHost"));
        Check(b.Velocity == 0, "Later-added arm did not recover saved velocities during core construction.");
    }
}

public class StartupWriteFailureProxy : RecordProxy
{
    protected override object? Invoke(System.Reflection.MethodInfo? target, object?[]? args)
    {
        if (target!.Name == "set_Velocity") throw new Exception("Write failed");
        return base.Invoke(target, args);
    }
}

public interface StartupSuspension : IMyMotorStator, IMyMotorSuspension { }
