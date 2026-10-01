using System.Reflection;
using System.Text.Json;

internal static class PackedProgramChecks
{
    const BindingFlags All=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Dictionary<Assembly,Dictionary<string,string>> Maps=new();
    static readonly Dictionary<Type,string> Owners=new();
    static readonly HashSet<Type> EntryPoints=new();
    internal static void Register(Type outer,string mapPath)
    {
        var map=JsonDocument.Parse(File.ReadAllText(mapPath)).RootElement.EnumerateArray()
            .Where(e=>e.GetProperty("symbol").GetString()!.StartsWith("Program.",StringComparison.Ordinal))
            .GroupBy(e=>e.GetProperty("symbol").GetString()!)
            .ToDictionary(g=>g.Key,g=>g.First().GetProperty("name").GetString()!);
        Maps[outer.Assembly]=map;
        EntryPoints.Add(outer);
        var implementation=outer.GetFields(All).Select(f=>f.FieldType).FirstOrDefault(t=>t.Assembly==outer.Assembly && t.GetMethod("Main")!=null);
        Owners[implementation??outer]="Program";
        foreach(var type in outer.Assembly.GetTypes())
        {
            if(Owners.ContainsKey(type))continue;
            var entry=map.FirstOrDefault(p=>p.Value==type.Name && !p.Key.Contains('('));
            if(entry.Key!=null)Owners[type]=entry.Key;
        }
    }
    internal static object Unwrap(object instance)
    {
        if(!EntryPoints.Contains(instance.GetType()))return instance;
        var field=instance.GetType().GetFields(All).FirstOrDefault(f=>Owners.TryGetValue(f.FieldType,out var owner)&&owner=="Program");
        return field?.GetValue(instance)??instance;
    }
    internal static string Name(Type type,string original)
    {
        if(!Owners.TryGetValue(type,out var owner)||!Maps.TryGetValue(type.Assembly,out var map))return original;
        if(map.TryGetValue(owner+"."+original,out var alias))return alias;
        var method=map.FirstOrDefault(p=>p.Key.StartsWith(owner+"."+original+"(",StringComparison.Ordinal));
        return method.Key==null?original:method.Value;
    }
}
