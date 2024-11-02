using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Unity.Mathematics;

namespace Unity.Entities.Editor.PerformanceTests
{
    class EntityHierarchyScenario
    {
        static class Values
        {
            public static class AmountOfEntities
            {
                public const int Low       =    10_000;
                public const int Medium    =   100_000;
                public const int High      = 1_000_000;
                public const int VeryHigh  = 5_000_000;
            }

            public static class AmountOfChange
            {
                public const float None    = 0.00f;
                public const float Low     = 0.01f;
                public const float Medium  = 0.10f;
                public const float High    = 0.25f;
                public const float All     = 1.00f;
            }

            public static class AmountOfArchetypeVariants
            {
                public const int Low       =  0; // Do not split user archetypes into variants
                public const int Medium    =  8; // Split user archetypes into 8 variants
                public const int High      = 16; // Split user archetypes into 16 variants
            }

            public static class AmountOfSegments
            {
                public const float Low     = 0.000f; // Nothing segmented, chunks are filled as much as possible
                public const float Medium  = 0.002f;
                public const float High    = 0.008f;
            }

            public static class DepthOfStructure
            {
                public const int Shallow   =  3;
                public const int Deep      = 10;
            }

            public static class ItemsVisibility
            {
                public const float AllCollapsed = 0.0f;
                public const float AllExpanded = 1.0f;
            }
        }

        const uint k_Seed = 0xDEAD_BEEF;

        static readonly Dictionary<AmountOfEntities, int> k_AmountOfEntitiesMap = new Dictionary<AmountOfEntities, int>
        {
            {AmountOfEntities.Low, Values.AmountOfEntities.Low}, {AmountOfEntities.Medium, Values.AmountOfEntities.Medium}, {AmountOfEntities.High, Values.AmountOfEntities.High}, { AmountOfEntities.VeryHigh, Values.AmountOfEntities.VeryHigh}
        };

        static readonly Dictionary<AmountOfChange, float> k_AmountOfChangeMap = new Dictionary<AmountOfChange, float>
        {
            {AmountOfChange.None, Values.AmountOfChange.None}, {AmountOfChange.Low, Values.AmountOfChange.Low}, {AmountOfChange.Medium, Values.AmountOfChange.Medium}, {AmountOfChange.High, Values.AmountOfChange.High}, {AmountOfChange.All, Values.AmountOfChange.All}
        };

        static readonly Dictionary<AmountOfFragmentation, int> k_AmountOfFragmentationToAmountOfVariantsMap = new Dictionary<AmountOfFragmentation, int>
        {
            {AmountOfFragmentation.Low, Values.AmountOfArchetypeVariants.Low}, {AmountOfFragmentation.Medium, Values.AmountOfArchetypeVariants.Medium}, {AmountOfFragmentation.High, Values.AmountOfArchetypeVariants.High}
        };

        static readonly Dictionary<AmountOfFragmentation, float> k_AmountOfFragmentationToSegmentationMap = new Dictionary<AmountOfFragmentation, float>
        {
            {AmountOfFragmentation.Low, Values.AmountOfSegments.Low}, {AmountOfFragmentation.Medium, Values.AmountOfSegments.Medium}, {AmountOfFragmentation.High, Values.AmountOfSegments.High}
        };

        static readonly Dictionary<DepthOfStructure, int> k_DepthOfStructureMap = new Dictionary<DepthOfStructure, int>
        {
            {DepthOfStructure.Shallow, Values.DepthOfStructure.Shallow}, {DepthOfStructure.Deep, Values.DepthOfStructure.Deep}
        };

        static readonly Dictionary<ItemsVisibility, float> k_ItemsVisibilityMap = new Dictionary<ItemsVisibility, float>
        {
            {ItemsVisibility.AllCollapsed, Values.ItemsVisibility.AllCollapsed}, {ItemsVisibility.AllExpanded, Values.ItemsVisibility.AllExpanded}
        };

        // Used to build named scenarios
        public static EntityHierarchyScenario GetScenario(ScenarioId scenarioId, ItemsVisibility itemsVisibility = ItemsVisibility.AllExpanded)
        {
            switch (scenarioId)
            {
                case ScenarioId.ScenarioA:
                {
                    return new EntityHierarchyScenario
                    (
                        AmountOfEntities.High, AmountOfChange.Low,
                        AmountOfFragmentation.Low, DepthOfStructure.Deep,
                        itemsVisibility,
                        scenarioId.ToString()
                    );
                }
                case ScenarioId.ScenarioB:
                {
                    return new EntityHierarchyScenario
                    (
                        AmountOfEntities.Low, AmountOfChange.High,
                        AmountOfFragmentation.Low, DepthOfStructure.Deep,
                        itemsVisibility,
                        scenarioId.ToString()
                    );
                }
                case ScenarioId.ScenarioC:
                {
                    return new EntityHierarchyScenario
                    (
                        AmountOfEntities.Medium, AmountOfChange.Low,
                        AmountOfFragmentation.High, DepthOfStructure.Deep,
                        itemsVisibility,
                        scenarioId.ToString()
                    );
                }
                case ScenarioId.ScenarioD:
                {
                    return new EntityHierarchyScenario
                    (
                        AmountOfEntities.Medium, AmountOfChange.Medium,
                        AmountOfFragmentation.Medium, DepthOfStructure.Deep,
                        itemsVisibility,
                        scenarioId.ToString()
                    );
                }
                case ScenarioId.ScenarioE:
                {
                    return new EntityHierarchyScenario
                    (
                        AmountOfEntities.VeryHigh, AmountOfChange.None,
                        AmountOfFragmentation.Low, DepthOfStructure.Shallow,
                        itemsVisibility,
                        scenarioId.ToString()
                    );
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(scenarioId), scenarioId, null);
                }
            }
        }

