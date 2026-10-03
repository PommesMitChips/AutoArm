function Set-BuildContext {
 param([string]$TrackRoot,[string]$RepoRoot,[string]$GameBin)
 $script:workspace=$TrackRoot; $script:RepositoryRoot=$RepoRoot; $script:GameBin=$GameBin
 $script:ReleaseVersion=(Get-Content -LiteralPath (Join-Path $TrackRoot "release.json") -Raw | ConvertFrom-Json).version
 if ($script:ReleaseVersion -notmatch "^[0-9]+\.[0-9]+(?:\.[0-9]+)?(?:-[a-zA-Z0-9._-]+)?$") { throw "Invalid release version." }
}
function Resolve-PartName {
 param([string]$Name)
 $matches=@(Get-ChildItem -LiteralPath (Join-Path $script:workspace 'src') -Recurse -File -Filter $Name)
 if ($matches.Count -ne 1) { throw "Expected exactly one source fragment $Name; found $($matches.Count)." }
 return [IO.Path]::GetRelativePath($script:workspace,$matches[0].FullName)
}
function Read-Part { param([string]$Name) [IO.File]::ReadAllText((Join-Path $script:workspace (Resolve-PartName $Name))).Replace("@@VERSION@@",$script:ReleaseVersion) }
function Invoke-ManagedTool {
 param([string]$Project,[string[]]$Arguments)
 & dotnet run --project (Join-Path $script:RepositoryRoot ('tools/'+$Project)) "-p:GameBin=$script:GameBin" -- @Arguments
 if ($LASTEXITCODE -ne 0) { throw "$Project failed: $Arguments" }
}
function Invoke-Pack {
 param([string]$Source,[string]$Output,[string]$GameBin=$script:GameBin,[switch]$Fast,[switch]$PreserveBodies)
 $mode=if($PreserveBodies){if($Fast){'pack-pb'}else{'pack-safe'}}elseif($Fast){'pack-fast'}else{'pack'}
 Invoke-ManagedTool ScriptPack @($mode,$Source,$Output)
}
function Build-CoreScript {
 param([string]$Name,[switch]$SourceOnly)
 $workspace=$script:workspace
 $cache=Join-Path $workspace '.build'; [IO.Directory]::CreateDirectory($cache)|Out-Null
 if($Name -in @('AutoArm','ToolSwap')) {
  $parts=if($Name -eq 'AutoArm'){@('Prefix','Topology','Config','Control','SavedDrives','Math','Pivot','ArmTip','ToolControl','Link','Path','ToolClient','Services','Safety')}else{@('ToolPrefix','ToolHost','SavedDrives','TopInfo','ToolGeometry','Tools','ToolDiscovery','ParkPoses','Math','Link')}
  $engine=($parts|ForEach-Object {Read-Part ('AutoArm.'+$_+'.cs.txt')}) -join "`n`n"
  $engineName=if($Name -eq 'AutoArm'){'AutoArm.Engine.txt'}else{'AutoArm.ToolEngine.txt'}
  [IO.File]::WriteAllText((Join-Path $cache $engineName),$engine,[Text.UTF8Encoding]::new($false))
  $source=Build-HostScript $engine ($Name -eq 'ToolSwap')
 }else{ $source=Read-Part ('AutoArm.'+$Name+'.cs.txt') }
 $stem=if($Name -eq 'AutoArm'){'AutoArm'}else{'AutoArm_'+$Name}
 $sourcePath=Join-Path $workspace ($stem+'_Source.txt'); $output=Join-Path $workspace ($stem+'_Compact.txt')
 [IO.File]::WriteAllText($sourcePath,$source,[Text.UTF8Encoding]::new($false))
 if($Name -in @('AutoArm','ToolSwap')){Invoke-ManagedTool ScriptPack @('specialize',$sourcePath,$sourcePath)}
 if(-not $SourceOnly){
  if($Name -in @('Bench','Survey')){Invoke-ManagedTool ScriptPack @('pack-pb',$sourcePath,$output)}
  elseif($Name -eq 'Collision'){Invoke-Pack $sourcePath $output -Fast -PreserveBodies}
  else{Invoke-Pack $sourcePath $output}
 }
 $rewrite=@('--combined',$sourcePath);if(-not $SourceOnly){$rewrite+=@($output)}
 Invoke-ManagedTool PBCompileChecks $rewrite
}
function Build-HostScript([string]$engine, [bool]$tool) {
    $hostText = [IO.File]::ReadAllText((Join-Path $workspace (Resolve-PartName 'AutoArm.Host.cs.txt'))).Replace('@@TOOL@@',$(if ($tool) {'true'} else {'false'})).Replace('@@FORMAT@@',$(if ($tool) {'2'} else {'7'}))
    $hostText = $hostText.Replace('@@VERSION@@',$script:ReleaseVersion)
    $coreText = [regex]::Replace($engine,'\bProgram\b','ArmCore')
    # Standalone fixtures supply their own input boundary; hosted arms use Config.
    $coreText = [regex]::Replace($coreText,'(?s)// BEGIN STANDALONE CONFIG.*?// END STANDALONE CONFIG','')
    $coreText = $coreText.Replace('readonly string ArmName = "Arm 1";','readonly string ArmName;')
    if (-not $tool) {
        $coreText = $coreText.Replace('long PilotSeatId => 0;', 'long PilotSeatId => Host.PilotSeatFor(Name);')
        $coreText = $coreText.Replace('bool SeatAvailable(IMyShipController c) { return true; }', 'bool SeatAvailable(IMyShipController c) { return Host.SeatAvailableFor(Name, c.EntityId); }')
    }
    $coreText = $coreText.Replace('Me.CustomData','CustomData').Replace('Runtime.UpdateFrequency','Frequency').Replace('Runtime.TimeSinceLastRun.TotalSeconds','Elapsed')
    $coreText = $coreText.Replace('P.IGC.UnicastListener.HasPendingMessage','P.Inbox.Count > 0').Replace('P.IGC.UnicastListener.AcceptMessage()','P.Inbox.Dequeue()')
    $coreText = $coreText.Replace('IGC.UnicastListener.HasPendingMessage','Inbox.Count > 0').Replace('IGC.UnicastListener.AcceptMessage()','Inbox.Dequeue()')
    $coreText = $coreText.Replace('"AutoArm/4"','"AutoArm/5"').Replace('"Version", 4','"Version", 5').Replace('"Version").ToInt32() == 4','"Version").ToInt32() == 5')
    $coreText = [regex]::Replace($coreText,'public ArmCore\(\)\s*\{','public ArmCore(Program host, string name, MyIni config, string storage) { Host = host; ArmName = name; Config = config; Storage = storage;')
    if (-not $tool) { $coreText = $coreText.Replace('bool CheckArmLayout(out string why) { why = ""; return true; }','bool CheckArmLayout(out string why) { return Host.Layout(this, out why); }') }
    else { $coreText = $coreText.Replace('if (!Tools.Load(out why)) throw new Exception(why);','RefreshConfig(); if (!Tools.Load(out why) || !Tools.NamesForArm(out why)) throw new Exception(why);') }
    $facadeText = [IO.File]::ReadAllText((Join-Path $workspace (Resolve-PartName 'AutoArm.CoreFacade.cs.txt')))
    $roleText = [IO.File]::ReadAllText((Join-Path $workspace $(if ($tool) {(Resolve-PartName 'AutoArm.ToolFacade.cs.txt')} else {(Resolve-PartName 'AutoArm.ArmFacade.cs.txt')})))
    return $hostText + "`nsealed class ArmCore {`n" + $facadeText + "`n" + $coreText + "`n" + $roleText + "`n}`n"
}

