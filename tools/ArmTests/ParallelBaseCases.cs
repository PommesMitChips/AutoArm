using Sandbox.ModAPI.Ingame;
using VRageMath;
internal static partial class Scenarios
{
    internal static void ParallelBaseCases(Type type)
    {
        Rig Build(int count,bool rotary,bool separate,bool unequal=false,bool skew=false) {
            var rig=new Rig();var joined=rig.Grid();
            for(int i=0;i<count;i++) {
                var first=separate?rig.Grid():joined;string name=i==0?"Arm 1 - Base":"unmarked base partner "+i;
                if(rotary)rig.Rotor(name,rig.Root,first,new Vector3D(skew&&i==count-1?.5:0,i*2,0),i%2==0?Vector3D.Up:Vector3D.Down);
                else rig.Piston(name,rig.Root,first,new Vector3D(i*2,0,0),i%2==0?Vector3D.Forward:Vector3D.Backward);
                if(separate) {
                    if(unequal&&i==count-1) {var extra=rig.Grid();rig.Piston("extra stage",first,extra,axis:Vector3D.Forward);first=extra;}
                    rig.Piston("rejoin "+i,first,joined,new Vector3D(i*2,0,-5),Vector3D.Forward);
                }
            }
            rig.Block<IMyShipDrill>("Arm 1 - Head",joined,new Vector3D(0,0,-12));rig.Cockpit();return rig;
        }
        foreach(bool rotary in new[]{false,true})foreach(bool separate in new[]{false,true})foreach(int count in new[]{2,3,7}) {
            var rig=Build(count,rotary,separate);var script=Start(type,rig);Run(script,"On");
            Check(Enabled(script),"Parallel base failed: "+rotary+"/"+separate+"/"+count+" | "+string.Join(" | ",rig.Log.TakeLast(2)));
            var groups=Groups(script);Check(groups.Length==(separate?2:1)&&Members(groups[0]).Length==count,"Unnamed base partners were not one generalized stage.");
            Check(Members(groups[0]).Select((m,i)=>(int)Get(m,"Sign")! ==(i%2==0?1:-1)).All(x=>x),"Opposing base axes lost signed coordination.");
            Check(rig.PB.CustomData.Contains("x"+count),"Generated actuator table omitted parallel base count.");
            var cockpit=(IMyShipController)rig.Blocks.Single(b=>b is IMyShipController);RecordProxy.Of(cockpit).Values["MoveIndicator"]=rotary?new Vector3(-1,0,0):new Vector3(0,0,-1);for(int i=0;i<12;i++)Run(script);
            var members=Members(groups[0]);double speed=members.Select(m=>Get(m,"B")).Select(b=>b is IMyPistonBase p?p.Velocity:((IMyMotorStator)b!).TargetVelocityRad).Select(Math.Abs).Max();
            Check(speed>1e-6,"Parallel base received no coordinated motion: "+rotary+"/"+separate+"/"+count+" | "+string.Join(" | ",rig.Log.TakeLast(2)));
            double reference=0;for(int i=0;i<members.Length;i++) {var block=Get(members[i],"B");double q=(block is IMyPistonBase p?p.Velocity:((IMyMotorStator)block!).TargetVelocityRad)*(int)Get(members[i],"Sign")!;if(i==0)reference=q;else Check(Math.Abs(q-reference)<1e-6,"Parallel base members received inconsistent signed speeds.");}
            Run(script,"Stop");NoVelocity(rig);
        }
        foreach(bool rotary in new[]{false,true}) {
            var rig=Build(48,rotary,false);var script=Start(type,rig);Run(script,"On");Check(Enabled(script)&&Members(Groups(script)[0]).Length==48,"Supported 48-member base bound was not accepted.");Run(script,"Stop");NoVelocity(rig);
            rig=Build(49,rotary,false);script=Start(type,rig);Run(script,"On");Check(!Enabled(script),"Parallel base bypassed the physical member budget.");NoVelocity(rig);
        }
        foreach(var rig in new[]{Build(3,true,true,unequal:true),Build(3,false,true,unequal:true),Build(3,true,false,skew:true)}) {var script=Start(type,rig);Run(script,"On");Check(!Enabled(script),"Unequal or non-coaxial base enabled.");NoVelocity(rig);}
        var sync=Build(3,false,true);var controller=Start(type,sync);Run(controller,"On");var mate=(IMyPistonBase)sync.Blocks.Single(b=>b.CustomName=="unmarked base partner 1");RecordProxy.Of(mate).Values["CurrentPosition"]=5.3f;Run(controller);Check(!Enabled(controller),"Base group desynchronisation did not stop the arm.");NoVelocity(sync);
        Console.WriteLine("Parallel bases: N signed coaxial hinges/rotors and offset parallel pistons, same-grid/rejoining branches, generated config, coordinated drives, member budget and integrity refusal PASS.");
    }
}
