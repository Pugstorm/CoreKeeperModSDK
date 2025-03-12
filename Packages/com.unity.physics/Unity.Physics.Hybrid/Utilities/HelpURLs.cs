namespace Unity.Physics.Authoring
{
    static class HelpURLs
    {
        // always display latest version of documentation
        public const string PackageVersion = "latest";

        // URL links point to API instead of Documentation (manual) page.
        const string k_BaseURL = "https://docs.unity3d.com/Packages/com.unity.physics@" + PackageVersion + "/index.html?subfolder=/api/";
        const string k_Html = ".html";

        public const string CustomPhysicsBodyTagNames = k_BaseURL + "Unity.Physics.Authoring.CustomPhysicsBodyTagNames" + k_Html;
        public const string PhysicsDebugDisplayAuthoring = k_BaseURL + "Unity.Physics.Authoring.PhysicsDebugDisplayAuthoring" + k_Html;
        public const string PhysicsStepAuthoring = k_BaseURL + "Unity.Physics.Authoring.PhysicsStepAuthoring" + k_Html;
        public const string CustomPhysicsProxyAuthoring = k_BaseURL + "Unity.Physics.Authoring.CustomPhysicsProxyAuthoring" + k_Html;
        public const string PhysicsWorldIndexAuthoring = k_BaseURL + "Unity.Physics.Authoring.PhysicsWorldIndexAuthoring" + k_Html;
        public const string ForceUniqueColliderAuthoring = k_BaseURL + "Unity.Physics.Authoring.ForceUniqueColliderAuthoring" + k_Html;
    }
}
