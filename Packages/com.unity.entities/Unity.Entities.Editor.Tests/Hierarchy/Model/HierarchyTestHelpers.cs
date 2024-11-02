using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Editor.Tests
{
    class HierarchyTestHelpers
    {
        static readonly Regex k_ExpectedNodePattern = new Regex(@"(?<depth>-*)\s?(?<id>-?\d+)", RegexOptions.Compiled);

        public static void AssertImmutableIsSequenceEqualTo(HierarchyNodeStore.Immutable buffer, string[] expectedNodes)
        {
            var nodesFromImmutable = new (int, int)[buffer.Count];
            for (var i = 0; i < buffer.Count; i++)
            {
                var n = buffer.GetNode(i);
                nodesFromImmutable[i] = (n.GetDepth() + 1, n.GetHandle().Index);
            }

            var nodesFromExpectation = new (int, int)[expectedNodes.Length];
            for (var i = 0; i < expectedNodes.Length; i++)
            {
                var match = k_ExpectedNodePattern.Match(expectedNodes[i]);
                nodesFromExpectation[i] = (match.Groups["depth"].Value.Length, int.Parse(match.Groups["id"].Value));
            }

            Assert.That(Enumerable.SequenceEqual(nodesFromImmutable, nodesFromExpectation), Is.True, () => $"Expected {Environment.NewLine}{Print(nodesFromExpectation)} but was {Environment.NewLine}{Print(nodesFromImmutable)}{Environment.NewLine}");

            static string Print((int, int)[] nodes)
            {
                var sb = new StringBuilder();
                foreach (var (depth, id) in nodes)
                {
                    sb.AppendLine($"{new string('-', depth)}{id}");
                }

                return sb.ToString();
            }
        }

        public static void AssertFilteredImmutableIsSequenceEqualTo(HierarchyNodeStore.Immutable buffer, NativeBitArray bitMask, HierarchyNodeHandle[] expectedNodes)
        {
            var filtered = new List<HierarchyNodeHandle>();
            for (var i = 0; i < buffer.Count; i++)
            {
                if (bitMask.IsSet(i))
                    filtered.Add(buffer[i].Handle);
            }

            Assert.That(Enumerable.SequenceEqual(filtered, expectedNodes), Is.True, () => $"Expected {Environment.NewLine}{Print(expectedNodes)} but was {Environment.NewLine}{Print(filtered)}{Environment.NewLine}");

            static string Print(IEnumerable<HierarchyNodeHandle> nodes)
            {
                var sb = new StringBuilder();
                foreach (var node in nodes)
                {
                    sb.AppendLine($"{node}");
                }

                return sb.ToString();
            }
        }
    }
}
