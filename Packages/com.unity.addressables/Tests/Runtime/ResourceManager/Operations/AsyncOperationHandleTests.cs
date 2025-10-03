using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.TestTools;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.Networking;
using UnityEngine.TestTools.Constraints;

namespace UnityEngine.ResourceManagement.Tests
{
    public class AsyncOperationHandleTests
    {
        class FakeTypedOperation : AsyncOperationBase<GameObject>
        {
            public FakeTypedOperation()
            {
                m_RM = new ResourceManager();
                m_RM.postProfilerEvents = true;
            }

            public object GetResultAsObject()
            {
                return null;
            }

            protected override void Execute()
            {
            }
        }

        void IncreaseRefCount(AsyncOperationHandle handle, int count)
        {
            for (int i = 0; i < count; i++)
                handle.Acquire();
        }

        void IncreaseRefCount<TObject>(AsyncOperationHandle<TObject> handle, int count)
        {
            for (int i = 0; i < count; i++)
                handle.Acquire();
        }

        int DestructiveGetRefCount(AsyncOperationHandle handle)
        {
            int count = 0;
            while (handle.IsValid())
            {
                count++;
                var copy = handle;
                copy.Release();
            }

            return count;
        }

        int DestructiveGetRefCount<TObject>(AsyncOperationHandle<TObject> handle)
        {
            int count = 0;
            while (handle.IsValid())
            {
                count++;
                var copy = handle;
                copy.Release();
            }

            return count;
        }

        [Test]
        public void AsyncOperationHandle_ConvertToTyped_WithInvalidOpThrows()
        {
            var op = new FakeTypedOperation();
            AsyncOperationHandle handle = new AsyncOperationHandle(op);
            AsyncOperationHandle handle2 = new AsyncOperationHandle(op);
            handle2.Release();

            Assert.Throws<Exception>(() => { handle.Convert<GameObject>(); });
        }

        [Test]
        public void AsyncOperationHandle_ConvertToTyped_WithValidOpSucceeds()
        {
            var op = new FakeTypedOperation();
            AsyncOperationHandle handle = new AsyncOperationHandle(op);

            AsyncOperationHandle<GameObject> typedHandle = handle.Convert<GameObject>();
            Assert.True(handle.IsValid());
            Assert.True(typedHandle.IsValid());
        }

        [Test]
        public void AsyncOperationHandle_ConvertToTypeless_MaintainsValidity()
        {
            var op = new FakeTypedOperation();
            AsyncOperationHandle<GameObject> typedHandle = new AsyncOperationHandle<GameObject>(op);

            //implicit conversion of valid op
            AsyncOperationHandle typelessHandle = (AsyncOperationHandle)typedHandle;

            Assert.IsNotNull(typelessHandle);
            Assert.IsTrue(typedHandle.IsValid());
            Assert.IsTrue(typelessHandle.IsValid());

            //make handle invalid
            AsyncOperationHandle<GameObject> typedHandle2 = new AsyncOperationHandle<GameObject>(op);
            typedHandle2.Release();

            //implicit conversion of invalid op
            AsyncOperationHandle invalidHandle = (AsyncOperationHandle)typedHandle;

            Assert.IsNotNull(invalidHandle);
            Assert.IsFalse(invalidHandle.IsValid());
            Assert.IsFalse(typedHandle.IsValid());
        }

        [Test]
        public void AsyncOperationHandle_Release_DecrementsRefCount()
        {
            int expectedCount = 10;
            var op = new FakeTypedOperation();

            AsyncOperationHandle<GameObject> typedHandle = new AsyncOperationHandle<GameObject>(op);
            AsyncOperationHandle<GameObject> validationHandle = new AsyncOperationHandle<GameObject>(op);
            IncreaseRefCount(typedHandle, expectedCount - 1);

            typedHandle.Release();
            expectedCount--;
            var actualRefCount = DestructiveGetRefCount(validationHandle);
            Assert.AreEqual(expectedCount, actualRefCount);

            op = new FakeTypedOperation();

            AsyncOperationHandle typelessHandle = new AsyncOperationHandle(op);
            AsyncOperationHandle typelessValidation = new AsyncOperationHandle(op);
            IncreaseRefCount(typelessHandle, expectedCount - 1);
            typelessHandle.Release();
            expectedCount--;
            actualRefCount = DestructiveGetRefCount(typelessValidation);
            Assert.AreEqual(expectedCount, actualRefCount);
        }

