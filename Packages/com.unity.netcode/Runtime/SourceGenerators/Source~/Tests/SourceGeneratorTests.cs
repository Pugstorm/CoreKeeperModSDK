using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.NetCode.Generators;
using System.IO;
using System.Linq;

namespace Unity.NetCode.GeneratorTests
{
    // TODO: Add tests for GhostEnabledBits.
    // TODO: Add tests for types moved to SerializationStrategy.

    [TestFixture]
    class SourceGeneratorTests : BaseTest
    {
        [Test]
        public void InnerNamespacesAreHandledCorrectly()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;

            namespace N1
            {
                public struct T1{}
                namespace N2
                {
                    public struct T2{}
                }
            }
            namespace N1.N2.N3
            {
                public struct T3
                {
                }
            }";

            var tree = CSharpSyntaxTree.ParseText(testData);
            var compilation = GeneratorTestHelpers.CreateCompilation(tree);
            var model = compilation.GetSymbolsWithName("T1").FirstOrDefault();
            Assert.IsNotNull(model);
            Assert.AreEqual("N1", Roslyn.Extensions.GetFullyQualifiedNamespace(model));
            Assert.AreEqual("N1.T1", Roslyn.Extensions.GetFullTypeName(model));
            model = compilation.GetSymbolsWithName("T2").FirstOrDefault();
            Assert.IsNotNull(model);
            Assert.AreEqual("N1.N2", Roslyn.Extensions.GetFullyQualifiedNamespace(model));
            Assert.AreEqual("N1.N2.T2", Roslyn.Extensions.GetFullTypeName(model));
            model = compilation.GetSymbolsWithName("T3").FirstOrDefault();
            Assert.IsNotNull(model);
            Assert.AreEqual("N1.N2.N3", Roslyn.Extensions.GetFullyQualifiedNamespace(model));
            Assert.AreEqual("N1.N2.N3.T3", Roslyn.Extensions.GetFullTypeName(model));
        }

        [Test]
        public void DeclaringTypePrependTypeName()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;

            public struct Outer
            {
                public struct Inner
                {
                }
            }

            namespace T1.T2.T3
            {
                public struct Outer
                {
                    public struct InnerWithNS
                    {
                    }
                }
            }";

