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

namespace Unity.Collections.UnsafeUtility.CodeGen
{
    internal class CollectionsUnsafeUtilityPostProcessor : ILPostProcessor
    {
        private static CollectionsUnsafeUtilityPostProcessor s_Instance;

        public override ILPostProcessor GetInstance()
        {
            if (s_Instance == null)
                s_Instance = new CollectionsUnsafeUtilityPostProcessor();
            return s_Instance;
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            if (compiledAssembly.Name == "Unity.Collections.LowLevel.ILSupport")
                return true;
            return false;
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!WillProcess(compiledAssembly))
                return null;

            var assemblyDefinition = AssemblyDefinitionFor(compiledAssembly);

            var ilSupportType = assemblyDefinition.MainModule.Types.FirstOrDefault(t => t.FullName == "Unity.Collections.LowLevel.Unsafe.ILSupport");
            if (ilSupportType == null)
                throw new InvalidOperationException();

            InjectUtilityAddressOfIn(ilSupportType);
            InjectUtilityAsRefIn(ilSupportType);

            var pe = new MemoryStream();
            var pdb = new MemoryStream();
            var writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(), SymbolStream = pdb, WriteSymbols = true
            };

            assemblyDefinition.Write(pe, writerParameters);
            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()));
        }

        private void InjectUtilityAddressOfIn(TypeDefinition ctx)
        {
            var method = ctx.Methods.Single(m => m.Name.Equals("AddressOf") && m.Parameters[0].IsIn);
            var il = GetILProcessorForMethod(method);

            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Ret));
        }

        private void InjectUtilityAsRefIn(TypeDefinition ctx)
        {
            var method = ctx.Methods.Single(m => m.Name.Equals("AsRef") && m.Parameters[0].IsIn);
            var il = GetILProcessorForMethod(method);

            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Ret));
        }

        internal static AssemblyDefinition AssemblyDefinitionFor(ICompiledAssembly compiledAssembly)
        {
            var readerParameters = new ReaderParameters
            {
                SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData.ToArray()),
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                ReadingMode = ReadingMode.Immediate
            };

            var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData.ToArray());
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(peStream, readerParameters);

            return assemblyDefinition;
        }

        private static ILProcessor GetILProcessorForMethod(TypeDefinition ctx, string methodName, bool clear = true)
        {
            var method = ctx.Methods.Single(m => m.Name.Equals(methodName) && m.HasGenericParameters);
            return GetILProcessorForMethod(method, clear);
        }

        private static ILProcessor GetILProcessorForMethod(MethodDefinition method, bool clear = true)
        {
            var ilProcessor = method.Body.GetILProcessor();

            if (clear)
            {
                ilProcessor.Body.Instructions.Clear();
                ilProcessor.Body.Variables.Clear();
                ilProcessor.Body.ExceptionHandlers.Clear();
            }

            return ilProcessor;
        }
    }
}

