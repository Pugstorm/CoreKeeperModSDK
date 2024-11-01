using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Model;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities.Hybrid;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGenerators.Test
{
    public static class CSharpSourceGeneratorVerifier<TSourceGenerator>
        where TSourceGenerator : ISourceGenerator, new()
    {
        /// <summary>
        /// if true a throwing verification test will replace the old source of truth with the correct source of truth.
        /// </summary>
        static bool k_OverrideSourceOfTruthOnTestThrow = false;

        public static DiagnosticResult CompilerError(string compileError) => DiagnosticResult.CompilerError(compileError);
        public static DiagnosticResult CompilerWarning(string compilerWarning) => DiagnosticResult.CompilerWarning(compilerWarning);
        public static DiagnosticResult CompilerInfo(string compilerInfo) => new (compilerInfo, DiagnosticSeverity.Info);

        public static async Task VerifySourceGeneratorAsync(string source, string generatedFolderName = "Default", params string[] expectedFileNames)
            => await VerifySourceGeneratorAsync(source, DiagnosticResult.EmptyDiagnosticResults, preprocessorSymbols: Array.Empty<string>(), true, generatedFolderName, expectedFileNames);

        public static async Task VerifySourceGeneratorWithPreprocessorSymbolAsync(string source, string[] preprocessorSymbols, string generatedFolderName = "Default", params string[] expectedFileNames)
            => await VerifySourceGeneratorAsync(source, DiagnosticResult.EmptyDiagnosticResults, preprocessorSymbols, true, generatedFolderName, expectedFileNames);
        public static async Task VerifySourceGeneratorAsync(string source, params DiagnosticResult[] expected)
            => await VerifySourceGeneratorAsync(source, expected, preprocessorSymbols: Array.Empty<string>(), false);

        public static async Task VerifySourceGeneratorAsync(string source, DiagnosticResult expected, Assembly additionalAssemblyOverride)
            => await VerifySourceGeneratorAsync(source, new []{expected}, new []{additionalAssemblyOverride}, preprocessorSymbols: Array.Empty<string>(), false);

        static async Task VerifySourceGeneratorAsync(string source, DiagnosticResult[] expected, string[] preprocessorSymbols, bool checksGeneratedSource = true, string generatedFolderName = "Default", params string[] expectedFileNames)
            => await VerifySourceGeneratorAsync(source, expected, new []{
            typeof(EntitiesMock).Assembly,
            typeof(EntitiesHybridMock).Assembly,
            typeof(BurstMock).Assembly,
            typeof(CollectionsMock).Assembly
        }, preprocessorSymbols, checksGeneratedSource, generatedFolderName, expectedFileNames);

        static async Task VerifySourceGeneratorAsync(string source, DiagnosticResult[] expected, IEnumerable<Assembly> additionalAssembliesOverride, string[] preprocessorSymbols, bool checksGeneratedSource = true, string generatedFolderName = "Default", params string[] expectedFileNames)
        {
            // Initial Test setup
            var test = new Test(preprocessorSymbols)
            {
                TestCode = source.ReplaceLineEndings()
            };
            foreach (var additionalReference in additionalAssembliesOverride)
                test.TestState.AdditionalReferences.Add(additionalReference);

            // Create results folder if not present
            var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

            try
            {
                // Include expected files with name from expectedFileNames from results folder as expected source generated files!
                if (checksGeneratedSource)
                {
                    var generatedFolderPath = Path.Join(executingAssemblyPath, "results", generatedFolderName);
                    Directory.CreateDirectory(generatedFolderPath);
                    var foundExpectedFiles = Directory.EnumerateFiles(generatedFolderPath).Select(Path.GetFileName).Where(expectedFileNames.Contains);
                    var existingSources = foundExpectedFiles.Select(file =>
                    {
                        if (file == null)
                            throw new InvalidOperationException();

                        var fileDir = Path.Join(generatedFolderPath, file);
                        var text = File.ReadAllText(fileDir).ReplaceLineEndings();
                        return (Test.GetTestPath(file), SourceText.From(text, Encoding.UTF8));
                    });
                    test.TestState.GeneratedSources.AddRange(existingSources);
                }

                // Run Test
                test.TestBehaviors = checksGeneratedSource ? TestBehaviors.None : TestBehaviors.SkipGeneratedSourcesCheck;
                test.ExpectedDiagnostics.AddRange(expected);
                await test.RunAsync(CancellationToken.None);
            }
            catch (Exception)
            {
                // Rethrow Quickly if we don't check for generated sources
                if (!checksGeneratedSource || !k_OverrideSourceOfTruthOnTestThrow)
                    throw;

                // Generate Correct Sources on throw
                var (correctSources, originalSource) = await test.GenerateCorrectSources();

                // Asserts an error if what it generated is different than the files input by the test
                var generatedFilesNotPartOfExpectedFileNames = correctSources.Select(s => s.fileName).Where(s => !expectedFileNames.Contains(s)).ToArray();
                if (generatedFilesNotPartOfExpectedFileNames.Length > 0)
                {
                    var filesJoined = string.Join(", ", generatedFilesNotPartOfExpectedFileNames.Select(actualFileName => $"\"{actualFileName}\""));
                    var plural = generatedFilesNotPartOfExpectedFileNames.Length > 1;
                    var anoutputfile = plural ? "output files" : "an output file";
                    var fileNames = plural ? "filenames" : "filename";
                    var was = plural ? "were" : "was";
                    var @is = plural ? "are" : "is";
                    var argument = plural ? "arguments" : "argument";
                    var expectedFileNameDiffErrorMsg = $"The test {generatedFolderName} generated {anoutputfile} which {was} not listed when calling VerifyCS.VerifySourceGeneratorAsync. ";
                    expectedFileNameDiffErrorMsg += $"To fix, add the {fileNames} that {@is} expected to be output from this test as the last {argument} to VerifySourceGeneratorAsync. ";
                    expectedFileNameDiffErrorMsg += $"(`await VerifyCS.VerifySourceGeneratorAsync(source, nameof({generatedFolderName}), {filesJoined});`).";
                    Assert.Fail($"{expectedFileNameDiffErrorMsg}");
                }

                // Recreate Testing Folder
                var verificationPath = Path.Join(Path.Combine(executingAssemblyPath, "..","..",".."), "results", generatedFolderName);
                if (Directory.Exists(verificationPath))
                {
                    var dir = new DirectoryInfo(verificationPath);
                    dir.Attributes &= ~FileAttributes.ReadOnly;
                    dir.Delete(true); // deletes folders including files in it.
                }
                Directory.CreateDirectory(verificationPath);

                // Write new sources
                var writers = new Task[correctSources.Length+1];
                for (var i = 0; i < correctSources.Length; i++)
                    writers[i] = File.WriteAllTextAsync(Path.Join(verificationPath, correctSources[i].fileName), correctSources[i].content.ToString());
                writers[^1] = File.WriteAllTextAsync(Path.Join(verificationPath, originalSource.fileName), originalSource.content.ToString());
                Task.WaitAll(writers);

                // Make sure to still throw original error.
                throw;
            }
        }

        class Test : CSharpSourceGeneratorTest<TSourceGenerator, MSTestVerifier>
        {
            public Test(string[] preprocessorSymbols)
            {
                ReferenceAssemblies = new ReferenceAssemblies("net6.0", new PackageIdentity("Microsoft.NETCore.App.Ref", "6.0.0"), Path.Combine("ref", "net6.0"));
                SolutionTransforms.Add((solution, projectId) =>
                {
                    var project = solution.GetProject(projectId);

                    var compilationOptions = project?.CompilationOptions;

                    if (compilationOptions == null)
                        throw new ArgumentException("ProjectId does not exist");

                    var parseOptions = project?.ParseOptions;

                    if (parseOptions == null)
                        parseOptions = new CSharpParseOptions(preprocessorSymbols: preprocessorSymbols);
                    else if (parseOptions is CSharpParseOptions cSharpParseOptions)
                        parseOptions =
                            cSharpParseOptions.WithPreprocessorSymbols(
                                cSharpParseOptions.PreprocessorSymbolNames.Concat(preprocessorSymbols));

                    compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
                        compilationOptions.SpecificDiagnosticOptions.SetItems(CSharpVerifierHelper.NullableWarnings));

                    return solution.WithProjectCompilationOptions(projectId, compilationOptions).WithProjectParseOptions(projectId, parseOptions);
                });
            }

            /// <summary>
            /// Folder used by TSourceGenerator to output generated source-files into.
            /// The test expects file-paths to match.
            /// </summary>
            /// <param name="filename">
            /// Filename of file we expect generator to have created.
            /// </param>
            /// <returns>relative filepath of source generated files</returns>
            public static string GetTestPath(string filename)
                => Path.Join(GetFilePathPrefixForGenerator(typeof(TSourceGenerator)), filename);

            public async Task<((string fileName, SourceText content)[] generatedSources, (string fileName, SourceText content) source)> GenerateCorrectSources()
            {
                // Initial Setup
                var cancellationToken = CancellationToken.None;
                var fixableDiagnostics = ImmutableArray<string>.Empty;
                var testState = TestState.WithInheritedValuesApplied(null, fixableDiagnostics).WithProcessedMarkup(MarkupOptions, null, ImmutableArray<DiagnosticDescriptor>.Empty, fixableDiagnostics, DefaultFilePath);
                var sourceGenerators = GetSourceGenerators().ToImmutableArray();

                // Create project with applied generators
                var project = await CreateProjectAsync(new EvaluatedProjectState(testState, ReferenceAssemblies), testState.AdditionalProjects.Values.Select(additionalProject => new EvaluatedProjectState(additionalProject, ReferenceAssemblies)).ToImmutableArray(), cancellationToken);
                (project, _) = await ApplySourceGeneratorAsync(sourceGenerators, project, Verify, cancellationToken).ConfigureAwait(false);

                // Splits project.Documents output into 'Generated Files' and Original Source Input
                var generatedSources = project.Documents
                    .Where(d => d.Name.Contains(".g.cs"))
                    .Select(async doc => (Path.GetFileName(doc.Name), await GetSourceTextFromDocumentAsync(doc, cancellationToken).ConfigureAwait(false)));
                return (generatedSources.Select(t=>t.Result).ToArray(),
                    (Path.GetFileName(project.Documents.First().Name), await GetSourceTextFromDocumentAsync(project.Documents.First(), cancellationToken).ConfigureAwait(false)));
            }

            #region Copied From Roslyn!

            /// <summary>
            /// Based on <see cref="GeneratorDriver.GetFilePathPrefixForGenerator"/> which is internal.
            /// </summary>
            static string GetFilePathPrefixForGenerator(Type sourceGenType)
                => Path.Combine(sourceGenType.Assembly.GetName().Name ?? string.Empty, sourceGenType.FullName!);

            /// <summary>
            /// <see cref="SourceGeneratorTest{TVerifier}.ApplySourceGeneratorAsync"/> is private so this is a copy of it
            /// </summary>
            async Task<(Project project, ImmutableArray<Diagnostic> diagnostics)> ApplySourceGeneratorAsync(ImmutableArray<ISourceGenerator> sourceGenerators, Project project, IVerifier verifier, CancellationToken cancellationToken)
            {
                var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
                verifier.True(compilation is { });

                var driver = CreateGeneratorDriver(project, sourceGenerators).RunGenerators(compilation, cancellationToken);
                var result = driver.GetRunResult();

                var updatedProject = project;
                foreach (var tree in result.GeneratedTrees)
                {
                    updatedProject = updatedProject.AddDocument(tree.FilePath, await tree.GetTextAsync(cancellationToken).ConfigureAwait(false), filePath: tree.FilePath).Project;
                }

                return (updatedProject, result.Diagnostics);
            }

            /// <summary>
            /// <see cref="SourceGeneratorTest{TVerifier}.GetSourceTextFromDocumentAsync"/> is private so this is a copy of it
            /// </summary>
            static async Task<SourceText> GetSourceTextFromDocumentAsync(Document document, CancellationToken cancellationToken)
            {
                var simplifiedDoc = await Simplifier.ReduceAsync(document, Simplifier.Annotation, cancellationToken: cancellationToken).ConfigureAwait(false);
                var formatted = await Formatter.FormatAsync(simplifiedDoc, Formatter.Annotation, cancellationToken: cancellationToken).ConfigureAwait(false);
                return await formatted.GetTextAsync(cancellationToken).ConfigureAwait(false);
            }

            #endregion
        }
    }
}
