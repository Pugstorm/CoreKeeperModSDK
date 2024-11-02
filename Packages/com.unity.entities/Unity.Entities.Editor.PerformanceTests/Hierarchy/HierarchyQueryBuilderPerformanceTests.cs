using NUnit.Framework;
using System.Linq;
using System.Text;
using Unity.PerformanceTesting;

namespace Unity.Entities.Editor.PerformanceTests
{
    class HierarchyQueryBuilderPerformanceTests
    {
        [Test, Performance]
        public void QueryBuilder_PerformanceTests()
        {
            var types = TypeManager
                .GetAllTypes()
                .Where(t => t.Type != null && (t.Category == TypeManager.TypeCategory.ComponentData || t.Category == TypeManager.TypeCategory.ISharedComponentData))
                .Take(50)
                .ToArray();
            var inputString = new StringBuilder();
            for (var i = 0; i < types.Length; i++)
            {
                inputString.AppendFormat("c:{0}{1} ble ", i % 2 == 0 ? "!" : string.Empty, types[i].Type.Namespace);
            }

            var input = inputString.ToString();

            Measure.Method(() => HierarchyQueryBuilder.BuildQuery(input))
                .SampleGroup($"Build query from string input containing {types.Length} type constraints")
                .WarmupCount(10)
                .MeasurementCount(1000)
                .Run();
        }
    }
}
