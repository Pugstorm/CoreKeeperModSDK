using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Unity.NetCode.GeneratorTests
{
    class SyntaxReceiver_Tests : BaseTest
    {
        [Test]
        public void SyntaxReceiver_FindTypes()
        {
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker {Receiver = receiver};

            CSharpSyntaxTree.ParseText(TestDataSource.AllComponentsTypesData).GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(3, receiver.Candidates.Count);
        }

        [Test]
        public void SyntaxReceiver_SkipGenericInterface()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;
            public struct MyTest : IEquatable<MyTest>, IComponentData
            {
                [GhostField] public int IntValue;
            }
            public struct MyTest2 : IComponentData, IEquatable<MyRpcType>
            {
                [GhostField] public int IntValue;
            }
            ";
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker {Receiver = receiver};

            CSharpSyntaxTree.ParseText(testData).GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(2, receiver.Candidates.Count);
        }

        [Test]
        public void SyntaxReceiver_FindVariants()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;
            using Unity.Transforms;

            [GhostComponentVariation(typeof(Translation))]
            public struct MyFirstVariantA
            {
                [GhostField] public float3 Value;
            }
            [GhostComponentVariationAttribute(typeof(Translation))]
            public struct MyFirstVariantB
            {
                [GhostField] public float3 Value;
            }
            [Unity.NetCode.GhostComponentVariation(typeof(Translation))]
            public struct MyFirstVariantC
            {
                [GhostField(Quantization=1000)] public float3 Value;
            }
            [Unity.NetCode.GhostComponentVariationAttribute(typeof(Translation))]
            public struct MyFirstVariantD
            {
                [GhostField] public float3 Value;
            }
            ";
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker {Receiver = receiver};

            CSharpSyntaxTree.ParseText(testData).GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(0, receiver.Candidates.Count);
            Assert.AreEqual(4, receiver.Variants.Count);
        }

        [Test]
        public void DistinguesComponentTypeCorrectly()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;
            using Unity.Transforms;

            public struct IsBuffer : IBufferElementData
            {
                [GhostField] public int Value;
            }
            public struct IsCommandData : ICommandData
            {
                public Unity.NetCode.NetworkTick Tick {get;set;}
                [GhostField] public int Value;
            }
            public struct IsComponent : IComponentData
            {
                [GhostField] public int Value;
            }
            ";
            var syntaxTree = CSharpSyntaxTree.ParseText(testData);
            var compilation = GeneratorTestHelpers.CreateCompilation(syntaxTree);
            var bufferModel = compilation.GetSymbolsWithName("IsBuffer").FirstOrDefault();
            var commandModel = compilation.GetSymbolsWithName("IsCommandData").FirstOrDefault();
            var componentModel = compilation.GetSymbolsWithName("IsComponent").FirstOrDefault();
            Assert.IsNotNull(bufferModel);
            Assert.IsNotNull(commandModel);
            Assert.IsNotNull(componentModel);
            Assert.IsTrue(Roslyn.Extensions.IsBuffer(bufferModel as ITypeSymbol));
            Assert.IsTrue(Roslyn.Extensions.IsCommand(commandModel as ITypeSymbol));
            //Is Command is also a buffer, lets check if the inheritance works
            Assert.IsTrue(Roslyn.Extensions.IsBuffer(commandModel as ITypeSymbol));
            Assert.IsTrue(Roslyn.Extensions.IsComponent(componentModel as ITypeSymbol));
        }

        [Test]
        public void SyntaxReceiver_NestedTypes()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;
            public struct MyTest
            {
                public struct InnerComponent : IComponentData
                {
                    [GhostField] int IntType;
                }
                public struct RpcComponent : IRpcCommand
                {
                    [GhostField] int IntType;
                }
                public struct SerializedCommand : ICommandData
                {
                    [GhostField] public Tick {get;set;}
                    [GhostField] int IntType;
                }
                public struct Command : ICommandData
                {
                    public Tick {get;set;}
                    int IntType;
                }
            }";
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker {Receiver = receiver};

            CSharpSyntaxTree.ParseText(testData).GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(4, receiver.Candidates.Count);
        }

        [Test]
        public void SyntaxReceiver_Namespaces()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;
            namespace MyTestNS
            {
                public struct InnerComponent : IComponentData
                {
                    [GhostField] int IntType;
                }
                public struct RpcComponent : IRpcCommand
                {
                    public Tick {get;set;}
                    [GhostField] int IntType;
                }
                public struct Command : ICommandData
                {
                    int IntType;
                }
            }
            namespace My.Nested.Namespace
            {
                public struct InnerComponent : IComponentData
                {
                    [GhostField] int IntType;
                }
                public struct RpcComponent : IRpcCommand
                {
                    [GhostField] int IntType;
                }
                public struct Command : ICommandData
                {
                    public Tick {get;set;}
                    int IntType;
                }
            }

            ";
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker {Receiver = receiver};

            CSharpSyntaxTree.ParseText(testData).GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(6, receiver.Candidates.Count);
        }
    }
}
