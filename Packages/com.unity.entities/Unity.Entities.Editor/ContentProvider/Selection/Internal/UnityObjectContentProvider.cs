using UnityEngine;

namespace Unity.Entities.UI
{
    class UnityObjectContentProvider : ContentProvider
    {
        public Object Object;

        public override string Name => Object.name;

        protected override ContentStatus GetStatus()
        {
            if (null == Object)
                return ContentStatus.ContentUnavailable;

            // We can hit a "fake null" object after an undo or redo operation and sometimes during domain reload. If we
            // encounter a "fake null" object, we'll reload the content, which should either find the correct instance
            // (if it exists) or set the instance to an actual "null".
            if (!Object)
                return ContentStatus.ReloadContent;

            return ContentStatus.ContentReady;
        }

        public override object GetContent() => Object;
    }
}
