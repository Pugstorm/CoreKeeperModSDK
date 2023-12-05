#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Unity.Burst;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TestTools;
using Assembly = System.Reflection.Assembly;
using Debug = UnityEngine.Debug;

namespace Unity.Collections.Tests
{
    /// <summary>
    /// Base class for semi-automated burst compatibility testing.
    /// </summary>
    /// <remarks>
    /// To create Burst compatibility tests for your assembly, you must do the following:<para/> <para/>
    ///
    /// 1. Set up a directory to contain the generated Burst compatibility code.<para/> <para/>
    ///
    /// 2. Create a new asmdef in that directory. You should set up the references so you can access Burst and
    /// your assembly. This new asmdef *must* support all platforms because the Burst compatibility tests use player
    /// builds to compile the code in parallel.<para/> <para/>
    ///
    /// 3. If you wish to test internal methods, you should make internals visible to the new asmdef you created in
    /// step 2.<para/> <para/>
    ///
    /// 4. Create a test scene (it can be empty) for the Burst compatibility tests to use when it builds a player.
    ///
    /// 5. Create a new class and inherit BurstCompatibilityTests. Call the base constructor with the appropriate
    /// arguments so BurstCompatibilityTests knows which assemblies to scan for the [GenerateTestsForBurstCompatibility] attribute and
    /// where to put the generated Burst compatibility code. This new class should live in an editor only assembly and
    /// the generated Burst compatibility code should be a part of the multiplatform asmdef you created in step 2.
    /// <para/> <para/>
    ///
    /// 6. If your generated code will live in your package directory, you may need to add the [EmbeddedPackageOnlyTest]
    /// attribute to your new class.<para/> <para/>
    ///
    /// 7. Start adding [GenerateTestsForBurstCompatibility] or [ExcludeFromBurstCompatTesting] attributes to your types or methods.<para/> <para/>
    ///
    /// 8. In the test runner, run the test called CompatibilityTests that can be found nested under the name of your
    /// new test class you implemented in step 4.<para/> <para/>
    /// </remarks>
    public abstract class BurstCompatibilityTests
    {
        private string m_GeneratedCodePath;
        private HashSet<string> m_AssembliesToVerify = new HashSet<string>();
        private string m_GeneratedCodeAssemblyName;
        private readonly string m_TempBurstCompatibilityPath;
        private readonly string m_TestScenePath;
        private static readonly string s_GeneratedClassName = "_generated_burst_compat_tests";

        /// <summary>
        /// Sets up the code generator for Burst compatibility tests.
        /// </summary>
        /// <param name="assemblyNameToVerifyBurstCompatibility">Name of the assembly to verify Burst compatibility.</param>
        /// <param name="generatedCodePath">Destination path for the generated Burst compatibility code.</param>
        /// <param name="generatedCodeAssemblyName">Name of the assembly that will contain the generated Burst compatibility code.</param>
        /// <param name="testScenePath">Path of the test scene to use when building a player.</param>
        protected BurstCompatibilityTests(string assemblyNameToVerifyBurstCompatibility, string generatedCodePath, string generatedCodeAssemblyName, string testScenePath)
        : this(new[] {assemblyNameToVerifyBurstCompatibility}, generatedCodePath, generatedCodeAssemblyName, testScenePath)
        {
        }

        /// <summary>
        /// Sets up the code generator for Burst compatibility tests.
        /// </summary>
        /// <remarks>
        /// This constructor takes multiple assembly names to verify, which allows you to check multiple assemblies with
        /// one test. Prefer to use this instead of separate tests that verify a single assembly if you need to minimize
        /// CI time.
        /// </remarks>
        /// <param name="assemblyNamesToVerifyBurstCompatibility">Names of the assemblies to verify Burst compatibility.</param>
        /// <param name="generatedCodePath">Destination path for the generated Burst compatibility code.</param>
        /// <param name="generatedCodeAssemblyName">Name of the assembly that will contain the generated Burst compatibility code.</param>
        /// <param name="testScenePath">Path of the test scene to use when building a player.</param>
        protected BurstCompatibilityTests(string[] assemblyNamesToVerifyBurstCompatibility, string generatedCodePath, string generatedCodeAssemblyName, string testScenePath)
        {
            m_GeneratedCodePath = generatedCodePath;

            foreach (var assemblyName in assemblyNamesToVerifyBurstCompatibility)
            {
                m_AssembliesToVerify.Add(assemblyName);
            }

            m_GeneratedCodeAssemblyName = generatedCodeAssemblyName;
            m_TempBurstCompatibilityPath = Path.Combine("Temp", "BurstCompatibility", GetType().Name);
            m_TestScenePath = testScenePath;
        }

