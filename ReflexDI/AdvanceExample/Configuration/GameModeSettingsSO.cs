using UnityEngine;

namespace TowerDefence.Reflex.Project.Configuration
{
    // Định nghĩa các loại dịch vụ lưu trữ có thể có
    public enum SaveServiceType
    {
        PlayerPrefs, // Lưu cục bộ
        Firebase     // Lưu lên cloud (dummy)
    }

    [CreateAssetMenu(fileName = "GameModeSettings", menuName = "TowerDefence/Game Mode Settings")]
    public class GameModeSettingsSO : ScriptableObject
    {
        [Header("Game Mode Info")]
        public string ModeName = "Normal";

        [Header("Initial Values")]
        public int InitialLives = 20;
        public int InitialCurrency = 150;

        [Header("System Configuration")]
        [Tooltip("Chọn dịch vụ sẽ được sử dụng để lưu/tải game.")]
        public SaveServiceType SaveService = SaveServiceType.PlayerPrefs;
    }
}
