//#define PROFILING_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Search;

namespace Unity.Entities.Editor.Tests
{
    public class QuickSearchTestCase
    {
        public string query;
        public int? expectedCount;
        public object[] expectedValues;
        public string[] expectedLabels;
        public bool ordered;
        public string errorMessage;
        public bool keepDuplicateValue;
        public bool printResults = true;

        public override string ToString()
        {
            return $"`{query}`";
        }
    }

    public class QuickSearchTests
    {
        static Dictionary<int, Action> s_EventHandlerOffs = new();

        public static IEnumerator WaitForResultsPending(ISearchList result)
        {
            while (result.pending)
                yield return null;
        }

        public static IEnumerator WaitForSceneHierarchyChanged(Action hierarchyModifier)
        {
            bool hierarchyHasChanged = false;
            Action onHierarchyChanged = () => hierarchyHasChanged = true;
            EditorApplication.hierarchyChanged += onHierarchyChanged;

            hierarchyModifier();

            int maxIter = 100;
            while (!hierarchyHasChanged && maxIter-- > 0)
            {
                yield return null;
            }
            EditorApplication.hierarchyChanged -= onHierarchyChanged;
        }

        public static IEnumerator CompoundEnumerator(params IEnumerator[] enumerators)
        {
            foreach (var enumerator in enumerators)
            {
                yield return enumerator;
            }
        }

        public static IEnumerator WaitForSeconds(float seconds)
        {
            var waitStartTime = EditorApplication.timeSinceStartup;
            while (true)
            {
                if ((EditorApplication.timeSinceStartup - waitStartTime) > seconds)
                    yield break;

                yield return null;
            }
        }

        public static IEnumerator WaitForSeconds(Func<bool> predicate, float timeoutSeconds)
        {
            return WaitForSeconds(predicate, timeoutSeconds, $"Timeout: Waited more than {timeoutSeconds} seconds");
        }

        public static IEnumerator WaitForSeconds(Func<bool> predicate, float timeoutSeconds, string message)
        {
            var waitStartTime = EditorApplication.timeSinceStartup;
            while (true)
            {
                if (predicate())
                    yield break;

                if ((EditorApplication.timeSinceStartup - waitStartTime) > timeoutSeconds)
                    Assert.Fail(message);

                yield return null;
            }
        }

        public static IEnumerator WaitFor(Func<bool> predicate, int times = 500)
        {
            for (int i = 0; i < times; ++i)
            {
                if (predicate())
                    break;
                yield return null;
            }
        }

        public static IEnumerator FetchItems(string providerId, string query, List<SearchItem> items, float timeoutSeconds = 3f)
        {
            var startSearchTime = EditorApplication.timeSinceStartup;
            using (var searchContext = SearchService.CreateContext(providerId, query))
            using (var fetchedItems = SearchService.Request(searchContext, SearchFlags.Sorted))
            {
                yield return WaitForSeconds(() => !fetchedItems.pending, timeoutSeconds, "Cannot fetch items");
                items.AddRange(fetchedItems);
            }
        }

        [OneTimeTearDown]
        public void CloseAllSearchViews()
        {

#if PROFILING_TESTS
        UnityEditorInternal.ProfilerDriver.enabled = false;
#endif
        }

        [TearDown]
        public void CleanTestData()
        {
            foreach (var off in s_EventHandlerOffs.Values)
            {
                off();
            }
            s_EventHandlerOffs.Clear();

        }

        [InitializeOnLoadMethod]
        internal static void SetupTestPreferences()
        {
            if (UnityEditorInternal.InternalEditorUtility.isHumanControllingUs)
                return;
            EditorPrefs.SetInt("ApplicationIdleTime", 0);
            EditorPrefs.SetInt("InteractionMode", 1);
        }

    }

}
