using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = Unity.Entities.Analyzer.Test.CSharpCodeFixVerifier<
    Unity.Entities.Analyzer.BlobAssetAnalyzer,
    Unity.Entities.Analyzer.EntitiesCodeFixProvider>;

namespace Unity.Entities.Analyzer
{
    [TestClass]
    public class EntitiesAnalyzerUnitTest
    {
        #region NoError

        [TestMethod]
        public async Task NoErrors() => await VerifyCS.VerifyAnalyzerAsync("");


        [TestMethod]
        public async Task WithGenericMethod()
        {
            const string test = @"
                using Unity.Collections;
                using Unity.Entities;
                using Unity.Entities.Tests;

                class TypeName {
                    BlobAssetReference<T> FunctionName<T>() where T : unmanaged
                    {
                        var builder = new BlobBuilder(Allocator.Temp);
                        ref var root = ref builder.ConstructRoot<T>();
                        return builder.CreateBlobAssetReference<T>(Allocator.Temp);
                    }
                }
            ";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task WithManagedRefInStaticField()
        {
            const string test = @"
                using Unity.Collections;
                using Unity.Entities;
                using Unity.Entities.Tests;

                struct ManagedRefInStaticField
                {
                    public static string s_ManagedString;
                    public int i;
                }

                class TypeName {
                    void FunctionName()
                    {
                        var builder = new BlobBuilder(Allocator.Temp);
                        ref var root = ref builder.ConstructRoot<ManagedRefInStaticField>();
                        root.i = 42;
                    }
                }
            ";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task ClassWithValidBlobReferenceUsage()
        {
            const string test = @"
                using Unity.Collections;
                using Unity.Entities;
                using Unity.Entities.Tests;

                class TypeName {
                    public class GenericTypeWithVolatile<T>
                    {
                        public volatile T[] buffer;
                        public T this[int i] { get => buffer[i]; set => buffer[i] = value; }
                    }
                    GenericTypeWithVolatile<int> _intGeneric;
                    BoidInAnotherAssembly _someField;
                    BlobAssetReference<MyBlob> _blobAssetReference;

                    void FunctionName()
                    {
                        _intGeneric = new GenericTypeWithVolatile<int>();
                        _intGeneric.buffer = new[] {32, 12, 41};
                        _someField = new BoidInAnotherAssembly();
                        ref BlobArray<float> myFloats = ref _blobAssetReference.Value.myfloats;
                        ref MyBlob blob = ref _blobAssetReference.Value;
                    }
                }
            ";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
        #endregion

        #region ID_EA001 - You may only access BlobAssetStorage by (non-readonly) ref
        [TestMethod]
        public async Task StoreBlobAssetReferenceValueInLocal()
        {
            const string test = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                class TypeName {
                    static BlobAssetReference<MyBlob> _blobAssetReference;
                    void FunctionName() {
                        {|#0:MyBlob blob = _blobAssetReference.Value;|}
                    }
                }
            ";
            const string fixedSource = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                class TypeName {
                    static BlobAssetReference<MyBlob> _blobAssetReference;
                    void FunctionName() {
                        ref MyBlob blob = ref _blobAssetReference.Value;
                    }
                }
            ";
            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0001).WithLocation(0)
                .WithArguments("_blobAssetReference.Value", "MyBlob", "blob");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedSource);
        }

        [TestMethod]
        public async Task WorksFromConstructRootErrors()
        {
            const string test = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                class TypeName {
                    void FunctionName() {
                        var blobBuilder = new BlobBuilder(Unity.Collections.Allocator.Temp);
                        {|#0:AnimationBlobData root = blobBuilder.ConstructRoot<AnimationBlobData>();|}
                    }
                }
            ";
            const string fixedSource = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                class TypeName {
                    void FunctionName() {
                        var blobBuilder = new BlobBuilder(Unity.Collections.Allocator.Temp);
                        ref AnimationBlobData root = ref blobBuilder.ConstructRoot<AnimationBlobData>();
                    }
                }
            ";
            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0001).WithLocation(0)
                .WithArguments("blobBuilder.ConstructRoot<AnimationBlobData>()", "AnimationBlobData","root");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedSource);
        }

        [TestMethod]
        public async Task WorksFromDirectLineErrors()
        {
            const string test = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                class TypeName {
                    void FunctionName() {
                        {|#0:var root = new BlobBuilder(Unity.Collections.Allocator.Temp).ConstructRoot<AnimationBlobData>();|}
                    }
                }
            ";

            const string fixedSource = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                class TypeName {
                    void FunctionName() {
                        ref var root = ref new BlobBuilder(Unity.Collections.Allocator.Temp).ConstructRoot<AnimationBlobData>();
                    }
                }
            ";
            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0001).WithLocation(0)
                .WithArguments("new BlobBuilder(Unity.Collections.Allocator.Temp).ConstructRoot<AnimationBlobData>()", "var", "root");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedSource);
        }

