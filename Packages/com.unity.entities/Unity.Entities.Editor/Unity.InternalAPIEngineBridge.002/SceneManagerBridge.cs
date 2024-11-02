using System;
using UnityEngine.SceneManagement;

namespace Unity.Editor.Bridge
{
    static class SceneManagerBridge
    {
        public static bool CanSetAsActiveScene(Scene scene) => SceneManager.CanSetAsActiveScene(scene);
    }
}