        struct MethodData : IComparable<MethodData>
        {
            public MethodBase methodBase;
            public Type InstanceType;
            public Type[] MethodGenericTypeArguments;
            public Type[] InstanceTypeGenericTypeArguments;
            public Dictionary<string, Type> MethodGenericArgumentLookup;
            public Dictionary<string, Type> InstanceTypeGenericArgumentLookup;
            public string RequiredDefine;
            public GenerateTestsForBurstCompatibilityAttribute.BurstCompatibleCompileTarget CompileTarget;

            public int CompareTo(MethodData other)
            {
                var lhs = methodBase;
                var rhs = other.methodBase;

                var ltn = methodBase.DeclaringType.FullName;
                var rtn = other.methodBase.DeclaringType.FullName;

                int tc = ltn.CompareTo(rtn);
                if (tc != 0) return tc;

                tc = lhs.Name.CompareTo(rhs.Name);
                if (tc != 0) return tc;

                var lp = lhs.GetParameters();
                var rp = rhs.GetParameters();
                if (lp.Length < rp.Length)
                    return -1;
                if (lp.Length > rp.Length)
                    return 1;

                var lb = new StringBuilder();
                var rb = new StringBuilder();
                for (int i = 0; i < lp.Length; ++i)
                {
                    GetFullTypeName(lp[i].ParameterType, lb, this);
                    GetFullTypeName(rp[i].ParameterType, rb, other);

                    tc = lb.ToString().CompareTo(rb.ToString());
                    if (tc != 0)
                        return tc;

                    lb.Clear();
                    rb.Clear();
                }

                return 0;
            }

            public Type ReplaceGeneric(Type genericType)
            {
                if (genericType.IsByRef)
                {
                    genericType = genericType.GetElementType();
                }

                if (MethodGenericArgumentLookup == null & InstanceTypeGenericArgumentLookup == null)
                {
                    throw new InvalidOperationException("For '{InstanceType.Name}.{Info.Name}', generic argument lookups are null! Did you forget to specify GenericTypeArguments in the [GenerateTestsForBurstCompatibility] attribute?");
                }

                bool hasMethodReplacement = MethodGenericArgumentLookup.ContainsKey(genericType.Name);
                bool hasInstanceTypeReplacement = InstanceTypeGenericArgumentLookup.ContainsKey(genericType.Name);

                if (hasMethodReplacement)
                {
                    return MethodGenericArgumentLookup[genericType.Name];
                }
                else if (hasInstanceTypeReplacement)
                {
                    return InstanceTypeGenericArgumentLookup[genericType.Name];
                }
                else
                {
                    throw new ArgumentException($"'{genericType.Name}' in '{InstanceType.Name}.{methodBase.Name}' has no generic type replacement in the generic argument lookups! Did you forget to specify GenericTypeArguments in the [GenerateTestsForBurstCompatibility] attribute?");
                }
            }
        }