        [TestMethod]
        public async Task WorksFromFieldErrors()
        {
            const string test = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                class TypeName {
                    void FunctionName() {
                        ref var root = ref new BlobBuilder(Unity.Collections.Allocator.Temp).ConstructRoot<AnimationBlobData>();
                        {|#0:var keys = root.Keys;|}
                    }
                }
            ";

            const string fixedSource = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                class TypeName {
                    void FunctionName() {
                        ref var root = ref new BlobBuilder(Unity.Collections.Allocator.Temp).ConstructRoot<AnimationBlobData>();
                        ref var keys = ref root.Keys;
                    }
                }
            ";

            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0001).WithLocation(0).WithArguments("root.Keys", "var", "keys");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedSource);
        }

        [TestMethod]
        public async Task LoadFieldFromBlobAssetReference()
        {
            const string test = @"
                using Unity.Collections;
                using Unity.Entities;
                using Unity.Entities.Tests;

                class TypeName {
                    static BlobAssetReference<MyBlob> _blobAssetReference;
                    void FunctionName()
                    {
                        var builder = new BlobBuilder(Allocator.Temp);
                        // Test
                        {|#0:BlobArray<float> myFloats = _blobAssetReference.Value.myfloats;|}
                    }
                }
            ";

            const string fixedSource = @"
                using Unity.Collections;
                using Unity.Entities;
                using Unity.Entities.Tests;

                class TypeName {
                    static BlobAssetReference<MyBlob> _blobAssetReference;
                    void FunctionName()
                    {
                        var builder = new BlobBuilder(Allocator.Temp);
                        // Test
                        ref BlobArray<float> myFloats = ref _blobAssetReference.Value.myfloats;
                    }
                }
            ";

            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0001).WithLocation(0)
                .WithArguments("_blobAssetReference.Value.myfloats", "BlobArray<float>", "myFloats");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedSource);
        }

        [TestMethod]
        public async Task StoreBlobAssetReferenceValue_IntoReadonlyReference()
        {
            const string test = @"
                using Unity.Entities;
                using Unity.Entities.Tests;

                class TypeName {
                    BlobAssetReference<MyBlob> _blobAssetReference;
                    void FunctionName()
                    {
                        // Test
                        {|#0:ref readonly MyBlob readonlyBlob = ref _blobAssetReference.Value;|}
                    }
                }
            ";

            const string fixedSource = @"
                using Unity.Entities;
                using Unity.Entities.Tests;

                class TypeName {
                    BlobAssetReference<MyBlob> _blobAssetReference;
                    void FunctionName()
                    {
                        // Test
                        ref MyBlob readonlyBlob = ref _blobAssetReference.Value;
                    }
                }
            ";
            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0001).WithLocation(0)
                .WithArguments("_blobAssetReference.Value", "MyBlob", "readonlyBlob");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedSource);
        }

        [TestMethod]
        public async Task LoadFieldFromBlobAssetReference_IntoReadonlyReference()
        {
            const string test = @"
                using Unity.Entities;
                using Unity.Entities.Tests;

                class TypeName {
                    BlobAssetReference<MyBlob> _blobAssetReference;
                    void FunctionName()
                    {
                        {|#0:ref readonly BlobArray<float> myReadOnlyFloats = ref _blobAssetReference.Value.myfloats;|}
                    }
                }
            ";

            const string fixedSource = @"
                using Unity.Entities;
                using Unity.Entities.Tests;

                class TypeName {
                    BlobAssetReference<MyBlob> _blobAssetReference;
                    void FunctionName()
                    {
                        ref BlobArray<float> myReadOnlyFloats = ref _blobAssetReference.Value.myfloats;
                    }
                }
            ";
            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0001).WithLocation(0)
                .WithArguments("_blobAssetReference.Value.myfloats", "BlobArray<float>", "myReadOnlyFloats");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedSource);
        }
        #endregion

        #region ID_EA002 - You cannot manually new a BlobAsset

        [TestMethod]
        public async Task ObjectInitOnNewVarErrors()
        {
            const string test = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                public struct Node
                {
                    BlobPtr<byte> _parent;
                    BlobArray<byte> _children;
                    unsafe public ref BlobPtr<Node> parent => throw new System.NotImplementedException();
                    unsafe public ref BlobArray<Node> children => throw new System.NotImplementedException();
                }

                class TypeName {
                    void FunctionName() {
                        BlobBuilder builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
                        ref var rootNode = ref builder.ConstructRoot<Node>();
                        {|#0:var child = new Node();|}
                        builder.SetPointer(ref rootNode.parent, ref child);
                    }
                }
            ";

            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0002).WithLocation(0).WithArguments("new Node()");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task CanSupressEA0002()
        {
            const string test = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                public struct Node
                {
                    BlobPtr<byte> _parent;
                    BlobArray<byte> _children;
                    unsafe public ref BlobPtr<Node> parent => throw new System.NotImplementedException();
                    unsafe public ref BlobArray<Node> children => throw new System.NotImplementedException();
                }

                class TypeName {
                    void FunctionName() {
                        BlobBuilder builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
                        ref var rootNode = ref builder.ConstructRoot<Node>();
                        #pragma warning disable EA0002 // Rethrow to preserve stack details
                        {|#0:var child = new Node();|}
                        #pragma warning restore EA0002 // Rethrow to preserve stack details
                        builder.SetPointer(ref rootNode.parent, ref child);
                    }
                }
            ";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        #endregion

        #region ID_EA003 - You cannot use the BlobBuilder to build a type containing Non-Blob References

        [TestMethod]
        public async Task PointerToFixedSize()
        {
            const string test = @"
                using Unity.Collections;
                using Unity.Entities;
                using Unity.Entities.Tests;
                public unsafe struct TestStruct256bytes
                {
                    public fixed int array[61];
                }
                class TypeName {
                    void FunctionName() {
                        var builder = new BlobBuilder(Allocator.Temp, 128);
                        ref var root = ref {|#0:builder.ConstructRoot<TestStruct256bytes>()|};
                    }
                }
            ";
            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0003).WithLocation(0).WithArguments("TestStruct256bytes", "TestStruct256bytes.array", "is a pointer.  Only non-reference types are allowed in Blobs.");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task WithPointerInBlob()
        {
            const string test = @"
                using Unity.Collections;
                using Unity.Entities;
                using Unity.Entities.Tests;
                unsafe struct BlobWithPointer
                {
                    public int* p;
                }
                class TypeName {
                    void FunctionName() {
                        var builder = new BlobBuilder(Allocator.Temp);
                        ref var root = ref {|#0:builder.ConstructRoot<BlobWithPointer>()|};
                    }
                }
            ";
            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0003).WithLocation(0)
                .WithArguments("BlobWithPointer", "BlobWithPointer.p", "is a pointer.  Only non-reference types are allowed in Blobs.");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task WithManagedRefsInBlob()
        {
            const string test = @"
                using Unity.Collections;
                using Unity.Entities;
                using Unity.Entities.Tests;

                class TypeName {
                    void FunctionName()
                    {
                        var builder = new BlobBuilder(Allocator.Temp);
                        ref var root = ref {|#0:builder.ConstructRoot<ManagedBlob>()|};
                    }
                }
            ";
            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0003).WithLocation(0)
                .WithArguments("Unity.Entities.Tests.ManagedBlob", "Unity.Entities.Tests.ManagedBlob.s", "is a reference.  Only non-reference types are allowed in Blobs.");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task WithManagedRefInBlobArray()
        {
            const string test = @"
                using Unity.Collections;
                using Unity.Entities;
                using Unity.Entities.Tests;

                struct ManagedRefInBlobArray
                {
                    public BlobArray<ManagedBlob> array;
                }
                class TypeName {
                    void FunctionName()
                    {
                        var builder = new BlobBuilder(Allocator.Temp);
                        ref var root = ref {|#0:builder.ConstructRoot<ManagedRefInBlobArray>()|};
                    }
                }
            ";

            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0003).WithLocation(0)
                .WithArguments("ManagedRefInBlobArray", "ManagedRefInBlobArray.array[].s", "is a reference.  Only non-reference types are allowed in Blobs.");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task WithUnmanagedPtrInBlobPtr()
        {
            const string test = @"
                using Unity.Collections;
                using Unity.Entities;
                using Unity.Entities.Tests;

                unsafe struct BlobWithPointer
                {
                    public int* p;
                }

                struct UnmanagedPtrInBlobPtr
                {
                    public BlobPtr<BlobWithPointer> ptr;
                }

                class TypeName {
                    void FunctionName()
                    {
                        var builder = new BlobBuilder(Allocator.Temp);
                        ref var root = ref {|#0:builder.ConstructRoot<UnmanagedPtrInBlobPtr>()|};
                    }
                }
            ";
            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0003).WithLocation(0)
                .WithArguments("UnmanagedPtrInBlobPtr", "UnmanagedPtrInBlobPtr.ptr.Value.p", "is a pointer.  Only non-reference types are allowed in Blobs.");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task WithWeakAssetRefInBlob()
        {
            const string test = @"
                using Unity.Collections;
                using Unity.Entities;
                using Unity.Entities.Tests;
                unsafe struct BlobWithWeakAssetRef
                {
                    public EntityPrefabReference PrefabRef;
                }

                class TypeName {
                    void FunctionName() {
                        var builder = new BlobBuilder(Allocator.Temp);
                        ref var root = ref {|#0:builder.ConstructRoot<BlobWithWeakAssetRef>()|};
                    }
                }
            ";
            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0003).WithLocation(0)
                .WithArguments("BlobWithWeakAssetRef", "BlobWithWeakAssetRef.PrefabRef.PrefabId", "is an UntypedWeakReferenceId. Weak asset references are not yet supported in Blobs.");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        #endregion

        #region ID_EA004 - You cannot pass a potential blobasset as anything but ref.

        [TestMethod]
        public async Task BlobPassedByValue()
        {
            const string test = @"
                using Unity.Collections;
                using Unity.Entities;
                using Unity.Entities.Tests;

                class TypeName {
                    void FunctionName(AnimationBlobData {|#0:data|}) {}
                }
            ";

            const string fixedSource = @"
                using Unity.Collections;
                using Unity.Entities;
                using Unity.Entities.Tests;

                class TypeName {
                    void FunctionName(ref AnimationBlobData data) {}
                }
            ";
            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0009).WithLocation(0)
                .WithArguments("global::Unity.Entities.Tests.AnimationBlobData", "TypeName.FunctionName(Unity.Entities.Tests.AnimationBlobData)");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedSource);
        }

        [TestMethod]
        public async Task BlobPassedByIn()
        {
            const string test = @"
                using Unity.Collections;
                using Unity.Entities;
                using Unity.Entities.Tests;

                class TypeName {
                    void FunctionName(in AnimationBlobData {|#0:data|}) {}
                }
            ";

            const string fixedSource = @"
                using Unity.Collections;
                using Unity.Entities;
                using Unity.Entities.Tests;

                class TypeName {
                    void FunctionName(ref AnimationBlobData data) {}
                }
            ";
            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0009).WithLocation(0)
                .WithArguments("global::Unity.Entities.Tests.AnimationBlobData", "TypeName.FunctionName(in Unity.Entities.Tests.AnimationBlobData)");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedSource);
        }
        #endregion
    }
}
