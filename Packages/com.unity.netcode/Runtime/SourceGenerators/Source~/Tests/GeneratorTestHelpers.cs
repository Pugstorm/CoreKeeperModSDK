using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Unity.NetCode.Generators;
using Debug = System.Diagnostics.Debug;

namespace Unity.NetCode.GeneratorTests
{
    class TestSyntaxWalker : CSharpSyntaxWalker
    {
        public NetCodeSyntaxReceiver? Receiver;

        public override void Visit(SyntaxNode? node)
        {
            Receiver?.OnVisitSyntaxNode(node);
            base.Visit(node);
        }
    }

    class InializationBlockWalker : CSharpSyntaxWalker
    {
        public InitializerExpressionSyntax? Intializer;
        public override void VisitInitializerExpression(InitializerExpressionSyntax node)
        {
            Intializer ??= node;
            base.VisitInitializerExpression(node);
        }
    }

    class DictBasedConfigOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> m_Options;

        public DictBasedConfigOptions(params KeyValuePair<string, string>[] optionsPairs)
        {
            m_Options = new Dictionary<string, string>(optionsPairs);
        }

        public override bool TryGetValue(string key, [MaybeNullWhen(false)] out string value)
        {
            return m_Options.TryGetValue(key, out value);
        }

        public void AddOrSet(string key, string value)
        {
            m_Options[key] = value;
        }
    }

    class TestConfigOptionProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions globalOptions;

        public TestConfigOptionProvider(AnalyzerConfigOptions global)
        {
            globalOptions = global;
        }
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            throw new NotImplementedException();
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            throw new NotImplementedException();
        }

        public override AnalyzerConfigOptions GlobalOptions => globalOptions;
    }

    static class GeneratorTestHelpers
    {
        public const string GeneratedAssemblyName = "Unity.NetCode.Test";
        public const string OutputFolder = "TestOutput";
        public static Compilation CreateCompilation(params SyntaxTree[] tree)
        {

            var metaReferences = new List<SyntaxTree>();
            metaReferences.AddRange(tree);
            metaReferences.AddRange(GetUnityNetCodeMetaRefs());
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Debug, allowUnsafe: true);

            var directoryName = Path.GetDirectoryName(typeof(object).Assembly.Location);
            Debug.Assert(directoryName != null, nameof(directoryName) + " != null");
            var compilation = CSharpCompilation.Create(GeneratedAssemblyName, metaReferences, options: options).AddReferences(
                MetadataReference.CreateFromFile(Path.Combine(directoryName, "netstandard.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(directoryName, "System.Runtime.dll")),
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

            //Enable this to check if there are some dependencies missing you don't expect.
            //NOTE: is normal to have some depedencies from not present like RpcExecutor, burst, entities stuff etc.
            //but for sake of this smoke testing they can be ignored

            // foreach(var d in compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
            //     Console.WriteLine(d.ToString());

            return compilation;
        }

        public class InMemoryAdditionalFile : AdditionalText
        {
            private string m_Path;
            private string m_Content;
            public InMemoryAdditionalFile(string path, string content)
            {
                m_Path = path;
                m_Content = content;
            }
            public override SourceText GetText(CancellationToken cancellationToken = new CancellationToken())
            {
                return SourceText.From(m_Content);
            }
            public override string Path => m_Path;
        }

        public static GeneratorDriver CreateGeneratorDriver(Dictionary<string, string>? customOptions = null)
        {
            var options = new DictBasedConfigOptions(
                KeyValuePair.Create(GlobalOptions.ProjectPath, Environment.CurrentDirectory),
                KeyValuePair.Create(GlobalOptions.OutputPath, OutputFolder),
                KeyValuePair.Create(GlobalOptions.DisableRerencesChecks, "1"),
                KeyValuePair.Create(GlobalOptions.TemplateFromAdditionalFiles, "1"));

            if (customOptions != null)
            {
                foreach (var kv in customOptions)
                    options.AddOrSet(kv.Key, kv.Value);
            }
            var driver = CSharpGeneratorDriver.Create(
                new ISourceGenerator[]{new NetCodeSourceGenerator()},
                optionsProvider: new TestConfigOptionProvider(options));
            return driver;
        }

        public static GeneratorRunResult RunGenerators(params SyntaxTree[] syntaxTree)
        {
            var compilation = CreateCompilation(syntaxTree);
            var driver = CreateGeneratorDriver();
            return driver.RunGenerators(compilation).GetRunResult().Results[0];
        }

        public static GeneratorRunResult RunGeneratorsWithOptions(Dictionary<string, string>? customOptions, params SyntaxTree[] syntaxTree)
        {
            var compilation = CreateCompilation(syntaxTree);
            var driver = CreateGeneratorDriver(customOptions);
            return driver.RunGenerators(compilation).GetRunResult().Results[0];
        }

        public static NetCodeSyntaxReceiver CreateSyntaxReceiver()
        {
            return new NetCodeSyntaxReceiver();
        }

        //Because we cannot have any Unity.XXX references here, let's embed our dependencies using some custom made code
        //that just suit our need for sake of testing
        private static SyntaxTree[] GetUnityNetCodeMetaRefs()
        {
            string hackyUnityRefs = @"
namespace Unity
{
    namespace Entities
    {
        public interface IComponentData
        {
        }

        public interface IBufferElementData
        {
        }

        public struct Entity
        {
        }
    }
    namespace Mathematics
    {
        public struct float2
        {
            public float x;
            public float y;
        }
        public struct float3
        {
            public float x;
            public float y;
            public float z;

            public float3 ShouldBeSkipped
            {
                get { return new float3();}
                set {}
            }
            public float this[int index] { get {return 0.0f;} set {}}
        }

        public struct float4
        {
            public float x;
            public float y;
            public float z;
            public float w;

            public float4 ShouldBeSkipped
            {
                get { return new float4();}
                set {}
            }
            public float this[int index] { get {return 0.0f;} set {}}
        }

        public struct quaternion
        {
            public float4 Value;
        }
    }
    namespace Collections
    {
        public struct FixedString32Bytes
        {
        }
        public struct FixedString64Bytes
        {
        }
        public struct FixedString128Bytes
        {
        }
        public struct FixedString512Bytes
        {
        }
        public struct FixedString4096Bytes
        {
        }
    }
    namespace Transforms
    {
        public struct Translation : Entities.IComponentData
        {
            public Mathematics.float3 Value;
        }
        public struct Rotation : Entities.IComponentData
        {
            public Mathematics.quaternion Value;
        }
    }
}";
            return new[]
            {
                CSharpSyntaxTree.ParseText(hackyUnityRefs),
                CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
                    "../../Authoring/GhostFieldAttribute.cs"))),
                CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
                    "../../Authoring/GhostComponentAttribute.cs"))),
                CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
                    "../../Authoring/GhostModifiers.cs"))),
                CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
                    "../../Authoring/GhostComponentVariation.cs"))),
                CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
                    "../../Authoring/DontSupportPrefabOverridesAttribute.cs"))),
                CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
                    "../../Authoring/SubTypes.cs"))),
                CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
                    "../../Command/ICommandData.cs"))),
                CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
                    "../../Rpc/IRpcCommand.cs"))),
                CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
                    "../../Authoring/UserDefinedTemplates.cs"))),
                CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
                    "../../Command/IInputComponentData.cs"))),
                CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
                    "../../ClientServerWorld/NetworkTime.cs"))),
            };
        }
    }
}
