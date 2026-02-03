using UnityEngine;
using Reflex.Attributes;
using TowerDefence.Reflex.Project.Core;

namespace TowerDefence.Reflex.Play
{
    /// <summary>
    /// Quản lý UI trong màn chơi, hiển thị thông tin từ GameManager.
    /// Hướng dẫn: Gắn component này vào một GameObject trên Canvas trong PlayScene.
    /// </summary>
    public class GameplayUI : MonoBehaviour
    {
        [Inject] private readonly GameManager _gameManager;
        [Inject] private readonly LevelManager _levelManager;

        private void OnEnable()
        {
            // Đăng ký sự kiện để cập nhật UI khi trạng thái game thay đổi
            if (_gameManager != null)
            {
                _gameManager.OnStateChanged += UpdateUI;
            }
            UpdateUI(); // Cập nhật lần đầu
        }

        private void OnDisable()
        {
            if (_gameManager != null)
            {
                _gameManager.OnStateChanged -= UpdateUI;
            }
        }

        private void Start()
        {
            Debug.Log("[GameplayUI] Started. Ready to display game state.");
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_gameManager == null) return;
            
            // Trong một dự án thực tế, bạn sẽ cập nhật các đối tượng Text, Slider, v.v.
            Debug.Log($"<color=cyan>[UI UPDATE] Lives: {_gameManager.Lives}, Currency: {_gameManager.Currency}</color>");
        }

        // Các hàm này có thể được gọi từ các nút bấm trên UI
        public void OnButton_SimulateEnemyLeak()
        {
            _levelManager.SimulateEnemyReachingEnd();
        }

        public void OnButton_SimulateBuildTower()
        {
            _levelManager.SimulateTowerPurchase();
        }
    }
}
