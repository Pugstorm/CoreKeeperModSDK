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
using UnityEngine.Scripting;

[assembly: InternalsVisibleTo("Unity.Entities.Hybrid.CodeGen")]
namespace Unity.Entities.CodeGen
{
    internal class EntitiesILPostProcessors : ILPostProcessor
    {
        bool _ReferencesEntities;
        bool _ReferencesJobs;
        public static string[] Defines { get; internal set; }

        static EntitiesILPostProcessor[] FindAllEntitiesILPostProcessors()
        {
            var processorTypes = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.Contains(".CodeGen"))
                    processorTypes.AddRange(assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(EntitiesILPostProcessor)) && !t.IsAbstract));
            }

            var result = processorTypes.Select(t => (EntitiesILPostProcessor)Activator.CreateInstance(t)).ToArray();

            Array.Sort(result);

            return result;
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!WillProcess(compiledAssembly))
                return null;

            using (var marker = new EntitiesILPostProcessorProfileMarker(compiledAssembly.Name))
            {
                var diagnostics = new List<DiagnosticMessage>();
                bool madeAnyChange = false;
                Defines = compiledAssembly.Defines;
                var assemblyDefinition = AssemblyDefinitionFor(compiledAssembly);
                var postProcessors = FindAllEntitiesILPostProcessors();

                TypeDefinition[] componentSystemTypes;
                var allTypes = assemblyDefinition.MainModule.GetAllTypes().ToArray();
                try
                {
                    using (marker.CreateChildMarker("GetAllComponentTypes"))
                        componentSystemTypes = allTypes.Where(type => type.IsComponentSystem()).ToArray();
                    // Make sure IL2CPP doesn't strip systems
                    if (componentSystemTypes.Length > 0) {
                        var alwaysLinkAssemblyAttributeConstructors = typeof(AlwaysLinkAssemblyAttribute).GetConstructors();
                        var alwaysLinkAssemblyAttribute = new CustomAttribute(assemblyDefinition.MainModule.ImportReference(alwaysLinkAssemblyAttributeConstructors[0]));
                        assemblyDefinition.MainModule.Assembly.CustomAttributes.Add(alwaysLinkAssemblyAttribute);
                    }
                }
                catch (FoundErrorInUserCodeException e)
                {
                    diagnostics.AddRange(e.DiagnosticMessages);
                    return null;
                }

                foreach (var postProcessor in postProcessors)
                {
                    postProcessor.Initialize(Defines, _ReferencesEntities, _ReferencesJobs);
                    if (!postProcessor.WillProcess())
                        continue;

                    using (marker.CreateChildMarker(postProcessor.GetType().Name))
                    {
                        diagnostics.AddRange(postProcessor.PostProcess(assemblyDefinition, componentSystemTypes, out var madeChange));
                        madeAnyChange |= madeChange;
                    }
                }

                var unmanagedComponentSystemTypes = allTypes.Where((x) => x.TypeImplements(typeof(ISystem))).ToArray();
                foreach (var postProcessor in postProcessors)
                {
                    diagnostics.AddRange(postProcessor.PostProcessUnmanaged(assemblyDefinition, unmanagedComponentSystemTypes, out var madeChange));
                    madeAnyChange |= madeChange;
                }

                // Hack to remove Entities => Entities circular references
                var selfName = assemblyDefinition.Name.FullName;
                foreach (var referenceName in assemblyDefinition.MainModule.AssemblyReferences)
                {
                    if (referenceName.FullName == selfName)
                    {
                        assemblyDefinition.MainModule.AssemblyReferences.Remove(referenceName);
                        break;
                    }
                }

                if (!madeAnyChange || diagnostics.Any(d => d.DiagnosticType == DiagnosticType.Error))
                    return new ILPostProcessResult(null, diagnostics);

                using (marker.CreateChildMarker("WriteAssembly"))
                {
                    var pe = new MemoryStream();
                    var pdb = new MemoryStream();
                    var writerParameters = new WriterParameters
                    {
                        SymbolWriterProvider = new PortablePdbWriterProvider(), SymbolStream = pdb, WriteSymbols = true
                    };

                    assemblyDefinition.Write(pe, writerParameters);
                    return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), diagnostics);
                }
            }
        }

        public override ILPostProcessor GetInstance()
        {
            return this;
        }

        // Today there is no mechanism for sorting which ILPostProcessor runs relative to another
        // As such a sort order mechanism was added to this ILPP via running "EntitiesILPostProcessor"s
        // and sorting by `SortWeight`. However, some "EntitiesILPostProcessor"s need to run even if an assembly
        // doesn't references Entities.dll, so we extend the WillProcess implementation here to be inclusive
        // to other assemblies until the CompilationPipeline.ILPostProcessing API is extended
        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            if (compiledAssembly.Name == "Unity.Entities")
            {
                _ReferencesEntities = true;
                _ReferencesJobs = true;
                return true;
            }
            if (compiledAssembly.Name == "Unity.Jobs")
            {
                _ReferencesEntities = false;
                _ReferencesJobs = true;
                return true;
            }

            if (compiledAssembly.Name.EndsWith("CodeGen.Tests", StringComparison.Ordinal))
                return false;

            for (int i = 0;
                (!_ReferencesEntities || !_ReferencesJobs) // If we found both we can stop searching
                && i < compiledAssembly.References.Length; ++i)
            {
                var fileName = Path.GetFileNameWithoutExtension(compiledAssembly.References[i]);
                if (fileName == "Unity.Entities")
                    _ReferencesEntities = true;
                else if (fileName == "Unity.Jobs")
                    _ReferencesJobs = true;
            }

            return _ReferencesEntities || _ReferencesJobs;
        }

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

            //apparently, it will happen that when we ask to resolve a type that lives inside Unity.Entities, and we
            //are also postprocessing Unity.Entities, type resolving will fail, because we do not actually try to resolve
            //inside the assembly we are processing. Let's make sure we do that, so that we can use postprocessor features inside
            //unity.entities itself as well.
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

    abstract class EntitiesILPostProcessor : IComparable<EntitiesILPostProcessor>
    {
        public virtual int SortWeight => 0;
        public string[] Defines { get; private set; }
        public bool ReferencesEntities { get; private set; }
        public bool ReferencesJobs { get; private set; }
        protected AssemblyDefinition AssemblyDefinition;

        internal void Initialize(string[] compilationDefines, bool referencesEntities, bool referencesJobs)
        {
            Defines = compilationDefines;
            ReferencesEntities = referencesEntities;
            ReferencesJobs = referencesJobs;
        }

        protected List<DiagnosticMessage> _diagnosticMessages = new List<DiagnosticMessage>();

        public IEnumerable<DiagnosticMessage> PostProcess(AssemblyDefinition assemblyDefinition, TypeDefinition[] componentSystemTypes, out bool madeAChange)
        {
            AssemblyDefinition = assemblyDefinition;
            try
            {
                madeAChange = PostProcessImpl(componentSystemTypes);
            }
            catch (FoundErrorInUserCodeException e)
            {
                madeAChange = false;
                return e.DiagnosticMessages;
            }

            return _diagnosticMessages;
        }

        public virtual bool WillProcess()
        {
            return ReferencesEntities;
        }

        protected abstract bool PostProcessImpl(TypeDefinition[] componentSystemTypes);
        protected abstract bool PostProcessUnmanagedImpl(TypeDefinition[] unmanagedComponentSystemTypes);
        
        protected void AddDiagnostic(DiagnosticMessage diagnosticMessage)
        {
            _diagnosticMessages.Add(diagnosticMessage);
        }

        public IEnumerable<DiagnosticMessage> PostProcessUnmanaged(AssemblyDefinition assemblyDefinition, TypeDefinition[] unmanagedComponentSystemTypes, out bool madeAChange)
        {
            AssemblyDefinition = assemblyDefinition;
            try
            {
                madeAChange = PostProcessUnmanagedImpl(unmanagedComponentSystemTypes);
            }
            catch (FoundErrorInUserCodeException e)
            {
                madeAChange = false;
                return e.DiagnosticMessages;
            }

            return _diagnosticMessages;
        }

        int IComparable<EntitiesILPostProcessor>.CompareTo(EntitiesILPostProcessor other)
        {
            // Sort the postprocessors according to weight primarily, and name secondarily
            // Needed for determinism and to allow things that work on results of other postprocessors to work
            // (such as job reflection data for jobs that previous post processors have just made)
            int diff = SortWeight - other.SortWeight;
            if (diff != 0)
                return diff;

            Type ltype = GetType();
            Type rtype = other.GetType();
            return ltype.Name.CompareTo(rtype.Name);
        }

    }
}
