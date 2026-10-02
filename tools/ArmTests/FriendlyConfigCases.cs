using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;

internal static partial class Scenarios
{
    static MyIni FCIni(string text)
    {
        var ini=new MyIni();Check(ini.TryParse(text),"Friendly configuration is not valid INI.");return ini;
    }
    static string[] FCRows(MyIni ini)=>ini.Get("Config","Actuators").ToString().Replace("\r","").Split('\n').Where(x=>x.Trim().Length>0).ToArray();
    static string FCRow(string row,double move,double turn)
    {
        var p=row.Split('|');p[2]=" "+move.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+" ";p[3]=" "+turn.ToString("R",System.Globalization.CultureInfo.InvariantCulture);return string.Join("|",p);
    }
    static string FCSig(object group)=>(string)Get(group,"Sig")!;
    static double FCWeight(object group,string name)=>Convert.ToDouble(Get(group,name));
    static MyIni FCStorageIni(string text){var ini=new MyIni();Check(ini.TryParse(text),"Generated Storage metadata is not INI.");return ini;}

    static void FriendlyConfigCases(Type type)
    {
        FCStrictFormatAndRows(type);
        FCNamesAndTopology(type);
        FCSettingsAndAtomicRefusal(type);
        FCMetadataAndReset(type);
        FCStorageCoexistence(type);
        FCReportedLayoutAndHome(type);
        Console.WriteLine("Friendly configuration: strict Format 6, readable/reordered rows, stable identities, atomic refusal, settings/bindings, topology evolution, reset and Storage coexistence.");
    }

    static void FCStrictFormatAndRows(Type type)
    {
        var rig=Fixtures.Serial(out _,out _,out _,out _);var script=Start(type,rig);var groups=Groups(script);
        string sig0=FCSig(groups[0]),sig1=FCSig(groups[1]);
        string current=rig.PB.CustomData,storage=((TestHost)script).Storage;
        Check(!FCIni(current).ContainsSection("Instructions"),"Generated Custom Data still contains removed Instructions section.");
        Check(FCIni(current).Get("AutoArm","Format").ToInt32()==6,"Blank Custom Data did not generate Format 6.");
        var legacy=new MyIni();legacy.Set("MArmOS","Format",1);
        legacy.Set("MArmOS Group 01","Signature",sig0);legacy.Set("MArmOS Group 01","Translation",.25);legacy.Set("MArmOS Group 01","Orientation",2.5);
        legacy.Set("MArmOS Group 02","Signature",sig1);legacy.Set("MArmOS Group 02","Translation",.75);legacy.Set("MArmOS Group 02","Orientation",1.5);
        string rejected=legacy.ToString();Run(script,"On");rig.Log.Clear();
        RecordProxy.Of(rig.PB).Values["CustomData"]=rejected;Run(script,"Reload");
        Check(!Enabled(script)&&rig.PB.CustomData==rejected&&((TestHost)script).Storage==storage,"Legacy configuration was migrated or changed external state.");
        Check(groups.All(g=>FCWeight(g,"MoveW")==1&&FCWeight(g,"TurnW")==1),"Rejected legacy weights were applied.");
        Check(rig.Log.Any(x=>x.Contains("delete Custom Data",StringComparison.OrdinalIgnoreCase)),"Legacy rejection did not explain how to regenerate Custom Data.");
        Run(script,"On");Check(!Enabled(script),"On bypassed the rejected legacy configuration.");NoVelocity(rig);

        RecordProxy.Of(rig.PB).Values["CustomData"]=current;Run(script,"Reload");
        var modern=FCIni(rig.PB.CustomData);var rows=FCRows(modern);Check(rows.Length==3,"Generated table did not contain header plus two rows.");
        string firstName=rows[1].Split('|')[0].Trim(),secondName=rows[2].Split('|')[0].Trim();
        rows[1]=FCRow(rows[1],.31,3.1);rows[2]=FCRow(rows[2],.82,1.2);
        modern.Set("Other","Keep","untouched");
        modern.Set("Config","Actuators",string.Join("\n",new[]{rows[0],rows[2],rows[1]}));
        RecordProxy.Of(rig.PB).Values["CustomData"]=modern.ToString();Run(script,"Reload");
        var bySig=Groups(script).ToDictionary(FCSig);
        Check(FCWeight(bySig[sig0],"MoveW")==.31&&FCWeight(bySig[sig0],"TurnW")==3.1&&FCWeight(bySig[sig1],"MoveW")==.82&&FCWeight(bySig[sig1],"TurnW")==1.2,"Reordering readable rows changed identity mapping.");
        Check(FCIni(rig.PB.CustomData).Get("Other","Keep").ToString()=="untouched","Current configuration lost unrelated sections.");
        Check(firstName!=secondName,"Generated row names unexpectedly collided.");

        RecordProxy.Of(rig.PB).Values["CustomData"]=" \r\n\t";Run(script,"Reload");
        var defaults=FCIni(rig.PB.CustomData);
        Check(defaults.Get("AutoArm","Format").ToInt32()==6&&defaults.Get("AutoArm","Arm").ToString()=="Arm 1","Whitespace Custom Data did not regenerate current defaults.");
        Check(Groups(script).All(g=>FCWeight(g,"MoveW")==1&&FCWeight(g,"TurnW")==1),"Blank Custom Data retained previous actuator preferences.");
    }

