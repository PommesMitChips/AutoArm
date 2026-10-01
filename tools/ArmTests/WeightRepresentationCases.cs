using System.Reflection;
using VRage.Game.ModAPI.Ingame.Utilities;

internal static partial class Scenarios
{
    internal static void WeightRepresentationCases(Type type)
    {
        var rig=Fixtures.Serial(out _,out _,out _,out _); var script=Start(type,rig);
        var config=type.GetNestedType("AutoConfig",BindingFlags.NonPublic)!;
        var reader=config.GetMethod("ReadWeights",BindingFlags.Public|BindingFlags.Static)!;
        var groups=Get(Get(script,"Topology")!,"Groups")!;
        var table=FCRows(FCIni(rig.PB.CustomData));
        string before=((TestHost)script).Storage;
        foreach(string malformed in new[]{
            string.Join("\n",table.Take(2)),
            string.Join("\n",new[]{table[0],table[1],table[1]}),
            table[0]+"\n"+FCRow(table[1],.3,2)+"\n"+FCRow(table[2],double.NaN,1),
            table[0]+"\n"+FCRow(table[1],.3,2)+"\n"+FCRow(table[2],1,11),
            string.Join("\n",table).Replace("Rotor x1","Hinge x1")})
        {
            var args=new object?[]{malformed,groups,null,null};
            Check(!(bool)reader.Invoke(null,args)!,"Malformed stage table was accepted.");
            Check(Groups(script).All(g=>FCWeight(g,"MoveW")==1&&FCWeight(g,"TurnW")==1)&&((TestHost)script).Storage==before,"Malformed stage table partially changed physical preferences.");
        }
        var reordered=new object?[]{table[0]+"\n"+FCRow(table[2],.7,3)+"\n"+FCRow(table[1],.2,4),groups,null,null};
        Check((bool)reader.Invoke(null,reordered)!,"Reordered complete stages were rejected.");
        var values=(System.Collections.IList)reordered[2]!;
        Check(((double[])values[0]!)[0]==.2&&((double[])values[1]!)[0]==.7,"Stage ordinals did not preserve template ordering.");
        var saved=FCStorageIni(before);
        string section="AutoArm Tool Weights";
        string rows=saved.Get(section,"Rows").ToString();
        saved.Set("Unknown section","Keep","untouched");
        saved.Set(section,"Rows",rows+"\nP:987654321+|0.25|2.5");
        ((TestHost)script).Storage=saved.ToString(); script.GetType().GetField("ToolWeightsLoaded",All)!.SetValue(script,false);
        Tests.Call(script,"RememberToolWeights",false);
        Tests.Call(script,"WriteToolWeights");
        var remembered=FCStorageIni(((TestHost)script).Storage);
        Check(remembered.Get(section,"Rows").ToString().Contains("P:987654321+|0.25|2.5")&&remembered.Get("Unknown section","Keep").ToString()=="untouched","Loading/writing preferences discarded parked identities or unrelated Storage.");
        string retained=((TestHost)script).Storage;
        saved=FCStorageIni(retained); saved.Set(section,"Rows",rows+"\n"+rows.Split('\n')[0]);
        ((TestHost)script).Storage=saved.ToString(); script.GetType().GetField("ToolWeightsLoaded",All)!.SetValue(script,false);
        bool rejected=false; try { Tests.Call(script,"RememberToolWeights",false); } catch(TargetInvocationException) { rejected=true; }
        Check(rejected&&((TestHost)script).Storage==saved.ToString(),"Duplicate durable identities were accepted or rewritten.");
        Console.WriteLine("Weight representation: shared stage reader, complete unique stages, atomic finite bounds, signed parked identity retention and opaque Storage coexistence.");
    }
}
