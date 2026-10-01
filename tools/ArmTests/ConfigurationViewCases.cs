using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;

internal static partial class Scenarios
{
    static void ConfigurationViewCases(Type armType, Type toolType)
    {
        var rig = MultiFixture(out _, out _, out _);
        var data = new MyIni(); data.TryParse(rig.PB.CustomData);
        data.Set("global", "ToolSwapPB", "{arm} - Missing Tool PB");
        data.Set("Arm 1", "ToolSwapPB", "");
        RecordProxy.Of(rig.PB).Values["CustomData"] = data.ToString();
        var host = Tests.Create(armType, rig);

        // Defaults are published after discovery. A live user edit made between
        // discovery and publication must survive, and applies only on reload.
        var live = new MyIni(); live.TryParse(rig.PB.CustomData);
        live.Set("global", "HeadSpeed", .93);
        live.Set("Arm 1", "Notes.User", "keep this live edit");
        RecordProxy.Of(rig.PB).Values["CustomData"] = live.ToString();
        for (int i = 0; i < 12; i++) HostFrame(host, rig);
        live.TryParse(rig.PB.CustomData);
        Check(live.Get("global", "HeadSpeed").ToDouble() == .93 &&
            live.Get("Arm 1", "Notes.User").ToString() == "keep this live edit",
            "Generated defaults overwrote a live user edit.");
        Check(live.ContainsKey("global", "ReadKeyboard") && !live.ContainsSection("AutoArm"),
            "Default publication leaked a private configuration section or omitted defaults.");

        HostReady(host, rig, "Arm 1");
        Check((double)Get(Core(host, "Arm 1"), "MoveMps")! == .93,
            "Command reload did not refresh the effective global setting.");
        Check(((MyIni)Get(Core(host, "Arm 2"), "Config")!).Get("Config", "HeadSpeed").ToDouble() == .35,
            "Reloading one arm reset another arm's effective override.");
        Check(live.Get("Arm 1", "ToolSwapPB").ToString() == "",
            "Explicit empty module override was replaced by the inherited value.");
        Check(rig.Log.Any(line => line.Contains("Arm 2 - Missing Tool PB")),
            "Inherited module names did not substitute the target arm name.");

        string published = rig.PB.CustomData;
        for (int i = 0; i < 20; i++) HostFrame(host, rig);
        Check(rig.PB.CustomData == published, "Ordinary frames republished or changed configuration.");
        HostFrame(host, rig, "StopAll");

        foreach (var role in new[] { (Type: armType, Format: 7), (Type: toolType, Format: 2) })
        foreach (string rejected in new[] { "malformed notes", "[global]\nFormat=0\n[Arm 1]\nTools.Enabled=true\n",
            "[global]\nFormat=" + (role.Format + 1) + "\n[Arm 1]\nTools.Enabled=true\n" })
        {
            var invalid = Fixtures.Serial(out _, out _, out _, out _);
            RecordProxy.Of(invalid.PB).Values["CustomData"] = rejected;
            var refused = Tests.Create(role.Type, invalid);
            for (int i = 0; i < 3; i++) HostFrame(refused, invalid);
            Check(invalid.PB.CustomData == rejected && ((TestHost)refused).Storage == invalid.Storage,
                "Public format/INI refusal rewrote configuration or saved state.");
            NoVelocity(invalid);
        }

        foreach (string invalidSetting in new[] { "", "NaN", "Infinity", "-1" })
        {
            var invalid = MultiFixture(out _, out _, out _);
            var ini = new MyIni(); ini.TryParse(invalid.PB.CustomData);
            ini.Set("global", "HeadSpeed", invalidSetting);
            ini.Delete("Arm 2", "HeadSpeed"); // Both arms must inherit the invalid setting.
            string rejected = ini.ToString();
            RecordProxy.Of(invalid.PB).Values["CustomData"] = rejected;
            var refused = Tests.Create(armType, invalid);
            for (int i = 0; i < 5; i++) HostFrame(refused, invalid);
            Check(invalid.PB.CustomData == rejected && !Enabled(Core(refused, "Arm 1")) && !Enabled(Core(refused, "Arm 2")),
                "Invalid effective settings generated defaults or enabled motion.");
            NoVelocity(invalid);
        }
    }
}
