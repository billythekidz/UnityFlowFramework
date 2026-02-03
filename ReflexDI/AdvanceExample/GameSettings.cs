using UnityEngine;
using TowerDefence.Reflex.Project.Configuration;

namespace TowerDefence.Reflex.Project
{
    /// <summary>
    /// Lớp chứa cấu hình cho phiên chơi game hiện tại.
    /// Dữ liệu được khởi tạo từ một ScriptableObject.
    /// </summary>
    public class GameSettings
    {
        public string ModeName { get; }
        public int InitialLives { get; }
        public int InitialCurrency { get; }

        // Constructor bây giờ nhận một ScriptableObject.
        public GameSettings(GameModeSettingsSO settingsSO)
        {
            ModeName = settingsSO.ModeName;
            InitialLives = settingsSO.InitialLives;
            InitialCurrency = settingsSO.InitialCurrency;
            Debug.Log($"[GameSettings] Created for '{ModeName}' mode with {InitialLives} lives and {InitialCurrency} currency.");
        }
    }
}
