using UnityEngine;

namespace Unity.Entities.Tests
{
    public class AuthoringComponentTestInheritance : MonoBehaviour { public bool Field; }

    public class AuthoringComponentBaseTest : MonoBehaviour { public bool Field; }

    public class AuthoringComponentChildTest : AuthoringComponentBaseTest { public bool ChildField; }

}