    static void FCNamesAndTopology(Type type)
    {
        var duplicate=Fixtures.Rails();var pistons=duplicate.Blocks.OfType<IMyPistonBase>().ToArray();
        RecordProxy.Of(pistons[0]).Values["CustomName"]="Twin";RecordProxy.Of(pistons[1]).Values["CustomName"]="Twin";
        var duplicateScript=Start(type,duplicate);var names=FCRows(FCIni(duplicate.PB.CustomData)).Skip(1).Select(x=>x.Split('|')[0].Trim()).ToArray();
        Check(names.Length==names.Distinct().Count()&&names.All(x=>char.IsDigit(x[0])),"Ordinal generated names did not disambiguate duplicate block names.");

        var parallel=Fixtures.Parallel(false,out var a,out var b);RecordProxy.Of(a).Values["CustomName"]="Arm 1 - Base | Main\r\nA";RecordProxy.Of(b).Values["CustomName"]="Partner|B\nC";
        Start(type,parallel);string tableText=FCIni(parallel.PB.CustomData).Get("Config","Actuators").ToString();
        Check(tableText.Contains("Rotor x2")&&!tableText.Contains("\r")&&FCRows(FCIni(parallel.PB.CustomData)).Skip(1).All(x=>x.Split('|').Length==4),"Parallel stage layout was not rendered into a four-column table.");

        var changed=Fixtures.Serial(out _,out var oldPiston,out var head,out _);var oldScript=Start(type,changed);var oldGroups=Groups(oldScript);
        string rootSig=FCSig(oldGroups[0]);var ini=FCIni(changed.PB.CustomData);var rows=FCRows(ini);rows[1]=FCRow(rows[1],.37,2.7);ini.Set("Config","Actuators",string.Join("\n",rows));
        RecordProxy.Of(changed.PB).Values["CustomData"]=ini.ToString();Run(oldScript,"Reload");
        ini=FCIni(changed.PB.CustomData);ini.Delete("Config","Actuators");RecordProxy.Of(changed.PB).Values["CustomData"]=ini.ToString(); changed.Storage=((TestHost)oldScript).Storage;var middle=changed.Grid();RecordProxy.Of(oldPiston).Values["TopGrid"]=middle;
        changed.Piston("new distal stage",middle,head.CubeGrid,new VRageMath.Vector3D(0,0,-7),VRageMath.Vector3D.Forward);
        var rebuilt=Start(type,changed);var rebuiltGroups=Groups(rebuilt);var root=rebuiltGroups.Single(g=>FCSig(g)==rootSig);
        Check(FCWeight(root,"MoveW")==.37&&FCWeight(root,"TurnW")==2.7,"Changed topology lost the unchanged exact-signature row.");
        Check(rebuiltGroups.Where(g=>FCSig(g)!=rootSig).All(g=>FCWeight(g,"MoveW")==1&&FCWeight(g,"TurnW")==1),"New/changed joints did not receive default weights.");
    }