function Invoke-ReallocationRecipe {
param([string]$workspace,[string]$RepositoryRoot,[switch]$SourceOnly)
$ErrorActionPreference = 'Stop'
$script:source = [IO.File]::ReadAllText((Join-Path $workspace 'AutoArm_Source.txt'))
function Replace-One([string]$old, [string]$new) {
    if (($script:source.Split(@($old), [StringSplitOptions]::None)).Count -ne 2) { throw "Arm prototype hook changed: $old" }
    $script:source = $script:source.Replace($old, $new)
}
$script:source = $script:source.Replace('"1.1"', '"1.1-reallocation"')
Replace-One 'if (protection != null) protection.Limit(LastQdot);' 'if (protection != null) protection.Constrain(LastQdot);'
Replace-One 'public void Limit(double[] rates)' ([IO.File]::ReadAllText((Join-Path $workspace (Resolve-PartName 'AutoArm.ConstraintRepair.cs.txt'))) + "`npublic void Limit(double[] rates)")
Replace-One 'ini.Set(LinkSection, "SafetyConstraints", Rows.Count);' 'ini.Set(LinkSection, "SafetyReallocated", Reallocated); ini.Set(LinkSection, "SafetyRepairPasses", RepairPasses); ini.Set(LinkSection, "SafetyRepairBudget", RepairBudget); ini.Set(LinkSection, "SafetyConstraints", Rows.Count);'
[IO.File]::WriteAllText((Join-Path $workspace 'AutoArm_Reallocation_Prototype_Source.txt'), $script:source, [Text.UTF8Encoding]::new($false))
if (-not $SourceOnly) { & ${function:Invoke-Pack} -Source AutoArm_Reallocation_Prototype_Source.txt -Output AutoArm_Reallocation_Prototype_Compact.txt }

}
function Invoke-SelfPairsRecipe {
param([string]$workspace,[string]$RepositoryRoot,[switch]$SourceOnly)
$ErrorActionPreference = 'Stop'
$script:source = (Read-Part 'AutoArm.Collision.cs.txt')
function Replace-One([string]$old, [string]$new) {
    if (($script:source.Split(@($old), [StringSplitOptions]::None)).Count -ne 2) { throw "Prototype hook changed: $old" }
    $script:source = $script:source.Replace($old, $new)
}
Replace-One '"1.1"' '"1.1-self-pairs"'
Replace-One 'Ready = true; Arms.Clear(); RefreshScene(true);' 'ConfigureSelfPairs(ini); Ready = true; Arms.Clear(); RefreshScene(true);'
Replace-One 'shape.Cursor = shape.Min; shape.Shapes.Clear();' 'shape.Cursor = shape.Min; SelfGeometryRevision++; shape.Shapes.Clear();'
Replace-One 'TilesVisited = CandidatesSeen = NarrowChecks = 0;' 'PrepareSelfPairs(arm); TilesVisited = CandidatesSeen = NarrowChecks = 0;'
Replace-One 'double range = QueryRange(arm, pair.Value, body, obstacle);' "double range = QueryRange(arm, pair.Value, body, obstacle);`n                if (SkipSelfPair(arm, body, obstacle, range)) continue;"
Replace-One '+ (Detailed ? ContactAudit() : ""));' '+ (Detailed ? ContactAudit() : "") + SelfPairInfo());'
$script:source += "`n" + [IO.File]::ReadAllText((Join-Path $workspace (Resolve-PartName 'AutoArm.SelfPairs.cs.txt')))
[IO.File]::WriteAllText((Join-Path $workspace 'AutoArm_Collision_Prototype_Source.txt'), $script:source, [Text.UTF8Encoding]::new($false))
if (-not $SourceOnly) {
    & ${function:Invoke-Pack} -Source AutoArm_Collision_Prototype_Source.txt -Output AutoArm_Collision_Prototype_Compact.txt -Fast -PreserveBodies
}

}
function Invoke-PassingRecipe {
param([string]$workspace,[string]$RepositoryRoot,[switch]$SourceOnly)
$ErrorActionPreference = 'Stop'
function Edit-One([string]$text,[string]$old,[string]$new) {
    if (($text.Split(@($old),[StringSplitOptions]::None)).Count -ne 2) {throw "Navigation hook changed: $old"}
    return $text.Replace($old,$new)
}
$arm = [IO.File]::ReadAllText((Join-Path $workspace 'AutoArm_Source.txt')).Replace('"1.1"','"1.1-passing"')
$arm = Edit-One $arm 'public void Limit(double[] rates)' ([IO.File]::ReadAllText((Join-Path $workspace (Resolve-PartName 'AutoArm.NavigationArm.cs.txt')))+"`npublic void Limit(double[] rates)")
$arm = Edit-One $arm 'public void Limit(double[] rates)' ([IO.File]::ReadAllText((Join-Path $workspace (Resolve-PartName 'AutoArm.ConstraintRepair.cs.txt')))+"`npublic void Limit(double[] rates)")
$arm = Edit-One $arm 'if (protection != null) protection.Limit(LastQdot);' 'if (protection != null) protection.Constrain(LastQdot);'
$arm = Edit-One $arm 'ini.Set(LinkSection, "SafetyConstraints", Rows.Count);' 'ini.Set(LinkSection, "SafetyReallocated", Reallocated); ini.Set(LinkSection, "SafetyRepairPasses", RepairPasses); ini.Set(LinkSection, "SafetyRepairBudget", RepairBudget); ini.Set(LinkSection, "SafetyConstraints", Rows.Count);'
$arm = Edit-One $arm 'ini.Set(LinkSection, "Caps", caps.ToString());' 'WriteNavigation(ini); ini.Set(LinkSection, "Caps", caps.ToString());'
$arm = Edit-One $arm 'Sent.Remove(ack); Bias = bias;' 'ReadNavigation(ini); Sent.Remove(ack); Bias = bias;'
$arm = Edit-One $arm 'ini.Set(LinkSection, "SafetyConstraints", Rows.Count);' 'NavigationStatus(ini); ini.Set(LinkSection, "SafetyConstraints", Rows.Count);'
$arm = Edit-One $arm 'public bool Docking, Capture;' 'public bool Docking, Capture; public string NavBudgetKey = ""; public double NavBudgetDistance;'
$arm = Edit-One $arm 'MatrixD goal = path.Current, demand = goal;' "MatrixD goal = path.Current, demand = goal;`n    if (Services != null && Services.Safety != null) Services.Safety.Navigate(path, ref demand);"
[IO.File]::WriteAllText((Join-Path $workspace 'AutoArm_Passing_Prototype_Source.txt'),$arm,[Text.UTF8Encoding]::new($false))
$collision = [IO.File]::ReadAllText((Join-Path $workspace 'AutoArm_Collision_Source.txt')).Replace('"1.1"','"1.1-passing"')
$collision = Edit-One $collision 'public int AuditIndex = -1;' 'public bool NavAllowed; public string NavKey; public Vector3D NavHead, NavGoal; public double NavSpeed; public PassingLane Lane; public int AuditIndex = -1;'
$collision = Edit-One $collision 'ParseArm(frame, ini);' 'ParseArm(frame, ini); ReadNavigation(frame, ini, arm);'
$collision = Edit-One $collision 'scale = 0;' 'if (!arm.NavAllowed) scale = 0;'
$collision = Edit-One $collision 'Status = arm.Name + ": " + reason;' 'Status = arm.Name + ": " + reason; PassingGuidance(arm, response);'
$collision += "`n" + [IO.File]::ReadAllText((Join-Path $workspace (Resolve-PartName 'AutoArm.NavigationCollision.cs.txt')))
[IO.File]::WriteAllText((Join-Path $workspace 'AutoArm_Collision_Passing_Prototype_Source.txt'),$collision,[Text.UTF8Encoding]::new($false))
$priorCliHome=$env:DOTNET_CLI_HOME
try {
    $env:DOTNET_CLI_HOME=Join-Path $RepositoryRoot 'tools/.dotnet'
    & dotnet run --project (Join-Path $RepositoryRoot 'tools/ScriptPack') -- blocks (Join-Path $workspace 'AutoArm_Passing_Prototype_Source.txt') (Join-Path $workspace 'AutoArm_Passing_Prototype_Source.txt')
    if ($LASTEXITCODE -ne 0) {throw 'Navigation source block expansion failed.'}
} finally {$env:DOTNET_CLI_HOME=$priorCliHome}
if (-not $SourceOnly) {
    & ${function:Invoke-Pack} -Source AutoArm_Passing_Prototype_Source.txt -Output AutoArm_Passing_Prototype_Compact.txt -PreserveBodies
    & ${function:Invoke-Pack} -Source AutoArm_Collision_Passing_Prototype_Source.txt -Output AutoArm_Collision_Passing_Prototype_Compact.txt -PreserveBodies
    $priorCliHome=$env:DOTNET_CLI_HOME
    try {
        $env:DOTNET_CLI_HOME=Join-Path $RepositoryRoot 'tools/.dotnet'
        foreach($name in @('AutoArm_Passing_Prototype_Compact.txt','AutoArm_Collision_Passing_Prototype_Compact.txt')) {
            $path=Join-Path $workspace $name
            & dotnet run --project (Join-Path $RepositoryRoot 'tools/ScriptPack') -- blocks-min $path $path
            if ($LASTEXITCODE -ne 0) {throw 'Generated forwarding body expansion failed.'}
            if ([IO.File]::ReadAllText($path).Length -gt 100000) {throw 'Prototype exceeds PB editor limit.'}
        }
    } finally {$env:DOTNET_CLI_HOME=$priorCliHome}
}

}
function Invoke-JointPlannerRecipe {
param([string]$workspace,[string]$RepositoryRoot,[switch]$SourceOnly)
$ErrorActionPreference='Stop'
function Change-One([string]$text,[string]$old,[string]$new) {
    $text=$text.Replace("`r`n","`n");$old=$old.Replace("`r`n","`n");$new=$new.Replace("`r`n","`n")
    if(($text.Split(@($old),[StringSplitOptions]::None)).Count -ne 2){throw "Joint planning hook changed: $old"}
    return $text.Replace($old,$new)
}
$arm=[IO.File]::ReadAllText((Join-Path $workspace 'AutoArm_Source.txt')).Replace('"1.1"','"1.1-joint-prototype"')
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
$arm=$arm.TrimEnd();$arm=$arm.Substring(0,$arm.Length-1)+[IO.File]::ReadAllText((Join-Path $workspace (Resolve-PartName 'AutoArm.JointGuide.cs.txt')))+"`n}`n"
$world=(Read-Part 'AutoArm.Collision.cs.txt')
$start=$world.IndexOf('public Program()');$end=$world.IndexOf('bool Budget()');$world=$world.Remove($start,$end-$start)
$start=$world.IndexOf('bool Authorised(');$end=$world.IndexOf('void ParseArm(');$world=$world.Remove($start,$end-$start)
foreach($receiver in @('body','obstacle','grid','a','b')){$world=$world.Replace("$receiver.Grid.WorldMatrix","World($receiver)")}
$world=$world.Replace('sealed class Arm','public sealed class Arm').Replace('sealed class Group','public sealed class Group').Replace('sealed class Joint','public sealed class Joint').Replace('sealed class ContactPair','public sealed class ContactPair')
$world=$world.Replace('static double Separation(','double Separation(').Replace('static Vector3D Support(','Vector3D Support(')
$world=Change-One $world 'double Clock, ScanAge;' 'double Clock => P.Clock;'
$world=Change-One $world 'ScanAge = 0;' ''
$world=$world.Replace('public long Tag;','public long Tag; public bool Rod;')
$world="sealed class PlanningWorld { readonly Program P; IMyGridTerminalSystem GridTerminalSystem => P.GridTerminalSystem; IMyGridProgramRuntimeInfo Runtime => P.Runtime; IMyProgrammableBlock Me => P.Me;`n"+$world+"`n"+[IO.File]::ReadAllText((Join-Path $workspace (Resolve-PartName 'AutoArm.PlanningWorld.cs.txt')))+"`n}`n"
$planner=[IO.File]::ReadAllText((Join-Path $workspace (Resolve-PartName 'AutoArm.Planner.cs.txt')))+"`n"+$world
$armPath=Join-Path $workspace 'AutoArm_JointPlanner_Prototype_Source.txt';$plannerPath=Join-Path $workspace 'AutoArm_Planner_Prototype_Source.txt'
[IO.File]::WriteAllText($armPath,$arm,[Text.UTF8Encoding]::new($false));[IO.File]::WriteAllText($plannerPath,$planner,[Text.UTF8Encoding]::new($false))
$taskOldCli=$env:DOTNET_CLI_HOME
try {
    $env:DOTNET_CLI_HOME=Join-Path $RepositoryRoot 'tools/.dotnet'
    foreach($path in @($armPath,$plannerPath)){
        & dotnet run --project (Join-Path $RepositoryRoot 'tools/ScriptPack') -- specialize $path $path
        if($LASTEXITCODE -ne 0){throw 'Planner specialization failed.'}
        & dotnet run --project (Join-Path $RepositoryRoot 'tools/ScriptPack') -- blocks $path $path
        if($LASTEXITCODE -ne 0){throw 'PB expression body expansion failed.'}
    }
}finally{$env:DOTNET_CLI_HOME=$taskOldCli}
if(-not $SourceOnly){
    & ${function:Invoke-Pack} -Source $armPath -Output (Join-Path $workspace 'AutoArm_JointPlanner_Prototype_Compact.txt') -PreserveBodies
    & ${function:Invoke-Pack} -Source $plannerPath -Output (Join-Path $workspace 'AutoArm_Planner_Prototype_Compact.txt') -Fast -PreserveBodies
    foreach($name in @('AutoArm_JointPlanner_Prototype_Compact.txt','AutoArm_Planner_Prototype_Compact.txt')){
        $path=Join-Path $workspace $name
        & dotnet run --project (Join-Path $RepositoryRoot 'tools/ScriptPack') -- blocks-min $path $path
        if($LASTEXITCODE -ne 0 -or [IO.File]::ReadAllText($path).Length -gt 100000){throw 'PB-safe compact expansion failed/oversized.'}
    }
    & dotnet run --project (Join-Path $RepositoryRoot 'tools/PBCompileChecks') -- --full $armPath (Join-Path $workspace 'AutoArm_JointPlanner_Prototype_Compact.txt') $plannerPath (Join-Path $workspace 'AutoArm_Planner_Prototype_Compact.txt')
    if($LASTEXITCODE -ne 0){throw 'Installed PB compiler rewrite failed.'}
    & dotnet run --project (Join-Path $RepositoryRoot 'tools/PBCompileChecks') -- --full --no-unused $plannerPath (Join-Path $workspace 'AutoArm_Planner_Prototype_Compact.txt')
    if($LASTEXITCODE -ne 0){throw 'Planner has unused fields or failed game compilation.'}
}

}

