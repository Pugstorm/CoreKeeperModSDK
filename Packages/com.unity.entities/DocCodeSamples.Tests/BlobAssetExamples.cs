using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

// The files in this namespace are used to test the code samples in the documentation.
namespace Doc.CodeSamples.Tests
{
    public class BlobAssetExamples
    {
        #region CreateSimpleBlobAsset
        struct MarketData
        {
            public float PriceOranges;
            public float PriceApples;
        }

        BlobAssetReference<MarketData> CreateMarketData()
        {
            // Create a new builder that will use temporary memory to construct the blob asset
            var builder = new BlobBuilder(Allocator.Temp);

            // Construct the root object for the blob asset. Notice the use of `ref`.
            ref MarketData marketData = ref builder.ConstructRoot<MarketData>();

            // Now fill the constructed root with the data:
            // Apples compare to Oranges in the universally accepted ratio of 2 : 1 .
            marketData.PriceApples = 2f;
            marketData.PriceOranges = 4f;

            // Now copy the data from the builder into its final place, which will
            // use the persistent allocator
            var result = builder.CreateBlobAssetReference<MarketData>(Allocator.Persistent);

            // Make sure to dispose the builder itself so all internal memory is disposed.
            builder.Dispose();
            return result;
        }
        #endregion

        #region CreateBlobAssetWithString
        struct CharacterSetup
        {
            public float Loveliness;
            public BlobString Name;
        }

        BlobAssetReference<CharacterSetup> CreateCharacterSetup(string name)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref CharacterSetup character = ref builder.ConstructRoot<CharacterSetup>();

            character.Loveliness = 9001; // it's just a very lovely character

            // Create a new BlobString and set it to the given name.
            builder.AllocateString(ref character.Name, name);

            var result = builder.CreateBlobAssetReference<CharacterSetup>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }
        #endregion

        #region CreateBlobAssetWithArray
        struct Hobby
        {
            public float Excitement;
            public int NumOrangesRequired;
        }

        struct HobbyPool
        {
            public BlobArray<Hobby> Hobbies;
        }

        BlobAssetReference<HobbyPool> CreateHobbyPool()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref HobbyPool hobbyPool = ref builder.ConstructRoot<HobbyPool>();

            // Allocate enough room for two hobbies in the pool. Use the returned BlobBuilderArray
            // to fill in the data.
            const int numHobbies = 2;
            BlobBuilderArray<Hobby> arrayBuilder = builder.Allocate(
                ref hobbyPool.Hobbies,
                numHobbies
            );

            // Initialize the hobbies.

            // An exciting hobby that consumes a lot of oranges.
            arrayBuilder[0] = new Hobby
            {
                Excitement = 1,
                NumOrangesRequired = 7
            };

            // A less exciting hobby that conserves oranges.
            arrayBuilder[1] = new Hobby
            {
                Excitement = 0.2f,
                NumOrangesRequired = 2
            };

            var result = builder.CreateBlobAssetReference<HobbyPool>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }
        #endregion

        #region CreateBlobAssetWithInternalPointer
        struct FriendList
        {
            public BlobPtr<BlobString> BestFriend;
            public BlobArray<BlobString> Friends;
        }

        BlobAssetReference<FriendList> CreateFriendList()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref FriendList friendList = ref builder.ConstructRoot<FriendList>();

            const int numFriends = 3;
            var arrayBuilder = builder.Allocate(ref friendList.Friends, numFriends);
            builder.AllocateString(ref arrayBuilder[0], "Alice");
            builder.AllocateString(ref arrayBuilder[1], "Bob");
            builder.AllocateString(ref arrayBuilder[2], "Joachim");

            // Set the best friend pointer to point to the second array element.
            builder.SetPointer(ref friendList.BestFriend, ref arrayBuilder[2]);

            var result = builder.CreateBlobAssetReference<FriendList>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }
        #endregion

        #region BlobAssetOnAComponent
        struct Hobbies : IComponentData
        {
            public BlobAssetReference<HobbyPool> Blob;
        }

        float GetExcitingHobby(ref Hobbies component, int numOranges)
        {
            // Get a reference to the pool of available hobbies. Note that it needs to be passed by
            // reference, because otherwise the internal reference in the BlobArray would be invalid.
            ref HobbyPool pool = ref component.Blob.Value;

            // Find the most exciting hobby we can participate in with our current number of oranges.
            float mostExcitingHobby = 0;
            for (int i = 0; i < pool.Hobbies.Length; i++)
            {
                // This is safe to use without a reference, because the Hobby struct does not
                // contain internal references.
                var hobby = pool.Hobbies[i];
                if (hobby.NumOrangesRequired > numOranges)
                    continue;
                if (hobby.Excitement >= mostExcitingHobby)
                    mostExcitingHobby = hobby.Excitement;
            }

            return mostExcitingHobby;
        }
        #endregion

        #region BlobAssetInRuntime
        public partial struct BlobAssetInRuntimeSystem : ISystem
        {
            private BlobAssetReference<MarketData> _blobAssetReference;

            public void OnCreate(ref SystemState state)
            {
                using (var builder = new BlobBuilder(Allocator.Temp))
                {
                    ref MarketData marketData = ref builder.ConstructRoot<MarketData>();
                    marketData.PriceApples = 2f;
                    marketData.PriceOranges = 4f;
                    _blobAssetReference =
                        builder.CreateBlobAssetReference<MarketData>(Allocator.Persistent);
                }
            }

            public void OnDestroy(ref SystemState state)
            {
                // Calling Dispose on the BlobAssetReference will destroy the referenced
                // BlobAsset and free its memory
                _blobAssetReference.Dispose();
            }
        }
        #endregion
    }
}
