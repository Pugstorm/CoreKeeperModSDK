using System.Collections;
using NUnit.Framework;
using Unity.Collections;

// ReSharper disable StringLiteralTypo
namespace Unity.Entities.Editor.Tests
{
    class SIMDSearchTests
    {
        static AllocatorHelper<RewindableAllocator> m_AllocatorHelper;
        static ref RewindableAllocator RwdAllocator => ref m_AllocatorHelper.Allocator;

        [OneTimeSetUp]
        public virtual void OneTimeSetUp()
        {
            m_AllocatorHelper = new AllocatorHelper<RewindableAllocator>(Allocator.Persistent);
            m_AllocatorHelper.Allocator.Initialize(128 * 1024, true);
        }

        [OneTimeTearDown]
        public virtual void OneTimeTearDown()
        {
            m_AllocatorHelper.Allocator.Dispose();
            m_AllocatorHelper.Dispose();
        }

        [TearDown]
        public virtual void TearDown()
        {
            RwdAllocator.Rewind();
            // This is test only behavior for determinism.  Rewind twice such that all
            // tests start with an allocator containing only one memory block.
            RwdAllocator.Rewind();
        }

        public static IEnumerable GetTestCases()
        {
            const string expectFalse = "Returns FALSE when:";
            const string expectTrue = "Returns TRUE when:";

            // False
            yield return new TestCaseData("short", "s h o r t").SetName($"{expectFalse} Pattern too long").Returns(false);
            yield return new TestCaseData("A random string of characters", "Not found").SetName($"{expectFalse} Pattern not found").Returns(false);
            yield return new TestCaseData("A random string of characters", ".").SetName($"{expectFalse} Pattern not found (single character pattern)").Returns(false);
            yield return new TestCaseData("A random string of characters", ".,").SetName($"{expectFalse} Pattern not found (2 characters pattern)").Returns(false);
            yield return new TestCaseData("AA", "B").SetName($"{expectFalse} Pattern not found (very short strings)").Returns(false);
            yield return new TestCaseData("12345678", "87654321").SetName($"{expectFalse} Pattern not found (same length strings)").Returns(false);
            yield return new TestCaseData("A random string of characters", "rondam").SetName($"{expectFalse} Pattern matches first and last characters but not middle").Returns(false);
            yield return new TestCaseData("A random string of characters", "ranDom").SetName($"{expectFalse} Pattern is in wrong casing").Returns(false);
            yield return new TestCaseData("ǬǶʤΞЖ҈Ի", "ΞϽ҈").SetName($"{expectFalse} Pattern not found (UTF16 characters)").Returns(false);

            yield return new TestCaseData("A random string of characters", null).SetName($"{expectFalse} Searching with a default (null) pattern").Returns(false);
            yield return new TestCaseData(null, "Hi").SetName($"{expectFalse} Searching on a default (null) string").Returns(false);
            yield return new TestCaseData(null, null).SetName($"{expectFalse} Searching on a default (null) string with a default (null) pattern").Returns(false);

            // True
            yield return new TestCaseData("A random string of characters", "A random").SetName($"{expectTrue} Pattern found (at beginning)").Returns(true);
            yield return new TestCaseData("A random string", "string").SetName($"{expectTrue} Pattern found (at end)").Returns(true);
            yield return new TestCaseData("A random string of characters", " rand").SetName($"{expectTrue} Pattern found (2nd position)").Returns(true);
            yield return new TestCaseData("A random string!", "string").SetName($"{expectTrue} Pattern found (2nd to last position)").Returns(true);
            yield return new TestCaseData("A random string!", "r").SetName($"{expectTrue} Pattern found (single character pattern)").Returns(true);
            yield return new TestCaseData("A random string!", "ra").SetName($"{expectTrue} Pattern found (2 characters pattern)").Returns(true);
            yield return new TestCaseData("AB", "A").SetName($"{expectTrue} Pattern found (very short strings, start)").Returns(true);
            yield return new TestCaseData("AB", "B").SetName($"{expectTrue} Pattern found (very short strings, end)").Returns(true);
            yield return new TestCaseData("A random string!", "A random string!").SetName($"{expectTrue} Pattern found (same length strings)").Returns(true);
            yield return new TestCaseData("ǬǶʤΞЖ҈Ի", "ΞЖ҈").SetName($"{expectTrue} Pattern found (UTF16 characters)").Returns(true);

            // These tests exist because we are matching 32 characters at a time
            yield return new TestCaseData("A very long and boring string of random characters", "string of random").SetName($"{expectTrue} Pattern found, starting before index 32 and ending after").Returns(true);
            yield return new TestCaseData("A very long and boring string of random characters", "char").SetName($"{expectTrue} Pattern found completely after index 32").Returns(true);

            // Ensures that 16-bit characters are not matched by two 8-bit characters that just happen to be the "upper" and "lower" portions of the value
            char[] utf16SourceData = {'H', 'e', 'l', 'l', 'o', (char) 0b01100101_11101100, '!'};
            char[] utf8PatternData = {'o', (char) 0b01100101, (char) 0b11101100, '!'};
            yield return new TestCaseData(new string(utf16SourceData), new string(utf8PatternData)).SetName($"{expectFalse} Comparing 2 UTF-8 characters with a UTF-16 character.").Returns(false);
        }

        [TestCaseSource(nameof(GetTestCases))]
        public bool SIMDSearchTestRunner(string source, string pattern)
            => Contains(
                string.IsNullOrEmpty(source) ? default : new FixedString64Bytes(source),
                string.IsNullOrEmpty(pattern) ? default : new FixedString64Bytes(pattern));

        static bool Contains(FixedString64Bytes source, FixedString64Bytes pattern)
        {
            using var sources = new NativeList<FixedString64Bytes>(1, RwdAllocator.ToAllocator) {source};
            using var patterns = new NativeList<FixedString64Bytes>(1, RwdAllocator.ToAllocator) {pattern};
            using var result = SIMDSearch.GetFilteredMatches(sources, patterns);

            return result.IsSet(0);
        }
    }
}
