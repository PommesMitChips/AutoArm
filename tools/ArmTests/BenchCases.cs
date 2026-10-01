using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

internal static partial class Scenarios
{
    sealed class BenchRig
    {
        internal Rig Rig, Client, SafetyRig;
        internal ModuleBus Bus;
        internal object Arm, Bench;
        internal bool Move=true, Guides=true, Hold;
        readonly Dictionary<string,long> Seen=new(), Sequences=new();
        internal BenchRig(Type armType,Type benchType)
        {
            Rig=MultiFixture(out _,out _,out _);
            RecordProxy.Of(Rig.PB).Values["CustomName"]="Arm PB";
            Client=new Rig(Rig,"Arm Bench"); SafetyRig=new Rig(Rig,"Collision PB");
            Bus=new ModuleBus{ArmId=Rig.PB.EntityId};Bus.Bind(Rig);Bus.Bind(Client);Bus.Bind(SafetyRig);
            var config=new MyIni();config.TryParse(Rig.PB.CustomData);config.Set("global","Peers","Collision PB | Safety\nArm Bench | Plan");RecordProxy.Of(Rig.PB).Values["CustomData"]=config.ToString();
            Arm=Tests.Create(armType,Rig); HostReady(Arm,Rig,"Arm 1");HostReady(Arm,Rig,"Arm 2");
            Bench=Tests.Create(benchType,Client);
            var data=new MyIni();data.TryParse(Client.PB.CustomData);data.Set("Bench","ArmPB","Arm PB");data.Set("Bench","Reference","Arm 1 - Base - Rotor");
            foreach(string name in new[]{"Arm 1","Arm 2"})data.Set("Baseline "+name,"Pose",PCRow(Head(name).WorldMatrix*MatrixD.Invert(Reference.WorldMatrix)));
            RecordProxy.Of(Client.PB).Values["CustomData"]=data.ToString();
            for(int i=0;i<40;i++)Tick();
        }
        internal IMyTerminalBlock Reference=>Rig.Blocks.Single(b=>b.CustomName=="Arm 1 - Base - Rotor");
        internal IMyTerminalBlock Head(string name)=>Rig.Blocks.Single(b=>b.CustomName==name+" - Head - Drill");
        internal void Tick(string command="")
        {
            RecordProxy.Actor="Bench";HostFrame(Bench,Client,command);RecordProxy.Actor="Arm";HostFrame(Arm,Rig);RecordProxy.Actor="";
            if(Guides) foreach(string name in new[]{"Arm 1","Arm 2"})
            {
                MyIni? check=null;
                foreach(var item in Bus.Sent.AsEnumerable().Reverse())
                {var ini=new MyIni();ini.TryParse(item.Data);if(item.Source==Rig.PB.EntityId&&item.Target==SafetyRig.PB.EntityId&&ini.Get("Link","Operation").ToString()=="CHECK"&&ini.Get("Link","Arm").ToString()==name){check=ini;break;}}
                if(check==null)continue;long seq=check.Get("Link","Sequence").ToInt64();if(seq==Seen.GetValueOrDefault(name))continue;Seen[name]=seq;
                long next=Sequences.GetValueOrDefault(name)+1;Sequences[name]=next;
                SendGuide(Arm,Rig,SafetyRig,Bus,Guide(check,next,"0 0",hold:Hold));
            }
            // Protocol model: move markers toward accepted goals. This deliberately
            // does not claim to simulate SE joints, flex, collision or solver delivery.
            if(Move&&!Hold&&Guides)foreach(string name in new[]{"Arm 1","Arm 2"})
            {
                var path=Get(Core(Arm,name),"LocalPath");if(path==null||(bool)Get(path,"Done")!)continue;
                var goal=(MatrixD)Get(path,"Current")!;var head=Head(name);var frame=head.WorldMatrix;var delta=goal.Translation-frame.Translation;
                if(delta.Length()>0)frame.Translation+=delta*(Math.Min(delta.Length(),.05/60)/delta.Length());
                RecordProxy.Of(head).Values["WorldMatrix"]=frame;
            }
        }
        internal string Finish(string command,int frames=7000,Action<int>? step=null)
        {
            Tick(command);for(int i=0;i<frames&&(int)Get(Bench,"Phase")! !=0;i++){step?.Invoke(i);Tick();}
            Check((int)Get(Bench,"Phase")! ==0,"Bench did not finish: "+Client.Log.Last());return Client.Log.Last();
        }
        internal string Report {get{var ini=new MyIni();ini.TryParse(Client.PB.CustomData);return ini.EndContent;}}
        internal List<MyIni> Paths()=>Bus.Sent.Where(x=>x.Source==Client.PB.EntityId).Select(x=>{var p=new MyIni();p.TryParse(x.Data);return p;}).Where(p=>p.Get("Link","Operation").ToString()=="PATH").ToList();
    }
    internal static void BenchCases(Type armType,Type benchType)
    {
        var f=new BenchRig(armType,benchType);
        string status=f.Finish("Check");Check(status.StartsWith("CHECK PASS"),"Readiness check failed: "+status);Check(f.Paths().Count==0,"Check sent movement.");
        status=f.Finish("Smoke");Check(status.StartsWith("PASS:"),"Sequential smoke test failed: "+status+" / "+f.Paths().FirstOrDefault());
        var smoke=f.Paths();Check(smoke.Count==2&&smoke.Select(p=>p.Get("Link","Arm").ToString()).SequenceEqual(new[]{"Arm 1","Arm 2"}),"Smoke did not target both arms in order.");
        foreach(var p in smoke)
        {
            var rows=p.Get("Link","Waypoints").ToString().Split('|');Check(rows.Length==2,"Smoke did not include outward and return poses.");
            var first=PCFrame(rows[0]);var last=PCFrame(rows[1]);Check(Math.Abs(first.Translation.Y-last.Translation.Y-.1)<1e-8,"Smoke did not use matching outward excursion.");
            Check(first.Forward==last.Forward&&first.Up==last.Up,"Translation altered requested head orientation.");
            Check(p.Get("Link","Anchor").ToInt64()==f.Reference.EntityId&&p.Get("Link","Lines").ToString()=="true|true","Smoke did not submit anchored straight stages.");
            Check(p.Get("Link","Moves").ToString()=="0.05|0.05","Smoke speed exceeded the test cap.");
        }
        Check(f.Report.Contains("RETURN Arm 1")&&f.Report.Contains("RETURN Arm 2")&&f.Report.Contains("SAMPLE "),"Bench omitted measured return/report samples.");
        status=f.Finish("Run");Check(status.StartsWith("PASS:"),"Full shared-plane test failed: "+status);
        foreach(var p in f.Paths().Skip(2))
        {var rows=p.Get("Link","Waypoints").ToString().Split('|');Check(rows.Length==5,"Shared-plane run waypoint count changed.");var start=PCFrame(rows.Last());foreach(var row in rows)Check(Math.Abs(PCFrame(row).Translation.Z-start.Translation.Z)<1e-8,"Full run requested unsupported depth movement.");}
        // No joint/property writes from the planner; only its own report changes.
        foreach(var block in f.Rig.Blocks.Where(b=>b.EntityId!=f.Client.PB.EntityId))Check(RecordProxy.Of(block).ActorWrites.All(w=>w.Actor!="Bench"),"Planner wrote another block.");
        foreach(var message in f.Bus.Sent.Where(x=>x.Source==f.Client.PB.EntityId))
        {var p=new MyIni();p.TryParse(message.Data);Check(message.Target==f.Rig.PB.EntityId&&p.Get("Link","Operation").ToString() is "HELLO" or "PING" or "PATH" or "STOP","Bench bypassed ArmService.");}
        var blocked=new BenchRig(armType,benchType){Hold=true};for(int i=0;i<15;i++)blocked.Tick();
        status=blocked.Finish("Smoke");Check(status.Contains("Safety not granting")&&blocked.Paths().Count==0,"Held Safety permitted a test path.");
        var loss=new BenchRig(armType,benchType);status=loss.Finish("Smoke",step:i=>{if(i==35)loss.Guides=false;});
        Check(status.StartsWith("ABORT:")&&loss.Report.Contains("Safety held without progress")&&loss.Paths().Count==1,"Safety expiry started the next arm or forced a return.");
        Check(!Enabled(Core(loss.Arm,"Arm 1"))&&Enabled(Core(loss.Arm,"Arm 2")),"Safety abort stopped the wrong scope.");
        var cancel=new BenchRig(armType,benchType);cancel.Tick("Run");for(int i=0;i<45;i++)cancel.Tick();
        status=cancel.Finish("Cancel");Check(status.StartsWith("ABORT:")&&cancel.Paths().Count==1&&!Enabled(Core(cancel.Arm,"Arm 1")),"Cancel failed to stop its owned path or forced recovery.");
        var noSafety=new BenchRig(armType,benchType);var cfg=new MyIni();cfg.TryParse(noSafety.Rig.PB.CustomData);cfg.Set("global","Peers","Arm Bench | Plan");RecordProxy.Of(noSafety.Rig.PB).Values["CustomData"]=cfg.ToString();
        status=noSafety.Finish("Smoke");Check(status.Contains("Safety peer")&&noSafety.Paths().Count==0,"Configuration without Safety launched motion.");
        var drift=new BenchRig(armType,benchType);var moved=drift.Head("Arm 1").WorldMatrix;moved.Translation+=Vector3D.Right;RecordProxy.Of(drift.Head("Arm 1")).Values["WorldMatrix"]=moved;
        status=drift.Finish("Smoke");Check(status.Contains("surveyed baseline")&&drift.Paths().Count==0,"An unrelated start was accepted.");
        var stopped=new BenchRig(armType,benchType);status=stopped.Finish("Run",step:i=>{if(i==40)HostFrame(stopped.Arm,stopped.Rig,"Stop(Arm 1)");});
        Check(status.StartsWith("ABORT:")&&stopped.Paths().Count==1&&!Enabled(Core(stopped.Arm,"Arm 1")),"External Stop triggered restart or return.");
        var replyLoss=new BenchRig(armType,benchType);status=replyLoss.Finish("Run",step:i=>{if(i==35)replyLoss.Bus.DropArm=true;});
        for(int i=0;i<40;i++)replyLoss.Tick();
        Check(status.StartsWith("ABORT:")&&replyLoss.Paths().Count==1&&!Enabled(Core(replyLoss.Arm,"Arm 1")),"Reply loss kept ownership alive or launched another arm.");
        Console.WriteLine("Head-only two-arm runner protocol: PASS ("+Tests.Assertions+" assertions; mock poses, not a physics simulation).");
    }
    static MatrixD PCFrame(string row)
    {var v=row.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(x=>double.Parse(x,System.Globalization.CultureInfo.InvariantCulture)).ToArray();return MatrixD.CreateWorld(new Vector3D(v[0],v[1],v[2]),new Vector3D(v[3],v[4],v[5]),new Vector3D(v[6],v[7],v[8]));}
}