    static void FCSettingsAndAtomicRefusal(Type type)
    {
        var rig=Fixtures.Serial(out _,out _,out _,out _);var script=Start(type,rig);var ini=FCIni(rig.PB.CustomData);
        double[] values={.31,6,7,.6,.3,.7,.4,4,2,.8,.25,6,2,.6,.3,.22,4};
        string[] keys={"HeadSpeed","HeadTurnSpeed","StepAngle","JointSpeed","PistonSpeed","HomeJointSpeed","HomePistonSpeed","OrientationTolerance","JointAcceleration","PistonAcceleration","PositionCorrectionCap","OrientationCorrectionCap","TargetLead","PositionIntegralGain","OrientationIntegralGain","PositionIntegralCap","OrientationIntegralCap"};
        for(int i=0;i<keys.Length;i++)ini.Set("Config",keys[i],values[i]);
        ini.Set("Config","ReadKeyboard",false);ini.Set("Config","ReadRoll",false);
        RecordProxy.Of(rig.PB).Values["CustomData"]=ini.ToString();Run(script,"Reload");
        string[] fields={"MoveMps","TurnDeg","StepDeg","JointRpm","PistonMps","HomeJointRpm","HomePistonMps","OrientationToleranceDeg","JointAccelerationRpmPerSec","PistonAccelerationMps2","PositionCorrectionCap","OrientationCorrectionCapDeg","MaximumPilotTargetLeadM","PositionKi","OrientationKi","PositionIntegralCap","OrientationIntegralCapDeg"};
        for(int i=0;i<fields.Length;i++)Check(Math.Abs(Convert.ToDouble(Get(script,fields[i]))-values[i])<1e-12,"Config key did not apply to "+fields[i]+".");
        Check(!(bool)Get(script,"ReadKeyboard")!&&!(bool)Get(script,"ReadRoll")!,"Boolean settings were not applied.");
        for(int i=0;i<5;i++)Run(script);Run(script,"On");InvokeFrame(script,rig,"Forward",UpdateType.Terminal,0);Check((bool)Get(script,"AimingTarget")!,"Named face-direction action was not dispatched explicitly.");
        Run(script,"Stop");Check(!Enabled(script),"Named Stop did not stop control.");

        FCAtomic(type,"late numeric",x=>{x.Set("Config","HeadSpeed",.9);x.Set("Config","TargetLead",99);});
        FCAtomic(type,"late table",x=>{var r=FCRows(x);var p=r[r.Length-1].Split('|');p[3]=" broken";r[r.Length-1]=string.Join("|",p);x.Set("Config","Actuators",string.Join("\n",r));});
        FCAtomic(type,"invalid mode",x=>x.Set("Config","MovementMode","HRZ Stop"));
        FCAtomic(type,"invalid mouse",x=>x.Set("Config","ReadMouse","broken"));
        FCAtomic(type,"missing header",x=>x.DeleteSection("AutoArm"));
        FCAtomic(type,"unidentified version header",x=>{x.Clear();x.Set("MArmOS","Format",99);});
    }

    static void FCAtomic(Type type,string label,Action<MyIni> damage)
    {
        var rig=Fixtures.Serial(out _,out _,out _,out _);var script=Start(type,rig);Run(script,"On");Check(Enabled(script),label+" precondition failed.");
        double move=Convert.ToDouble(Get(script,"MoveMps")),weight=FCWeight(Groups(script)[0],"MoveW");string storage=((TestHost)script).Storage;
        var ini=FCIni(rig.PB.CustomData);damage(ini);string bad=ini.ToString();RecordProxy.Of(rig.PB).Values["CustomData"]=bad;Run(script,"Reload");
        Check(!Enabled(script),label+" did not leave controller Off.");Check(rig.PB.CustomData==bad&&((TestHost)script).Storage==storage,label+" mutated rejected external state.");
        Check(Convert.ToDouble(Get(script,"MoveMps"))==move&&FCWeight(Groups(script)[0],"MoveW")==weight,label+" partially applied runtime state.");
        Run(script,"On");Check(!Enabled(script)&&rig.PB.CustomData==bad&&((TestHost)script).Storage==storage,label+" was bypassed by On after Reload.");NoVelocity(rig);
    }

    static void FCMetadataAndReset(Type type)
    {
        FCMetadataRefusal(type,"missing",s=>"");
        FCMetadataRefusal(type,"corrupt rows",s=>{var i=FCStorageIni(s);i.Set("AutoArm Config","Rows","malformed");return i.ToString();});
        FCMetadataRefusal(type,"corrupt signature",s=>{var i=FCStorageIni(s);var rows=i.Get("AutoArm Config","Rows").ToString().Split('\n');rows[0]=rows[0].Split('|')[0]+"|Rgarbage";i.Set("AutoArm Config","Rows",string.Join("\n",rows));return i.ToString();});
        FCMetadataRefusal(type,"other PB",s=>{var i=FCStorageIni(s);i.Set("AutoArm Config","PB",-77);return i.ToString();});
        FCMetadataRefusal(type,"other Arm",s=>{var i=FCStorageIni(s);i.Set("AutoArm Config","Arm","Arm 2");return i.ToString();});
        var rig=Fixtures.Serial(out _,out _,out _,out _);var script=Start(type,rig);var ini=FCIni(rig.PB.CustomData);var rows=FCRows(ini);rows[1]=FCRow(rows[1],.2,4);ini.Set("Config","Actuators",string.Join("\n",rows));
        RecordProxy.Of(rig.PB).Values["CustomData"]=ini.ToString();Run(script,"Reload");Check(FCWeight(Groups(script)[0],"MoveW")==.2,"Reset precondition did not apply.");
        ini=FCIni(rig.PB.CustomData);ini.Set("Config","Actuators","");RecordProxy.Of(rig.PB).Values["CustomData"]=ini.ToString();Run(script,"Reload");
        Check(Groups(script).All(g=>FCWeight(g,"MoveW")==1&&FCWeight(g,"TurnW")==1),"Explicit blank Actuators did not reset all weights.");
        Check(FCRows(FCIni(rig.PB.CustomData)).Length==Groups(script).Length+1,"Blank reset did not regenerate readable rows.");
    }

