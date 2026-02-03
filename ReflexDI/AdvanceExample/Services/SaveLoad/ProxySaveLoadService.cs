using UnityEngine;
using TowerDefence.Reflex.Project.Services; // Thêm using cho ISaveLoadService
using TowerDefence.Reflex.Project.Services.SaveLoad.Implementations; // Thêm using cho các Implementations

namespace TowerDefence.Reflex.Project.Services.SaveLoad
{
    /// <summary>
    /// Lớp Proxy triển khai ISaveLoadService.
    /// Nó sẽ quyết định lúc runtime nên sử dụng dịch vụ lưu trữ nào (local hay cloud)
    /// dựa trên trạng thái kết nối mạng.
    /// </summary>
    public class ProxySaveLoadService : ISaveLoadService
    {
        private readonly NetworkStatusService _networkStatus;
        private readonly FirebaseSaveLoadService _cloudService;
        private readonly PlayerPrefsSaveLoadService _localService;

        // Proxy này yêu cầu tất cả các dịch vụ mà nó có thể cần dưới dạng các lớp cụ thể.
        public ProxySaveLoadService(
            NetworkStatusService networkStatus, 
            FirebaseSaveLoadService cloudService, 
            PlayerPrefsSaveLoadService localService)
        {
            _networkStatus = networkStatus;
            _cloudService = cloudService;
            _localService = localService;
            Debug.Log("[ProxySaveLoadService] Initialized. Ready to delegate save/load calls.");
        }

        // Khi được yêu cầu Load, nó sẽ ưu tiên cloud nếu có mạng.
        public T Load<T>(string key, T defaultValue = default)
        {
            if (_networkStatus.IsOnline)
            {
                Debug.Log("[ProxySaveLoadService] Network is ON. Attempting to load from Cloud.");
                return _cloudService.Load<T>(key, defaultValue);
            }
            
            Debug.Log("[ProxySaveLoadService] Network is OFF. Loading from Local.");
                return _localService.Load<T>(key, defaultValue);
        }

        // Khi được yêu cầu Save, nó sẽ thử lưu lên cloud nếu có mạng, nếu không sẽ lưu về local.
        public void Save<T>(string key, T data)
        {
            if (_networkStatus.IsOnline)
            {
                Debug.Log("[ProxySaveLoadService] Network is ON. Saving to Cloud.");
                _cloudService.Save(key, data);
            }
            else
            {
                Debug.Log("[ProxySaveLoadService] Network is OFF. Saving to Local.");
                _localService.Save(key, data);
            }
        }
    }
}
