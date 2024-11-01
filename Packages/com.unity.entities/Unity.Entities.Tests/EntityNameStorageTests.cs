#if !UNITY_DOTSPLAYER
using System;
using System.Globalization;
using System.Text;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Tests;
using UnityEngine;
using UnityEngine.TestTools;
using Random = Unity.Mathematics.Random;

namespace Entities.Tests
{

    [TestFixture("en-US")]
    [TestFixture("da-DK")]
    internal class EntityNameStorageTests
    {
        CultureInfo testCulture;
        CultureInfo backupCulture;
        World world;
        EntityNameStorage.State backupEntityNameState;
        NativeArray<FixedString64Bytes> backupNames;

        public EntityNameStorageTests(string culture)
        {
            testCulture = CultureInfo.CreateSpecificCulture(culture);
        }

        [SetUp]
        public virtual unsafe void Setup()
        {
            backupCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = testCulture;

            world = new World("Test World");
#if !DOTS_DISABLE_DEBUG_NAMES
            //backup name storage, for validation
            backupEntityNameState = EntityNameStorage.s_State.Data;
            //set to false so initialize fully clears data, as if it's a new playmode.
            EntityNameStorage.s_State.Data.initialized = 0;
            EntityNameStorage.Initialize();
            Assert.AreEqual(1,EntityNameStorage.s_State.Data.entries);
#endif //!DOTS_DISABLE_DEBUG_NAMES
        }

        [TearDown]
        public virtual unsafe void TearDown()
        {
#if !DOTS_DISABLE_DEBUG_NAMES
            //restore name storage
            EntityNameStorage.Shutdown();
            EntityNameStorage.s_State.Data = backupEntityNameState;
#endif //DOTS_DISABLE_DEBUG_NAMES
            world.Dispose();
            Thread.CurrentThread.CurrentCulture = backupCulture;
        }

#if !DOTS_DISABLE_DEBUG_NAMES
        [Test]
        public void EntityName_TestCapacity()
        {
            Random random = new Random(0x1234);
            unsafe string RandomBigName()
            {
                var name = stackalloc char[256];
                for (int i = 0; i < 256; i++)
                {
                    name[i] = (char) random.NextInt('a', 'z');
                }

                return new string(name);
            }

            int capacity = EntityNameStorage.kMaxEntries;
            //because the first entry in the storage is 0 for "empty string"
            var entities = world.EntityManager.CreateEntity(world.EntityManager.CreateArchetype(), capacity - 1, world.UpdateAllocator.ToAllocator);
            for (int i = 0; i < capacity - 1; i++)
            {
                var bigSystemStringName = RandomBigName();
                var truncatedBigName = new FixedString64Bytes();
                truncatedBigName.Initialize(bigSystemStringName);
                world.EntityManager.SetName(entities[i], truncatedBigName);
            }
            Assert.AreEqual(capacity,EntityNameStorage.s_State.Data.entries);
            Assert.LessOrEqual(EntityNameStorage.s_State.Data.chars,EntityNameStorage.kMaxChars);

            Entity overE = world.EntityManager.CreateEntity();
            world.EntityManager.SetName(overE,"Overflow Entity");
            Assert.AreEqual(capacity,EntityNameStorage.s_State.Data.entries);

            LogAssert.Expect(LogType.Error,EntityNameStorage.s_State.Data.kMaxEntriesMsg.ToString());

        }

