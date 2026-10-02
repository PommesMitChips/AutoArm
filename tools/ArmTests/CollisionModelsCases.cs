using System.Collections;
using System.Reflection;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

internal static partial class Scenarios
{
    static readonly Dictionary<string,BoundingBoxD> ModelBoxes=new();
    static BoundingBoxD? InstalledModelBox(string name)
    {
        if(ModelBoxes.TryGetValue(name,out var found))return found;
        string file=name switch{
            "LargeAdvancedStator"=>"MotorAdvancedStator", "LargeStator"=>"MotorStator", "LargeHinge"=>"Hinge",
            "LargePistonBase"=>"PistonBase", "LargePistonBaseReskin"=>"PistonReskinBase",
            "LargeHingeHead"=>"HingeHead", "LargeAdvancedRotor"=>"MotorAdvancedRotor", "LargeRotor"=>"MotorRotor",
            "LargePistonTop"=>"PistonTop", "LargePistonTopReskin"=>"PistonReskinTop",_=>""};
        if(file=="")return null;
        string path=Path.Combine(Path.GetDirectoryName(Tests.GameBin)!,"Content","Models","Cubes","Large",file+".mwm");
        if(!File.Exists(path))throw new FileNotFoundException("Validation requires the installed model asset.",path);
        var type=Assembly.LoadFrom(Path.Combine(Tests.GameBin,"VRage.Render.dll")).GetType("VRageRender.Import.MyModelImporter",true)!;
        var importer=Activator.CreateInstance(type)!;type.GetMethod("ImportData")!.Invoke(importer,new object[]{path,new[]{"BoundingBox"}});
        var tags=(IDictionary)type.GetMethod("GetTagData")!.Invoke(importer,null)!;var raw=(BoundingBox)tags["BoundingBox"]!;
        var result=new BoundingBoxD(raw.Min,raw.Max);ModelBoxes[name]=result;return result;
    }
    static BoundingBoxD WorldModelBox(BoundingBoxD local,MatrixD world)
    {
        var box=BoundingBoxD.CreateInvalid();for(int i=0;i<8;i++)box.Include(Vector3D.Transform(local.GetCorner(i),world));return box;
    }
    static void SetModelAabb(IMyCubeBlock block)
    {var box=InstalledModelBox(block.BlockDefinition.SubtypeName);if(box!=null)RecordProxy.Of(block).Values["WorldAABB"]=WorldModelBox(box.Value,block.WorldMatrix);}
    static void CollisionModelsCases(Type type)
    {
        MountedCollarCases(type);
        var rig=new Rig();var mid=rig.Grid();var end=rig.Grid();
        var rotor=rig.Rotor("Arm 1 - Base",rig.Root,mid);
        var hinge=rig.Rotor("hinge",mid,end,new Vector3D(0,2.7,0),Vector3D.Right);
        RecordProxy.Of(rotor).Values["BlockDefinition"]=ToolSwapFixture.Definition("LargeAdvancedStator");
        RecordProxy.Of(hinge).Values["BlockDefinition"]=ToolSwapFixture.Definition("LargeHinge");
        foreach(var joint in new[]{rotor,hinge}){
            var top=RecordProxy.Of(joint.Top);top.Values["WorldMatrix"]=joint.WorldMatrix;top.Values["BlockDefinition"]=ToolSwapFixture.Definition(joint==rotor?"LargeAdvancedRotor":"LargeHingeHead");
            SetModelAabb(joint);SetModelAabb(joint.Top);
        }
        var service=PackedProgramChecks.Unwrap(Tests.Create(type,rig));var owner=service.GetType();
        object Make(string name)=>Activator.CreateInstance(owner.GetNestedType(PackedProgramChecks.Name(owner,name),All)!,true)!;
        object Invoke(string name,params object[] args)=>owner.GetMethod(PackedProgramChecks.Name(owner,name),All)!.Invoke(service,args)!;
        object Grid(IMyCubeGrid native){var g=Make("GridShape");Put(g,"Grid",native);return g;}
        object Box(long tag,Vector3D centre,Vector3D half){var b=Make("Shape");Put(b,"Tag",tag);Put(b,"Centre",centre);Put(b,"Half",half);return b;}
        foreach(string name in new[]{"LargeAdvancedStator","LargeStator","LargeHinge","LargePistonBase","LargePistonBaseReskin","LargeHingeHead","LargeAdvancedRotor","LargeRotor","LargePistonTop","LargePistonTopReskin"})
        {
            IMyCubeBlock block=name.Contains("PistonTop")?RecordProxy.Make<IMyPistonTop>():name.Contains("PistonBase")?rig.Block<IMyPistonBase>(name,rig.Root):name.Contains("Head")||name.EndsWith("Rotor")?RecordProxy.Make<IMyMotorRotor>():rig.Block<IMyMotorStator>(name,rig.Root);
            var v=RecordProxy.Of(block).Values;v["CubeGrid"]=rig.Root;v["WorldMatrix"]=MatrixD.Identity;v["BlockDefinition"]=ToolSwapFixture.Definition(name);SetModelAabb(block);
            var parameters=new object[]{block,Vector3D.Zero,Vector3D.Zero};Check((bool)Invoke("ModelBounds",parameters),"Installed model profile refused "+name);
            var raw=InstalledModelBox(name)!.Value;var min=(Vector3D)parameters[1];var max=(Vector3D)parameters[2];
            Check(min.X<=raw.Min.X&&min.Y<=raw.Min.Y&&min.Z<=raw.Min.Z&&max.X>=raw.Max.X&&max.Y>=raw.Max.Y&&max.Z>=raw.Max.Z,"Baked profile did not enclose installed mesh bounds: "+name);
        }
        var parent=Grid(rig.Root);var child=Grid(mid);var grandchild=Grid(end);
        var ghost=Box(rotor.EntityId,new Vector3D(0,2.5,0),new Vector3D(1.25));var body=Box(hinge.EntityId,new Vector3D(0,2.5,0),new Vector3D(1.25));
        Check((bool)Invoke("InterfacePair",parent,ghost,child,body),"Actual base/following-hinge interface was not identified.");
        var args=new object[]{parent,ghost,ghost,false};Check(!(bool)Invoke("ClipModel",args)&&(bool)args[3],"Empty allocation cell was retained as a solid rotor body.");
        Check(!(bool)Invoke("InterfacePair",parent,Box(0,Vector3D.Zero,new Vector3D(1.25)),child,body),"Main-grid armour received interface refinement.");
        var far=rig.Grid();Check(!(bool)Invoke("InterfacePair",parent,ghost,Grid(far),body),"Unrelated arm/grid received interface refinement.");
        var head=Box(hinge.Top.EntityId,new Vector3D(0,2.7,0),new Vector3D(1.25));
        Check((bool)Invoke("InterfacePair",parent,ghost,grandchild,head),"Exact following-hinge top was not recognised.");
        Check(!(bool)Invoke("InterfacePair",parent,ghost,grandchild,Box(0,Vector3D.Zero,new Vector3D(1.25))),"Whole grandchild grid was refined.");
        foreach(string property in new[]{"IsAttached","PendingAttachment"})
        {var v=RecordProxy.Of(rotor).Values;v[property]=property=="PendingAttachment";Check(!(bool)Invoke("InterfacePair",parent,ghost,child,body),"Detached/pending joint retained refinement.");v[property]=property=="IsAttached";}
        var oldBounds=rotor.WorldAABB;RecordProxy.Of(rotor).Values["WorldAABB"]=new BoundingBoxD(new(-10,-10,-10),new(10,10,10));
        ((IDictionary)Get(service,"Models")!).Clear();
        args=new object[]{parent,ghost,ghost,false};Check((bool)Invoke("ClipModel",args)&&!(bool)args[3]&&ReferenceEquals(args[2],ghost),"Changed model exceeding baked envelope did not retain voxel protection.");RecordProxy.Of(rotor).Values["WorldAABB"]=oldBounds;
        RecordProxy.Of(rotor).Values["BlockDefinition"]=ToolSwapFixture.Definition("ModdedRotor");args=new object[]{parent,ghost,ghost,false};
        ((IDictionary)Get(service,"Models")!).Clear();
        Check((bool)Invoke("ClipModel",args)&&!(bool)args[3],"Unknown model silently used vanilla geometry.");
        // A shared rigid rotation cannot change self separation. Compare two
        // points whose naive fixed-normal derivatives would differ strongly.
        var arm=Make("Arm");Put(arm,"Caps",new[]{.1});Put(arm,"Rates",new[]{0d});
        var group=Make("Group");Put(group,"Rotary",true);var member=Make("Joint");Put(member,"Block",rotor);Put(member,"From",rig.Root.EntityId);Put(member,"To",mid.EntityId);Put(member,"Sign",1);
        ((IList)Get(group,"Members")!).Add(member);((IList)Get(arm,"Groups")!).Add(group);((IDictionary)Get(arm,"Bodies")!)[end.EntityId]=1;
        var rows=Activator.CreateInstance(typeof(List<>).MakeGenericType(typeof(double[])))!;
        args=new object[]{arm,1,grandchild,Box(0,new Vector3D(10,0,0),new Vector3D(1)),child,Box(0,Vector3D.Zero,new Vector3D(1)),.1,Vector3D.Backward,.1,true,new[]{0d},rows};
        Check((bool)Invoke("AddConstraint",args)&&((IList)rows).Count==0,"Common ancestor generated a false self-collision constraint.");
        // Genuine relative motion still produces a binding row.
        ((IDictionary)Get(arm,"Bodies")!)[end.EntityId]=0;args[7]=Vector3D.Backward;
        Check((bool)Invoke("AddConstraint",args)&&((IList)rows).Count==1,"Relative joint movement lost its collision constraint.");
        var saved=(double[])((IList)rows)[0]!;double first=saved[0];
        args[3]=Box(0,new Vector3D(12,0,0),new Vector3D(1));
        Check((bool)Invoke("AddConstraint",args)&&((IList)rows).Count==1&&Math.Abs(((double[])((IList)rows)[0]!)[0]-first)<1e-9,"Positively collinear support rows were not safely normalised.");
        // Near-parallel merge tightens the retained bound enough to imply both
        // source rows for every cap-bounded second joint value.
        var r2=rig.Rotor("relative axis",rig.Root,mid,axis:Vector3D.Right);
        var g2=Make("Group");Put(g2,"Rotary",true);var m2=Make("Joint");Put(m2,"Block",r2);Put(m2,"From",rig.Root.EntityId);Put(m2,"To",mid.EntityId);Put(m2,"Sign",1);
        ((IList)Get(g2,"Members")!).Add(m2);((IList)Get(arm,"Groups")!).Add(g2);Put(arm,"Caps",new[]{.1,.1});Put(arm,"Rates",new[]{0d,0d});
        rows=Activator.CreateInstance(typeof(List<>).MakeGenericType(typeof(double[])))!;args[1]=3;args[3]=Box(0,new Vector3D(10,0,0),new Vector3D(1));args[10]=new[]{0d,0d};args[11]=rows;
        Check((bool)Invoke("AddConstraint",args),"Initial merge test row failed.");
        args[3]=Box(0,new Vector3D(10,.0005,0),new Vector3D(1));Check((bool)Invoke("AddConstraint",args)&&((IList)rows).Count==1,"Safe near-parallel reduction failed.");
        var merged=(double[])((IList)rows)[0]!;double q0=merged[2]/merged[0]+1e-9;
        foreach(double q1 in new[]{-.1,0,.1})
            Check(10*q0-.0005*q1>=-1e-12&&10*q0>=-1e-12,"Merged inequality weakened an original constraint at a cap-bounded corner.");
        Console.WriteLine("Mechanical interface bounds: installed MWM envelopes, exact attachment scope, armour/other-arm protection, changed/unknown models, and common-ancestor cancellation PASS.");
    }
    static void MountedCollarCases(Type type)
    {
        var rig=new Rig();var middle=rig.Grid();var end=rig.Grid();
        var piston=rig.Piston("preceding piston",rig.Root,middle);
        var hinge=rig.Rotor("directly mounted hinge",middle,end,new Vector3D(0,2.5,0),Vector3D.Right);
        RecordProxy.Of(piston).Values["BlockDefinition"]=ToolSwapFixture.Definition("LargePistonBaseReskin");
        RecordProxy.Of(piston.Top).Values["BlockDefinition"]=ToolSwapFixture.Definition("LargePistonTopReskin");
        RecordProxy.Of(piston.Top).Values["WorldMatrix"]=MatrixD.Identity;
        RecordProxy.Of(hinge).Values["BlockDefinition"]=ToolSwapFixture.Definition("LargeHinge");
        var hingeFrame=MatrixD.CreateWorld(new(0,2.5,0),Vector3D.Forward,Vector3D.Right);
        RecordProxy.Of(hinge).Values["WorldMatrix"]=hingeFrame;
        RecordProxy.Of(hinge.Top).Values["WorldMatrix"]=hingeFrame;
        RecordProxy.Of(hinge.Top).Values["BlockDefinition"]=ToolSwapFixture.Definition("LargeHingeHead");
        foreach(var top in new IMyCubeBlock[]{piston.Top,hinge.Top}) {var proxy=RecordProxy.Of(top);proxy.Call=(m,a)=>m.Name=="GetPosition"?((MatrixD)proxy.Values["WorldMatrix"]!).Translation:m.ReturnType.IsValueType?Activator.CreateInstance(m.ReturnType):null;}
        foreach(var part in new IMyCubeBlock[]{piston,piston.Top,hinge,hinge.Top})SetModelAabb(part);
        var service=PackedProgramChecks.Unwrap(Tests.Create(type,rig));var owner=service.GetType();
        bool Allowed(long a,long b)=>(bool)owner.GetMethod(PackedProgramChecks.Name(owner,"MountedCollars"),All)!.Invoke(service,new object[]{a,b})!;
        Check(Allowed(piston.Top.EntityId,hinge.Top.EntityId),"Direct piston/hinge mounting collars were not recognised.");
        Check(!Allowed(piston.EntityId,hinge.Top.EntityId)&&!Allowed(piston.Top.EntityId,hinge.EntityId)&&!Allowed(0,hinge.Top.EntityId),"Collar allowance leaked to actuator bodies or untagged armour.");
        var worldP=piston.Top.WorldMatrix;var worldH=hinge.WorldMatrix;var worldT=hinge.Top.WorldMatrix;
        for(int i=0;i<40;i++) {
            var frame=MatrixD.CreateFromYawPitchRoll(i*.137,i*.081,i*.193);frame.Translation=new Vector3D(200+i,-80,40);
            RecordProxy.Of(piston.Top).Values["WorldMatrix"]=worldP*frame;
            RecordProxy.Of(hinge).Values["WorldMatrix"]=worldH*frame;
            var head=worldT.GetOrientation()*MatrixD.CreateFromAxisAngle(worldH.Up,i*.03);
            head.Translation=worldT.Translation;RecordProxy.Of(hinge.Top).Values["WorldMatrix"]=head*frame;
            foreach(var part in new IMyCubeBlock[]{piston.Top,hinge,hinge.Top})SetModelAabb(part);
            Check(Allowed(piston.Top.EntityId,hinge.Top.EntityId),"Rotated/offset mounted collars lost their exact interface allowance.");
        }
        RecordProxy.Of(piston.Top).Values["WorldMatrix"]=worldP;RecordProxy.Of(hinge).Values["WorldMatrix"]=worldH;RecordProxy.Of(hinge.Top).Values["WorldMatrix"]=worldT;
        foreach(var part in new IMyCubeBlock[]{piston.Top,hinge,hinge.Top})SetModelAabb(part);
        foreach(var joint in new[]{(IMyMechanicalConnectionBlock)piston,hinge})foreach(var property in new[]{"IsAttached","PendingAttachment"}) {
            RecordProxy.Of(joint).Values[property]=property=="PendingAttachment";
            Check(!Allowed(piston.Top.EntityId,hinge.Top.EntityId),"Detached or pending coupling retained a collar allowance.");
            RecordProxy.Of(joint).Values[property]=property=="IsAttached";
        }
        var displaced=worldH;displaced.Translation+=new Vector3D(0,.1,0);RecordProxy.Of(hinge).Values["WorldMatrix"]=displaced;SetModelAabb(hinge);
        Check(!Allowed(piston.Top.EntityId,hinge.Top.EntityId),"Indirect/spaced mounting received a collar allowance.");RecordProxy.Of(hinge).Values["WorldMatrix"]=worldH;SetModelAabb(hinge);
        RecordProxy.Of(piston.Top).Values["WorldMatrix"]=MatrixD.CreateWorld(Vector3D.Zero,Vector3D.Backward,Vector3D.Down);SetModelAabb(piston.Top);
        Check(!Allowed(piston.Top.EntityId,hinge.Top.EntityId),"Non-facing mounting received a collar allowance.");RecordProxy.Of(piston.Top).Values["WorldMatrix"]=worldP;SetModelAabb(piston.Top);
        RecordProxy.Of(piston.Top).Values["BlockDefinition"]=ToolSwapFixture.Definition("ModdedTop");
        Check(!Allowed(piston.Top.EntityId,hinge.Top.EntityId),"Unknown model received a collar allowance.");RecordProxy.Of(piston.Top).Values["BlockDefinition"]=ToolSwapFixture.Definition("LargePistonTopReskin");
        RecordProxy.Of(hinge.Top).Values["WorldAABB"]=new BoundingBoxD(new(-100),new(100));
        Check(!Allowed(piston.Top.EntityId,hinge.Top.EntityId),"Changed model escaping its vanilla envelope retained a collar allowance.");SetModelAabb(hinge.Top);
        // The same direct mounting works for a rotor base on the preceding top.
        RecordProxy.Of(hinge).Values["BlockDefinition"]=ToolSwapFixture.Definition("LargeAdvancedStator");RecordProxy.Of(hinge).Values["WorldMatrix"]=MatrixD.CreateTranslation(0,2.5,0);
        RecordProxy.Of(hinge.Top).Values["BlockDefinition"]=ToolSwapFixture.Definition("LargeAdvancedRotor");RecordProxy.Of(hinge.Top).Values["WorldMatrix"]=MatrixD.CreateTranslation(0,2.7,0);
        SetModelAabb(hinge);SetModelAabb(hinge.Top);Check(Allowed(piston.Top.EntityId,hinge.Top.EntityId),"Direct piston/rotor mounting collars were not recognised.");
        // A compatible-looking head on an unrelated arm is never the exact top
        // of this mounted stator, even when the two happen to be coincident.
        var unrelated=rig.Rotor("another arm",rig.Root,rig.Grid());
        RecordProxy.Of(unrelated.Top).Values["BlockDefinition"]=ToolSwapFixture.Definition("LargeAdvancedRotor");RecordProxy.Of(unrelated.Top).Values["WorldMatrix"]=hinge.Top.WorldMatrix;SetModelAabb(unrelated.Top);
        ((IDictionary)Get(service,"Parts")!)[unrelated.Top.EntityId]=unrelated.Top;
        Check(!Allowed(piston.Top.EntityId,unrelated.Top.EntityId),"Coincident unrelated arm collar was exempted.");
        RecordProxy.Of(middle).Values["GridSize"]=.5f;
        Check(!Allowed(piston.Top.EntityId,hinge.Top.EntityId),"Unsupported grid scale received the vanilla collar exception.");
        RecordProxy.Of(middle).Values["GridSize"]=2.5f;
        Console.WriteLine("Mounted collars: exact facing vanilla top/stator mounting, rotated frames, bodies/armour/spacing/detached/pending/unknown-model protection PASS.");
    }
}