        /// <summary>
        /// Generates the code for the Burst compatibility tests.
        /// </summary>
        /// <param name="path">Path of the generated file.</param>
        /// <param name="methodsTestedCount">Number of methods being tested.</param>
        /// <returns>True if the file was generated successfully; false otherwise.</returns>
        private bool UpdateGeneratedFile(string path, out int methodsTestedCount)
        {
            var buf = new StringBuilder();
            var success = GetTestMethods(out MethodData[] methods);

            if (!success)
            {
                methodsTestedCount = 0;
                return false;
            }

            buf.AppendLine(
@"// auto-generated
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;");

            // This serves to force the player build to skip all the shader variants which dramatically
            // reduces the player build time. Since we only care about burst compilation, this is fine
            // and desirable. The reason why this code is generated is that as soon as this definition
            // exists, the player build pipeline will start using it. Since we don't want to bloat user
            // build pipelines with extra callbacks or modify their behavior, we generate the code here
            // for test use only.
            buf.AppendLine(@"
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Rendering;

class BurstCompatibleSkipShaderVariants : IPreprocessShaders
{
    public int callbackOrder => 0;

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        data.Clear();
    }
}
#endif
");

            buf.AppendLine("[BurstCompile]");
            buf.AppendLine($"public unsafe class {s_GeneratedClassName}");
            buf.AppendLine("{");
            buf.AppendLine("    private delegate void TestFunc(IntPtr p);");
            buf.AppendLine("    static unsafe ref T CreateRef<T>() where T : unmanaged => ref UnsafeUtility.AsRef<T>((void*)default);");

            var overloadHandling = new Dictionary<string, int>();

            foreach (var methodData in methods)
            {
                var method = methodData.methodBase;
                var isGetter = method.Name.StartsWith("get_");
                var isSetter = method.Name.StartsWith("set_");
                var isProperty = isGetter | isSetter;
                var isIndexer = method.Name.Equals("get_Item") || method.Name.Equals("set_Item");
                var sourceName = isProperty ? method.Name.Substring(4) : method.Name;

                var safeName = GetSafeName(methodData);
                if (overloadHandling.ContainsKey(safeName))
                {
                    int num = overloadHandling[safeName]++;
                    safeName += $"_overload{num}";
                }
                else
                {
                    overloadHandling.Add(safeName, 0);
                }

                if (methodData.RequiredDefine != null)
                {
                    buf.AppendLine($"#if {methodData.RequiredDefine}");
                }

                if (methodData.CompileTarget == GenerateTestsForBurstCompatibilityAttribute.BurstCompatibleCompileTarget.Editor)
                {
                    // Emit #if UNITY_EDITOR in case GenerateTestsForBurstCompatibility attribute doesn't have any required
                    // unity defines. We want to be sure that we actually test the editor only code when
                    // we are actually in the editor.
                    buf.AppendLine("#if UNITY_EDITOR");
                }

                buf.AppendLine("    [BurstCompile(CompileSynchronously = true)]");
                buf.AppendLine($"    public static void Burst_{safeName}(IntPtr p)");
                buf.AppendLine("    {");

                // Generate targets for out/ref parameters
                var parameters = method.GetParameters();
                for (int i = 0; i < parameters.Length; ++i)
                {
                    var param = parameters[i];
                    var pt = param.ParameterType;

                    if (pt.IsGenericParameter ||
                       (pt.IsByRef && pt.GetElementType().IsGenericParameter))
                    {
                        pt = methodData.ReplaceGeneric(pt);
                    }

                    if (pt.IsGenericTypeDefinition)
                    {
                        pt = pt.MakeGenericType(methodData.InstanceTypeGenericTypeArguments);
                    }

                    if (pt.IsPointer)
                    {
                        TypeToString(pt, buf, methodData);
                        buf.Append($" v{i} = (");
                        TypeToString(pt, buf, methodData);
                        buf.AppendLine($") ((byte*)p + {i * 1024});");
                    } else if (HasAttribute<IsByRefLikeAttribute>(pt) || (pt.HasElementType && pt.GetElementType().IsPointer)) // is `ref struct`
                    {
                        buf.Append($"        var v{i} = default(");
                        TypeToString(pt, buf, methodData);
                        buf.AppendLine(");");
                    }
                    else
                    {
                        buf.Append($"        ref var v{i} = ref CreateRef<");
                        TypeToString(pt, buf, methodData);
                        buf.AppendLine(">();");
                    }
                }

                var info = method as MethodInfo;
                var refStr = "";
                var readonlyStr = "";

                // [MayOnlyLiveInBlobStorage] forces types to be referred to by reference only, so we
                // must be sure to make any local vars use ref whenever something is a ref return.
                //
                // Furthermore, we also care about readonly since some returns are ref readonly returns.
                // Detecting readonly is done by checking the required custom modifiers for the InAttribute.
                if (info != null && info.ReturnType.IsByRef)
                {
                    refStr = "ref ";

                    if (info.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(InAttribute)))
                    {
                        readonlyStr = "readonly ";
                    }
                }

                if (method.IsStatic)
                {
                    if (isGetter)
                        buf.Append($"        {refStr}{readonlyStr}var __result = {refStr}");
                    TypeToString(methodData.InstanceType, buf, methodData);
                    buf.Append($".{sourceName}");
                }
                else
                {
                    StringBuilder typeStringBuilder = new StringBuilder();
                    TypeToString(methodData.InstanceType, typeStringBuilder, methodData);

                    // ref structs need special handling, can't be used as type arguments
                    if (typeStringBuilder.ToString() == "Unity.Entities.SystemState" ||
                        typeStringBuilder.ToString() == "Unity.Entities.EntityQueryBuilder")
                    {
                        buf.Append($"        ref var instance = ref *(");
                        buf.Append(typeStringBuilder);
                        buf.AppendLine("*)((void*)p);");
                    }
                    else
                    {
                        buf.Append($"        ref var instance = ref UnsafeUtility.AsRef<");
                        buf.Append(typeStringBuilder);
                        buf.AppendLine(">((void*)p);");
                    }

                    if (isIndexer)
                    {
                        if (isGetter)
                            buf.Append($"        {refStr}{readonlyStr}var __result = {refStr}instance");
                        else if (isSetter)
                            buf.Append("        instance");
                    }
                    else if (method.IsConstructor)
                    {
                        // Begin the setup for the constructor call. Arguments will be handled later.
                        buf.Append("        instance = new ");
                        TypeToString(methodData.InstanceType, buf, methodData);
                    }
                    else
                    {
                        if (isGetter)
                            buf.Append($"        {refStr}{readonlyStr}var __result = {refStr}");
                        buf.Append($"        instance.{sourceName}");
                    }
                }

                if (method.IsGenericMethod)
                {
                    buf.Append("<");
                    var args = method.GetGenericArguments();
                    for (int i = 0; i < args.Length; ++i)
                    {
                        if (i > 0)
                            buf.Append(", ");
                        TypeToString(args[i], buf, methodData);
                    }
                    buf.Append(">");
                }

                // Make dummy arguments.
                if (isIndexer)
                {
                    buf.Append("[");
                }
                else
                {
                    if (isGetter)
                    {
                    }
                    else if (isSetter)
                        buf.Append("=");
                    else
                        buf.Append("(");
                }

                for (int i = 0; i < parameters.Length; ++i)
                {
                    // Close the indexer brace and assign the value if we're handling an indexer and setter.
                    if (isIndexer && isSetter && i + 1 == parameters.Length)
                    {
                        buf.Append($"] = v{i}");
                        break;
                    }

                    if (i > 0)
                        buf.Append(" ,");

                    // Close the indexer brace. This is separate from the setter logic above because the
                    // comma separating arguments is required for the getter case but not for the setter.
                    if (isIndexer && isGetter && i + 1 == parameters.Length)
                    {
                        buf.Append($"v{i}]");
                        break;
                    }

                    var param = parameters[i];

                    if (param.IsOut)
                    {
                        buf.Append("out ");
                    }
                    else if (param.IsIn)
                    {
                        buf.Append("in ");
                    }
                    else if (param.ParameterType.IsByRef)
                    {
                        buf.Append("ref ");
                    }

                    buf.Append($"v{i}");
                }

                if (!isProperty)
                    buf.Append(")");

                buf.AppendLine(";");
                buf.AppendLine("    }");

                if (methodData.CompileTarget == GenerateTestsForBurstCompatibilityAttribute.BurstCompatibleCompileTarget.Editor)
                {
                    // Closes #if UNITY_EDITOR that surrounds the actual method/property being tested.
                    buf.AppendLine("#endif");
                }

                // Set up the call to BurstCompiler.CompileFunctionPointer only in the cases where it is necessary.
                switch (methodData.CompileTarget)
                {
                    case GenerateTestsForBurstCompatibilityAttribute.BurstCompatibleCompileTarget.PlayerAndEditor:
                    case GenerateTestsForBurstCompatibilityAttribute.BurstCompatibleCompileTarget.Editor:
                    {
                        buf.AppendLine("#if UNITY_EDITOR");
                        buf.AppendLine($"    public static void BurstCompile_{safeName}()");
                        buf.AppendLine("    {");
                        buf.AppendLine($"        BurstCompiler.CompileFunctionPointer<TestFunc>(Burst_{safeName});");
                        buf.AppendLine("    }");
                        buf.AppendLine("#endif");
                        break;
                    }
                }

                if (methodData.RequiredDefine != null)
                {
                    buf.AppendLine($"#endif");
                }
            }

            buf.AppendLine("}");

            File.WriteAllText(path, buf.ToString());
            methodsTestedCount = methods.Length;
            return true;
        }

