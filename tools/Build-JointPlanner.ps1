param([switch]$SourceOnly)
$ErrorActionPreference='Stop'
$workspace=Split-Path -Parent $PSScriptRoot
function Change-One([string]$text,[string]$old,[string]$new) {
    $text=$text.Replace("`r`n","`n");$old=$old.Replace("`r`n","`n");$new=$new.Replace("`r`n","`n")
    if(($text.Split(@($old),[StringSplitOptions]::None)).Count -ne 2){throw "Joint planning hook changed: $old"}
    return $text.Replace($old,$new)
}
$arm=[IO.File]::ReadAllText((Join-Path $workspace 'AutoArm_Source.txt')).Replace('"5.1.0"','"1.1.0-joint-prototype"')
# The optional empirical pivot diagnostic stays in the production script.
# Omit it here so the guide and PB-safe block bodies fit without changing control.
$arm=Change-One $arm @'
        case "pivotprobe":
            DisplayPinned = true;
            if (PivotProbe == null) PivotProbe = new PivotProbeTool(this, Topology);
            PivotProbe.Run(w);
            break;
'@ ''
$arm=$arm.Replace('PivotProbeTool PivotProbe;','').Replace('PivotProbe = null;','')
$probeStart=$arm.IndexOf('class PivotProbeTool')
$probeEnd=$arm.IndexOf('// Arm-side focus', $probeStart)
if($probeStart -lt 0 -or $probeEnd -le $probeStart){throw 'Pivot diagnostic boundary changed.'}
$arm=$arm.Remove($probeStart,$probeEnd-$probeStart)
$arm=Change-One $arm 'public bool Docking, Capture;' 'public bool Docking, Capture; public double[][] Joints; public string JointStamp;'
$arm=Change-One $arm 'link.Received = seq; link.Age = 0; client.Error = "";' 'link.Received = seq; link.Age = 0; client.Error = ""; MyIni model = null;'
$pathHook='else if (op == "PATH")' + "`r`n            {`r`n                string why;"
if(!$arm.Contains($pathHook)){$pathHook='else if (op == "PATH")'+"`n            {`n                string why;"}
$arm=Change-One $arm $pathHook ('else if (op == "MODEL") { if (client.Role != "Plan" || Planning != null && Planning != client) throw new Exception("No planning authority/free arm."); model = new MyIni(); P.WritePlanningModel(model); Planning = client; client.State="Model"; client.ReplyAge = 0; P.Topology.ZeroOwned(); if(P.LastQdot != null) Array.Clear(P.LastQdot,0,P.LastQdot.Length); } else if(op == "RELEASEMODEL") { if(Planning == client) {Planning = null; client.State="Idle";} } '+$pathHook)
$arm=Change-One $arm 'Client Owner;' @'
Client Owner, Planning;
public void CancelModel(string reason){if(Planning!=null){Planning.Error=reason;Planning.State="Canceled";Planning=null;}}
public bool HoldingModel { get { if(Planning == null) return false; if(P.HasPilotInput()){CancelModel("Planning canceled by pilot.");return false;} return true; } }
'@
$arm=Change-One $arm 'if (Tools != null && Tools.Handle(command)) { SwapBatchLock = true; return; }' 'if(Services != null && c != "info" && c != "help" && c != "status" && c != "version" && c != "settings" && c != "joints" && c != "bindings" && c != "homeinfo") Services.CancelModel("Planning canceled by local command."); if (Tools != null && Tools.Handle(command)) { SwapBatchLock = true; return; }'
$arm=Change-One $arm "public void Revoke(string reason)`n    {`n        Revision++;" "public void Revoke(string reason)`n    {`n        Planning = null; Revision++;"
$arm=Change-One $arm 'if (Owner == null) return;' @'
if(Planning != null){var c=Planning;if(Finite(elapsed)&&elapsed>0){c.Link.Age+=elapsed;c.ReplyAge+=elapsed;}var b=c.Link.Peer;if(b.Closed||!b.IsFunctional||!b.IsSameConstructAs(P.Me)||c.Link.Age>.5||c.ReplyAge>.5)CancelModel("Planning lease expired.");}
if (Owner == null) return;
'@
$arm=Change-One $arm 'if (Owner == client) P.StopMotion("Planner restarted.");' 'if (Owner == client) P.StopMotion("Planner restarted."); if(Planning == client) Planning = null;'
$arm=Change-One $arm 'Owner = null; Revision++;' 'if(client.Path.Joints != null && client.Path.Done && P.Enabled) Planning=client; Owner = null; Revision++;'
$arm=Change-One $arm 'if (Owner != null || P.Tools.Busy || P.LocalPath != null || P.GoingHome)' 'if (Owner != null || Planning != null && Planning != client || P.Tools.Busy || P.LocalPath != null || P.GoingHome)'
$arm=Change-One $arm 'if (safety != null && (!safety.Ready || safety.Blocked)) { safety.Hold(); return; }' 'if (safety != null && (!safety.Ready || safety.Blocked)) { safety.Hold(); return; } if(!ToolDriving && Services != null && Services.HoldingModel) { Topology.ZeroOwned(); if(LastQdot != null) Array.Clear(LastQdot,0,LastQdot.Length); return; }'
$arm=Change-One $arm 'P.ConfigurePathStages(path, ini);' 'P.ConfigurePathStages(path, ini); P.ReadJointGuide(path, ini);'
$arm=Change-One $arm 'if (op != "GUIDE") Reply(client, seq); return true;' 'if (op != "GUIDE") Reply(client, seq, client.Error.Length == 0 ? model : null); return true;'
$arm=Change-One $arm 'void Reply(Client client, long ack)' 'void Reply(Client client, long ack, MyIni model = null)'
$arm=Change-One $arm 'if (P.Topology.Ready) WriteFrame(ini, "Pose", P.Topology.EndPose);' 'if (P.Topology.Ready) WriteFrame(ini, "Pose", P.Topology.EndPose); if (model != null) { var keys = new List<MyIniKey>(); model.GetKeys(keys); foreach (var key in keys) ini.Set(key.Section, key.Name, model.Get(key).ToString()); }'
$old='if (!ToolPoseStep(demand, dt, observed, path.CurrentMove, path.CurrentTurn, pt, at, out settled, out why, path.Docking ? 2 : 0)) return false;'
$arm=Change-One $arm $old 'if (path.Joints != null) { if (!StepJointGuide(path, dt, out settled, out why)) return false; } else if (!ToolPoseStep(demand, dt, observed, path.CurrentMove, path.CurrentTurn, pt, at, out settled, out why, path.Docking ? 2 : 0)) return false;'
$arm=Change-One $arm 'double lo = Math.Max(physicalLo, -cap), hi = Math.Min(physicalHi, cap);' 'double lo = Math.Max(physicalLo, -cap), hi = Math.Min(physicalHi, cap); if (GuidedRates != null) { lo = Math.Max(lo, GuidedLo[i]); hi = Math.Min(hi, GuidedHi[i]); }'
$arm=Change-One $arm 'path.Completed++; path.Elapsed = path.Budget = 0;' 'if (path.Joints != null && LastQdot != null) Array.Clear(LastQdot,0,LastQdot.Length); path.Completed++; path.Elapsed = path.Budget = 0;'
$arm=Change-One $arm 'double s = Scale[i] = preference > 0 ? cap * Math.Sqrt(preference) : 0;' 'if (GuidedRates != null) preference = .5 * (g.MoveW + g.TurnW); double s = Scale[i] = preference > 0 ? cap * Math.Sqrt(preference) : 0;'
$arm=Change-One $arm 'Lo[i] = s > 1e-12 ? lo / s : 0;' 'if (GuidedRates != null && s > 1e-12) Bias[i] = Clamp(GuidedRates[i], -cap, cap) / s; Lo[i] = s > 1e-12 ? lo / s : 0;'
$arm=Change-One $arm 'P.Tools.Contacts; }' 'P.Tools.Contacts + "/" + P.JointRpm + "/" + P.HomeJointRpm + "/" + P.PistonMps + "/" + P.HomePistonMps; }'
$arm=$arm.TrimEnd();$arm=$arm.Substring(0,$arm.Length-1)+[IO.File]::ReadAllText((Join-Path $workspace 'src/AutoArm.JointGuide.cs.txt'))+"`n}`n"
$world=[IO.File]::ReadAllText((Join-Path $workspace 'src/AutoArm.Collision.cs.txt'))
$start=$world.IndexOf('public Program()');$end=$world.IndexOf('bool Budget()');$world=$world.Remove($start,$end-$start)
$start=$world.IndexOf('bool Authorised(');$end=$world.IndexOf('void ParseArm(');$world=$world.Remove($start,$end-$start)
foreach($receiver in @('body','obstacle','grid','a','b')){$world=$world.Replace("$receiver.Grid.WorldMatrix","World($receiver)")}
$world=$world.Replace('sealed class Arm','public sealed class Arm').Replace('sealed class Group','public sealed class Group').Replace('sealed class Joint','public sealed class Joint').Replace('sealed class ContactPair','public sealed class ContactPair')
$world=$world.Replace('static double Separation(','double Separation(').Replace('static Vector3D Support(','Vector3D Support(')
$world=Change-One $world 'double Clock, ScanAge;' 'double Clock => P.Clock;'
$world=Change-One $world 'ScanAge = 0;' ''
$world=$world.Replace('public long Tag;','public long Tag; public bool Rod;')
$world="sealed class PlanningWorld { readonly Program P; IMyGridTerminalSystem GridTerminalSystem => P.GridTerminalSystem; IMyGridProgramRuntimeInfo Runtime => P.Runtime; IMyProgrammableBlock Me => P.Me;`n"+$world+"`n"+[IO.File]::ReadAllText((Join-Path $workspace 'src/AutoArm.PlanningWorld.cs.txt'))+"`n}`n"
$planner=[IO.File]::ReadAllText((Join-Path $workspace 'src/AutoArm.Planner.cs.txt'))+"`n"+$world
$armPath=Join-Path $workspace 'AutoArm_JointPlanner_Prototype_Source.txt';$plannerPath=Join-Path $workspace 'AutoArm_Planner_Prototype_Source.txt'
[IO.File]::WriteAllText($armPath,$arm,[Text.UTF8Encoding]::new($false));[IO.File]::WriteAllText($plannerPath,$planner,[Text.UTF8Encoding]::new($false))
$taskOldCli=$env:DOTNET_CLI_HOME
try {
    $env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.dotnet'
    foreach($path in @($armPath,$plannerPath)){
        & dotnet run --project (Join-Path $PSScriptRoot 'ScriptPack') -- specialize $path $path
        if($LASTEXITCODE -ne 0){throw 'Planner specialization failed.'}
        & dotnet run --project (Join-Path $PSScriptRoot 'ScriptPack') -- blocks $path $path
        if($LASTEXITCODE -ne 0){throw 'PB expression body expansion failed.'}
    }
}finally{$env:DOTNET_CLI_HOME=$taskOldCli}
if(-not $SourceOnly){
    & (Join-Path $PSScriptRoot 'Build.ps1') -Source $armPath -Output (Join-Path $workspace 'AutoArm_JointPlanner_Prototype_Compact.txt') -PreserveBodies
    & (Join-Path $PSScriptRoot 'Build.ps1') -Source $plannerPath -Output (Join-Path $workspace 'AutoArm_Planner_Prototype_Compact.txt') -Fast -PreserveBodies
    foreach($name in @('AutoArm_JointPlanner_Prototype_Compact.txt','AutoArm_Planner_Prototype_Compact.txt')){
        $path=Join-Path $workspace $name
        & dotnet run --project (Join-Path $PSScriptRoot 'ScriptPack') -- blocks-min $path $path
        if($LASTEXITCODE -ne 0 -or [IO.File]::ReadAllText($path).Length -gt 100000){throw 'PB-safe compact expansion failed/oversized.'}
    }
    & dotnet run --project (Join-Path $PSScriptRoot 'PBCompileChecks') -- --full $armPath (Join-Path $workspace 'AutoArm_JointPlanner_Prototype_Compact.txt') $plannerPath (Join-Path $workspace 'AutoArm_Planner_Prototype_Compact.txt')
    if($LASTEXITCODE -ne 0){throw 'Installed PB compiler rewrite failed.'}
    & dotnet run --project (Join-Path $PSScriptRoot 'PBCompileChecks') -- --full --no-unused $plannerPath (Join-Path $workspace 'AutoArm_Planner_Prototype_Compact.txt')
    if($LASTEXITCODE -ne 0){throw 'Planner has unused fields or failed game compilation.'}
}