            var tree = CSharpSyntaxTree.ParseText(testData);
            var compilation = GeneratorTestHelpers.CreateCompilation(tree);
            var model = compilation.GetSymbolsWithName("Inner").FirstOrDefault();
            Assert.IsNotNull(model);
            var fullTypeName = Roslyn.Extensions.GetFullTypeName(model);
            Assert.AreEqual("Outer+Inner", fullTypeName);
            model = compilation.GetSymbolsWithName("InnerWithNS").FirstOrDefault();
            Assert.IsNotNull(model);
            fullTypeName = Roslyn.Extensions.GetFullTypeName(model);
            Assert.AreEqual("T1.T2.T3.Outer+InnerWithNS", fullTypeName);
        }

        [Test]
        public void SourceGenerator_PrimitiveTypes()
        {
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(TestDataSource.TestComponentsData);
            tree.GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(1, walker.Receiver.Candidates.Count);
            //Check generated files match
            var resuls = GeneratorTestHelpers.RunGenerators(tree);
            Assert.AreEqual(2, resuls.GeneratedSources.Length, "Num generated files does not match");
            var outputTree = resuls.GeneratedSources[0].SyntaxTree;
            var snapshotDataSyntax = outputTree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()
                .First(node => node.Identifier.ValueText == "Snapshot");
            var expected = new[]
            {
                //byte
                ("uint", "EnumValue8"),
                //short
                ("int", "EnumValue16"),
                //nothing (default int)
                ("int", "EnumValue32"),
                //long
                ("long", "EnumValue64"),
                ("int", "IntValue"),
                ("uint", "UIntValue"),
                ("long", "LongValue"),
                ("ulong", "ULongValue"),
                ("int", "ShortValue"),
                ("uint", "UShortValue"),
                ("int", "SByteValue"),
                ("uint", "ByteValue"),
                ("uint", "BoolValue"),
                ("float", "FloatValue"),
                ("float", "InterpolatedFloat"),
                ("int", "QuantizedFloat"),
                ("int", "InterpolatedQuantizedFloat")
            };
            var members = snapshotDataSyntax.DescendantNodes().OfType<FieldDeclarationSyntax>().ToArray();
            Assert.AreEqual(expected.Length, members.Length);
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.AreEqual(expected[i].Item1, (members[i].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text,
                    $"{i}");
                Assert.AreEqual(expected[i].Item2, members[i].Declaration.Variables[0].Identifier.Text);
            }
        }

        [Test]
        public void SourceGenerator_Mathematics()
        {
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(TestDataSource.MathematicsTestData);
            tree.GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(1, walker.Receiver.Candidates.Count);

            //Check generated files match
            var resuls = GeneratorTestHelpers.RunGenerators(tree);
            Assert.AreEqual(2, resuls.GeneratedSources.Length, "Num generated files does not match");
            var outputTree = resuls.GeneratedSources[0].SyntaxTree;
            var snapshotDataSyntax = outputTree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()
                .First(node => node.Identifier.ValueText == "Snapshot");
            var members = snapshotDataSyntax.DescendantNodes().OfType<FieldDeclarationSyntax>().ToArray();
            //Each block generate 13 variables
            var numVariablePerBlock = 13;
            Assert.AreEqual(4 * numVariablePerBlock, members.Length);
            for (int i = 0; i < 2 * numVariablePerBlock; ++i)
            {
                Assert.AreEqual("float", (members[i].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text);
            }

            for (int i = 2 * numVariablePerBlock; i < 4 * numVariablePerBlock; ++i)
            {
                Assert.AreEqual("int", (members[i].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text);
            }

            var prefixes = new[] { "", "i", "q", "iq" };
            for (int i = 0, k = 0; i < 4; ++i, k += numVariablePerBlock)
            {
                Assert.AreEqual(prefixes[i] + "Float2Value_x", members[k + 0].Declaration.Variables[0].Identifier.Text);
                Assert.AreEqual(prefixes[i] + "Float2Value_y", members[k + 1].Declaration.Variables[0].Identifier.Text);
                Assert.AreEqual(prefixes[i] + "Float3Value_x", members[k + 2].Declaration.Variables[0].Identifier.Text);
                Assert.AreEqual(prefixes[i] + "Float3Value_y", members[k + 3].Declaration.Variables[0].Identifier.Text);
                Assert.AreEqual(prefixes[i] + "Float3Value_z", members[k + 4].Declaration.Variables[0].Identifier.Text);
                Assert.AreEqual(prefixes[i] + "Float4Value_x", members[k + 5].Declaration.Variables[0].Identifier.Text);
                Assert.AreEqual(prefixes[i] + "Float4Value_y", members[k + 6].Declaration.Variables[0].Identifier.Text);
                Assert.AreEqual(prefixes[i] + "Float4Value_z", members[k + 7].Declaration.Variables[0].Identifier.Text);
                Assert.AreEqual(prefixes[i] + "Float4Value_w", members[k + 8].Declaration.Variables[0].Identifier.Text);
                Assert.AreEqual(prefixes[i] + "QuaternionValueX",
                    members[k + 9].Declaration.Variables[0].Identifier.Text);
                Assert.AreEqual(prefixes[i] + "QuaternionValueY",
                    members[k + 10].Declaration.Variables[0].Identifier.Text);
                Assert.AreEqual(prefixes[i] + "QuaternionValueZ",
                    members[k + 11].Declaration.Variables[0].Identifier.Text);
                Assert.AreEqual(prefixes[i] + "QuaternionValueW",
                    members[k + 12].Declaration.Variables[0].Identifier.Text);
            }
        }

        [Test]
        public void SourceGenerator_GenerateCorrectFiles()
        {
            const string testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;

            public struct TestComponent : IComponentData
            {
                [GhostField] public int x;
            }
            ";
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(testData);
            tree.GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(1, walker.Receiver.Candidates.Count);
            //Make a full pass: generate the code and write files to disk
            GeneratorTestHelpers.RunGeneratorsWithOptions(new Dictionary<string, string> { {GlobalOptions.WriteFilesToDisk, "1"}}, tree);
            Assert.IsTrue(File.Exists($"{GeneratorTestHelpers.OutputFolder}/{GeneratorTestHelpers.GeneratedAssemblyName}/TestComponentSerializer.cs"));
            Assert.IsTrue(File.Exists($"{GeneratorTestHelpers.OutputFolder}/{GeneratorTestHelpers.GeneratedAssemblyName}/GhostComponentSerializerCollection.cs"));
        }

        [Test]
        public void SourceGenerator_NestedTypes()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;
            public struct MyTest
            {
                public struct Nested
                {
                    public float2 f;
                    public int a;
                    public long b;
                }
                public struct InnerComponent : IComponentData
                {
                    [GhostField] public float x;
                    [GhostField] public float y;
                    [GhostField] public Nested m;
                }
            }";
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(testData);
            tree.GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(1, walker.Receiver.Candidates.Count);

            var results = GeneratorTestHelpers.RunGenerators(tree);
            Assert.AreEqual(2, results.GeneratedSources.Length, "Num generated files does not match");
            var outputTree = results.GeneratedSources[0].SyntaxTree;
            var snapshotDataSyntax = outputTree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()
                .First(node => node.Identifier.ValueText == "Snapshot");
            var expected = new[]
            {
                ("float", "x"),
                ("float", "y"),
                ("float", "m_f_x"),
                ("float", "m_f_y"),
                ("int", "m_a"),
                ("long", "m_b"),
            };
            var maskBits = outputTree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>()
                .First(t => t.Declaration.Variables[0].Identifier.ValueText == "ChangeMaskBits");
            var equalsValueClauseSyntax = maskBits.Declaration.Variables[0].Initializer;
            Assert.IsNotNull(equalsValueClauseSyntax);
            Assert.AreEqual("5", equalsValueClauseSyntax!.Value.ToString());
            var members = snapshotDataSyntax.DescendantNodes().OfType<FieldDeclarationSyntax>().ToArray();
            Assert.AreEqual(expected.Length, members.Length);
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.AreEqual(expected[i].Item1, (members[i].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text,
                    $"{i}");
                Assert.AreEqual(expected[i].Item2, members[i].Declaration.Variables[0].Identifier.Text);
            }
        }

        [Test]
        public void SourceGenerator_CompositeTemplates()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;
            public struct AllCompositeTemplates : IComponentData
            {
            [GhostField] public float2 f2;
            [GhostField] public float3 f3;
            [GhostField] public float4 f4;
            [GhostField] public quaternion q;
            }
            ";
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(testData);
            tree.GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(1, walker.Receiver.Candidates.Count);
            var results = GeneratorTestHelpers.RunGenerators(tree);
            var outputTree = results.GeneratedSources[0].SyntaxTree;
            var maskBits = outputTree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>()
                .First(t => t.Declaration.Variables[0].Identifier.ValueText == "ChangeMaskBits");
            Assert.IsNotNull(maskBits.Declaration.Variables[0].Initializer);
            Assert.AreEqual("4", maskBits.Declaration.Variables[0].Initializer?.Value.ToString());
        }

        [Test]
        public void SourceGenerator_CompositeFlags()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;

            //Normally it would have 2 bits mask.
            //If used with aggregation will have one bit mask
            public struct TwoFieldStruct
            {
                public float x; //1bit
                public float y; //1bit
            }
            //not mask or bits
            public struct EmptyStruct
            {
            }
            //float2 uses 1bits mask, float 1 bit total 2 bits
            //by setting composite (outside) we expect the whole struct takes 1 bit
            public struct InnerCompositeStruct
            {
                public float2 f; //1bit always
                public int g; //1bit
                public TwoFieldStruct tf; //2bits
                [GhostField(Composite=true) public TwoFieldStruct ctf; //1bit
            }

            public struct ComponentA : IComponentData
            {
                [GhostField] public Empty e;  //0 bit
                [GhostField(Composite=true)] public InnerCompositeStruct a; //1bit
                [GhostField(Composite=true)] public TwoFieldStruct b; //1bit
            }
            public struct ComponentB : IComponentData
            {
                [GhostField] public Empty e; //0 bit
                [GhostField(Composite=false)] public InnerCompositeStruct a; //5bit (because composite cannot affect float2)
                [GhostField(Composite=false)] public TwoFieldStruct b; //2bit
            }";
            var tree = CSharpSyntaxTree.ParseText(testData);
            var resuls = GeneratorTestHelpers.RunGenerators(tree);
            Assert.AreEqual(3, resuls.GeneratedSources.Length, "Num generated files does not match");

            void CheckOutput(SyntaxTree outputTree, int numBits, (string, string)[] fields)
            {
                var snapshotDataSyntax = outputTree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()
                    .First(node => node.Identifier.ValueText == "Snapshot");
                var maskBits = outputTree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>()
                    .First(t => t.Declaration.Variables[0].Identifier.ValueText == "ChangeMaskBits");

                Assert.IsNotNull(maskBits.Declaration.Variables[0].Initializer);
                Assert.AreEqual(numBits.ToString(), maskBits.Declaration.Variables[0].Initializer!.Value.ToString());
                var members = snapshotDataSyntax.DescendantNodes().OfType<FieldDeclarationSyntax>().ToArray();
                Assert.AreEqual(fields.Length, members.Length);
                for (int i = 0; i < fields.Length; ++i)
                {
                    Assert.AreEqual(fields[i].Item1, (members[i].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text,
                        $"{i}");
                    Assert.AreEqual(fields[i].Item2, members[i].Declaration.Variables[0].Identifier.Text);
                }
            }

            var expected = new[]
            {
                ("float", "a_f_x"),
                ("float", "a_f_y"),
                ("int", "a_g"),
                ("float", "a_tf_x"),
                ("float", "a_tf_y"),
                ("float", "a_ctf_x"),
                ("float", "a_ctf_y"),
                ("float", "b_x"),
                ("float", "b_y"),
            };
            CheckOutput(resuls.GeneratedSources[0].SyntaxTree, 2, expected);
            CheckOutput(resuls.GeneratedSources[1].SyntaxTree, 7, expected);
        }

        [Test]
        public void SourceGenerator_FlatType()
        {
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(TestDataSource.FlatTypeTest);
            tree.GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(1, walker.Receiver.Candidates.Count);

            var results = GeneratorTestHelpers.RunGenerators(tree);
            var errors = results.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            Assert.AreEqual(2, results.GeneratedSources.Length, "Num generated files does not match");
            Assert.AreEqual(0, errors.Length);
            var maskBits = results.GeneratedSources[0].SyntaxTree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>()
                .First(t => t.Declaration.Variables[0].Identifier.ValueText == "ChangeMaskBits");
            Assert.IsNotNull(maskBits.Declaration.Variables[0].Initializer);
            Assert.AreEqual("44", maskBits.Declaration.Variables[0].Initializer!.Value.ToString());
        }

        [Test]
        public void SourceGenerator_Recurse()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;
            using Unity.Transforms;
            namespace Unity.NetCode
            {
                public struct TestRecurse : IComponentData
                {
                    public int x;
                    public int this[int index]
                    {
                        get { return this.x; }
                        set { x = value; }
                    }
                    public TestRecurse DontSerialize { get { return new TestRecurse();} set {}}
                }

                public struct ProblematicType : IComponentData
                {
                    [GhostField] public TestRecurse MyType;
                }
            }";
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(testData);
            tree.GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(2, walker.Receiver.Candidates.Count);

            var resuls = GeneratorTestHelpers.RunGenerators(tree);
            Assert.AreEqual(0, resuls.Diagnostics.Count(m=>m.Severity == DiagnosticSeverity.Error));
            Assert.AreEqual(2, resuls.GeneratedSources.Length, "Num generated files does not match");
        }

        [Test]
        public void SourceGenerator_TransformsVariants()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;
            using Unity.Transforms;
            namespace Unity.NetCode
            {
                [GhostComponentVariation(typeof(Transforms.Translation))]
                [GhostComponent(PrefabType=GhostPrefabType.All, SendTypeOptimization=GhostSendType.All)]
                public struct TranslationVariant
                {
                    [GhostField(Composite=true,Smoothing=SmoothingAction.Interpolate)] public float3 Value;
                }

                //This in invalid and should report an error
                [GhostComponentVariation(typeof(Transforms.Rotation))]
                public struct InvalidRotation
                {
                    [GhostField] public float3 Value;
                }

                [GhostComponentVariation(typeof(Transforms.Rotation))]
                [GhostComponent(PrefabType=GhostPrefabType.All, SendTypeOptimization=GhostSendType.All)]
                public struct RotationVariant
                {
                    [GhostField(Composite=true,Quantization=100, Smoothing=SmoothingAction.Interpolate)] public quaternion Value;
                }
            }";

            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(testData);
            tree.GetCompilationUnitRoot().Accept(walker);
            //All the variants are detected as candidates
            Assert.AreEqual(3, walker.Receiver.Variants.Count);

            var resuls = GeneratorTestHelpers.RunGenerators(tree);
            var diagnostics = resuls.Diagnostics;
            //Expect to see one error
            Assert.AreEqual(1, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error), "errorCount");
            Assert.AreEqual("InvalidRotation: Cannot find member Value type: float3 in Rotation",
                diagnostics.First(d => d.Severity == DiagnosticSeverity.Error).GetMessage());
            Assert.AreEqual(3, resuls.GeneratedSources.Length, "Num generated files does not match");

            var outputTree = resuls.GeneratedSources[0].SyntaxTree;
            var snapshotDataSyntax = outputTree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()
                .First(node => node.Identifier.ValueText == "Snapshot");
            //Quantizatio not used
            var expected = new[]
            {
                ("float", "Value_x"),
                ("float", "Value_y"),
                ("float", "Value_z"),
            };
            var maskBits = outputTree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>()
                .First(t => t.Declaration.Variables[0].Identifier.ValueText == "ChangeMaskBits");
            Assert.IsNotNull(maskBits.Declaration.Variables[0].Initializer);
            Assert.AreEqual("1", maskBits.Declaration.Variables[0].Initializer!.Value.ToString());
            var members = snapshotDataSyntax.DescendantNodes().OfType<FieldDeclarationSyntax>().ToArray();
            Assert.AreEqual(expected.Length, members.Length);
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.AreEqual(expected[i].Item1, (members[i].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text,
                    $"{i}");
                Assert.AreEqual(expected[i].Item2, members[i].Declaration.Variables[0].Identifier.Text);
            }

            expected = new[]
            {
                ("int", "ValueX"),
                ("int", "ValueY"),
                ("int", "ValueZ"),
                ("int", "ValueW"),
            };
            outputTree = resuls.GeneratedSources[1].SyntaxTree;
            snapshotDataSyntax = outputTree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()
                .First(node => node.Identifier.ValueText == "Snapshot");

            maskBits = outputTree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>()
                .First(t => t.Declaration.Variables[0].Identifier.ValueText == "ChangeMaskBits");
            Assert.IsNotNull(maskBits.Declaration.Variables[0].Initializer);
            Assert.AreEqual("1", maskBits.Declaration.Variables[0].Initializer!.Value.ToString());
            members = snapshotDataSyntax.DescendantNodes().OfType<FieldDeclarationSyntax>().ToArray();
            Assert.AreEqual(expected.Length, members.Length);
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.AreEqual(expected[i].Item1, (members[i].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text,
                    $"{i}");
                Assert.AreEqual(expected[i].Item2, members[i].Declaration.Variables[0].Identifier.Text);
            }
        }

        [Test]
        public void SourceGenerator_VariantUseCorrectClassTypeAndHash()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;
            using Unity.Transforms;
            namespace Unity.NetCode
            {
                [GhostComponentVariationAttribute(typeof(Transforms.Translation))]
                [GhostComponent(PrefabType=GhostPrefabType.All, SendTypeOptimization=GhostSendType.All)]
                public struct VariantTest
                {
                    [GhostField(Smoothing=SmoothingAction.Interpolate)] public float3 Value;
                }

                //This in invalid and should report an error (type not present in the base class)
                [GhostComponentVariation(typeof(Transforms.Rotation))]
                public struct InvalidVariant
                {
                    [GhostField] public float3 Value;
                }
            }";

            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(testData);
            tree.GetCompilationUnitRoot().Accept(walker);
            //All the variants are detected as candidates
            Assert.AreEqual(2, walker.Receiver.Variants.Count);
            var results = GeneratorTestHelpers.RunGenerators(tree);
            Assert.AreEqual(2, results.GeneratedSources.Length, "Num generated files does not match");
            var diagnostics = results.Diagnostics;
            //Expect to see one error
            Assert.AreEqual(1, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));
            Assert.AreEqual("InvalidVariant: Cannot find member Value type: float3 in Rotation",
                diagnostics.First(d => d.Severity == DiagnosticSeverity.Error).GetMessage());
            //Parse the output and check for the class name match what we expect
            var outputTree = results.GeneratedSources[0].SyntaxTree;
            var initBlockWalker = new InializationBlockWalker();
            outputTree.GetCompilationUnitRoot().Accept(initBlockWalker);
            Assert.IsNotNull(initBlockWalker.Intializer);
            var componentTypeAssignment = initBlockWalker.Intializer!.Expressions
                .First(e => ((AssignmentExpressionSyntax)e).Left.ToString() == "ComponentType");
            Assert.IsTrue(componentTypeAssignment.ToString().Contains("Unity.Transforms.Translation"),
                componentTypeAssignment.ToString());
            var variantHashField = initBlockWalker.Intializer.Expressions
                .First(e => ((AssignmentExpressionSyntax)e).Left.ToString() == "VariantHash");
            Assert.IsTrue(variantHashField.IsKind(SyntaxKind.SimpleAssignmentExpression));
            Assert.AreNotEqual("0", ((AssignmentExpressionSyntax)variantHashField).Right.ToString());
            //Check that the GhostSerializerAttribute also is present and initialized correctly
            var serializationAttribute = outputTree.GetRoot().DescendantNodes()
                .First(n => n.IsKind(SyntaxKind.Attribute) && ((AttributeSyntax)n).Name.ToString() == "GhostSerializer");
            Assert.AreEqual(2, ((AttributeSyntax)serializationAttribute).ArgumentList?.Arguments.Count);
            Assert.AreEqual("typeof(Unity.Transforms.Translation)",
                ((AttributeSyntax)serializationAttribute).ArgumentList?.Arguments[0].ToString());
            Assert.AreNotEqual("0", ((AttributeSyntax)serializationAttribute).ArgumentList?.Arguments[1].ToString());
        }

        [Test]
        public void SourceGenerator_Command_GenerateBufferSerializer()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;

            namespace Unity.NetCode { public struct NetworkTick { } }

            [GhostComponent(PrefabType=GhostPrefabType.All, SendTypeOptimization=GhostSendType.Predicted)]
            public struct CommandTest : ICommandData
            {
                [GhostField]public Unity.NetCode.NetworkTick Tick {get;set;}
                [GhostField]public int Value;
            }
            ";

            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(testData);
            tree.GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(1, walker.Receiver.Candidates.Count);
            var results = GeneratorTestHelpers.RunGenerators(tree);
            Assert.AreEqual(3, results.GeneratedSources.Length, "Num generated files does not match");
            var diagnostics = results.Diagnostics;
            //Expect to see one error
            if (diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error) != 0)
            {
                foreach (var d in diagnostics)
                {
                    if (d.Severity == DiagnosticSeverity.Error)
                        Console.WriteLine(d.GetMessage());
                }

                Assert.True(false, "Error found");
            }

            //Parse the output and check for the class name match what we expect
            // Ironically, the real ICommandData has `[DontSerializeForCommand] NetworkTick Tick`.
            var expected = new[] { ("int", "Value"), ("uint", "Tick") };

            var outputTree = results.GeneratedSources[0].SyntaxTree;
            var snapshotDataSyntax = outputTree.GetRoot().DescendantNodes()
                .OfType<StructDeclarationSyntax>()
                .First(node => node.Identifier.ValueText == "Snapshot");
            var members = snapshotDataSyntax.DescendantNodes().OfType<FieldDeclarationSyntax>().ToArray();
            Assert.AreEqual(expected.Length, members.Length);
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.AreEqual(expected[i].Item1, (members[i].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text,
                    $"{i}");
                Assert.AreEqual(expected[i].Item2, members[i].Declaration.Variables[0].Identifier.Text);
            }
        }

        [Test]
        public void SourceGenerator_ErrorIsReportedIfPropertiesAresInvalid()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;
            using Unity.Transforms;

            namespace Unity.NetCode { public struct NetworkTick { } }

            public struct Buffer : IBufferElementData
            {
                [GhostField] public int BValue1;                                 // Fine.
                public int BValue2;                                              // ! All fields must be GhostFields.
                [GhostField] public int BValue3;                                 // Fine.
                public int BValue4 { get; private set; }                         // ! All properties must be GhostFields.
                [GhostField] public int BValue6 { get; private set; }            // ! GhostFields must have public setters.
                [GhostField(SendData = false)] public int BValue7 { get; set; }  // Fine (SendData = false is allowed).
                [GhostField(SendData = false)] public int BValue8;               // Fine (SendData = false is allowed).
            }
            public struct CommandData : ICommandData
            {
                public Unity.NetCode.NetworkTick Tick {get;set;}                         // Fine.
                public int CValue1;                                                      // ! All fields must be GhostFields.
                [GhostField] public int CValue2;                                         // Fine.
                public ulong CValue3 { get; private set; }                               // Fine (properties with implicit backing fields can be non-GhostFields).
                [GhostField] public int CValue4 { get; }                                 // ! GhostFields must have setters.
                [GhostField(SendData = false)] public int CValue5 { get; private set; }  // Fine (SendData = false is allowed).
                [GhostField(SendData = false)] public int CValue6 { private get; set; }  // Fine (SendData = false is allowed).
            }
            public struct ComponentData : IComponentData
            {
                public int VValue1;                                    // Fine.
                [GhostField] public int VValue2;                       // Fine.
                public float VValue3 { get; private set; }             // Fine.
                public Unity.NetCode.NetworkTick VValue4 {set;}        // ! GhostFields must have getters.
                [GhostField] public int this[int i] { get {} set {} }  // ! GhostFields must not be indexers.
            }
            ";
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(testData);
            tree.GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(3, walker.Receiver.Candidates.Count);
            var results = GeneratorTestHelpers.RunGenerators(tree);
            //only the command serializer
            Assert.AreEqual(3, results.GeneratedSources.Length, "Num generated files does not match");
            //But some errors are reported too
            var diagnostics = results.Diagnostics.Where(m=>m.Severity == DiagnosticSeverity.Error).ToArray();
            int i = 0;
            Assert.True(diagnostics[i++].GetMessage().StartsWith("GhostField present on an invalid property Buffer.BValue6: Setter is not public.", StringComparison.Ordinal));
            Assert.True(diagnostics[i++].GetMessage().StartsWith("GhostField missing on field Buffer.BValue2.", StringComparison.Ordinal));
            Assert.True(diagnostics[i++].GetMessage().StartsWith("GhostField present on an invalid property CommandData.CValue4: No setter.", StringComparison.Ordinal));
            Assert.True(diagnostics[i++].GetMessage().StartsWith("GhostField missing on field CommandData.CValue1.", StringComparison.Ordinal));
            Assert.True(diagnostics[i++].GetMessage().StartsWith("GhostField missing on field CommandData.Tick.", StringComparison.Ordinal));
            Assert.True(diagnostics[i++].GetMessage().StartsWith("GhostField present on an invalid property ComponentData.this[int]: Indexer.", StringComparison.Ordinal));
            Assert.True(diagnostics[i++].GetMessage().StartsWith("GhostField present on an invalid property CommandData.CValue4: No setter.", StringComparison.Ordinal));
            Assert.AreEqual(7, diagnostics.Length);
        }

        [Test]
        public void SourceGenerator_ErrorIsReported_IfStructInheritFromMultipleInterfaces()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;
            using Unity.Transforms;

            namespace Unity.NetCode { public struct NetworkTick { } }

            namespace Test
            {
                public struct Invalid1 : IComponentData, IRpcCommand
                {
                    public int Value1;
                }
                public struct Invalid2 : IComponentData, ICommandData
                {
                    public Unity.NetCode.NetworkTick Tick {get;set}
                    public int Value1;
                }
                public struct Invalid3 : IComponentData, IBufferElementData
                {
                    public int Value1;
                }
                public struct Invalid4: IBufferElementData, ICommandData
                {
                    public Unity.NetCode.NetworkTick Tick {get;set}
                    public int Value1;
                }
                public struct Invalid5 : IBufferElementData, IRpcCommand
                {
                    public int Value1;
                }
            }
            ";
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(testData);
            tree.GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(5, walker.Receiver.Candidates.Count);
            var results = GeneratorTestHelpers.RunGenerators(tree);
            Assert.AreEqual(0, results.GeneratedSources.Length, "Num generated files does not match");
            var diagnostics = results.Diagnostics.Where(m=>m.Severity == DiagnosticSeverity.Error).ToArray();
            Assert.AreEqual(5, diagnostics.Length);
            Assert.True(diagnostics[0].GetMessage()
                .StartsWith("struct Test.Invalid1 cannot implement Component,Rpc interfaces at the same time", StringComparison.Ordinal));
            Assert.True(diagnostics[1].GetMessage()
                .StartsWith("struct Test.Invalid2 cannot implement Component,CommandData interfaces at the same time", StringComparison.Ordinal));
            Assert.True(diagnostics[2].GetMessage()
                .StartsWith("struct Test.Invalid3 cannot implement Component,Buffer interfaces at the same time", StringComparison.Ordinal));
            Assert.True(diagnostics[3].GetMessage()
                .StartsWith("struct Test.Invalid4 cannot implement Buffer,CommandData interfaces at the same time", StringComparison.Ordinal));
            Assert.True(diagnostics[4].GetMessage()
                .StartsWith("struct Test.Invalid5 cannot implement Buffer,Rpc interfaces at the same time", StringComparison.Ordinal));
        }

        [Test]
        public void SourceGenerator_SubTypes()
        {
            var customTemplates = @"
            using Unity.NetCode;
            using System.Collections.Generic;
            namespace Unity.NetCode.Generators
            {
                internal static partial class UserDefinedTemplates
                {
                    static partial void RegisterTemplates(List<TypeRegistryEntry> templates)
                    {
                        templates.AddRange(new[]
                        {
                            new TypeRegistryEntry
                            {
                                Type = ""System.Single"",
                                Quantized = false,
                                Smoothing = SmoothingAction.Clamp
                                SupportCommand = false,
                                Composite = false,
                                SubType = 1
                                Template = $""NetCode.GhostSnapshotValueFloatUnquantized.cs""
                            },
                        });
                    }
                }
            }";
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;

            public struct MyType : IComponentData
            {
                [GhostField(SubType=1)] public float AngleType;
            }
            ";
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(testData);
            tree.GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(1, walker.Receiver.Candidates.Count);
            //Check generated files match
            var templateTree = CSharpSyntaxTree.ParseText(customTemplates);
            var results = GeneratorTestHelpers.RunGenerators(tree, templateTree);
            Assert.AreEqual(2, results.GeneratedSources.Length, "Num generated files does not match");

            var outputTree = results.GeneratedSources[0].SyntaxTree;
            var snapshotDataSyntax = outputTree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()
                .First(node => node.Identifier.ValueText == "Snapshot");
            var expected = new[]
            {
                ("float", "AngleType"),
            };
            var members = snapshotDataSyntax.DescendantNodes().OfType<FieldDeclarationSyntax>().ToArray();
            Assert.AreEqual(expected.Length, members.Length);
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.AreEqual(expected[i].Item1, (members[i].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text,
                    $"{i}");
                Assert.AreEqual(expected[i].Item2, members[i].Declaration.Variables[0].Identifier.Text);
            }
        }

        [Test]
        public void SourceGenerator_GhostComponentWithNoFields()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;

            [GhostComponent]
            public struct MyData : IComponentData
            {
                public float MyField;
            }
            [GhostComponent]
            public struct MyCommand : ICommandData
            {
                public float MyField;
            }
            [GhostComponent]
            public struct MyBuffer : IBufferElementData
            {
                public float MyField;
            }
            ";

            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(testData);
            tree.GetCompilationUnitRoot().Accept(walker);
            var results = GeneratorTestHelpers.RunGenerators(tree);

            // No error during processing
            Assert.AreEqual(0, results.Diagnostics.Count(m => m.Severity == DiagnosticSeverity.Error));
            // No ghost snapshot serializer is generated (but does contain serializer collection with empty variants + client-to-server command serializer)
            Assert.AreEqual(2, results.GeneratedSources.Length, "Num generated files does not match");
            Assert.IsTrue(results.GeneratedSources[0].SourceText.ToString().Contains("SerializerIndex = -1"));
            Assert.AreEqual(false, results.GeneratedSources[1].SyntaxTree.ToString().Contains("GhostComponentSerializer.State"));
        }

        [Test]
        public void SourceGenerator_GhostComponentWithInvalidField()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;

            [GhostComponent]
            public struct MyType : IComponentData
            {
                [GhostField] public char MyField;
            }
            ";

            var tree = CSharpSyntaxTree.ParseText(testData);
            var results = GeneratorTestHelpers.RunGenerators(tree);
            // foreach (var msg in results.Diagnostics)
            //     Console.WriteLine($"ERROR: {msg.GetMessage()}");
            var errors = results.Diagnostics.Where(m => m.Severity == DiagnosticSeverity.Error).ToArray();
            Assert.AreEqual(1, errors.Length);
            Assert.IsTrue(errors[0].GetMessage().Contains("Inside type 'MyType', we could not find the exact template for field 'MyField' with configuration 'Type:System.Char Key:System.Char (quantized=-1 composite=False smoothing=0 subtype=0)'"));
        }

        [Test]
        public void SourceGenerator_QuantizeError()
        {
            var customTemplates = @"
            using Unity.NetCode;
            using System.Collections.Generic;
            namespace Unity.NetCode.Generators
            {
                internal static partial class UserDefinedTemplates
                {
                    static partial void RegisterTemplates(List<TypeRegistryEntry> templates)
                    {
                        templates.AddRange(new[]
                        {
                            new TypeRegistryEntry
                            {
                                Type = ""System.Single"",
                                Quantized = true,
                                Smoothing = SmoothingAction.Clamp
                                SupportCommand = false,
                                Composite = false,
                                SubType = 1,
                                Template = $""NetCode.GhostSnapshotValueFloat.cs""
                            },
                        });
                    }
                }
            }";
            var testDataWrong = @"
            using Unity.Entities;
            using Unity.NetCode;

            public struct MyType : IComponentData
            {
                [GhostField(SubType=1)] public float AngleType;
            }
            ";
            var testDataCorrect = @"
            using Unity.Entities;
            using Unity.NetCode;

            public struct MyType : IComponentData
            {
                [GhostField(SubType=1, Quantization=1)] public float AngleType;
            }
            ";

            var tree = CSharpSyntaxTree.ParseText(testDataWrong);
            var templateTree = CSharpSyntaxTree.ParseText(customTemplates);
            var results = GeneratorTestHelpers.RunGenerators(tree, templateTree);
            var diagnostics = results.Diagnostics.Where(m => m.Severity == DiagnosticSeverity.Error).ToArray();
            Assert.AreEqual(2, diagnostics.Length);
            Assert.IsTrue(diagnostics[0].GetMessage().Contains("Unable to find the Template associated with 'TypeRegistryEntry:[Type: System.Single, Template: NetCode.GhostSnapshotValueFloat.cs, TemplateOverride: , SubType: 1, Smoothing: Clamp, Quantized: True, SupportCommand: False, Composite: False]'."));

            tree = CSharpSyntaxTree.ParseText(testDataCorrect);
            templateTree = CSharpSyntaxTree.ParseText(customTemplates);
            results = GeneratorTestHelpers.RunGenerators(tree, templateTree);
            Assert.AreEqual(2, results.GeneratedSources.Length, "Num generated files does not match");
            var outputTree = results.GeneratedSources[0].SyntaxTree;
            var snapshotDataSyntax = outputTree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()
                .First(node => node.Identifier.ValueText == "Snapshot");
            var expected = new[]
            {
                ("int", "AngleType"),
            };
            var members = snapshotDataSyntax.DescendantNodes().OfType<FieldDeclarationSyntax>().ToArray();
            Assert.AreEqual(expected.Length, members.Length);
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.AreEqual(expected[i].Item1, (members[i].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text, $"{i}");
                Assert.AreEqual(expected[i].Item2, members[i].Declaration.Variables[0].Identifier.Text);
            }
        }

        [Test]
        public void SourceGenerator_SubTypeCompositeError()
        {
            var customTemplates = @"
            using Unity.NetCode;
            using System.Collections.Generic;
            namespace Unity.NetCode.Generators
            {
                internal static partial class UserDefinedTemplates
                {
                    static partial void RegisterTemplates(List<TypeRegistryEntry> templates)
                    {
                        templates.AddRange(new[]
                        {
                            new TypeRegistryEntry
                            {
                                Type = ""Unity.Mathematics.float3"",
                                SubType = 1,
                                Quantized = true,
                                Smoothing = SmoothingAction.InterpolateAndExtrapolate,
                                SupportCommand = false,
                                Composite = true,
                                Template = ""/Path/To/MyTemplate"",
                            }
                        });
                    }
                }
            }";
            var testData = @"
            using Unity.Mathematics;
            using Unity.NetCode;
            using Unity.Transforms;

            [GhostComponentVariation(typeof(Translation), ""Translation - 2D"")]
            [GhostComponent(PrefabType = GhostPrefabType.All, SendTypeOptimization = GhostSendType.All)]
            public struct Translation2d
            {
                [GhostField(Quantization=1000, Smoothing=SmoothingAction.InterpolateAndExtrapolate, SubType=1)]
                public float3 Value;
            }
            ";
            //this is an hacky way to make this supported by both 2020.x and 2021+
            //we se the templateId the same as the path, so this is resolved correclty in both case.
            var additionalTexts = ImmutableArray.Create(new AdditionalText[]
            {
                new GeneratorTestHelpers.InMemoryAdditionalFile(
                    $"/Path/To/MyTemplate{NetCodeSourceGenerator.NETCODE_ADDITIONAL_FILE}",
                    $"#templateid:/Path/To/MyTemplate\n{TestDataSource.CustomTemplate}")
            });

            var tree = CSharpSyntaxTree.ParseText(testData);
            {
                var templateTree = CSharpSyntaxTree.ParseText(customTemplates);
                var compilation = GeneratorTestHelpers.CreateCompilation(tree, templateTree);
                var driver = GeneratorTestHelpers.CreateGeneratorDriver().AddAdditionalTexts(additionalTexts);
                var results = driver.RunGenerators(compilation).GetRunResult();
                var diagnostics = results.Diagnostics.Where(m=>m.Severity == DiagnosticSeverity.Error).ToArray();
                Assert.That(diagnostics[0].GetMessage().Contains("Subtyped types cannot also be defined as composite"));
            }

            customTemplates = customTemplates.Replace("Composite = true", "Composite = false", StringComparison.Ordinal);
            {
                // Fix issue and verify it now works as expected (composite true->false)
                var templateTree = CSharpSyntaxTree.ParseText(customTemplates);
                var compilation = GeneratorTestHelpers.CreateCompilation(tree, templateTree);
                var driver = GeneratorTestHelpers.CreateGeneratorDriver().AddAdditionalTexts(additionalTexts);
                var results = driver.RunGenerators(compilation).GetRunResult();
                Assert.AreEqual(2, results.GeneratedTrees.Length, "Num generated files does not match");
                var outputTree = results.GeneratedTrees[0];
                var snapshotDataSyntax = outputTree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()
                    .First(node => node.Identifier.ValueText == "Snapshot");
                var expected = new[]
                {
                    ("int", "ValueX"),
                    ("int", "ValueY"),
                };
                var members = snapshotDataSyntax.DescendantNodes().OfType<FieldDeclarationSyntax>().ToArray();
                Assert.AreEqual(expected.Length, members.Length);
                for (int i = 0; i < expected.Length; ++i)
                {
                    Assert.AreEqual(expected[i].Item1, (members[i].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text, $"{i}");
                    Assert.AreEqual(expected[i].Item2, members[i].Declaration.Variables[0].Identifier.Text);
                }
            }
        }

        [Test]
        public void SourceGenerator_GhostComponentAttributeDefaultsAreCorrect()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            public struct DefaultComponent : IComponentData
            {
                [GhostField] public int Value;
            }";

            var tree = CSharpSyntaxTree.ParseText(testData);
            var results = GeneratorTestHelpers.RunGenerators(tree);

            //Parse the output and check that the flag on the generated class is correct (one source is registration system)
            Assert.AreEqual(2, results.GeneratedSources.Count(), "Num generated files does not match");
            var outputTree = results.GeneratedSources[0].SyntaxTree;
            var initBlockWalker = new InializationBlockWalker();
            outputTree.GetCompilationUnitRoot().Accept(initBlockWalker);
            Assert.IsNotNull(initBlockWalker.Intializer);

            // SendTypeOptimization=GhostSendType.All and PrefabType=GhostPrefabType.All makes the SendMask interpolated+predicted
            var componentTypeAssignment = initBlockWalker.Intializer!.Expressions.First(e =>
                ((AssignmentExpressionSyntax) e).Left.ToString() == "SendMask") as AssignmentExpressionSyntax;
            Assert.That(componentTypeAssignment, Is.Not.Null);
            Assert.AreEqual(componentTypeAssignment!.Right.ToString(),
                "GhostComponentSerializer.SendMask.Interpolated|GhostComponentSerializer.SendMask.Predicted");

            // OwnerSendType = SendToOwnerType.All
            componentTypeAssignment = initBlockWalker.Intializer.Expressions.FirstOrDefault(e =>
                ((AssignmentExpressionSyntax) e).Left.ToString() == "SendToOwner") as AssignmentExpressionSyntax;
            Assert.That(componentTypeAssignment, Is.Not.Null);
            Assert.AreEqual(componentTypeAssignment!.Right.ToString(), "SendToOwnerType.All");

            // TODO: Fix this, as it has been moved to the SS.
            // SendDataForChildEntity = false
            // componentTypeAssignmet = initBlockWalker.intializer.Expressions.FirstOrDefault(e =>
            //         ((AssignmentExpressionSyntax) e).Left.ToString() == "SendForChildEntities") as
            //     AssignmentExpressionSyntax;
            // Assert.IsNotNull(componentTypeAssignmet);
            // Assert.AreEqual(componentTypeAssignmet.Right.ToString(), "0");
        }

        [Test]
        public void SourceGenerator_SendToChildEntityIsSetCorrectly()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;
            using Unity.Transforms;
            namespace Unity.NetCode
            {
                public struct SendToChildDefault : IComponentData
                {
                    [GhostField] public int Value;
                }
                [GhostComponent(SendDataForChildEntity=true)]
                public struct SendToChild : IComponentData
                {
                    [GhostField] public int Value;
                }
                [GhostComponent(SendDataForChildEntity=false)]
                public struct DontSendToChild : IComponentData
                {
                    [GhostField] public int Value;
                }
            }";

            var tree = CSharpSyntaxTree.ParseText(testData);
            var results = GeneratorTestHelpers.RunGenerators(tree);

            Assert.AreEqual(4, results.GeneratedSources.Length, "Num generated files does not match");
            var diagnostics = results.Diagnostics;
            Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));
            //Parse the output and check that the flag on the generated class is correct
            for (int i = 0; i < 3; ++i)
            {
                var outputTree = results.GeneratedSources[i].SyntaxTree;
                var initBlockWalker = new InializationBlockWalker();
                outputTree.GetCompilationUnitRoot().Accept(initBlockWalker);
                Assert.IsNotNull(initBlockWalker.Intializer);

                // TODO: Fix this, as it has been moved to the SS.
                // var componentTypeAssignmet = initBlockWalker.intializer.Expressions.FirstOrDefault(e =>
                //         ((AssignmentExpressionSyntax) e).Left.ToString() == "SendForChildEntities") as
                //     AssignmentExpressionSyntax;
                // Assert.IsNotNull(componentTypeAssignmet);
                // Assert.AreEqual(componentTypeAssignmet.Right.ToString(), (i == 1 ? "1" : "0"), "Only the GhostComponent explicitly sending child entities should have that flag.");
            }
        }

        [Test]
        [TestCase(GhostPrefabType.All, GhostSendType.AllClients,
            ExpectedResult =
                "GhostComponentSerializer.SendMask.Interpolated|GhostComponentSerializer.SendMask.Predicted")]
        [TestCase(GhostPrefabType.All, GhostSendType.OnlyPredictedClients,
            ExpectedResult = "GhostComponentSerializer.SendMask.Predicted")]
        [TestCase(GhostPrefabType.All, GhostSendType.OnlyInterpolatedClients,
            ExpectedResult = "GhostComponentSerializer.SendMask.Interpolated")]
        [TestCase(GhostPrefabType.PredictedClient, GhostSendType.OnlyPredictedClients,
            ExpectedResult = "GhostComponentSerializer.SendMask.Predicted")]
        [TestCase(GhostPrefabType.PredictedClient, GhostSendType.OnlyInterpolatedClients,
            ExpectedResult = "GhostComponentSerializer.SendMask.Predicted")]
        [TestCase(GhostPrefabType.InterpolatedClient, GhostSendType.OnlyPredictedClients,
            ExpectedResult = "GhostComponentSerializer.SendMask.Interpolated")]
        [TestCase(GhostPrefabType.InterpolatedClient, GhostSendType.OnlyInterpolatedClients,
            ExpectedResult = "GhostComponentSerializer.SendMask.Interpolated")]
        [TestCase(GhostPrefabType.Server, GhostSendType.AllClients, ExpectedResult = "GhostComponentSerializer.SendMask.None")]
        public string SourceGenerator_SendType_IsSetCorrectly(GhostPrefabType prefabType, GhostSendType sendType)
        {
            var testData = $@"
            using Unity.Entities;
            using Unity.NetCode;
            using Unity.Mathematics;
            using Unity.Transforms;

            [GhostComponent(PrefabType=GhostPrefabType.{prefabType}, SendTypeOptimization=GhostSendType.{sendType})]
            public struct SendToChild : IComponentData
            {{
                [GhostField] public int Value;
            }}
            }}";

            var tree = CSharpSyntaxTree.ParseText(testData);
            var results = GeneratorTestHelpers.RunGenerators(tree);

            Assert.AreEqual(2, results.GeneratedSources.Length, "Num generated files does not match");
            var diagnostics = results.Diagnostics;
            Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));
            //Parse the output and check that the flag on the generated class is correct
            var outputTree = results.GeneratedSources[0].SyntaxTree;
            var initBlockWalker = new InializationBlockWalker();
            outputTree.GetCompilationUnitRoot().Accept(initBlockWalker);
            Assert.IsNotNull(initBlockWalker.Intializer);
            var componentTypeAssignment = initBlockWalker.Intializer!.Expressions.FirstOrDefault(e =>
                ((AssignmentExpressionSyntax) e).Left.ToString() == "SendMask") as AssignmentExpressionSyntax;
            Assert.IsNotNull(componentTypeAssignment);
            return componentTypeAssignment!.Right.ToString();
        }

        [Test]
        public void SourceGenerator_Validate_OnlyReport_KeywordNotSubst()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;

            namespace Unity.NetCode { public struct NetworkTick { } }

            namespace __GHOST_NAMESPACE__
            {
                public enum InvalidEnum
                {
                    Value = 0
                }
                public struct CantBeValid : IComponentData
                {
                    [GhostField]
                    public int field;
                }
            }
            namespace __UNDERSCORE_IS_WELCOME__
            {
                public struct __DUNNO_WHAT_BUT_IT_IS_VALID__ : IComponentData
                {
                    [GhostField]
                    public int __GHOST_IS_RESERVED;
                    [GhostField]
                    public int __ValidField;
                }

                public struct __My_Command__: ICommandData
                {
                    public Unity.NetCode.NetworkTick Tick {get;set;}
                    public int __ValidField;
                    public int __COMMAND_IS_RESERVED;
                }
            }";

            var tree = CSharpSyntaxTree.ParseText(testData);
            var results = GeneratorTestHelpers.RunGenerators(tree);
            var errorCount = 0;
            for (int i = 0; i < results.Diagnostics.Length; ++i)
            {
                if (results.Diagnostics[i].Severity == DiagnosticSeverity.Error)
                {
                    Console.WriteLine(results.Diagnostics[i].ToString());
                    ++errorCount;
                }
            }

            Assert.AreEqual(3, errorCount, "errorCount");
        }

        [Test]
        public void SourceGenerator_DisambiguateEntity()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            namespace Unity.Entities
            {
                public struct Entity<T>
                {
                    public Entity ent;
                }
            }
            namespace B
            {
                public struct TestComponent : IComponentData
                {
                    [GhostField] public Entity<int> genericEntity;
                }

                public struct TestComponent2 : IComponentData
                {
                    [GhostField] public Entity entity;
                }
            }
            ";
            var tree = CSharpSyntaxTree.ParseText(testData);
            var results = GeneratorTestHelpers.RunGenerators(tree);
            Assert.AreEqual(0, results.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Error));
            Assert.AreEqual(3, results.GeneratedSources.Length, "Num generated files does not match");
            Assert.IsTrue(results.GeneratedSources[0].SourceText.ToString().Contains("TestComponent"));
            Assert.IsTrue(results.GeneratedSources[1].SourceText.ToString().Contains("TestComponent2"));

            var outputTree = results.GeneratedSources[0].SyntaxTree;
            var snapshotDataSyntax = outputTree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>().First(node => node.Identifier.ValueText == "Snapshot");
            var members = snapshotDataSyntax.DescendantNodes().OfType<FieldDeclarationSyntax>().ToArray();
            Assert.AreEqual("int", (members[0].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text);
            Assert.AreEqual("genericEntity_ent", members[0].Declaration.Variables[0].Identifier.Text);
            Assert.AreEqual("uint", (members[1].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text);
            Assert.AreEqual("genericEntity_entSpawnTick", members[1].Declaration.Variables[0].Identifier.Text);

            outputTree = results.GeneratedSources[1].SyntaxTree;
            snapshotDataSyntax = outputTree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>().First(node => node.Identifier.ValueText == "Snapshot");
            members = snapshotDataSyntax.DescendantNodes().OfType<FieldDeclarationSyntax>().ToArray();
            Assert.AreEqual("int", (members[0].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text);
            Assert.AreEqual("entity", members[0].Declaration.Variables[0].Identifier.Text);
            Assert.AreEqual("uint", (members[1].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text);
            Assert.AreEqual("entitySpawnTick", members[1].Declaration.Variables[0].Identifier.Text);
        }


        // NW: Test broken in master, not fixing in branch.
        [Test]
        public void SourceGenerator_SameClassInDifferentNamespace_UseCorrectHintName()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            namespace A
            {
                public struct TestComponent : IComponentData
                {
                    [GhostField] public int value;
                }
            }
            namespace B
            {
                public struct TestComponent : IComponentData
                {
                    [GhostField] public int value;
                }
            }
            ";
            var tree = CSharpSyntaxTree.ParseText(testData);
            var results = GeneratorTestHelpers.RunGenerators(tree);
            var errorCount = 0;
            for (int i = 0; i < results.Diagnostics.Length; ++i)
            {
                if (results.Diagnostics[i].Severity == DiagnosticSeverity.Error)
                {
                    Console.WriteLine(results.Diagnostics[i].ToString());
                    ++errorCount;
                }
            }
            Assert.AreEqual(0, errorCount);
            Assert.AreEqual(3, results.GeneratedSources.Length, "Num generated files does not match");
            var hintA=Generators.Utilities.TypeHash.FNV1A64(Path.Combine(GeneratorTestHelpers.GeneratedAssemblyName, "A_TestComponentSerializer.cs"));
            var hintB=Generators.Utilities.TypeHash.FNV1A64(Path.Combine(GeneratorTestHelpers.GeneratedAssemblyName, "B_TestComponentSerializer.cs"));
            var hintG=Generators.Utilities.TypeHash.FNV1A64(Path.Combine(GeneratorTestHelpers.GeneratedAssemblyName, "GhostComponentSerializerCollection.cs"));
            Assert.AreEqual($"{hintA}.cs",results.GeneratedSources[0].HintName);
            Assert.AreEqual($"{hintB}.cs",results.GeneratedSources[1].HintName);
            Assert.AreEqual($"{hintG}.cs",results.GeneratedSources[2].HintName);
        }

        // NW: Test broken in master, not fixing in branch.
        [Test]
        public void SourceGenerator_VeryLongFileName_Works()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            namespace VERYVERYVERYLONG.VERYVERYVERYLONG.VERYVERYVERYLONG.VERYVERYVERYLONG.VERYVERYVERYLONG.VERYVERYVERYLONG.VERYVERYVERYLONG.VERYVERYVERYLONG.VERYVERYVERYLONG
            {
                public struct TestComponent : IComponentData
                {
                    [GhostField] public int value;
                }
            }
            ";
            var tree = CSharpSyntaxTree.ParseText(testData);
            var results = GeneratorTestHelpers.RunGenerators(tree);
            var errorCount = 0;
            for (int i = 0; i < results.Diagnostics.Length; ++i)
            {
                if (results.Diagnostics[i].Severity == DiagnosticSeverity.Error)
                {
                    Console.WriteLine(results.Diagnostics[i].ToString());
                    ++errorCount;
                }
            }
            Assert.AreEqual(0, errorCount);
            Assert.AreEqual(2, results.GeneratedSources.Length, "Num generated files does not match");
            var expetedHint1=Generators.Utilities.TypeHash.FNV1A64(Path.Combine(GeneratorTestHelpers.GeneratedAssemblyName,
                "VERYVERYVERYLONG.VERYVERYVERYLONG.VERYVERYVERYLONG.VERYVERYVERYLONG.VERYVERYVERYLONG.VERYVERYVERYLONG.VERYVERYVERYLONG.VERYVERYVERYLONG.VERYVERYVERYLONG_TestComponentSerializer.cs"));
            Assert.AreEqual($"{expetedHint1}.cs",results.GeneratedSources[0].HintName);
            var expetedHint2=Generators.Utilities.TypeHash.FNV1A64(Path.Combine(GeneratorTestHelpers.GeneratedAssemblyName, "GhostComponentSerializerCollection.cs"));
            Assert.AreEqual($"{expetedHint2}.cs",results.GeneratedSources[1].HintName);
        }

        [Test]
        public void SourceGenerator_InputComponentData()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            namespace Unity.Test
            {
                public struct PlayerInput : IInputComponentData
                {
                    public int Horizontal;
                    public int Vertical;
                    public InputEvent Jump;
                }
            }
            ";
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(testData);
            tree.GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(1, walker.Receiver.Candidates.Count);

            // Should get input buffer struct (IInputBufferData etc) and the command data (ICommandDataSerializer etc) generated from that
            // and the registration system with the empty variant registration data
            var results = GeneratorTestHelpers.RunGenerators(tree);
            Assert.AreEqual(3, results.GeneratedSources.Length, "Num generated files does not match");
            var bufferSourceData = results.GeneratedSources[0].SyntaxTree;
            var commandSourceData = results.GeneratedSources[1].SyntaxTree;

            var inputBufferSyntax = bufferSourceData.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()
                .FirstOrDefault(node => node.Identifier.ValueText == "PlayerInputInputBufferData");
            Assert.IsNotNull(inputBufferSyntax);
            var commandSyntax = commandSourceData.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()
                .FirstOrDefault(node => node.Identifier.ValueText == "PlayerInputInputBufferDataSerializer");
            Assert.IsNotNull(commandSyntax);

            // Verify the 3 variables are being serialized in the command serialize methods (normal one and baseline one)
            var commandSerializerSyntax = commandSourceData.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(node => node.Identifier.ValueText == "Serialize");
            Assert.IsNotNull(commandSerializerSyntax);
            Assert.AreEqual(2, commandSerializerSyntax.Count());
            foreach (var serializerMethod in commandSerializerSyntax)
                Assert.AreEqual(3, serializerMethod.GetText().Lines.Where((line => line.ToString().Contains("data."))).Count());
        }

        [Test]
        public void SourceGenerator_InputComponentDataComplex()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            public class ParentClass1
            {
                public class ParentClass2
                {
                    public struct PlayerInput : IInputComponentData
                    {
                        public DataComposition Data;
                    }
                }
            }

            struct DataComposition
            {
                public int Horizontal;
                public int Vertical;
                public InputEvent Jump;
            }
            ";
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(testData);
            tree.GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(1, walker.Receiver.Candidates.Count);

            // 1 - ParentClass1_ParentClass2_PlayerInputInputBufferData
            // 2 - ParentClass1_ParentClass2_PlayerInputInputBufferDataSerializer
            // 3 - GhostComponentSerializerRegistrationSystem
            var results = GeneratorTestHelpers.RunGenerators(tree);
            Assert.AreEqual(3, results.GeneratedSources.Length, "Num generated files does not match");
            var bufferSourceData = results.GeneratedSources[0].SyntaxTree;
            var commandSourceData = results.GeneratedSources[1].SyntaxTree;

            var inputBufferSyntax = bufferSourceData.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()
                .FirstOrDefault(node => node.Identifier.ValueText == "ParentClass1_ParentClass2_PlayerInputInputBufferData");
            Assert.IsNotNull(inputBufferSyntax);
            var commandSyntax = commandSourceData.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()
                .FirstOrDefault(node => node.Identifier.ValueText == "ParentClass1_ParentClass2_PlayerInputInputBufferDataSerializer");
            Assert.IsNotNull(commandSyntax);

            // Verify the 3 variables are being serialized in the command serialize methods (normal one and baseline one)
            var commandSerializerSyntax = commandSourceData.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(node => node.Identifier.ValueText == "Serialize");
            Assert.IsNotNull(commandSerializerSyntax);
            Assert.AreEqual(2, commandSerializerSyntax.Count());
            foreach (var serializerMethod in commandSerializerSyntax)
                Assert.AreEqual(3, serializerMethod.GetText().Lines.Where((line => line.ToString().Contains("data."))).Count());
        }

        [Test]
        public void SourceGenerator_InputComponentData_RemotePlayerInputPrediction()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;

            namespace Unity.NetCode
            {
                public struct NetworkTick { }
                public interface IInputComponentData
                {
                     public NetworkTick Tick {get;set}
                }
            }

            namespace Unity.Test
            {
                public struct PlayerInput : IInputComponentData
                {
                    [GhostField] public int Horizontal;
                    [GhostField] public int Vertical;
                    [GhostField] public InputEvent Jump;
                }
            }
            ";
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(testData);
            tree.GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(1, walker.Receiver.Candidates.Count);

            var results = GeneratorTestHelpers.RunGenerators(tree);
            Assert.AreEqual(4, results.GeneratedSources.Length, "Num generated files does not match");
            var bufferSourceData = results.GeneratedSources[0].SyntaxTree;
            var commandSourceData = results.GeneratedSources[1].SyntaxTree;
            var componentSourceData = results.GeneratedSources[2].SyntaxTree;
            var registrationSourceData = results.GeneratedSources[3].SyntaxTree;
            var inputBufferSyntax = bufferSourceData.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()
                .FirstOrDefault(node => node.Identifier.ValueText == "PlayerInputInputBufferData");
            Assert.IsNotNull(inputBufferSyntax);

            var commandSyntax = commandSourceData.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()
                .First(node => node.Identifier.ValueText == "PlayerInputInputBufferDataSerializer");
            var sourceText = commandSyntax.GetText();
            Assert.AreEqual(0, sourceText.Lines.Where((line => line.ToString().Contains("data.Tick"))).Count());

            var componentSyntax = componentSourceData.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()
                .First(node => node.Identifier.ValueText == "PlayerInputInputBufferDataGhostComponentSerializer");

            // Verify the component snapshot data is set up correctly, this means the ghost fields
            // are configured properly in the generated input buffer for remote player prediction
            var snapshotSyntax = componentSyntax.DescendantNodes().OfType<StructDeclarationSyntax>()
                .First(node => node.Identifier.ValueText == "Snapshot");
            var fields = snapshotSyntax.DescendantNodes().OfType<FieldDeclarationSyntax>().ToArray();
            Assert.AreEqual("int", (fields[0].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text);
            Assert.AreEqual("InternalInput_Horizontal", fields[0].Declaration.Variables[0].Identifier.Text);
            Assert.AreEqual("int", (fields[1].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text);
            Assert.AreEqual("InternalInput_Vertical", fields[1].Declaration.Variables[0].Identifier.Text);
            Assert.AreEqual("uint", (fields[2].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text);
            Assert.AreEqual("InternalInput_Jump_Count", fields[2].Declaration.Variables[0].Identifier.Text);
            // Ironically, the real ICommandData has `[DontSerializeForCommand] NetworkTick Tick`.
            Assert.AreEqual("uint", (fields[3].Declaration.Type as PredefinedTypeSyntax)?.Keyword.Text);
            Assert.AreEqual("Tick", fields[3].Declaration.Variables[0].Identifier.Text);

            // Verify the ghost component parameters are set up properly for the input buffer to synch
            // in the ghost snapshots for remote players
            sourceText = componentSyntax.GetText();
            Assert.AreEqual(1, sourceText.Lines.Where((line => line.ToString().Contains("PrefabType = GhostPrefabType.All"))).Count());
            Assert.AreEqual(1, sourceText.Lines.Where((line => line.ToString().Contains("SendMask = GhostComponentSerializer.SendMask.Interpolated|GhostComponentSerializer.SendMask.Predicted"))).Count());
            Assert.AreEqual(1, sourceText.Lines.Where((line => line.ToString().Contains("SendToOwner = SendToOwnerType.SendToNonOwner"))).Count());

            var maskBits = componentSyntax.DescendantNodes().OfType<FieldDeclarationSyntax>()
                .First(t => t.Declaration.Variables[0].Identifier.ValueText == "ChangeMaskBits");
            var equalsValueClauseSyntax = maskBits.Declaration.Variables[0].Initializer;
            Assert.That(equalsValueClauseSyntax, Is.Not.Null);
            Assert.AreEqual("4", equalsValueClauseSyntax!.Value.ToString());

            var registrationSyntax = registrationSourceData.GetRoot().DescendantNodes().OfType<SimpleBaseTypeSyntax>()
                .FirstOrDefault(node => node.ToString().Contains("IGhostComponentSerializerRegistration"));
            Assert.IsNotNull(registrationSyntax);
            Assert.AreEqual(1, registrationSourceData.GetText().Lines.Where((line => line.ToString().Contains("data.AddSerializer(PlayerInputInputBufferDataGhostComponentSerializer.GetState"))).Count());
        }

        [Test]
        public void SourceGenerator_RPC_DontSerializeForCommand()
        {
            var testData = @"
            using Unity.Entities;
            using Unity.NetCode;
            namespace Unity.Test
            {
                public struct MyRpcType : IRpcCommand
                {
                    public int y;
                    public int x;
                    [DontSerializeForCommand] public int z;

                    // will not be serialized due to not having a set method
                    public int area => return x*y;

                    private int w;
                    public int W {
                        get => return w;
                        set => w = value;
                    }

                    // will not be, as private set.
                    public int W2 { get; private set; }

                    [DontSerializeForCommand]
                    public int myProperty {
                        get => return z;
                        set => z = value;
                    }
                }
            }
            ";
            var receiver = GeneratorTestHelpers.CreateSyntaxReceiver();
            var walker = new TestSyntaxWalker { Receiver = receiver };
            var tree = CSharpSyntaxTree.ParseText(testData);
            tree.GetCompilationUnitRoot().Accept(walker);
            Assert.AreEqual(1, walker.Receiver.Candidates.Count);

            var results = GeneratorTestHelpers.RunGenerators(tree);
            Assert.IsNotNull(results);
            var errors = results.Diagnostics.Where(m => m.Severity == DiagnosticSeverity.Error).ToArray();
            Assert.IsEmpty(errors);

            var syntaxTree = results.GeneratedSources[0].SyntaxTree;

            var commandSerializerSyntax = syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(node => node.Identifier.ValueText == "Serialize");
            Assert.IsNotNull(commandSerializerSyntax);
            Assert.AreEqual(1, commandSerializerSyntax.Count());
            foreach (var serializerMethod in commandSerializerSyntax)
                Assert.AreEqual(3, serializerMethod.GetText().Lines.Where((line => line.ToString().Contains("data."))).Count());

            var commandDeserializerSyntax = syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(node => node.Identifier.ValueText == "Deserialize");
            Assert.IsNotNull(commandSerializerSyntax);
            Assert.AreEqual(1, commandDeserializerSyntax.Count());
            foreach (var serializerMethod in commandDeserializerSyntax)
                Assert.AreEqual(3, serializerMethod.GetText().Lines.Where((line => line.ToString().Contains("data."))).Count());
        }
    }
}