        static bool HasAttribute<T>(MemberInfo type) => type.CustomAttributes.Any(ca => ca.AttributeType.IsEquivalentTo(typeof(T)));

        private static void TypeToString(Type t, StringBuilder buf, in MethodData methodData)
        {
            if (t.IsPrimitive || t == typeof(void))
            {
                buf.Append(PrimitiveTypeToString(t));
                return;
            }

            if (t.IsByRef)
            {
                TypeToString(t.GetElementType(), buf, methodData);
                return;
            }

            // This should come after the IsByRef check above to avoid adding an extra asterisk.
            // You could have a T*& (ref to a pointer) which causes t.IsByRef and t.IsPointer to both be true and if
            // you check t.IsPointer first then descend down the types with t.GetElementType() you end up with
            // T* which causes you to descend a second time then print an asterisk twice as you come back up the
            // recursion.
            if (t.IsPointer)
            {
                TypeToString(t.GetElementType(), buf, methodData);
                buf.Append("*");
                return;
            }

            GetFullTypeName(t, buf, methodData);
        }

        private static string PrimitiveTypeToString(Type type)
        {
            if (type == typeof(void))
                return "void";
            if (type == typeof(bool))
                return "bool";
            if (type == typeof(byte))
                return "byte";
            if (type == typeof(sbyte))
                return "sbyte";
            if (type == typeof(short))
                return "short";
            if (type == typeof(ushort))
                return "ushort";
            if (type == typeof(int))
                return "int";
            if (type == typeof(uint))
                return "uint";
            if (type == typeof(long))
                return "long";
            if (type == typeof(ulong))
                return "ulong";
            if (type == typeof(char))
                return "char";
            if (type == typeof(double))
                return "double";
            if (type == typeof(float))
                return "float";
            if (type == typeof(IntPtr))
                return "IntPtr";
            if (type == typeof(UIntPtr))
                return "UIntPtr";

            throw new InvalidOperationException($"{type} is not a primitive type");
        }

