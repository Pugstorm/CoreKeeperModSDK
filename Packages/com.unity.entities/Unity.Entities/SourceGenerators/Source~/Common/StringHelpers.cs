namespace Unity.Entities.SourceGen.Common
{
    public static class StringHelpers
    {
        public static string EmitIfTrue(this string emitString, bool someCondition) => someCondition ? emitString : string.Empty;
    }
}