        [Test]
        public void AsyncOperationHandle_ReleaseToZero_InvalidatesAllHandles()
        {
            var op = new FakeTypedOperation();
            AsyncOperationHandle<GameObject> typedHandle = new AsyncOperationHandle<GameObject>(op);
            AsyncOperationHandle<GameObject> typedHandle2 = new AsyncOperationHandle<GameObject>(op);
            typedHandle.Release();
            Assert.IsFalse(typedHandle.IsValid());
            Assert.IsFalse(typedHandle2.IsValid());

            op = new FakeTypedOperation();
            AsyncOperationHandle typelessHandle = new AsyncOperationHandle(op);
            AsyncOperationHandle typelessHandle2 = new AsyncOperationHandle(op);
            typelessHandle.Release();
            Assert.IsFalse(typelessHandle.IsValid());
            Assert.IsFalse(typelessHandle2.IsValid());
        }

        [Test]
        public void AsyncOperationHandle_ReleaseToNonZero_InvalidatesOnlyCurrentHandle()
        {
            var op = new FakeTypedOperation();
            AsyncOperationHandle<GameObject> typedHandle = new AsyncOperationHandle<GameObject>(op);
            IncreaseRefCount(typedHandle, 1);
            AsyncOperationHandle<GameObject> typedHandle2 = new AsyncOperationHandle<GameObject>(op);
            typedHandle.Release();
            Assert.IsFalse(typedHandle.IsValid());
            Assert.IsTrue(typedHandle2.IsValid());

            op = new FakeTypedOperation();
            AsyncOperationHandle typelessHandle = new AsyncOperationHandle(op);
            IncreaseRefCount(typelessHandle, 1);
            AsyncOperationHandle typelessHandle2 = new AsyncOperationHandle(op);
            typelessHandle.Release();
            Assert.IsFalse(typelessHandle.IsValid());
            Assert.IsTrue(typelessHandle2.IsValid());
        }

        [Test]
        public void AsyncOperationHandle_Acquire_IncrementsRefCount()
        {
            int expectedCount = 2;
            var op = new FakeTypedOperation();

            AsyncOperationHandle<GameObject> typedHandle = new AsyncOperationHandle<GameObject>(op);
            var copyTyped = typedHandle.Acquire();
            Assert.True(copyTyped.IsValid());
            Assert.True(typedHandle.IsValid());
            int actualCount = DestructiveGetRefCount(typedHandle);
            Assert.AreEqual(expectedCount, actualCount);


            op = new FakeTypedOperation();
            AsyncOperationHandle typelessHandle = new AsyncOperationHandle(op);
            var copyTypeless = typelessHandle.Acquire();
            Assert.True(copyTypeless.IsValid());
            Assert.True(typelessHandle.IsValid());
            actualCount = DestructiveGetRefCount(typelessHandle);
            Assert.AreEqual(expectedCount, actualCount);
        }

        [Test]
        public void AsyncOperationHandle_Version_FromOp()
        {
            var op = new FakeTypedOperation() { m_Version = 12345 };
            var handle = new AsyncOperationHandle<GameObject>(op);
            Assert.AreEqual(op.Version, handle.Version);
        }

        [Test]
        public void AsyncOperationHandle_Version_FromOp_Typeless()
        {
            var op = (IAsyncOperation)new FakeTypedOperation() { m_Version = 12345 };
            var handle = new AsyncOperationHandle(op);
            Assert.AreEqual(op.Version, handle.Version);
        }