        private static void GetFullTypeName(Type type, StringBuilder buf, in MethodData methodData)
        {
            // If we encounter a generic parameter (typically T) then we should replace it with a real one
            // specified by [GenerateTestsForBurstCompatibility(GenericTypeArguments = new [] { typeof(...) })].
            if (type.IsGenericParameter)
            {
                GetFullTypeName(methodData.ReplaceGeneric(type), buf, methodData);
                return;
            }

            if (type.DeclaringType != null)
            {
                GetFullTypeName(type.DeclaringType, buf, methodData);
                buf.Append(".");
            }
            else
            {
                // These appends for the namespace used to be protected by an if check for Unity.Collections or
                // Unity.Collections.LowLevel.Unsafe, but HashSetExtensions lives in both so just fully disambiguate
                // by always appending the namespace.
                buf.Append(type.Namespace);
                buf.Append(".");
            }

            var name = type.Name;

            var idx = name.IndexOf('`');
            if (-1 != idx)
            {
                name = name.Remove(idx);
            }

            buf.Append(name);

            if (type.IsConstructedGenericType || type.IsGenericTypeDefinition)
            {
                var gt = type.GetGenericArguments();

                // Avoid printing out the generic arguments for cases like UnsafeParallelHashMap<TKey, TValue>.ParallelWriter.
                // ParallelWriter is considered to be a generic type and will have two generic parameters inherited
                // from UnsafeParallelHashMap. Because of this, if we don't do this check, we could code gen this:
                //
                // UnsafeParallelHashMap<int, int>.ParallelWriter<int, int>
                //
                // But we want:
                //
                // UnsafeParallelHashMap<int, int>.ParallelWriter
                //
                // ParallelWriter doesn't actually have generic arguments you can give it directly so it's not correct
                // to give it generic arguments. If the nested type has the same number of generic arguments as its
                // declaring type, then there should be no new generic arguments and therefore nothing to print.
                if (type.IsNested && gt.Length == type.DeclaringType.GetGenericArguments().Length)
                {
                    return;
                }

                buf.Append("<");

                for (int i = 0; i < gt.Length; ++i)
                {
                    if (i > 0)
                    {
                        buf.Append(", ");
                    }

                    TypeToString(gt[i], buf, methodData);
                }

                buf.Append(">");
            }
        }

        private static readonly Type[] EmptyGenericTypeArguments = { };

