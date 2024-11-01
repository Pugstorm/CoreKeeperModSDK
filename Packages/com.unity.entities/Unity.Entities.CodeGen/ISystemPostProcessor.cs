using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.Burst;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Unity.Entities.CodeGen
{
    internal class UnmanagedSystemPostprocessor : EntitiesILPostProcessor
    {
        private static readonly string RegisterGenericSystemTypeAttributeName = typeof(RegisterGenericSystemTypeAttribute).FullName;
        private TypeDefinition _registrationClassDef;

        protected override bool PostProcessImpl(TypeDefinition[] types)
        {
            return false;
        }

        struct TypeMemo
        {
            public TypeReference m_SystemType;
            public MethodDefinition[] m_Wrappers;
            public int m_BurstCompileBits;
        }

        protected override bool PostProcessUnmanagedImpl(TypeDefinition[] unmanagedComponentSystemTypes)
        {
            bool changes = false;

            // Create the registration class first so that if we need to put forwarders for generic ISystems into it, it's ready.
            // This must use a stable hash code function (do not using string.GetHashCode).
            var autoClassName = $"__UnmanagedPostProcessorOutput__{TypeHash.FNV1A64(AssemblyDefinition.FullName)}";
            var mod = AssemblyDefinition.MainModule;

            _registrationClassDef = new TypeDefinition("", autoClassName, TypeAttributes.Class, AssemblyDefinition.MainModule.ImportReference(typeof(object)));
            _registrationClassDef.IsBeforeFieldInit = false;
            var burstCompileAttributeConstructor = typeof(BurstCompileAttribute).GetConstructor(Type.EmptyTypes);
            _registrationClassDef.CustomAttributes.Add(new CustomAttribute(mod.ImportReference(burstCompileAttributeConstructor)));
            mod.Types.Add(_registrationClassDef);

            var memos = new List<TypeMemo>();

            foreach (var td in unmanagedComponentSystemTypes)
            {
                if (td.HasGenericParameters)
                    continue;

                changes = true;

                // We will be generating functions using these types (e.g. GetHashCode64<T>()) which will require internal access rights
                td.MakeTypeInternal();
                memos.Add(AddStaticForwarders(td));
            }

            var assemblyAttributes = AssemblyDefinition.CustomAttributes;
            foreach (var attr in assemblyAttributes)
            {
                if (attr.AttributeType.Resolve().FullName == RegisterGenericSystemTypeAttributeName)
                {
                    var typeRef = (TypeReference)attr.ConstructorArguments[0].Value;
                    var openType = typeRef.Resolve();

                    typeRef = mod.ImportReference(typeRef);

                    if (!typeRef.IsGenericInstance || !openType.IsValueType)
                    {
                        _diagnosticMessages.Add(UserError.DC3002(openType));
                        continue;
                    }

                    memos.Add(AddStaticForwarders(openType, typeRef, _registrationClassDef));
                    changes = true;
                }
            }

            if (!changes)
                return false;

            AddRegistrationCode(memos);

            return changes;
        }

        private static readonly string GeneratedPrefix = "__codegen__";

        static readonly string[] MethodNames = new string[] { "OnCreate", "OnUpdate", "OnDestroy", "OnStartRunning", "OnStopRunning", "OnCreateForCompiler" };

        static readonly string[] MethodFullNames = new string[]
        {
            "Unity.Entities.ISystem.OnCreate",
            "Unity.Entities.ISystem.OnUpdate",
            "Unity.Entities.ISystem.OnDestroy",
            "Unity.Entities.ISystemStartStop.OnStartRunning",
            "Unity.Entities.ISystemStartStop.OnStopRunning",
            "Unity.Entities.ISystemCompilerGenerated.OnCreateForCompiler"
        };

        private MethodReference _targetMethodRef;
        private MethodDefinition _targetMethodDef;
        private MethodDefinition _methodDef;


        /// <summary>
        /// https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf section II.7.3
        /// basically if you don't do this, sometimes when you refer to types from another assembly, and you ldtoken
        /// those types, you will get an error like
        /// "System.BadImageFormatException: Expected reference type but got type kind 17"
        /// so if you are searching for that error and you find this code, use this to launder your type refs, and
        /// it will probably go away!
        /// </summary>
        /// <param name="r_"></param>
        /// <returns></returns>
        private TypeReference LaunderTypeRef(TypeReference r_)
        {
            ModuleDefinition mod = AssemblyDefinition.MainModule;
            TypeDefinition def = r_.Resolve();
            TypeReference result;

            if (r_ is GenericInstanceType git)
            {
                var gt = new GenericInstanceType(LaunderTypeRef(def));
                foreach (var gp in git.GenericParameters)
                {
                    gt.GenericParameters.Add(gp);
                }

                foreach (var ga in git.GenericArguments)
                {
                    gt.GenericArguments.Add(LaunderTypeRef(ga));
                }

                result = gt;

            }
            else
            {
                result = new TypeReference(def.Namespace, def.Name, def.Module, def.Scope, def.IsValueType);
                if (def.DeclaringType != null)
                {
                    result.DeclaringType = LaunderTypeRef(def.DeclaringType);
                }
            }

            return mod.ImportReference(result);
        }

        private TypeMemo AddStaticForwarders(TypeDefinition systemType, TypeReference specializedSystemType = null, TypeDefinition alternateTypeToInsertForwarders = null)
        {
            var mod = AssemblyDefinition.MainModule;
            var intPtrRef = mod.ImportReference(typeof(IntPtr));
            var intPtrToVoid = mod.ImportReference(intPtrRef.Resolve().Methods.FirstOrDefault(x => x.Name == nameof(IntPtr.ToPointer)));

            TypeMemo memo = default;
            memo.m_SystemType = LaunderTypeRef(specializedSystemType ?? systemType);
            memo.m_Wrappers = new MethodDefinition[6];

            var hasStartStop = systemType.Interfaces.Any((x) => x.InterfaceType.FullName == "Unity.Entities.ISystemStartStop");
            var hasCompilerGenerated = systemType.Interfaces.Any((x) => x.InterfaceType.FullName == "Unity.Entities.ISystemCompilerGenerated");

            for (int i = 0; i < MethodNames.Length; ++i)
            {
                var name = MethodNames[i];
                var fullName = MethodFullNames[i]; // Cecil sees interface method names from other assemblies as the full namespaced name

                if (!hasStartStop && i >= 3 && i < 5)
                    continue;

                if (!hasCompilerGenerated && i == 5)
                    break;

                var wrapperName = name;

                if (specializedSystemType != null)
                {
                    wrapperName =
                        specializedSystemType.FullName.Replace('/', '_')
                            .Replace(',', '_')
                            .Replace('`', '_')
                            .Replace('.', '_')
                            .Replace('<', '_')
                            .Replace('>', '_') +
                        "_" +
                        name;
                }

                _methodDef = new MethodDefinition(GeneratedPrefix + wrapperName, MethodAttributes.Static | MethodAttributes.Assembly, mod.ImportReference(typeof(void)));
                _methodDef.Parameters.Add(new ParameterDefinition("self", ParameterAttributes.None, intPtrRef));
                _methodDef.Parameters.Add(new ParameterDefinition("state", ParameterAttributes.None, intPtrRef));

                _targetMethodDef = systemType.Methods.FirstOrDefault(x => x.Parameters.Count == 1 && (x.Name == name || x.Name == fullName));
                if (_targetMethodDef == null)
                    continue;
                _targetMethodRef = mod.ImportReference(_targetMethodDef);
                if (_targetMethodDef.DeclaringType.HasGenericParameters)
                {
                    _targetMethodRef =
                        mod.ImportReference(_targetMethodRef.MakeGenericHostMethod(specializedSystemType));
                }
                if (_targetMethodRef == null)
                    continue;

                // Transfer any BurstCompile attribute from target function to the forwarding wrapper
                Func<CustomAttribute, bool> isBurstAttribute = x => x.Constructor.DeclaringType.Name == nameof(BurstCompileAttribute);
                var burstAttribute = _targetMethodDef.CustomAttributes.FirstOrDefault(isBurstAttribute);
                if (burstAttribute != null)
                {
                    if (!_targetMethodDef.DeclaringType.CustomAttributes.Any(isBurstAttribute))
                        _targetMethodDef.DeclaringType.CustomAttributes.Add(new CustomAttribute(mod.ImportReference(typeof(BurstCompileAttribute).GetConstructor(Type.EmptyTypes))));

                    _methodDef.CustomAttributes.Add(new CustomAttribute(mod.ImportReference(burstAttribute.Constructor), burstAttribute.GetBlob()));
                    memo.m_BurstCompileBits |= 1 << i;
                }

                // Adding MonoPInvokeCallbackAttribute needed for IL2CPP to work when burst is disabled
                var monoPInvokeCallbackAttributeConstructors = typeof(MonoPInvokeCallbackAttribute).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var monoPInvokeCallbackAttribute = new CustomAttribute(mod.ImportReference(monoPInvokeCallbackAttributeConstructors[0]));
                monoPInvokeCallbackAttribute.ConstructorArguments.Add(new CustomAttributeArgument(mod.ImportReference(typeof(Type)), mod.ImportReference(typeof(SystemBaseDelegates.Function))));

                _methodDef.CustomAttributes.Add(monoPInvokeCallbackAttribute);

                var processor = _methodDef.Body.GetILProcessor();

                processor.Emit(OpCodes.Ldarga, 0);
                processor.Emit(OpCodes.Call, intPtrToVoid);
                processor.Emit(OpCodes.Ldarga, 1);
                processor.Emit(OpCodes.Call, intPtrToVoid);
                processor.Emit(OpCodes.Call, _targetMethodRef);
                processor.Emit(OpCodes.Ret);

                (alternateTypeToInsertForwarders ?? systemType).Methods.Add(_methodDef);
                memo.m_Wrappers[i] = _methodDef;
            }

            return memo;
        }

        private void AddRegistrationCode(List<TypeMemo> memos)
        {
            var mod = AssemblyDefinition.MainModule;
            var funcDef = new MethodDefinition("EarlyInit", MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig, AssemblyDefinition.MainModule.ImportReference(typeof(void)));
            funcDef.Body.InitLocals = false;

            if (!Defines.Contains("UNITY_EDITOR"))
            {
                // Needs to run automatically in the player, but we need to
                // exclude this attribute when building for the editor, or
                // it will re-run the registration for every enter play mode.
                var loadTypeEnumType = mod.ImportReference(typeof(UnityEngine.RuntimeInitializeLoadType));
                var attributeCtor = mod.ImportReference(typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute).GetConstructor(new[] { typeof(UnityEngine.RuntimeInitializeLoadType) }));
                var attribute = new CustomAttribute(attributeCtor);
                attribute.ConstructorArguments.Add(new CustomAttributeArgument(loadTypeEnumType, UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded));
                funcDef.CustomAttributes.Add(attribute);
            }
            else
            {
                // Needs to run automatically in the editor.
                var attributeCtor2 = AssemblyDefinition.MainModule.ImportReference(typeof(UnityEditor.InitializeOnLoadMethodAttribute).GetConstructor(Type.EmptyTypes));
                funcDef.CustomAttributes.Add(new CustomAttribute(attributeCtor2));
            }

            _registrationClassDef.Methods.Add(funcDef);

            var processor = funcDef.Body.GetILProcessor();

            var registryType = mod.ImportReference(typeof(SystemBaseRegistry)).Resolve();
            var addMethod = mod.ImportReference(registryType.Methods.FirstOrDefault((x) => x.Name == nameof(SystemBaseRegistry.AddUnmanagedSystemType)));
            var delegateCtor = mod.ImportReference(registryType.NestedTypes.FirstOrDefault((x) => x.Name == nameof(SystemBaseRegistry.ForwardingFunc)).GetConstructors().FirstOrDefault((x) => x.Parameters.Count == 2));
            var genericHashFunc = mod.ImportReference(typeof(BurstRuntime)).Resolve().Methods.FirstOrDefault((x) => x.Name == nameof(BurstRuntime.GetHashCode64) && x.HasGenericParameters);
            var typeType = mod.ImportReference(typeof(Type)).Resolve();
            var getTypeFromHandle = mod.ImportReference(typeType.Methods.FirstOrDefault((x) => x.Name == "GetTypeFromHandle"));

            foreach (var memo in memos)
            {
                // This craziness is equivalent to typeof(n)
                processor.Emit(OpCodes.Ldtoken, mod.ImportReference(memo.m_SystemType));
                processor.Emit(OpCodes.Call, getTypeFromHandle);
                processor.Emit(OpCodes.Call, mod.ImportReference(genericHashFunc.MakeGenericInstanceMethod(mod.ImportReference(memo.m_SystemType))));

                for (int i = 0; i < memo.m_Wrappers.Length; ++i)
                {
                    if (memo.m_Wrappers[i] != null)
                    {
                        processor.Emit(OpCodes.Ldnull);
                        processor.Emit(OpCodes.Ldftn, mod.ImportReference(memo.m_Wrappers[i]));
                        processor.Emit(OpCodes.Newobj, delegateCtor);
                    }
                    else
                    {
                        processor.Emit(OpCodes.Ldnull);
                    }
                }

                processor.Emit(OpCodes.Ldstr, memo.m_SystemType.FullName);
                processor.Emit(OpCodes.Ldc_I4, memo.m_BurstCompileBits);
                processor.Emit(OpCodes.Call, addMethod);
            }

            processor.Emit(OpCodes.Ret);
        }
    }
}
