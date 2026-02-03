using UnityEngine;
using Reflex.Core;

namespace TowerDefence.Reflex.Play
{
    /// <summary>
    /// Cài đặt các dịch vụ cục bộ cho PlayScene.
    /// Hướng dẫn: Gắn component này vào GameObject "SceneScope" trong PlayScene.
    /// </summary>
    public class PlaySceneInstaller : MonoBehaviour, IInstaller
    {
        public void InstallBindings(ContainerBuilder builder)
        {
            // Bind LevelManager như một Singleton trong phạm vi Scene này.
            // Reflex sẽ tự động inject GameManager (từ ProjectScope) vào constructor của LevelManager.
            builder.AddSingleton(typeof(LevelManager));
            Debug.Log("[PlaySceneInstaller] LevelManager has been bound to the Scene Scope.");
        }
    }
}
