using UnityEngine;
using UnityEngine.SceneManagement;
using Reflex.Core;
using Reflex.Extensions;

namespace TowerDefence.Reflex.Boot
{
    /// <summary>
    /// Script khởi động, đặt trong BootScene.
    /// Nhiệm vụ:
    /// 1. Thiết lập container của BootScene làm container cha cho các scene sau.
    /// 2. Tải PlayScene ở chế độ Additive.
    /// </summary>
    public class BootLoader : MonoBehaviour
    {
        [Tooltip("Tên của scene gameplay chính")]
        [SerializeField] private string _playSceneName = "PlayScene";

        private void Start()
        {
            Debug.Log("[BootLoader] Boot scene started.");
            
            var bootSceneContainer = gameObject.scene.GetSceneContainer();

            void OverrideParent(Scene scene, ContainerBuilder builder)
            {
                // Chỉ override cho PlayScene, không override cho chính BootScene hoặc các scene khác.
                if (scene.name == _playSceneName)
                {
                    Debug.Log($"[BootLoader] Overriding parent for '{scene.name}' scene container.");
                    builder.SetParent(bootSceneContainer);
                }
            }

            SceneScope.OnSceneContainerBuilding += OverrideParent;

            // Tải PlayScene ở chế độ Additive để BootScene không bị hủy.
            // Điều này đảm bảo container của BootScene vẫn tồn tại để làm cha.
            var loadOperation = SceneManager.LoadSceneAsync(_playSceneName, LoadSceneMode.Additive);
            
            loadOperation.completed += operation =>
            {
                // Sau khi PlayScene đã load xong, chúng ta có thể hủy đăng ký callback.
                SceneScope.OnSceneContainerBuilding -= OverrideParent;
                
                // (Tùy chọn) Đặt PlayScene làm scene hoạt động chính.
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(_playSceneName));
            };
        }
    }
}