        public int TotalEntities { get; }
        public float PercentageOfEntitiesToChange { get; }
        public int ChangeCount { get; }
        public int AmountOfArchetypeVariants { get; }
        public float PercentageOfSegmentation { get; }
        public int SegmentsCount { get; }
        public float PercentageOfVisibleItems { get; }
        public int MaximumDepth { get; }

        /// <summary>
        /// If any component of the scenario requires randomness, use this seed
        /// </summary>
        public uint Seed => k_Seed;

        // Stored for identification
        readonly AmountOfEntities m_AmountOfEntities;
        readonly AmountOfChange m_AmountOfChange;
        readonly AmountOfFragmentation m_AmountOfFragmentation;
        readonly DepthOfStructure m_DepthOfStructure;
        readonly ItemsVisibility m_ItemsVisibility;

        readonly string m_ScenarioId;

        public EntityHierarchyScenario(
            AmountOfEntities amountOfEntities,
            AmountOfChange amountOfChange,
            AmountOfFragmentation amountOfFragmentation,
            DepthOfStructure depthOfStructure,
            ItemsVisibility itemsVisibility,
            string scenarioId = null // For identification
        )
        {
            m_AmountOfEntities = amountOfEntities;
            TotalEntities = k_AmountOfEntitiesMap[amountOfEntities];

            m_AmountOfChange = amountOfChange;
            PercentageOfEntitiesToChange = k_AmountOfChangeMap[amountOfChange];

            m_AmountOfFragmentation = amountOfFragmentation;
            AmountOfArchetypeVariants = k_AmountOfFragmentationToAmountOfVariantsMap[amountOfFragmentation];
            PercentageOfSegmentation = k_AmountOfFragmentationToSegmentationMap[amountOfFragmentation];

            m_DepthOfStructure = depthOfStructure;
            MaximumDepth = k_DepthOfStructureMap[depthOfStructure];
            if (MaximumDepth < 1)
                throw new ArgumentException("Cannot create scenario with maximum depth of less than 1.");
            if (MaximumDepth > TotalEntities)
                throw new InvalidOperationException("Cannot create scenario with a maximum depth greater than the number of entities.");

            m_ItemsVisibility = itemsVisibility;
            PercentageOfVisibleItems = k_ItemsVisibilityMap[m_ItemsVisibility];

            m_ScenarioId = scenarioId;

            ChangeCount = (int) math.floor(TotalEntities * PercentageOfEntitiesToChange);
            SegmentsCount = math.max((int) math.floor(TotalEntities * PercentageOfSegmentation), 1); // Can't have less than 1 segment.
        }

        [SuppressMessage("ReSharper", "HeapView.BoxingAllocation")]
        public override string ToString()
        {
            return $@"Scenario: {m_ScenarioId ?? "Unnamed Scenario"} / Visibility: {nameof(ItemsVisibility)}.{m_ItemsVisibility}
    Data: {nameof(AmountOfEntities)}.{m_AmountOfEntities}, {nameof(AmountOfChange)}.{m_AmountOfChange}, {nameof(AmountOfFragmentation)}.{m_AmountOfFragmentation}, {nameof(DepthOfStructure)}.{m_DepthOfStructure}";
        }
    }

    /// <summary>
    /// The actual number of entities in a given test world
    /// </summary>
    enum AmountOfEntities
    {
        Low, Medium, High, VeryHigh
    }

    /// <summary>
    /// The percentage of entities that will change during a single iteration of a test
    /// </summary>
    enum AmountOfChange
    {
        None, Low, Medium, High, All
    }

    /// <summary>
    /// How much fragmentation to introduce into the world (number of archetype variants and segmentation through ISharedComponentData)
    /// </summary>
    enum AmountOfFragmentation
    {
        Low, Medium, High
    }

    /// <summary>
    /// How many levels deep is the deepest entity node
    /// </summary>
    enum DepthOfStructure
    {
        Shallow, Deep
    }

    /// <summary>
    /// What percentage of the TreeView items are "visible" (child of an expanded parent)
    /// </summary>
    enum ItemsVisibility
    {
        AllCollapsed, AllExpanded
    }

    enum ScenarioId
    {
        // TODO: Replace with research data
        ScenarioA,
        ScenarioB,
        ScenarioC,
        ScenarioD,
        ScenarioE
    }
}
