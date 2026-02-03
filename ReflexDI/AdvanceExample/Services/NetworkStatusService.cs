using UnityEngine;

namespace TowerDefence.Reflex.Project.Services
{
    /// <summary>
    /// Dịch vụ kiểm tra trạng thái kết nối mạng của thiết bị.
    /// </summary>
    public class NetworkStatusService
    {
        /// <summary>
        /// Trả về true nếu có kết nối mạng (Wifi hoặc Dữ liệu di động).
        /// </summary>
        public bool IsOnline => Application.internetReachability != NetworkReachability.NotReachable;
    }
}
