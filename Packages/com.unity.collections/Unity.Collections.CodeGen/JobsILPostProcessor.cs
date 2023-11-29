using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace Unity.Jobs.CodeGen
{   
    // Jobs ILPP entry point
    internal partial class JobsILPostProcessor : ILPostProcessor
    {
        AssemblyDefinition AssemblyDefinition;
        List<DiagnosticMessage> DiagnosticMessages = new List<DiagnosticMessage>();
        public string[] Defines { get; private set; }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            bool madeAnyChange = false;
            Defines = compiledAssembly.Defines;

            try 
            {
                AssemblyDefinition = AssemblyDefinitionFor(compiledAssembly);
            }
            catch (BadImageFormatException)
            {
                return new ILPostProcessResult(null, DiagnosticMessages);
            }

            try
            {
                // This only works because the PostProcessorAssemblyResolver is explicitly loading 
                // transitive dependencies (and then some) and so if we can't find a references to
                // Unity.Jobs (via EarlyInitHelpers) in there than we are confident the assembly doesn't need processing
                var earlyInitHelpers = AssemblyDefinition.MainModule.ImportReference(typeof(EarlyInitHelpers)).CheckedResolve();
            }
            catch (ResolutionException)
            {
                return new ILPostProcessResult(null, DiagnosticMessages);
            }

            madeAnyChange = PostProcessImpl();

            // Hack to remove circular references
            var selfName = AssemblyDefinition.Name.FullName;
            foreach (var referenceName in AssemblyDefinition.MainModule.AssemblyReferences)
            {
                if (referenceName.FullName == selfName)
                {
                    AssemblyDefinition.MainModule.AssemblyReferences.Remove(referenceName);
                    break;
                }
            }

            if (!madeAnyChange || DiagnosticMessages.Any(d => d.DiagnosticType == DiagnosticType.Error))
                return new ILPostProcessResult(null, DiagnosticMessages);

            var pe = new MemoryStream();
            var pdb = new MemoryStream();
            var writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(), SymbolStream = pdb, WriteSymbols = true
            };

            AssemblyDefinition.Write(pe, writerParameters);
            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), DiagnosticMessages);
        }

        public override ILPostProcessor GetInstance()
        {
            return this;
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            if (compiledAssembly.Name.EndsWith("CodeGen.Tests", StringComparison.Ordinal))
                return false;

            if (compiledAssembly.InMemoryAssembly.PdbData == null || compiledAssembly.InMemoryAssembly.PeData == null)
                return false;

            return true;
        }

        // *******************************************************************************
        // ** NOTE
        // ** Everything below this is a copy of the same process used in EntitiesILPostProcessor and 
        // ** should stay synced with it.
        // *******************************************************************************

        class PostProcessorAssemblyResolver : IAssemblyResolver
        {
            private readonly string[] _referenceDirectories;
            private Dictionary<string, HashSet<string>> _referenceToPathMap;
            Dictionary<string, AssemblyDefinition> _cache = new Dictionary<string, AssemblyDefinition>();
            private ICompiledAssembly _compiledAssembly;
            private AssemblyDefinition _selfAssembly;

            public PostProcessorAssemblyResolver(ICompiledAssembly compiledAssembly)
            {
                _compiledAssembly = compiledAssembly;
                _referenceToPathMap = new Dictionary<string, HashSet<string>>();
                foreach (var reference in compiledAssembly.References)
                {
                    var assemblyName = Path.GetFileNameWithoutExtension(reference);
                    if (!_referenceToPathMap.TryGetValue(assemblyName, out var fileList))
                    {
                        fileList = new HashSet<string>();
                        _referenceToPathMap.Add(assemblyName, fileList);
                    }
                    fileList.Add(reference);
                }

                _referenceDirectories = _referenceToPathMap.Values.SelectMany(pathSet => pathSet.Select(Path.GetDirectoryName)).Distinct().ToArray();
            }

            public void Dispose()
            {
            }

            public AssemblyDefinition Resolve(AssemblyNameReference name)
            {
                return Resolve(name, new ReaderParameters(ReadingMode.Deferred));
            }

            public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                {
                    if (name.Name == _compiledAssembly.Name)
                        return _selfAssembly;

                    var fileName = FindFile(name);
                    if (fileName == null)
                        return null;

                    var cacheKey = fileName;

                    if (_cache.TryGetValue(cacheKey, out var result))
                        return result;

                    parameters.AssemblyResolver = this;

                    var ms = MemoryStreamFor(fileName);

                    var pdb = fileName + ".pdb";
                    if (File.Exists(pdb))
                        parameters.SymbolStream = MemoryStreamFor(pdb);

                    var assemblyDefinition = AssemblyDefinition.ReadAssembly(ms, parameters);
                    _cache.Add(cacheKey, assemblyDefinition);
                    return assemblyDefinition;
                }
            }

            private string FindFile(AssemblyNameReference name)
            {
                if (_referenceToPathMap.TryGetValue(name.Name, out var paths))
                {
                    if(paths.Count == 1)
                        return paths.First();

                    // If we have more than one assembly with the same name loaded we now need to figure out which one
                    // is being requested based on the AssemblyNameReference
                    foreach (var path in paths)
                    {
                        var onDiskAssemblyName = AssemblyName.GetAssemblyName(path);
                        if (onDiskAssemblyName.FullName == name.FullName)
                            return path;
                    }
                    throw new ArgumentException($"Tried to resolve a reference in assembly '{name.FullName}' however the assembly could not be found. Known references which did not match: \n{string.Join("\n",paths)}");
                }

                // Unfortunately the current ICompiledAssembly API only provides direct references.
                // It is very much possible that a postprocessor ends up investigating a type in a directly
                // referenced assembly, that contains a field that is not in a directly referenced assembly.
                // if we don't do anything special for that situation, it will fail to resolve.  We should fix this
                // in the ILPostProcessing api. As a workaround, we rely on the fact here that the indirect references
                // are always located next to direct references, so we search in all directories of direct references we
                // got passed, and if we find the file in there, we resolve to it.
                foreach (var parentDir in _referenceDirectories)
                {
                    var candidate = Path.Combine(parentDir, name.Name + ".dll");
                    if (File.Exists(candidate))
                    {
                        if (!_referenceToPathMap.TryGetValue(candidate, out var referencePaths))
                        {
                            referencePaths = new HashSet<string>();
                            _referenceToPathMap.Add(candidate, referencePaths);
                        }
                        referencePaths.Add(candidate);

                        return candidate;
                    }
                }

                return null;
            }

            static MemoryStream MemoryStreamFor(string fileName)
            {
                return Retry(10, TimeSpan.FromSeconds(1), () => {
                    byte[] byteArray;
                    using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        byteArray = new byte[fs.Length];
                        var readLength = fs.Read(byteArray, 0, (int)fs.Length);
                        if (readLength != fs.Length)
                            throw new InvalidOperationException("File read length is not full length of file.");
                    }

                    return new MemoryStream(byteArray);
                });
            }

            private static MemoryStream Retry(int retryCount, TimeSpan waitTime, Func<MemoryStream> func)
            {
                try
                {
                    return func();
                }
                catch (IOException)
                {
                    if (retryCount == 0)
                        throw;
                    Console.WriteLine($"Caught IO Exception, trying {retryCount} more times");
                    Thread.Sleep(waitTime);
                    return Retry(retryCount - 1, waitTime, func);
                }
            }

            public void AddAssemblyDefinitionBeingOperatedOn(AssemblyDefinition assemblyDefinition)
            {
                _selfAssembly = assemblyDefinition;
            }
        }

        internal static AssemblyDefinition AssemblyDefinitionFor(ICompiledAssembly compiledAssembly)
        {
            var resolver = new PostProcessorAssemblyResolver(compiledAssembly);
            var readerParameters = new ReaderParameters
            {
                SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData.ToArray()),
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                AssemblyResolver = resolver,
                ReflectionImporterProvider = new PostProcessorReflectionImporterProvider(),
                ReadingMode = ReadingMode.Immediate
            };

            var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData.ToArray());
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(peStream, readerParameters);

            //apparently, it will happen that when we ask to resolve a type that lives inside Unity.Jobs, and we
            //are also postprocessing Unity.Jobs, type resolving will fail, because we do not actually try to resolve
            //inside the assembly we are processing. Let's make sure we do that, so that we can use postprocessor features inside
            //unity.Jobs itself as well.
            resolver.AddAssemblyDefinitionBeingOperatedOn(assemblyDefinition);

            return assemblyDefinition;
        }
    }

    internal class PostProcessorReflectionImporterProvider : IReflectionImporterProvider
    {
        public IReflectionImporter GetReflectionImporter(ModuleDefinition module)
        {
            return new PostProcessorReflectionImporter(module);
        }
    }

    internal class PostProcessorReflectionImporter : DefaultReflectionImporter
    {
        private const string SystemPrivateCoreLib = "System.Private.CoreLib";
        private AssemblyNameReference _correctCorlib;

        public PostProcessorReflectionImporter(ModuleDefinition module) : base(module)
        {
            _correctCorlib = module.AssemblyReferences.FirstOrDefault(a => a.Name == "mscorlib" || a.Name == "netstandard" || a.Name == SystemPrivateCoreLib);
        }

        public override AssemblyNameReference ImportReference(AssemblyName reference)
        {
            if (_correctCorlib != null && reference.Name == SystemPrivateCoreLib)
                return _correctCorlib;

            return base.ImportReference(reference);
        }
    }
}
