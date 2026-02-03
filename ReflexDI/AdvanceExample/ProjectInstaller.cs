using System;
using UnityEngine;
using Reflex.Core;
using TowerDefence.Reflex.Project.Services; // Thêm using cho các services

namespace TowerDefence.Reflex
{
    /// <summary>
    /// Cài đặt các dịch vụ thực sự toàn cục, không phụ thuộc vào cấu hình từ scene.
    /// Nó có thể được dùng để bind các dịch vụ hệ thống khác nếu cần (ví dụ: Analytics, Logging).
    /// </summary>
    public class ProjectInstaller : MonoBehaviour, IInstaller
    {
        public void InstallBindings(ContainerBuilder builder)
        {
            // Dịch vụ này không cần cấu hình gì từ scene cả.
            // builder.AddSingleton(typeof(AnalyticsService));

            // AudioManager có thể đọc cài đặt âm lượng từ PlayerPrefs, không phụ thuộc scene.
            // builder.AddSingleton(typeof(AudioManager));
        }
    }
}

/*  [Explanatory Comments]
    Trong một dự án lớn thực tế, ProjectInstaller sẽ rất quan trọng và chứa nhiều thứ. Dưới đây là những ví dụ điển hình:
    1. Các Dịch vụ Hệ thống Không đổi:
    SaveLoadService: Hệ thống lưu và tải game. Logic này luôn giống nhau bất kể bạn đang ở màn chơi nào.
    AuthenticationService: Dịch vụ xác thực người dùng, kết nối với server backend.
    PlayerProfileService: Quản lý thông tin người chơi như tên, avatar, các vật phẩm vĩnh viễn.
    AnalyticsService: Dịch vụ gửi các sự kiện phân tích (analytics event).
    2. Các Trình quản lý Toàn cục:
    AudioManager: Quản lý nhạc nền, âm thanh UI, và các cài đặt âm lượng chung.
    AssetProviderService: Một lớp bao bọc (wrapper) cho Addressables hoặc Resources để quản lý việc tải asset một cách tập trung.
    GlobalEventBus: Một hệ thống truyền tin nhắn (message bus) để các hệ thống khác nhau có thể giao tiếp với nhau một cách lỏng lẻo.
    3. Các Factory (Nhà máy sản xuất):
    EnemyFactory, TowerFactory: Các lớp chịu trách nhiệm tạo ra kẻ địch hoặc tháp. Bản thân "nhà máy" có thể là một dịch vụ toàn cục, nhưng dữ liệu để tạo ra một kẻ địch cụ thể (ví dụ: EnemyData) sẽ được truyền vào lúc chạy.
*/