        [Test]
        public void AsyncOperationHandle_Version_ConstructParam()
        {
            var op = new FakeTypedOperation() { m_Version = 12345 };
            var expected = 987654;
            var handle = new AsyncOperationHandle<GameObject>(op, expected);
            Assert.AreEqual(expected, handle.Version);
        }

        [Test]
        public void AsyncOperationHandle_Version_ConstructParam_Typeless()
        {
            var op = (IAsyncOperation)new FakeTypedOperation() { m_Version = 12345 };
            var expected = 987654;
            var handle = new AsyncOperationHandle(op, expected);
            Assert.AreEqual(expected, handle.Version);
        }

        [Test]
        public void AsyncOperationHandle_Version_CopiedByAcquire()
        {
            var expected = 987654;
            var op = new FakeTypedOperation() { m_Version = expected };
            var handle = new AsyncOperationHandle<GameObject>(op);
            var handle2 = handle.Acquire();
            Assert.AreEqual(expected, handle2.Version);
        }

        [Test]
        public void AsyncOperationHandle_Version_CopiedByAcquire_Typeless()
        {
            var expected = 987654;
            var op = (IAsyncOperation)new FakeTypedOperation() { m_Version = expected };
            var handle = new AsyncOperationHandle(op);
            var handle2 = handle.Acquire();
            Assert.AreEqual(expected, handle2.Version);
        }

        [Test]
        public void AsyncOperationHandle_LocationName_Default()
        {
            var op = new FakeTypedOperation();
            var handle = new AsyncOperationHandle<GameObject>(op);
            Assert.IsNull(handle.LocationName);
        }
        [Test]
        public void AsyncOperationHandle_LocationName_Default_Typeless()
        {
            var op = new FakeTypedOperation();
            var handle = new AsyncOperationHandle<GameObject>(op);
            Assert.IsNull(handle.LocationName);
        }

        [Test]
        public void AsyncOperationHandle_LocationName_Set()
        {
            var expected = "LocationNameSet";
            var handle = new AsyncOperationHandle<GameObject>(null);
            handle.LocationName = expected;
            Assert.AreEqual(expected, handle.LocationName);
        }
        [Test]
        public void AsyncOperationHandle_LocationName_Set_Typeless()
        {
            var expected = "LocationNameSet";
            var handle = new AsyncOperationHandle(null);
            handle.LocationName = expected;
            Assert.AreEqual(expected, handle.LocationName);
        }

        [Test]
        public void AsyncOperationHandle_LocationName_ConstructParam()
        {
            var op = new FakeTypedOperation();
            var expected = "LocationNameConstructParam";
            var handle = new AsyncOperationHandle<GameObject>(op, expected);
            Assert.AreEqual( expected, handle.LocationName);
        }

        [Test]
        public void AsyncOperationHandle_LocationName_ConstructParam_Typeless()
        {
            var op = (IAsyncOperation)new FakeTypedOperation();
            var expected = "LocationNameConstructParam";
            var handle = new AsyncOperationHandle(op, expected);
            Assert.AreEqual(expected, handle.LocationName);
        }

        [Test]
        public void AsyncOperationHandle_LocationName_CopiedByAcquire()
        {
            var op = new FakeTypedOperation();
            var expected = "LocationNameConstructParam";
            var handle = new AsyncOperationHandle<GameObject>(op, expected);
            var handle2 = handle.Acquire();
            Assert.AreEqual(expected, handle2.LocationName);
        }

        [Test]
        public void AsyncOperationHandle_LocationName_CopiedByAcquire_Typeless()
        {
            var op = (IAsyncOperation)new FakeTypedOperation();
            var expected = "LocationNameConstructParam";
            var handle = new AsyncOperationHandle(op, expected);
            var handle2 = handle.Acquire();
            Assert.AreEqual(expected, handle2.LocationName);
        }
    }
}
