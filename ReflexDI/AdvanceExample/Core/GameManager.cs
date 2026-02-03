using UnityEngine;
using TowerDefence.Reflex.Project.Configuration;
using TowerDefence.Reflex.Project.Services;

namespace TowerDefence.Reflex.Project.Core
{
    /// <summary>
    /// Dịch vụ toàn cục, quản lý trạng thái cốt lõi của game như mạng sống, tiền tệ.
    /// </summary>
    public class GameManager
    {
        public int Lives { get; private set; }
        public int Currency { get; private set; }
        public string ModeName { get; }

        public System.Action OnStateChanged;

        private readonly ISaveLoadService _saveLoadService;
        private const string SAVE_KEY = "GameManager_State";

        // Dữ liệu để lưu trữ
        [System.Serializable]
        private class GameState
        {
            public int Lives;
            public int Currency;
        }

        // CONSTRUCTOR INJECTION: GameManager bây giờ yêu cầu cả cấu hình và dịch vụ lưu/tải.
        // Nó không biết 'ISaveLoadService' là PlayerPrefs hay một thứ khác, nó chỉ sử dụng "hợp đồng".
        public GameManager(GameModeSettingsSO settings, ISaveLoadService saveLoadService)
        {
            _saveLoadService = saveLoadService;
            ModeName = settings.ModeName;

            // Thử tải lại trạng thái game đã lưu
            var loadedState = _saveLoadService.Load(SAVE_KEY, new GameState { Lives = -1 });
            if (loadedState != null && loadedState.Lives != -1)
            {
                Lives = loadedState.Lives;
                Currency = loadedState.Currency;
                Debug.Log($"[GameManager] Loaded saved state: {Lives} Lives, {Currency} Currency.");
            }
            else
            {
                // Nếu không có save, dùng giá trị từ settings
                Lives = settings.InitialLives;
                Currency = settings.InitialCurrency;
                Debug.Log($"[GameManager] Initialized for '{ModeName}' mode with {Lives} Lives and {Currency} Currency.");
            }
        }

        public void DecreaseLives(int amount)
        {
            if (Lives <= 0) return;
            Lives -= amount;
            Debug.Log($"[GameManager] Player lost {amount} live(s). Remaining: {Lives}");
            OnStateChanged?.Invoke();
            SaveState(); // Lưu lại trạng thái sau khi thay đổi

            if (Lives <= 0)
            {
                Lives = 0;
                Debug.LogWarning($"[GameManager] GAME OVER! No lives left.");
            }
        }

        public bool SpendCurrency(int amount)
        {
            if (Currency >= amount)
            {
                Currency -= amount;
                Debug.Log($"[GameManager] Spent {amount} currency. Remaining: {Currency}");
                OnStateChanged?.Invoke();
                SaveState(); // Lưu lại trạng thái sau khi thay đổi
                return true;
            }
            
            Debug.LogWarning($"[GameManager] Not enough currency. Needed: {amount}, Have: {Currency}");
            return false;
        }

        private void SaveState()
        {
            var state = new GameState { Lives = this.Lives, Currency = this.Currency };
            _saveLoadService.Save(SAVE_KEY, state);
        }
    }
}
