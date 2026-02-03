using UnityEngine;
using TowerDefence.Reflex.Project.Services; // Thêm using cho ISaveLoadService

namespace TowerDefence.Reflex.Project.Services.SaveLoad.Implementations
{
    /// <summary>
    /// Một triển khai cụ thể của ISaveLoadService, sử dụng PlayerPrefs để lưu/tải dữ liệu.
    /// Lớp này không biết gì về GameManager hay bất kỳ logic game nào khác, nó chỉ làm một việc duy nhất.
    /// </summary>
    public class PlayerPrefsSaveLoadService : ISaveLoadService
    {
        public void Save<T>(string key, T data)
        {
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
            Debug.Log($"[PlayerPrefsSaveLoadService] Saved data to key '{key}'.");
        }

        public T Load<T>(string key, T defaultValue = default)
        {
            if (PlayerPrefs.HasKey(key))
            {
                string json = PlayerPrefs.GetString(key);
                Debug.Log($"[PlayerPrefsSaveLoadService] Loaded data from key '{key}'.");
                return JsonUtility.FromJson<T>(json);
            }
            
            Debug.Log($"[PlayerPrefsSaveLoadService] No data found for key '{key}', returning default value.");
            return defaultValue;
        }
    }
}
