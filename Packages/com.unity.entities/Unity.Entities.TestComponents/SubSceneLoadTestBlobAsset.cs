using System;
using Unity.Collections;
using Unity.Entities;

namespace Unity.Scenes.Editor.Tests
{
    public struct SubSceneLoadTestBlobAsset
    {
        public int Int;
        public BlobPtr<int> Ptr;
        public BlobString String;
        public BlobArray<BlobString> Strings;

        internal static string[] MakeStrings(int n)
        {
            var strings = new string[n];
            for (int i = 0; i < strings.Length; i++)
                strings[i] = i.ToString();
            return strings;
        }

        internal static BlobAssetReference<SubSceneLoadTestBlobAsset> Make(int n, int ptrN, string str, string[] strings)
        {
            var bb = new BlobBuilder(Allocator.Temp);
            ref var root = ref bb.ConstructRoot<SubSceneLoadTestBlobAsset>();
            root.Int = n;
            ref var allocatedInt = ref bb.Allocate(ref root.Ptr);
            allocatedInt = ptrN;
            bb.AllocateString(ref root.String, str);
            var stringsArray = bb.Allocate(ref root.Strings, strings.Length);
            for (int i = 0; i < strings.Length; i++)
                bb.AllocateString(ref stringsArray[i], strings[i]);
            var result = bb.CreateBlobAssetReference<SubSceneLoadTestBlobAsset>(Allocator.Persistent);
            bb.Dispose();
            return result;
        }
    }
}