        private bool GetTestMethods(out MethodData[] methods)
        {
            var seenMethods = new HashSet<MethodBase>();
            var result = new List<MethodData>();
            int errorCount = 0;

            void LogError(string message)
            {
                ++errorCount;
                Debug.LogError(message);
            }

            void MaybeAddMethod(MethodBase m, Type[] methodGenericTypeArguments, Type[] declaredTypeGenericTypeArguments, string requiredDefine, MemberInfo attributeHolder, GenerateTestsForBurstCompatibilityAttribute.BurstCompatibleCompileTarget compileTarget)
            {
                if (m.IsPrivate)
                {
                    // Private methods that were explicitly tagged as [GenerateTestsForBurstCompatibility] should generate an error to
                    // avoid users thinking the method is being tested when it actually isn't.
                    if (m.GetCustomAttribute<GenerateTestsForBurstCompatibilityAttribute>() != null)
                    {
                        // Just return and avoiding printing duplicate errors if we've already seen this method.
                        if (seenMethods.Contains(m))
                            return;

                        seenMethods.Add(m);
                        LogError($"GenerateTestsForBurstCompatibilityAttribute cannot be used with private methods, but found on private method `{m.DeclaringType}.{m}`. Make method public, internal, or ensure that this method is called by another public/internal method that is tested.");
                    }

                    return;
                }

                // Burst IL post processing might create a new method that contains $ to ensure it doesn't
                // conflict with any real names (as an example, it can append $BurstManaged to the original method name).
                // However, trying to call that method directly in C# isn't possible because the $ is invalid for
                // identifiers.
                if (m.Name.Contains('$'))
                {
                    return;
                }

                if (attributeHolder.GetCustomAttribute<ObsoleteAttribute>() != null)
                    return;
                if (attributeHolder.GetCustomAttribute<ExcludeFromBurstCompatTestingAttribute>() != null)
                    return;
                if (attributeHolder.GetCustomAttribute<BurstDiscardAttribute>() != null)
                    return;

                // If this is not a property but still has a special name, ignore it.
                if (attributeHolder is MethodInfo && m.IsSpecialName)
                    return;

                var methodGenericArgumentLookup = new Dictionary<string, Type>();

                if (m.IsGenericMethodDefinition)
                {
                    if (methodGenericTypeArguments == null)
                    {
                        LogError($"Method `{m.DeclaringType}.{m}` is generic but doesn't have a type array in its GenerateTestsForBurstCompatibility attribute");
                        return;
                    }

                    var genericArguments = m.GetGenericArguments();

                    if (genericArguments.Length != methodGenericTypeArguments.Length)
                    {
                        LogError($"Method `{m.DeclaringType}.{m}` is generic with {genericArguments.Length} generic parameters but GenerateTestsForBurstCompatibility attribute has {methodGenericTypeArguments.Length} types, they must be the same length!");
                        return;
                    }

                    try
                    {
                        m = (m as MethodInfo).MakeGenericMethod(methodGenericTypeArguments);
                    }
                    catch (Exception e)
                    {
                        LogError($"Could not instantiate method `{m.DeclaringType}.{m}` with type arguments `{methodGenericTypeArguments}`.");
                        Debug.LogException(e);
                        return;
                    }

                    // Build up the generic name to type lookup for this method.
                    for (int i = 0; i < genericArguments.Length; ++i)
                    {
                        var name = genericArguments[i].Name;
                        var type = methodGenericTypeArguments[i];

                        try
                        {
                            methodGenericArgumentLookup.Add(name, type);
                        }
                        catch (Exception e)
                        {
                            LogError($"For method `{m.DeclaringType}.{m}`, could not add ({name}, {type}).");
                            Debug.LogException(e);
                            return;
                        }
                    }
                }

                var instanceType = m.DeclaringType;
                var instanceTypeGenericLookup = new Dictionary<string, Type>();

                if (instanceType.IsGenericTypeDefinition)
                {
                    var instanceGenericArguments = instanceType.GetGenericArguments();

                    if (declaredTypeGenericTypeArguments == null)
                    {
                        LogError($"Type `{m.DeclaringType}` is generic but doesn't have a type array in its GenerateTestsForBurstCompatibility attribute");
                        return;
                    }

                    if (instanceGenericArguments.Length != declaredTypeGenericTypeArguments.Length)
                    {
                        LogError($"Type `{instanceType}` is generic with {instanceGenericArguments.Length} generic parameters but GenerateTestsForBurstCompatibility attribute has {declaredTypeGenericTypeArguments.Length} types, they must be the same length!");
                        return;
                    }

                    try
                    {
                        instanceType = instanceType.MakeGenericType(declaredTypeGenericTypeArguments);
                    }
                    catch (Exception e)
                    {
                        LogError($"Could not instantiate type `{instanceType}` with type arguments `{declaredTypeGenericTypeArguments}`.");
                        Debug.LogException(e);
                        return;
                    }

                    // Build up the generic name to type lookup for this method.
                    for (int i = 0; i < instanceGenericArguments.Length; ++i)
                    {
                        var name = instanceGenericArguments[i].Name;
                        var type = declaredTypeGenericTypeArguments[i];

                        try
                        {
                            instanceTypeGenericLookup.Add(name, type);
                        }
                        catch (Exception e)
                        {
                            LogError($"For type `{instanceType}`, could not add ({name}, {type}).");
                            Debug.LogException(e);
                            return;
                        }
                    }
                }

                //if (m.GetParameters().Any((p) => !p.ParameterType.IsValueType && !p.ParameterType.IsPointer))
                //   return;

                // These are crazy nested function names. They'll be covered anyway as the parent function is burst compatible.
                if (m.Name.Contains('<'))
                    return;

                if (seenMethods.Contains(m))
                    return;

                seenMethods.Add(m);
                result.Add(new MethodData
                {
                    methodBase = m, InstanceType = instanceType, MethodGenericTypeArguments = methodGenericTypeArguments,
                    InstanceTypeGenericTypeArguments = declaredTypeGenericTypeArguments, RequiredDefine = requiredDefine,
                    MethodGenericArgumentLookup = methodGenericArgumentLookup, InstanceTypeGenericArgumentLookup = instanceTypeGenericLookup,
                    CompileTarget = compileTarget
                });
            }

            var declaredTypeGenericArguments = new Dictionary<Type, Type[]>();

            // Go through types tagged with [GenerateTestsForBurstCompatibility] and their methods before performing the direct method
            // search (below this loop) to ensure that we get the declared type generic arguments.
            //
            // If we were to run the direct method search first, it's possible we would add the method to the seen list
            // and then by the time we execute this loop we might skip it because we think we have seen the method
            // already but we haven't grabbed the declared type generic arguments yet.
            foreach (var t in TypeCache.GetTypesWithAttribute<GenerateTestsForBurstCompatibilityAttribute>())
            {
                if (!m_AssembliesToVerify.Contains(t.Assembly.GetName().Name))
                    continue;

                foreach (var typeAttr in t.GetCustomAttributes<GenerateTestsForBurstCompatibilityAttribute>())
                {
                    // As we go through all the types with [GenerateTestsForBurstCompatibility] on them, remember their GenericTypeArguments
                    // in case we encounter the type again when we do the direct method search later. When we do the
                    // direct method search, we don't have as easy access to the [GenerateTestsForBurstCompatibility] attribute on the
                    // type so just remember this now to make life easier.
                    declaredTypeGenericArguments[t] = typeAttr.GenericTypeArguments;

                    const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public |
                                               BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                    foreach (var c in t.GetConstructors(flags))
                    {
                        var attributes = c.GetCustomAttributes<GenerateTestsForBurstCompatibilityAttribute>().ToArray();

                        if (attributes.Length == 0)
                        {
                            MaybeAddMethod(c, EmptyGenericTypeArguments, typeAttr.GenericTypeArguments, typeAttr.RequiredUnityDefine, c, typeAttr.CompileTarget);
                        }
                        else
                        {
                            foreach (var methodAttr in attributes)
                            {
                                MaybeAddMethod(c, methodAttr.GenericTypeArguments, typeAttr.GenericTypeArguments, methodAttr.RequiredUnityDefine ?? typeAttr.RequiredUnityDefine, c, methodAttr.CompileTarget);
                            }
                        }
                    }

                    foreach (var m in t.GetMethods(flags))
                    {
                        // If this is a property getter/setter, the attributes will be stored on the property itself, not
                        // the method.
                        MemberInfo attributeHolder = m;
                        if (m.IsSpecialName && (m.Name.StartsWith("get_") || m.Name.StartsWith("set_")))
                        {
                            attributeHolder = m.DeclaringType.GetProperty(m.Name.Substring("get_".Length), flags);
                            Debug.Assert(attributeHolder != null, $"Failed to find property for {m.Name} in {m.DeclaringType.Name}");
                        }

                        var attributes = attributeHolder.GetCustomAttributes<GenerateTestsForBurstCompatibilityAttribute>().ToArray();

                        if (attributes.Length == 0)
                        {
                            MaybeAddMethod(m, EmptyGenericTypeArguments, typeAttr.GenericTypeArguments, typeAttr.RequiredUnityDefine, attributeHolder, typeAttr.CompileTarget);
                        }
                        else
                        {
                            foreach (var methodAttr in attributes)
                            {
                                MaybeAddMethod(m, methodAttr.GenericTypeArguments, typeAttr.GenericTypeArguments, methodAttr.RequiredUnityDefine ?? typeAttr.RequiredUnityDefine, attributeHolder, methodAttr.CompileTarget);
                            }
                        }
                    }
                }
            }

            // Direct method search.
            foreach (var m in TypeCache.GetMethodsWithAttribute<GenerateTestsForBurstCompatibilityAttribute>())
            {
                if (!m_AssembliesToVerify.Contains(m.DeclaringType.Assembly.GetName().Name))
                    continue;

                // Look up the GenericTypeArguments on the declaring type that we probably got earlier. If the key
                // doesn't exist then it means [GenerateTestsForBurstCompatibility] was only on the method and not the type, which is fine
                // but if [GenerateTestsForBurstCompatibility] was on the type we should go ahead and use whatever GenericTypeArguments
                // it may have had.
                var typeGenericArguments = declaredTypeGenericArguments.ContainsKey(m.DeclaringType) ? declaredTypeGenericArguments[m.DeclaringType] : EmptyGenericTypeArguments;

                foreach (var attr in m.GetCustomAttributes<GenerateTestsForBurstCompatibilityAttribute>())
                {
                    MaybeAddMethod(m, attr.GenericTypeArguments, typeGenericArguments, attr.RequiredUnityDefine, m, attr.CompileTarget);
                }
            }

            if (errorCount > 0)
            {
                methods = new MethodData[] {};
                return false;
            }

            methods = result.ToArray();
            Array.Sort(methods);
            return true;
        }

