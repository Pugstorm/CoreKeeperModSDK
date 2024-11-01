
namespace Unity.Entities.UI
{
    /// <summary>
    /// Defines the different mode of display for the <see cref="System.Version"/> type.
    /// </summary>
    public enum SystemVersionUsage
    {
        /// <summary>
        /// The inspector should only show the major and minor version numbers.
        /// </summary>
        MajorMinor = 0,
        /// <summary>
        /// The inspector should only show the major, minor and build version numbers.
        /// </summary>
        MajorMinorBuild = 1,
        /// <summary>
        /// The inspector should show the major, minor, build and revision version numbers.
        /// </summary>
        MajorMinorBuildRevision = 2
    }

    /// <summary>
    /// Use this attribute on fields and properties of type <see cref="System.Version"/> to indicate which properties
    /// should be displayed.
    /// </summary>
    public class SystemVersionUsageAttribute : InspectorAttribute
    {
        /// <summary>
        /// Returns the information about how a <see cref="System.Version"/> should be displayed.
        /// </summary>
        public SystemVersionUsage Usage { get; }

        /// <summary>
        /// Return <see langword="true"/> if the <see cref="System.Version.Build"/> property should be displayed.
        /// </summary>
        public bool IncludeBuild =>
            Usage == SystemVersionUsage.MajorMinorBuild || Usage == SystemVersionUsage.MajorMinorBuildRevision;

        /// <summary>
        /// Return <see langword="true"/> if the <see cref="System.Version.Revision"/> property should be displayed.
        /// </summary>
        public bool IncludeRevision =>
            Usage == SystemVersionUsage.MajorMinorBuildRevision;

        /// <summary>
        /// Constructs a new instance of <see cref="SystemVersionUsageAttribute"/> with the provided usage.
        /// </summary>
        /// <param name="usage">The indented usage of the <see cref="System.Version"/> type.</param>
        public SystemVersionUsageAttribute(SystemVersionUsage usage = SystemVersionUsage.MajorMinorBuildRevision)
        {
            Usage = usage;
        }
    }
}
