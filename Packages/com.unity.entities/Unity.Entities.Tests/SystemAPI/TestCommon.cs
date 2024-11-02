namespace Unity.Entities.Tests.TestSystemAPI
{
    public enum SystemAPIAccess {
        SystemAPI,
        Using
    }

    public enum SingletonVersion {
        ComponentData,
        Buffer
    }

    public enum MemberUnderneath {
        WithMemberUnderneath,
        WithoutMemberUnderneath
    }

    public enum ReadAccess {
        ReadOnly,
        ReadWrite
    }

    public enum TypeArgumentExplicit
    {
        TypeArgumentShown,
        TypeArgumentHidden
    }
}