        [Test]
        [TestRequiresCollectionChecks]
        public void EntityName_ThrowsOn_Long_Strings()
        {
            string validString = "This is a string that is exactly 62 characters, padddddddding";
            //make a string 1 character longer than FixedString64Bytes.utf8MaxLengthInBytes - 1
            string invalidString = "This is a string that is exactly 62 characters, paddddddddingX";

            Entity overE = world.EntityManager.CreateEntity();

            Assert.DoesNotThrow(() =>
            {
                world.EntityManager.SetName(overE, validString);
                string truncName = world.EntityManager.GetName(overE);
                Assert.AreEqual(FixedString64Bytes.utf8MaxLengthInBytes,truncName.Length);
                Assert.AreEqual(validString,truncName);
            });

            Assert.Throws<ArgumentException>(() =>
            {
                world.EntityManager.SetName(overE, invalidString);
            });
        }

#endif //!DOTS_DISABLE_DEBUG_NAMES
        [TestCase("red")]
        [TestCase("orange")]
        [TestCase("yellow")]
        [TestCase("green")]
        [TestCase("blue")]
        [TestCase("indigo")]
        [TestCase("violet")]
        [TestCase("紅色", TestName="{m}(Chinese-Red)")]
        [TestCase("橙色", TestName="{m}(Chinese-Orange)")]
        [TestCase("黄色", TestName="{m}(Chinese-Yellow)")]
        [TestCase("绿色", TestName="{m}(Chinese-Green)")]
        [TestCase("蓝色", TestName="{m}(Chinese-Blue")]
        [TestCase("靛蓝色", TestName="{m}(Chinese-Indigo")]
        [TestCase("紫罗兰色", TestName="{m}(Chinese-Violet")]
        [TestCase("црвена", TestName = "{m}(Serbian-Red)")]
        [TestCase("наранџаста", TestName = "{m}(Serbian-Orange)")]
        [TestCase("жута", TestName = "{m}(Serbian-Yellow)")]
        [TestCase("зелена", TestName = "{m}(Serbian-Green)")]
        [TestCase("плава", TestName = "{m}(Serbian-Blue")]
        [TestCase("индиго", TestName = "{m}(Serbian-Indigo")]
        [TestCase("љубичаста", TestName = "{m}(Serbian-Violet")]
        [TestCase("George Washington")]
        [TestCase("John Adams")]
        [TestCase("Thomas Jefferson")]
        [TestCase("James Madison")]
        [TestCase("James Monroe")]
        [TestCase("John Quincy Adams")]
        [TestCase("Andrew Jackson")]
        [TestCase("村上春樹", TestName="{m}(HarukiMurakami)")]
        [TestCase("三島 由紀夫", TestName="{m}(MishimaYukio)")]
        [TestCase("吉本ばなな", TestName="{m}(YoshimotoBanana)")]
        [TestCase("大江健三郎", TestName="{m}(OeKenzaburo)")]
        [TestCase("川端 康成", TestName="{m}(KawabataYasunari)")]
        [TestCase("桐野夏生", TestName="{m}(TongyeXiasheng)")]
        [TestCase("芥川龍之介", TestName="{m}(RyunosukeAkutagawa)")]
        [TestCase("로마는 하루아침에 이루어진 것이 아니다", TestName="{m}(Korean-Proverb1)")]
        [TestCase("낮말은 새가 듣고 밤말은 쥐가 듣는다", TestName="{m}(Korean-Proverb2)")]
        [TestCase("말을 냇가에 끌고 갈 수는 있어도 억지로 물을 먹일 수는 없다", TestName="{m}(Korean-Proverb3)")]
        [TestCase("호랑이에게 물려가도 정신만 차리면 산다", TestName="{m}(Korean-Proverb4)")]
        [TestCase("Љубазни фењерџија чађавог лица хоће да ми покаже штос.", TestName = "{m}(Serbian-Pangram)")]
        [TestCase("Лако ти је плитку воду замутити и будалу наљутити", TestName = "{m}(Serbian-Proverb)")]
        [TestCase("Үнэн үг хэлсэн хүнд ноёд өстэй, үхэр унасан хүнд ноход өстэй.", TestName="{m}(Mongolian-Proverb1)")]
        [TestCase("Өнгөрсөн борооны хойноос эсгий нөмрөх.", TestName="{m}(Mongolian-Proverb2)")]
        [TestCase("Барын сүүл байснаас батганы толгой байсан нь дээр.", TestName="{m}(Mongolian-Proverb3)")]
        [TestCase("Гараар ганц хүнийг дийлэх. Tолгойгоор мянган хүнийг дийлэх.", TestName="{m}(Mongolian-Proverb4)")]
        [TestCase("Աղւէսը բերանը խաղողին չի հասնում, ասում է՝ խակ է", TestName="{m}(Armenian-Proverb1)")]
        [TestCase("Ամեն փայտ շերեփ չի դառնա, ամեն սար՝ Մասիս", TestName="{m}(Armenian-Proverb2)")]
        [TestCase("Արևին ասում է դուրս մի արի՝ ես դուրս եմ եկել", TestName="{m}(Armenian-Proverb3)")]
        [TestCase("Գայլի գլխին Աւետարան են կարդում, ասում է՝ շուտ արէ՛ք, գալլէս գնաց", TestName="{m}(Armenian-Proverb4)")]
        [TestCase("पृथिव्यां त्रीणी रत्नानि जलमन्नं सुभाषितम्।", TestName="{m}(Hindi-Proverb1)")]
        [TestCase("जननी जन्मभुमिस्छ स्वर्गादपि गरीयसि", TestName="{m}(Hindi-Proverb2)")]
        [TestCase("न अभिशेको न संस्कारः सिम्हस्य कृयते वनेविक्रमार्जितसत्वस्य स्वयमेव मृगेन्द्रता", TestName="{m}(Hindi-Proverb3)")]
        public void EntityName_Works(String inputValue)
        {
            // TODO: this test should run on a sandboxed new EntityNameStorage instance, so it doesn't inherit/corrupt the existing global name storage
            var inputValueFs = new FixedString64Bytes();
            inputValueFs.Initialize(inputValue); // Manual truncation as casting to FixedString64Bytes will throw on overflow.

            EntityName w = new EntityName();

            // Write:
            // Disabled as repeat test runs may occur. Assert.IsFalse(EntityNameStorage.Contains(inputValueFs), $"Expected that this store doesn't yet contain '{inputValueFs}'. From inputValue '{inputValue}'.");
            w.SetFixedString(in inputValueFs);

            // Read:
            FixedString64Bytes readValueFs = default;
            w.ToFixedString(ref readValueFs);

            // Validate:
            //Debug.Log($"{inputValueFs} ({inputValueFs.Length}) vs. {readValue} ({readValue.Length})");

            // String compare input to output:
            Assert.AreEqual(inputValueFs.Length, readValueFs.Length, $"Expected the truncated input FS to be the same length as the read FS. '{inputValue}' became '{inputValueFs}', read as '{readValueFs}'.");
            for (int i = 0; i < inputValueFs.Length; i++)
            {
                Assert.AreEqual(inputValueFs[i], readValueFs[i], $"Expected all values from inputValueFs to match outputValue at {i}. inputValue: '{inputValue}' became '{inputValueFs}' read as '{readValueFs}'");
            }

            // Test store index write:
            Assert.IsTrue(EntityNameStorage.Contains(inputValueFs), $"Expected EntityNameStorage to contain this name. inputValue: '{inputValue}'.");
            var storeIndex = EntityNameStorage.GetIndexFromHashAndFixedString(inputValueFs.GetHashCode(), in inputValueFs);
            Assert.GreaterOrEqual(storeIndex, 0, $"Expected store Index to return a valid index for input value '{inputValueFs}'. inputValue: '{inputValue}'.");
            Assert.AreEqual(storeIndex, w.Index, $"Expected EnityName.Index to match store index. inputValue: '{inputValue}'.");

            // Test duplicate values:
            EntityName wDuplicate = new EntityName();
            wDuplicate.SetFixedString(in inputValueFs);
            Assert.AreEqual(w.Index, wDuplicate.Index, $"The store should return the same index for the same string. inputValue: '{inputValue}'.");
            Assert.AreEqual(storeIndex, wDuplicate.Index, $"The store should return the same index for the same string. inputValue: '{inputValue}'.");
            var duplicateStoreIndex = EntityNameStorage.GetIndexFromHashAndFixedString(inputValueFs.GetHashCode(), in inputValueFs);
            Assert.AreEqual(duplicateStoreIndex, wDuplicate.Index, $"Expected EnityName.Index to match store index. inputValue: '{inputValue}'.");

            // Test duplicate values even if new value added.
            EntityName dummy = new EntityName();
            var dummyFs = inputValueFs;
            Assert.AreNotEqual(';', dummyFs[0], $"Test cannot function as need to modify first byte to unique value to test multiple adds. inputValue: '{inputValue}'.");
            dummyFs[0] = (byte) ';';
            dummy.SetFixedString(in dummyFs);

            var duplicateStoreIndex2 = EntityNameStorage.GetIndexFromHashAndFixedString(inputValueFs.GetHashCode(), in inputValueFs);
            Assert.AreEqual(duplicateStoreIndex2, wDuplicate.Index, $"Expected EntityName.Index to match store index (even after other value is added). inputValue: '{inputValue}'.");

            // Test add always increments the store:
            Assert.AreEqual(dummy.Index, w.Index + 1, $"Expected store Index to always increment when new values are added. inputValue: '{inputValue}'.");
        }
    }
}
#endif