        private static string GetSafeName(in MethodData methodData)
        {
            var method = methodData.methodBase;
            return GetSafeName(method.DeclaringType, methodData) + "_" + r.Replace(method.Name, "__");
        }

        public static readonly Regex r = new Regex(@"[^A-Za-z_0-9]+");

        private static string GetSafeName(Type t, in MethodData methodData)
        {
            var b = new StringBuilder();
            GetFullTypeName(t, b, methodData);
            return r.Replace(b.ToString(), "__");
        }

        Assembly GetAssemblyByName(string name)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            return assemblies.SingleOrDefault(assembly => assembly.GetName().Name == name);
        }

        private StreamWriter m_BurstCompileLogs;

        // This is a log handler to save all the logs during burst compilation and eventually write to a file
        // so it is easier to see all the logs without truncation in the test runner.
        void LogHandler(string logString, string stackTrace, LogType type)
        {
            m_BurstCompileLogs.WriteLine(logString);
        }

        [UnityTest]
        public IEnumerator CompatibilityTests()
        {
            int runCount = 0;
            int successCount = 0;
            bool playerBuildSucceeded = false;
            var playerBuildTime = new TimeSpan();
            var compileFunctionPointerTime = new TimeSpan();

            try
            {
                if (!UpdateGeneratedFile(m_GeneratedCodePath, out var generatedCount))
                {
                    yield break;
                }

                // Make another copy of the generated code and put it in Temp so it's easier to inspect.
                var debugCodeGenTempDest = Path.Combine(m_TempBurstCompatibilityPath, "generated.cs");
                Directory.CreateDirectory(m_TempBurstCompatibilityPath);
                File.Copy(m_GeneratedCodePath, debugCodeGenTempDest, true);
                Debug.Log($"Generated {generatedCount} Burst compatibility tests.");
                Debug.Log($"You can inspect a copy of the generated code at: {Path.GetFullPath(debugCodeGenTempDest)}");

                // BEWARE: this causes a domain reload and can cause variables to go null.
                yield return new RecompileScripts();

                var t = GetAssemblyByName(m_GeneratedCodeAssemblyName).GetType(s_GeneratedClassName);
                if (t == null)
                {
                    throw new ApplicationException($"could not find generated type {s_GeneratedClassName} in assembly {m_GeneratedCodeAssemblyName}");
                }

                var logPath = Path.Combine(m_TempBurstCompatibilityPath, "BurstCompileLog.txt");
                m_BurstCompileLogs = File.CreateText(logPath);
                Debug.Log($"Logs from Burst compilation written to: {Path.GetFullPath(logPath)}\n");
                Application.logMessageReceived += LogHandler;
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name.StartsWith("BurstCompile_"))
                    {
                        ++runCount;
                        try
                        {
                            m.Invoke(null, null);
                            ++successCount;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }
                    }
                }