    static void FCMetadataRefusal(Type type,string label,Func<string,string> mutate)
    {
        var rig=Fixtures.Serial(out _,out _,out _,out _);var script=Start(type,rig);string data=rig.PB.CustomData,changed=mutate(((TestHost)script).Storage);
        ((TestHost)script).Storage=changed;Run(script,"Reload");
        Check(!Enabled(script)&&rig.PB.CustomData==data&&((TestHost)script).Storage==changed,"Metadata "+label+" was not refused atomically.");
    }

    static void FCStorageCoexistence(Type type)
    {
        const string opaque="\r\nopaque legacy storage payload\r\n---\r\n[Other]\r\nOriginal=untouched\r\n\r\n";var rig=Fixtures.Serial(out _,out _,out _,out _);rig.Storage=opaque;var script=Start(type,rig);
        Check(((TestHost)script).Storage.Contains(opaque)&&((TestHost)script).Storage.Contains("[AutoArm Config]"),"Opaque legacy EndContent was not preserved with metadata.");
        var ini=FCIni(rig.PB.CustomData);var rows=FCRows(ini);rows[1]=FCRow(rows[1],.42,2.2);ini.Set("Config","Actuators",string.Join("\n",rows));RecordProxy.Of(rig.PB).Values["CustomData"]=ini.ToString();Run(script,"Reload");
        for(int i=0;i<5;i++)Run(script);Run(script,"SetHome");Run(script,"Reload");for(int i=0;i<5;i++)Run(script);
        string saved=((TestHost)script).Storage;Check(saved.Contains(opaque)&&saved.Contains("[AutoArm Config]")&&saved.Contains("[AutoArm Home]"),"SetHome/Reload did not preserve opaque data, metadata and Home together.");
        rig.Storage=saved;var rebuilt=Start(type,rig);
        Check(FCWeight(Groups(rebuilt)[0],"MoveW")==.42,"Recompile lost cached readable-row identity/weight.");
        Check((string)Get(rebuilt,"HomeFingerprint")==(string)Get(Get(rebuilt,"Topology")!,"Fingerprint")!,"Recompile lost matching Home while retaining config cache.");
        Check(((TestHost)rebuilt).Storage.Contains(opaque),"Recompile discarded opaque EndContent.");
        Check(FCStorageIni(((TestHost)rebuilt).Storage).EndContent==opaque,"Legacy Storage payload did not survive byte-for-byte.");
    }

    static void FCReportedLayoutAndHome(Type type)
    {
        var rig=ReportedTenRotaries(out var fingerprint);var script=Start(type,rig);string text=rig.PB.CustomData;
        Check(!text.Contains(fingerprint)&&!text.Contains("Signature=")&&!text.Contains("MArmOS"),"New layout exposed legacy branding or physical identities.");
        Check(FCRows(FCIni(text)).Length==11,"Reported ten-actuator table lost rows.");
        Check(FCIni(text).Get("AutoArm","Format").ToInt32()==6,"Generated preview does not use Format 6.");
        var exampleDirectory=Path.Combine(Tests.Workspace,"examples");
        Directory.CreateDirectory(exampleDirectory);
        File.WriteAllText(Path.Combine(exampleDirectory,"CustomData.ini"),text);
        Run(script,"SetHome");var saved=FCStorageIni(((TestHost)script).Storage);
        saved.DeleteSection("AutoArm Home");saved.Set("MArmOS Home","Fingerprint",fingerprint);
        foreach(var block in rig.Blocks.OfType<IMyMotorStator>())saved.Set("MArmOS Home","E"+block.EntityId,(double)block.Angle);
        ((TestHost)script).Storage=saved.ToString();Run(script,"Reload");
        Check((string)Get(script,"HomeFingerprint")! == fingerprint,"Old saved home did not migrate.");
        Check(((TestHost)script).Storage.Contains("[AutoArm Home]")&&!((TestHost)script).Storage.Contains("MArmOS"),"Old home branding was not removed.");

        var unrelated=Fixtures.Serial(out _,out _,out _,out _);
        RecordProxy.Of(unrelated.PB).Values["CustomData"]="[AutoArm]\nArm=Arm 1\nFormat=6\n[Lighting]\nFormat=1\n[Lighting Group 01]\nSignature=pattern-v1\n";
        Start(type,unrelated);var data=FCIni(unrelated.PB.CustomData);
        Check(data.Get("Lighting Group 01","Signature").ToString()=="pattern-v1","Unrelated signature namespace was mistaken for arm configuration.");
    }
}
