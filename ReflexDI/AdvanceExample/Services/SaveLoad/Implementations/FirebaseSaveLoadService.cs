using UnityEngine;
using TowerDefence.Reflex.Project.Services; // Thêm using cho ISaveLoadService

namespace TowerDefence.Reflex.Project.Services.SaveLoad.Implementations
{
    /// <summary>
    /// Một triển khai "giả" của ISaveLoadService, mô phỏng việc lưu/tải dữ liệu lên Firebase.
    /// Trong một dự án thực tế, lớp này sẽ chứa code để gọi Firebase SDK.
    /// </summary>
    public class FirebaseSaveLoadService : ISaveLoadService
    {
        public void Save<T>(string key, T data)
        {
            string json = JsonUtility.ToJson(data);
            // Trong thực tế, bạn sẽ gửi 'json' này lên Firebase Realtime Database hoặc Firestore.
            Debug.Log($"<color=orange>[FirebaseSaveLoadService]</color> Simulating SAVE to cloud for key '{key}'. Data: {json}");
        }

        public T Load<T>(string key, T defaultValue = default)
        {
            // Trong thực tế, bạn sẽ lắng nghe dữ liệu từ Firebase.
            // Ở đây, chúng ta luôn giả vờ là không có dữ liệu để game bắt đầu lại từ đầu.
            Debug.Log($"<color=orange>[FirebaseSaveLoadService]</color> Simulating LOAD from cloud for key '{key}'. No data found, returning default.");
            return defaultValue;
        }
    }
}
