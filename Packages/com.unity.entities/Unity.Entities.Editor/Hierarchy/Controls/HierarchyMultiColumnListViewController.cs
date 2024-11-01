using System.Collections;
using System.Collections.Generic;
using UnityEngine.UIElements;
using HierarchyModel = Unity.Entities.Editor.Hierarchy;

namespace Unity.Entities.Editor
{
    class HierarchyMultiColumnListViewController : MultiColumnListViewController
    {
        readonly HierarchyModel m_Model;

        public HierarchyMultiColumnListViewController(HierarchyModel model, Columns columns, SortColumnDescriptions sortDescriptions, List<SortColumnDescription> sortedColumns)
            : base(columns, sortDescriptions, sortedColumns)
        {
            m_Model = model;
        }

        public override IList itemsSource => m_Model.GetNodes();
    }
}
