using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class SpecializerChecks
{
    internal static void Run()
    {
        foreach (bool role in new[] { false, true })
        {
            string input = @"
const bool Role = ROLE;
static int Counter;
static int UnusedA = 1, UnusedB = 2;
static int Unused = Bump();
static int Cold { get; } = Bump();
static int Bump() { return ++Counter; }
static bool Touch() { Counter++; return true; }
static int Callback() { return 5; }
static int Pick(int value) { return 1000; }
static int Pick(double value) { return 20; }
static void Discarded() { throw new System.Exception(""dead helper""); }
static int Probe()
{
    int sum = 0;
    if (Role) { int x = 7; sum += x; } else { int x = 11; sum += x; }
    if (!Role) sum += 100;
    sum += 2 * (Role ? 13 : 17);
    sum += Pick(Role ? 1 : 2.0);
    bool a = false && Touch(), b = true || Touch();
    bool c = Touch() && false, d = Touch() || true;
    System.Func<int> callback = Callback;
    try { if (false) throw new System.Exception(); } catch { sum += 999; }
    return sum + Counter + callback();
}
public static void Main() { Probe(); }
public static void Save() { }
".Replace("ROLE", role ? "true" : "false");
            string output = Specializer.Run(input);
            if (output.Contains("dead helper") || output.Contains("const bool Role") ||
                output.Contains("try {") || !output.Contains("Bump()") || !output.Contains("Callback()"))
                throw new Exception("Specialization retained dead code or removed an initializer/callback.");
            if (Execute(input) != Execute(output)) throw new Exception("Specialization changed branch, scope, precedence or side effects.");
            if (Specializer.Run(output) != output) throw new Exception("Specialization is not idempotent.");
        }
        Console.WriteLine("Specialization regressions: PASS (both roles, scopes, precedence, callbacks and initializer/short-circuit effects).");
    }

    static int Execute(string source)
    {
        var tree = CSharpSyntaxTree.ParseText("public class Program {" + source + "}", ScriptPack.ParseOptions);
        var compilation = CSharpCompilation.Create("SpecializeTest_" + Guid.NewGuid().ToString("N"), new[] { tree },
            ScriptPack.Compile("", "references").References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        var type = Assembly.Load(stream.ToArray()).GetType("Program")!;
        return (int)type.GetMethod("Probe", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
    }
}
