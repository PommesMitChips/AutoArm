using System.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

internal static partial class Scenarios
{
    static void CollisionCapacityCases(Type armType,Type collisionType)
    {
        // Independent one-hot inequalities prove the producer neither drops
        // distinct contacts nor weakens them to fit its negotiated capacity.
        var rig=new Rig();var child=rig.Grid();var obstacle=rig.Grid();
        var service=PackedProgramChecks.Unwrap(Tests.Create(collisionType,rig));var owner=service.GetType();
        object Make(string n)=>Activator.CreateInstance(owner.GetNestedType(PackedProgramChecks.Name(owner,n),All)!,true)!;
        object Call(string n,params object[] a)=>owner.GetMethod(PackedProgramChecks.Name(owner,n),All)!.Invoke(service,a)!;
        var arm=Make("Arm");Put(arm,"Caps",Enumerable.Repeat(.1,24).ToArray());Put(arm,"Rates",new double[24]);
        for(int i=0;i<24;i++) {
            var piston=rig.Piston("joint "+i,rig.Root,child,axis:Vector3D.Right);
            var group=Make("Group");var member=Make("Joint");Put(member,"Block",piston);Put(member,"From",rig.Root.EntityId);Put(member,"To",child.EntityId);Put(member,"Sign",1);
            ((IList)Get(group,"Members")!).Add(member);((IList)Get(arm,"Groups")!).Add(group);
        }
        var body=Make("GridShape");Put(body,"Grid",child);var other=Make("GridShape");Put(other,"Grid",obstacle);
        var a=Make("Shape");var b=Make("Shape");Put(a,"Half",new Vector3D(1));Put(b,"Half",new Vector3D(1));
        var rows=new List<double[]>();
        bool Add(int i)=>(bool)Call("AddConstraint",arm,1<<i,other,a,body,b,.1,Vector3D.Right,.1,true,new double[24],rows);
        Check((int)Get(arm,"ConstraintLimit")! ==8,"Unadvertised producer capacity changed.");
        for(int i=0;i<8;i++)Check(Add(i),"Legacy independent row rejected too early.");
        Check(!Add(8)&&rows.Count==8,"Legacy client received more than eight rows.");
        var legacy=rows.Select(r=>r.ToArray()).ToArray();rows.Clear();Put(arm,"ConstraintLimit",16);
        for(int i=0;i<16;i++)Check(Add(i),"Expanded capacity rejected independent row "+i);
        Check(rows.Count==16&&!Add(16)&&rows.Count==16,"Capacity overflow dropped a required row or granted permission.");
        for(int i=0;i<8;i++)Check(rows[i].SequenceEqual(legacy[i]),"Expanded capacity weakened existing inequality.");
        for(int i=0;i<16;i++)Check(rows[i][i]>0&&rows[i].Where((_,j)=>j!=i).All(x=>x==0),"Independent contact inequality was altered.");

        // The receiving arm must validate every row, including the last one,
        // and reject oversized/malformed packets without partial replacement.
        var clientRig=new Rig();var mid=clientRig.Grid();var end=clientRig.Grid();
        clientRig.Piston("Arm 1 - Base",clientRig.Root,mid,axis:Vector3D.Right);
        clientRig.Piston("second",mid,end,new Vector3D(5,0,0),Vector3D.Right);
        clientRig.Block<IMyShipDrill>("Arm 1 - Head",end,new Vector3D(10,0,0));var peer=new Rig(clientRig,"Collision PB");
        var bus=new ModuleBus();bus.Bind(clientRig);bus.Bind(peer);var config=new MyIni();config.Set("global","Format",7);config.Set("Arm 1","Peers","Collision PB | Safety");RecordProxy.Of(clientRig.PB).Values["CustomData"]=config.ToString();
        var host=Tests.Create(armType,clientRig);HostReady(host,clientRig,"Arm 1");
        long seq=0;MyIni Fresh() {for(int i=0;i<8;i++)HostFrame(host,clientRig);return LastCheck(bus,clientRig.PB.EntityId,peer.PB.EntityId);}
        var check=Fresh();Check(check.Get("Link","ConstraintLimit").ToInt32()==16,"New arm did not advertise supported capacity.");
        SendGuide(host,clientRig,peer,bus,Guide(check,++seq,"0 0"));
        check=Fresh();var text=string.Join("\n",Enumerable.Repeat("1 0 -1",15).Append("0 1 0"));
        var packet=Guide(check,++seq,"0 0",text);packet.Set("Link","ConstraintLimit",16);SendGuide(host,clientRig,peer,bus,packet);
        var safety=Get(Get(Core(host,"Arm 1"),"Services")!,"Safety")!;Check(((IList)Get(safety,"Rows")!).Count==16,"Receiver did not retain all sixteen rows.");
        var candidate=new[]{.1,-.1};var receiver=PackedProgramChecks.Unwrap(safety);
        receiver.GetType().GetMethod(PackedProgramChecks.Name(receiver.GetType(),"Limit"),All)!.Invoke(receiver,new object[]{candidate});
        Check(candidate.All(x=>x==0),"The sixteenth inequality was not enforced.");
        var saved=((IList)Get(safety,"Rows")!).Cast<double[]>().Select(r=>r.ToArray()).ToArray();
        foreach(string limit in new[]{"17","NaN","8"}) {
            check=Fresh();packet=Guide(check,++seq,"0 0",text);packet.Set("Link","ConstraintLimit",limit);SendGuide(host,clientRig,peer,bus,packet);
            Check(((IList)Get(safety,"Rows")!).Cast<double[]>().Select((r,i)=>r.SequenceEqual(saved[i])).All(x=>x),"Invalid capacity partially replaced valid guidance.");
        }
        check=Fresh();packet=Guide(check,++seq,"0 0",text+"\n1 0 0");packet.Set("Link","ConstraintLimit",16);SendGuide(host,clientRig,peer,bus,packet);
        Check(((IList)Get(safety,"Rows")!).Count==16,"Seventeenth row bypassed receiver limit.");
        check=Fresh();packet=Guide(check,++seq,"0 0",string.Join("\n",Enumerable.Repeat("1 0 -1",8)));SendGuide(host,clientRig,peer,bus,packet);
        Check(((IList)Get(safety,"Rows")!).Count==8&&(int)Get(safety,"ConstraintLimit")! ==8,"New receiver rejected legacy eight-row guidance.");
        HostFrame(host,clientRig,"StopAll");
        Console.WriteLine("Collision capacity: independent 8/16-row retention, unchanged inequalities, final-row enforcement, legacy fallback and malformed/overflow refusal PASS.");
    }
}
