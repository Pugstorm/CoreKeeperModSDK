namespace Unity.Entities.SourceGen.SystemGenerator.SystemAPI.Query;

readonly struct ReturnedTupleElementDuringEnumeration
{
    public readonly string TypeSymbolFullName;
    public readonly string TypeArgumentFullName;
    public readonly string Name;
    public readonly QueryType Type;

    public ReturnedTupleElementDuringEnumeration(
        string typeSymbolFullName,
        string typeArgumentFullName,
        string elementName,
        QueryType type)
    {
        TypeSymbolFullName = typeSymbolFullName;
        TypeArgumentFullName = typeArgumentFullName;
        Name = elementName;
        Type = type;
    }
}
