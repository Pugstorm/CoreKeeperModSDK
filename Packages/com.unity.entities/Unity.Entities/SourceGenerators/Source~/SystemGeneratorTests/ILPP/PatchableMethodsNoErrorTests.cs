using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Mono.Cecil;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;
using VerifyCS =
    Unity.Entities.SourceGenerators.Test.CSharpSourceGeneratorVerifier<
        Unity.Entities.SourceGen.SystemGenerator.SystemGenerator>;

namespace Unity.Entities.SourceGenerators
{
    [TestClass]
    public class PatchableMethodsNoErrorTests
    {
        [DataTestMethod]
        // Check basic types
        [DataRow("void Main(int test)")]
        // Check nullables
        [DataRow("void Main(int? test)")]
        // Check modifiers
        [DataRow("void Main(in int test)")]
        [DataRow("void Main(ref int test)")]
        // Check Arrays
        [DataRow("void Main(int[] boi)")]
        [DataRow("void Main(int[,] boi)")]
        [DataRow("void Main(int[,,] boi)")]
        [DataRow("void Main(int[,,,,,,,,,,,,,,,] boi)")]
        // Check Tuples
        [DataRow("void Main((int,float) value)")]
        [DataRow("void Main((int,float, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int) value)")]
        // Check Generics
        [DataRow("void Main<T1>(T1 val)")]
        [DataRow("void Main<T1>(System.Collections.Generic.List<System.Collections.Generic.List<TestType<int>.IDK>> test1)", "public struct TestType<T2> { public class IDK {}}")]
        [DataRow("void Main(MyDicKey<string?>.MyDicValue<System.Collections.Generic.Dictionary<int, int?>>.Dictionary funky)", "public struct MyDicKey<TKey> { public struct MyDicValue<TValue> {  public struct Dictionary {} }}")]
        // Check Function Pointers
        [DataRow("unsafe void Main(delegate* <int, void> funky)")]
        [DataRow("unsafe void Main(delegate* unmanaged[Stdcall]<int, void> funky)")]
        [DataRow("unsafe void Main(delegate* unmanaged[Fastcall]<int, void> funky)")]
        [DataRow("unsafe void Main(delegate* unmanaged[Thiscall]<int, void> funky)")]
        [DataRow("unsafe void Main(delegate* unmanaged[Cdecl]<int, void> funky)")]
        [DataRow("unsafe void Main(delegate* unmanaged<int, void> funky)")]
        // Check pointers
        [DataRow("unsafe void Main(int* arg)")]
        [DataRow("unsafe void Main(int** arg)")]
        [DataRow("unsafe void Main(int**[,] arg)")]
        [DataRow("unsafe void Main(Unity.Entities.Entity* arg)", "namespace Unity.Entities { public struct Entity { } }")]
        [DataRow("unsafe void Main(Unity.Entities.Entity** arg)", "namespace Unity.Entities { public struct Entity { } }")]
        // Check properties
        [DataRow("int Main", "", "get => 5;")]
        // Check ovveride
        [DataRow("protected override void Main(in int val)", "public abstract partial class Base { protected abstract void Main(in int message); }", "", "namespace MyNameSpace.Wonky { class Test : Base { ")]
        [DataRow("protected override void Main(ref int val)", "public abstract partial class Base { protected abstract void Main(ref int message); }", "", "namespace MyNameSpace.Wonky { class Test : Base { ")]
        [DataRow("protected override void Main(out int val)", "public abstract partial class Base { protected abstract void Main(out int message); }", "val = 5;", "namespace MyNameSpace.Wonky { class Test : Base { ")]
        public async Task GeneratedRoslynSignatureMatchesCecil(string methodSignature, string additionalTypes = "", string returnStatement = "", string beforeSignature = "namespace MyNameSpace.Wonky { class Test { ")
        {
            // Setup C# project with a test.cs containing a method of the given signature
            var workspace = new AdhocWorkspace();
            var project = workspace.AddProject("test", LanguageNames.CSharp);
            project = project.WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            var refer = new ReferenceAssemblies("net6.0", new PackageIdentity("Microsoft.NETCore.App.Ref", "6.0.0"), Path.Combine("ref", "net6.0"));
            project = project.WithMetadataReferences(refer.ResolveAsync(LanguageNames.CSharp, CancellationToken.None).Result);
            project = project.AddDocument("test.cs", $"{beforeSignature} {methodSignature}{{{returnStatement}}} }} {additionalTypes} }}").Project;

            // Compile project into an assembly stream
            var compilation = await project.GetCompilationAsync().ConfigureAwait(false);
            var assemblyStream = new MemoryStream();
            var result = compilation!.Emit(assemblyStream);
            Assert.IsTrue(result.Success, "Compilation failed because: " + string.Join(", ", result.Diagnostics.Select(d => d.GetMessage())));

            // Read Assembly with Cecil
            assemblyStream.Position = 0;
            var myAssembly = AssemblyDefinition.ReadAssembly(assemblyStream);
            var module = myAssembly.MainModule;

            // Find cecil method containing Main and generate ilName
            MethodReference? mainMethod = null;
            foreach (var method in module.GetType("MyNameSpace.Wonky.Test").Methods)
            {
                mainMethod = method;
                if (mainMethod.Name.Contains("Main"))
                    break;
            }
            Assert.IsNotNull(mainMethod);
            var ilName = GetMethodNameAndParamsAsString(mainMethod);

            // Find roslyn method containing Main and generate roslynName
            var roslynSymbol = compilation.GetTypeByMetadataName("MyNameSpace.Wonky.Test")!.GetMembers().First(m => m.Name.Contains("Main"));
            var roslynMethod = roslynSymbol switch
            {
                IMethodSymbol m => m,
                IPropertySymbol p => p.GetMethod,
                _ => null
            };
            var roslynName = roslynMethod?.GetMethodAndParamsAsString(default(DiagnosticBag));

            // Assert that Cecil and Roslyn Generated Cecil matches
            Assert.AreEqual(ilName, roslynName, $"GeneratedCecil: {roslynName}, ActualCecil: {ilName}");
            await assemblyStream.DisposeAsync();
        }

        struct DiagnosticBag : ISourceGeneratorDiagnosable
        {
            public List<Diagnostic> SourceGenDiagnostics { get; }
        }

        // Copied from `Cloner.cs` please keep in sync
        static string GetMethodNameAndParamsAsString(MethodReference method)
        {
            var strBuilder = new StringBuilder();
            strBuilder.Append(method.Name);
            strBuilder.Append($"_T{method.GenericParameters.Count}");

            foreach (var parameter in method.Parameters)
            {
                if (parameter.ParameterType.IsByReference)
                {
                    if (parameter.IsIn)
                        strBuilder.Append($"_in");
                    else if (parameter.IsOut)
                        strBuilder.Append($"_out");
                    else
                        strBuilder.Append($"_ref");
                }

                strBuilder.Append($"_{parameter.ParameterType}");
            }

            return strBuilder.ToString();
        }
    }
}
