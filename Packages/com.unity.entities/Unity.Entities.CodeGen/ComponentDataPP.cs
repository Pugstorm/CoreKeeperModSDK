using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.Burst;
using Unity.CompilationPipeline.Common.Diagnostics;
using UnityEngine.Scripting;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Unity.Entities.CodeGen
{
    internal class ComponentDataPP : EntitiesILPostProcessor
    {
        /*
         * For IRefCounted and IEquatable for shared components, generate static forwarders for both hybrid and dots
         * runtime, just like isystem, and
         * transfer burstcompile attributes, and add [Preserve] and [MonoPInvokeCallback] attributes.
         *
         * For dots runtime, generate a function to register all the IRefCounted & IEquatable implemented methods
         * and have typemanager generate a call to that registration function. .
         *
         * For class-based IComponentData, just add [RequiredMember] to the default ctors.
         */
        protected override bool PostProcessImpl(TypeDefinition[] componentSystemTypes)
        {
            var irefCountedType = AssemblyDefinition.MainModule.ImportReference(typeof(IRefCounted)).Resolve();
            var iequatableType = AssemblyDefinition.MainModule.ImportReference(typeof(IEquatable<>)).Resolve();
            var isharedcomponentdataType =
                AssemblyDefinition.MainModule.ImportReference(typeof(ISharedComponentData)).Resolve();

            var preserveAttrCtor = AssemblyDefinition.MainModule.ImportReference(
                typeof(PreserveAttribute).GetConstructors(BindingFlags.Public |
                                                          BindingFlags.NonPublic |
                                                          BindingFlags.Instance)[0]);

            var modified = false;
            var memos = new List<TypeMemo>();

            var mod = AssemblyDefinition.MainModule;
            var intPtrRef = mod.ImportReference(typeof(IntPtr));
            var voidstarRef = mod.ImportReference(typeof(void*));
            mod.ImportReference(intPtrRef.Resolve()
                .Methods.FirstOrDefault(x => x.Name == nameof(IntPtr.ToPointer)));

            foreach (var type in AssemblyDefinition.MainModule.GetAllTypes())
            {
                bool hasISD = false;
                bool hasIRC = false;
                bool hasIEQ = false;

                foreach (var iface in type.Interfaces)
                {
                    if (iface.InterfaceType.Name == isharedcomponentdataType.Name)
                        hasISD = true;
                    if (iface.InterfaceType.Name == iequatableType.Name)
                        hasIEQ = true;
                    if (iface.InterfaceType.Name == irefCountedType.Name)
                        hasIRC = true;
                }

                if (!type.IsValueType() && type.IsChildTypeOf(mod.ImportReference(typeof(SystemBase)).Resolve()))
                {
                    foreach (var m in type.Methods)
                    {
                        if (m.Name is "OnCreate" or "OnDestroy" or "OnUpdate" or "OnStartRunning" or "OnStopRunning") 
                        {
                            m.CustomAttributes.Add(new CustomAttribute(preserveAttrCtor));
                        }
                    }

                    modified = PreserveDefaultCtor(type, preserveAttrCtor);
                }

                if (!type.IsValueType() && !type.IsInterface && type.TypeImplements(typeof(IComponentData)))
                {
                    try
                    {
                        modified = PreserveDefaultCtor(type, preserveAttrCtor);
                    }
                    catch
                    {
                        _diagnosticMessages.Add(new DiagnosticMessage()
                        {
                            Column = 0, 
                            DiagnosticType = DiagnosticType.Error, 
                            File = null, 
                            Line = 0,
                            MessageData =
                                $"Class IComponentData type {type.FullName} has no default constructor, but one is required when implementing class-based IComponentData."
                        });
                    }
                }

                if (hasISD)
                {
                    var memo = new TypeMemo
                        {m_Type = type, m_Wrappers = null, m_BurstCompileBits = 0};
                    IEnumerable<string> methodNames = new List<string>();

                    var numwrappers = 0;
                    if (hasIRC)
                    {
                        methodNames =
                            methodNames.Concat(new[] {nameof(IRefCounted.Retain), nameof(IRefCounted.Release)});
                        numwrappers += 2;
                    }

                    if (hasIEQ)
                    {
                        methodNames = methodNames.Concat(new[] {"Equals", "GetHashCode"});
                        numwrappers += 2;
                    }

                    memo.m_Wrappers = new MethodDefinition[numwrappers];
                    var methodNameArray = methodNames.ToArray();

                    for (int i = 0; i < methodNameArray.Length; ++i)
                    {
                        var name = methodNameArray[i];

                        MethodDefinition targetMethod;
                        try
                        {
                            targetMethod = type.Methods.Single(x =>
                            {
                                var isEquals = (name == "Equals");
                                if (isEquals)
                                    return x.Name == name &&
                                           x.Parameters.Count == 1 &&
                                           x.Parameters[0].ParameterType.Name == type.Name;
                                else
                                {
                                    return x.Name == name && x.Parameters.Count == 0;
                                }
                            });
                        }
                        catch (Exception e)
                        {
                            //gethashcode is optional
                            if (name == "GetHashCode")
                                continue;
                            
                            _diagnosticMessages.Add(new DiagnosticMessage()
                            {
                                Column = 0, 
                                DiagnosticType = DiagnosticType.Error, 
                                File = null, 
                                Line = 0,
                                MessageData =
                                    $"Signature mismatch in ilpp on type {type} method {name} exception {e.Message}. Seeing this error indicates a bug in Entities. Please submit a bug with About->Report a bug... Thanks!"

                            });
                            
                            int j = 0;
                            foreach (var m in type.Methods.Where(x => x.Name == name))
                            {
                                Console.WriteLine($"{name} {j}");
                                j++;
                                foreach (var p in m.Parameters)
                                    Console.WriteLine(p.ParameterType.Name);
                            }

                            throw e;
                        }

                        var methodDef = new MethodDefinition(GeneratedPrefix + name,
                            MethodAttributes.Static | MethodAttributes.Public,
                            mod.ImportReference(targetMethod.ReturnType));

                        methodDef.Parameters.Add(new ParameterDefinition("self",
                            ParameterAttributes.None,
                            (name == "Equals" || name == "GetHashCode") ? voidstarRef : intPtrRef));

                        foreach (var p in targetMethod.Parameters)
                        {
                            methodDef.Parameters.Add(new ParameterDefinition(voidstarRef));
                        }

                        // Transfer any BurstCompile attribute from target function to the forwarding wrapper
                        var burstAttribute = targetMethod.CustomAttributes.FirstOrDefault(x =>
                            x.Constructor.DeclaringType.Name == nameof(BurstCompileAttribute));
                        if (burstAttribute != null)
                        {
                            methodDef.CustomAttributes.Add(new CustomAttribute(burstAttribute.Constructor,
                                burstAttribute.GetBlob()));
                            memo.m_BurstCompileBits |= 1 << i;
                        }

                        // Adding MonoPInvokeCallbackAttribute needed for IL2CPP to work when burst is disabled
                        var monoPInvokeCallbackAttributeConstructors =
                            typeof(MonoPInvokeCallbackAttribute).GetConstructors(BindingFlags.Public |
                                BindingFlags.NonPublic |
                                BindingFlags.Instance);
                        var monoPInvokeCallbackAttribute =
                            new CustomAttribute(mod.ImportReference(monoPInvokeCallbackAttributeConstructors[0]));
                        monoPInvokeCallbackAttribute.ConstructorArguments.Add(new CustomAttributeArgument(mod.ImportReference(typeof(Type)), mod.ImportReference(typeof(SystemBaseDelegates.Function))));
                        methodDef.CustomAttributes.Add(monoPInvokeCallbackAttribute);

                        methodDef.CustomAttributes.Add(new CustomAttribute(preserveAttrCtor));

                        var processor = methodDef.Body.GetILProcessor();

                        processor.Emit(OpCodes.Ldarg_S, (byte) 0);
                        for (int j = 1; j < methodDef.Parameters.Count; j++)
                        {
                            processor.Emit(OpCodes.Ldarg_S, (byte) j);
                            processor.Emit(OpCodes.Ldobj, type);

                        }

                        processor.Emit(OpCodes.Call, targetMethod);
                        processor.Emit(OpCodes.Ret);

                        type.Methods.Add(methodDef);
                        memo.m_Wrappers[i] = methodDef;
                    }

                    if (hasIEQ || hasIRC)
                    {
                        modified = true;
                        memos.Add(memo);
                    }
                }
            }
            AddRegistrationCode(memos);
            return modified;
        }

        private static bool PreserveDefaultCtor(TypeDefinition type, MethodReference preserveAttrCtor)
        {
            TypeDefinition tmptype = type;
            MethodDefinition defaultctor = null;
            var modified = false;
            //look for default ctors in self and parent classes
            while (tmptype != null)
            {
                defaultctor = tmptype.GetConstructors().FirstOrDefault(c => c.HasParameters == false);
                if (defaultctor != null)
                    break;
                tmptype = tmptype.BaseType?.Resolve();
            }

            if (defaultctor != null)
            {
                defaultctor.CustomAttributes.Add(new CustomAttribute(preserveAttrCtor));
                modified = true;
            }

            return modified;
        }

        public struct TypeMemo
        {
            public TypeDefinition m_Type;
            public MethodDefinition[] m_Wrappers;
            public int m_BurstCompileBits;
        }

        private static readonly string GeneratedPrefix = "__codegen__";

        protected override bool PostProcessUnmanagedImpl(TypeDefinition[] unmanagedComponentSystemTypes)
        {
            return false;
        }

        [Conditional("UNITY_DOTSRUNTIME")]
        private void AddRegistrationCode(List<TypeMemo> memos)
        {
            var autoClassName =
                $"__SharedComponentPostProcessorOutput__{(uint) AssemblyDefinition.FullName.GetHashCode()}";
            var mod = AssemblyDefinition.MainModule;

            var classDef = new TypeDefinition("",
                autoClassName,
                TypeAttributes.Class,
                AssemblyDefinition.MainModule.ImportReference(typeof(object)));
            classDef.IsBeforeFieldInit = false;
            mod.Types.Add(classDef);

            var funcDef = new MethodDefinition("RegisterSharedComponentFunctions",
                MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig,
                AssemblyDefinition.MainModule.ImportReference(typeof(void)));
            funcDef.Body.InitLocals = true;

            classDef.Methods.Add(funcDef);

            var processor = funcDef.Body.GetILProcessor();

            var typeManagerType = mod.ImportReference(typeof(TypeManager)).Resolve();
            var iRefCountedType = mod.ImportReference(typeof(IRefCounted)).Resolve();
            var addRetainMethod =
                mod.ImportReference(typeManagerType.Methods.FirstOrDefault((x) =>
                    x.Name == nameof(TypeManager.SetIRefCounted_RetainFn)));
            var addReleaseMethod =
                mod.ImportReference(typeManagerType.Methods.FirstOrDefault((x) =>
                    x.Name == nameof(TypeManager.SetIRefCounted_ReleaseFn)));
            var addEqualsMethod =
                mod.ImportReference(typeManagerType.Methods.FirstOrDefault((x) =>
                    x.Name == nameof(TypeManager.SetIEquatable_EqualsFn)));
            var addGetHashCodeMethod =
                mod.ImportReference(typeManagerType.Methods.FirstOrDefault((x) =>
                    x.Name == nameof(TypeManager.SetIEquatable_GetHashCodeFn)));

            var intType = mod.ImportReference(typeof(int)).Resolve();

            var genericGetTypeIndex =
                mod.ImportReference(typeManagerType.Methods.FirstOrDefault((x) =>
                    x.Name == nameof(TypeManager.GetTypeIndex) && x.Parameters.Count == 0));

            var ircDelegateCtor = mod.ImportReference(iRefCountedType.NestedTypes
                .FirstOrDefault((x) => x.Name == nameof(IRefCounted.RefCountDelegate))
                .GetConstructors()
                .FirstOrDefault((x) => x.Parameters.Count == 2));

            var equalsDelegateCtor = mod.ImportReference(
                mod.ImportReference(typeof(FastEquality.TypeInfo.CompareEqualDelegate))
                .Resolve()
                .GetConstructors()
                .FirstOrDefault(x => x.Parameters.Count == 2));

            var ghcDelegateCtor = mod.ImportReference(
                mod.ImportReference(typeof(FastEquality.TypeInfo.GetHashCodeDelegate))
                .Resolve()
                .GetConstructors()
                .FirstOrDefault(x => x.Parameters.Count == 2));


            foreach (var memo in memos)
            {
                var typeIndexLocal = new VariableDefinition(intType);
                funcDef.Body.Variables.Add(typeIndexLocal);

                processor.Emit(OpCodes.Call,
                    mod.ImportReference(genericGetTypeIndex.MakeGenericInstanceMethod(memo.m_Type)));
                processor.Emit(OpCodes.Stloc, typeIndexLocal);

                for (int i = 0; i < memo.m_Wrappers.Length; i++)
                {
                    var mname = memo.m_Wrappers[i].Name;
                    processor.Emit(OpCodes.Ldloc, typeIndexLocal);

                    //something like this (but different for release/equals/gethashcode)
                    //TypeManager.SetIRefCounted_RetainFn(TypeManager.GetTypeIndex<T>(), new IRefCounted.RefCountDelegate(ThisComponent.ThisFn), burstcompilebits & 1 > 0);
                    processor.Emit(OpCodes.Ldnull);
                    processor.Emit(OpCodes.Ldftn, memo.m_Wrappers[i]);
                    if (mname == "__codegen__Retain" || mname == "__codegen__Release")
                        processor.Emit(OpCodes.Newobj, ircDelegateCtor);
                    else if (mname == "__codegen__Equals")
                        processor.Emit(OpCodes.Newobj, equalsDelegateCtor);
                    else if (mname == "__codegen__GetHashCode")
                        processor.Emit(OpCodes.Newobj, ghcDelegateCtor);

                    if ((memo.m_BurstCompileBits & (1 << i)) > 0)
                        processor.Emit(OpCodes.Ldc_I4_1);
                    else
                        processor.Emit(OpCodes.Ldc_I4_0);

                    if (mname == "__codegen__Retain")
                        processor.Emit(OpCodes.Call, addRetainMethod);
                    else if (mname == "__codegen__Release")
                        processor.Emit(OpCodes.Call, addReleaseMethod);
                    else if (mname == "__codegen__Equals")
                        processor.Emit(OpCodes.Call, addEqualsMethod);
                    else if (mname == "__codegen__GetHashCode")
                        processor.Emit(OpCodes.Call, addGetHashCodeMethod);
                }
            }

            processor.Emit(OpCodes.Ret);
        }
    }
}
