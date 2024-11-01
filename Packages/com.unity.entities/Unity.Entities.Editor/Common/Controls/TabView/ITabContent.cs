namespace Unity.Entities.Editor
{
    interface ITabContent
    {
        string TabName { get; }

        void OnTabVisibilityChanged(bool isVisible);
    }
}
