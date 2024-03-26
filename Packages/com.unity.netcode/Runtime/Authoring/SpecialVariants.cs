namespace Unity.NetCode
{
    /// <summary>
    /// <para>Special universal component variant that can be assigned to any component and/or buffer when configuring
    /// the GhostComponentSerializerCollectionSystemGroup. Mostly used for stripping components from the server-side ghost prefabs.</para>
    /// <para>To use this for your own types: Set it as the default in your own <see cref="DefaultVariantSystemBase.RegisterDefaultVariants"/> method.</para>
    /// </summary>
    public sealed class ClientOnlyVariant
    {
    }
    /// <summary>
    /// <para>Special universal component variant that can be assigned to any component and/or buffer when configuring
    /// the GhostComponentSerializerCollectionSystemGroup. Mostly used for stripping components from the client-side ghost prefabs.</para>
    /// <para>To use this for your own types: Set it as the default in your own <see cref="DefaultVariantSystemBase.RegisterDefaultVariants"/> method.</para>
    /// </summary>
    public sealed class ServerOnlyVariant
    {
    }

    /// <summary>
    /// Special universal component variant that can be assigned to any component and/or buffer. When a component
    /// serializer is set to DontSerializeVariant, the component itself is <b>not</b> stripped from the client or server version of
    /// the prefab, but at runtime it is <b>not</b> serialized (and thus <b>not</b> sent to the clients).
    /// </summary>
    /// <remarks>`DontSerializeVariant` is the default variant for all child entities, and is available for all serialized types automatically.</remarks>
    public sealed class DontSerializeVariant
    {
    }
}
