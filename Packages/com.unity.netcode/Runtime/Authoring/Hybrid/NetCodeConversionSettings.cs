#if USING_PLATFORMS_PACKAGE
#if UNITY_EDITOR
using Unity.Build;
#endif

#if UNITY_EDITOR
public class NetCodeConversionSettings : IBuildComponent
{
    public NetcodeConversionTarget Target;
    public string Name => "NetCode Conversion Settings";

    public bool OnGUI()
    {
        UnityEditor.EditorGUI.BeginChangeCheck();
        Target = (NetcodeConversionTarget) UnityEditor.EditorGUILayout.EnumPopup("Target", Target);
        return UnityEditor.EditorGUI.EndChangeCheck();
    }
}
#endif
#endif
