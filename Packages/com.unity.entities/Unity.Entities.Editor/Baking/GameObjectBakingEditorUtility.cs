using UnityEngine;

namespace Unity.Entities.Editor
{
    enum GameObjectBakingResultStatus
    {
        /// <summary>
        /// This <see cref="GameObject"/> will not be baked.
        /// </summary>
        NotBaked,

        /// <summary>
        /// This <see cref="GameObject"/> will be baked because it is part of a sub-scene.
        /// </summary>
        BakedBySubScene,
    }

    static class GameObjectBakingEditorUtility
    {
        /// <summary>
        /// Returns an enum detailing if the given <see cref="GameObject"/> will be baked and how.
        /// </summary>
        /// <param name="gameObject">The <see cref="GameObject"/> to be converted.</param>
        /// <returns>A <see cref="GameObjectBakingResultStatus"/> code detailing how the <see cref="GameObject"/> will be baked.</returns>
        public static GameObjectBakingResultStatus GetGameObjectBakingResultStatus(GameObject gameObject)
        {
            if (null == gameObject || !gameObject)
                return GameObjectBakingResultStatus.NotBaked;

            return gameObject.scene.isSubScene
                ? GameObjectBakingResultStatus.BakedBySubScene
                : GameObjectBakingResultStatus.NotBaked;
        }

        public static bool IsBaked(this GameObjectBakingResultStatus status) =>
            status == GameObjectBakingResultStatus.BakedBySubScene;

        public static bool IsBaked(GameObject gameObject) =>
            GetGameObjectBakingResultStatus(gameObject).IsBaked();
    }
}