                stopwatch.Stop();
                compileFunctionPointerTime = stopwatch.Elapsed;

                var buildOptions = new BuildPlayerOptions();
                buildOptions.scenes = new[] { m_TestScenePath };
                buildOptions.target = EditorUserBuildSettings.activeBuildTarget;
                buildOptions.locationPathName = FileUtil.GetUniqueTempPathInProject();
                // TODO: DOTS-4886
                // Remove when fixed
                // Made a development build due to a bug in the Editor causing Debug.isDebugBuild to be false
                // on the next Domain reload after this build is made
                buildOptions.options = BuildOptions.IncludeTestAssemblies | BuildOptions.Development;

                // Temporary hack to work around issue where headless no-graphics CI pass would spit out
                // `RenderTexture.Create with shadow sampling failed` error, causing this test to fail.
                // Feature has been requested to NOT log this error when running headless.
                var ignoreFailingMessagesCache = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                var buildReport = BuildPipeline.BuildPlayer(buildOptions);
                LogAssert.ignoreFailingMessages = ignoreFailingMessagesCache;

                playerBuildSucceeded = buildReport.summary.result == BuildResult.Succeeded;
                playerBuildTime = buildReport.summary.totalTime;
            }
            finally
            {
                Application.logMessageReceived -= LogHandler;
                m_BurstCompileLogs?.Close();
                Debug.Log($"Player build duration: {playerBuildTime.TotalSeconds} s.");

                if (playerBuildSucceeded)
                {
                    Debug.Log("Burst AOT compile via player build succeeded.");
                }
                else
                {
                    Debug.LogError("Player build FAILED.");
                }

                Debug.Log($"Compile function pointer duration: {compileFunctionPointerTime.TotalSeconds} s.");

                if (runCount != successCount)
                {
                    Debug.LogError($"Burst compatibility tests failed; ran {runCount} editor tests, {successCount} OK, {runCount - successCount} FAILED.");
                }
                else
                {
                    Debug.Log($"Ran {runCount} editor Burst compatible tests, all OK.");
                }

                AssetDatabase.DeleteAsset(m_GeneratedCodePath);
            }

            yield return new RecompileScripts();
        }
    }
}
#endif
