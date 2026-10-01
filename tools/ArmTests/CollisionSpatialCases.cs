using System.Collections;
using VRageMath;

internal static partial class Scenarios
{
    static void CollisionSpatialCases(Type type)
    {
        var rig=new Rig();var other=rig.Grid();var service=PackedProgramChecks.Unwrap(Tests.Create(type,rig));var owner=service.GetType();
        object Make(string name)=>Activator.CreateInstance(owner.GetNestedType(PackedProgramChecks.Name(owner,name),All)!,true)!;
        object Invoke(string name,params object[] args)=>owner.GetMethod(PackedProgramChecks.Name(owner,name),All)!.Invoke(service,args)!;
        object Grid(VRage.Game.ModAPI.Ingame.IMyCubeGrid g){var s=Make("GridShape");Put(s,"Grid",g);return s;}
        object Box(Vector3D c,Vector3D h){var b=Make("Shape");Put(b,"Centre",c);Put(b,"Half",h);return b;}
        var a=Grid(rig.Root);var b=Grid(other);var tiles=(IDictionary)Get(b,"Tiles")!;
        void Index(object shape)
        {
            var c=(Vector3D)Get(shape,"Centre")!;var h=(Vector3D)Get(shape,"Half")!;
            var lo=c-h;var hi=c+h;
            int Tile(double d)=>(int)Math.Floor(Math.Round(d/2.5)/8);
            for(int z=Tile(lo.Z);z<=Tile(hi.Z);z++)for(int y=Tile(lo.Y);y<=Tile(hi.Y);y++)for(int x=Tile(lo.X);x<=Tile(hi.X);x++)
            {
                var key=new Vector3I(x,y,z);if(!tiles.Contains(key))tiles[key]=Activator.CreateInstance(typeof(List<>).MakeGenericType(shape.GetType()))!;
                ((IList)tiles[key]!).Add(shape);
            }
        }
        // Distant boxes in the same 20 m tile must not reach SAT.
        var close=Box(Vector3D.Zero,new Vector3D(1.25));Index(close);
        for(int i=0;i<100;i++)Index(Box(new Vector3D(10+i*.05,10,10),new Vector3D(.1)));
        var source=Box(Vector3D.Zero,new Vector3D(1.25));var found=(IList)Invoke("Candidates",a,source,b,.75);
        Check(found.Count==1&&ReferenceEquals(found[0],close),"Crowded tile contents were not filtered before SAT.");
        // Brute-force SAT oracle verifies no near shape disappears after
        // conservative projection, for rotated grids and long/coalesced boxes.
        var random=new Random(8871);
        for(int trial=0;trial<70;trial++)
        {
            RecordProxy.Of(rig.Root).Values["WorldMatrix"]=MatrixD.CreateFromYawPitchRoll((float)(random.NextDouble()*3),(float)(random.NextDouble()*3),(float)(random.NextDouble()*3));
            var frame=MatrixD.CreateFromYawPitchRoll((float)(random.NextDouble()*3),(float)(random.NextDouble()*3),(float)(random.NextDouble()*3));frame.Translation=new Vector3D(random.NextDouble()*5,random.NextDouble()*5,random.NextDouble()*5);RecordProxy.Of(other).Values["WorldMatrix"]=frame;
            tiles.Clear();var all=new List<object>();
            for(int i=0;i<80;i++)
            {
                var shape=Box(new Vector3D(random.NextDouble()*25-12,random.NextDouble()*25-12,random.NextDouble()*25-12),new Vector3D(1.25+random.Next(5)*2.5,1.25,1.25));all.Add(shape);Index(shape);
            }
            var query=Box(new Vector3D(random.NextDouble()*8-4,random.NextDouble()*8-4,random.NextDouble()*8-4),new Vector3D(1.25+random.Next(4)*2.5,1.25,1.25));
            found=(IList)Invoke("Candidates",a,query,b,.75);
            foreach(var shape in all)
            {var args=new object[]{a,query,b,shape,Vector3D.Zero};double separation=(double)Invoke("Separation",args);if(separation<.75)Check(found.Contains(shape),"Spatial prefilter omitted a SAT-near or overlapping shape.");}
        }
        // The filter must also abort inside a crowded list, not merely between
        // tiles, when the native instruction budget is running out.
        tiles.Clear();for(int i=0;i<100;i++)Index(Box(Vector3D.Zero,new Vector3D(1.25)));
        var runtime=RecordProxy.Of(rig.Runtime);runtime.Values.Remove("CurrentInstructionCount");int reads=0;
        runtime.Call=(m,args)=>m.Name=="get_CurrentInstructionCount"?(++reads>4?40000:0):m.ReturnType.IsValueType?Activator.CreateInstance(m.ReturnType):null;
        bool refused=false;try{Invoke("Candidates",a,source,b,.75);}catch(System.Reflection.TargetInvocationException e){refused=e.InnerException!.Message.Contains("budget");}
        Check(refused,"Crowded candidate-list iteration bypassed budget guards.");
        Console.WriteLine("Spatial query: crowded-tile rejection, rotated/coalesced SAT coverage and in-list budget refusal PASS.");
    }
}