function Build-PrototypeSnapshot {
 param([string]$Name,[switch]$SourceOnly)
 $stems=@{Reallocation='AutoArm_Reallocation_Prototype';PassingArm='AutoArm_Passing_Prototype';PassingCollision='AutoArm_Collision_Passing_Prototype';JointPlannerArm='AutoArm_JointPlanner_Prototype';Planner='AutoArm_Planner_Prototype'}
 $source=Join-Path $script:workspace ($stems[$Name]+'_Source.txt')
 $output=Join-Path $script:workspace ($stems[$Name]+'_Compact.txt')
 [IO.File]::WriteAllText($source,(Read-Part ($Name+'.Program.cs.txt')),[Text.UTF8Encoding]::new($false))
 if(-not $SourceOnly){
  if($Name -eq 'Reallocation'){Invoke-Pack $source $output}
  elseif($Name -eq 'Planner'){Invoke-Pack $source $output -Fast -PreserveBodies}
  else{Invoke-Pack $source $output -PreserveBodies}
  if($Name -ne 'Reallocation'){Invoke-ManagedTool ScriptPack @('blocks-min',$output,$output)}
 }
 $args=@('--combined',$source);if(-not $SourceOnly){$args+=@($output)}
 Invoke-ManagedTool PBCompileChecks $args
}
