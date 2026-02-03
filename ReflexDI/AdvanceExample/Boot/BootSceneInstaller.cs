using UnityEngine;
using Reflex.Core;
using System;
using TowerDefence.Reflex.Project.Configuration; // Namespace mới
using TowerDefence.Reflex.Project.Core;         // Namespace mới
using TowerDefence.Reflex.Project.Services;     // Namespace mới
using TowerDefence.Reflex.Project.Services.SaveLoad; // Namespace mới
using TowerDefence.Reflex.Project.Services.SaveLoad.Implementations; // Namespace mới

namespace TowerDefence.Reflex.Boot
{
    /// <summary>
    /// Installer cho BootScene.
    /// Chịu trách nhiệm khởi tạo tất cả các dịch vụ cốt lõi.
    /// </summary>
    public class BootSceneInstaller : MonoBehaviour, IInstaller
    {
        [Header("Game Configuration")]
        [Tooltip("Kéo ScriptableObject của chế độ chơi vào đây.")]
        [SerializeField] private GameModeSettingsSO _selectedGameMode;

        public void InstallBindings(ContainerBuilder builder)
        {
            if (_selectedGameMode == null)
            {
                Debug.LogError("[BootSceneInstaller] Chưa chọn chế độ chơi!", this);
                return;
            }
            
            Debug.Log($"[BootSceneInstaller] Binding services for '{_selectedGameMode.ModeName}' mode.");

            // 1. Bind các dịch vụ cơ bản và các triển khai cụ thể.
            //    Chúng sẽ được inject vào Proxy.
            builder.AddSingleton(_selectedGameMode);
            builder.AddSingleton(typeof(NetworkStatusService));
            builder.AddSingleton(typeof(PlayerPrefsSaveLoadService));
            builder.AddSingleton(typeof(FirebaseSaveLoadService));

            // 2. Bind ProxySaveLoadService dưới "danh nghĩa" của interface ISaveLoadService.
            //    Reflex sẽ tự động tìm các dependency (NetworkStatusService, PlayerPrefsSaveLoadService, ...)
            //    mà Proxy cần từ các binding ở trên.
            builder.AddSingleton(typeof(ProxySaveLoadService), typeof(ISaveLoadService));
            Debug.Log("[BootSceneInstaller] ProxySaveLoadService has been bound as ISaveLoadService.");

            // 3. Bind GameManager.
            //    GameManager không thay đổi. Nó vẫn chỉ yêu cầu ISaveLoadService.
            //    Reflex sẽ cung cấp cho nó instance của ProxySaveLoadService.
            builder.AddSingleton(typeof(GameManager));
            
        }
    }
}

/*
 * --- GIẢI THÍCH CHI TIẾT VỀ CÁCH REFLEX HOẠT ĐỘNG TRONG FILE NÀY ---
 * 
 * 1. Mấu chốt của kiến trúc Proxy: Bind Lớp cụ thể vs. Bind Interface
 * 
 *    Câu hỏi đặt ra: Tại sao `GameManager` lại không biết đến sự tồn tại của 
 *    `PlayerPrefsSaveLoadService` hay `FirebaseSaveLoadService`?
 * 
 *    - `GameManager` nhận được `ProxySaveLoadService` không phải vì nó là "binding cuối cùng",
 *      mà vì nó là **binding duy nhất** cho interface `ISaveLoadService`.
 *      Khi constructor của `GameManager` yêu cầu một `ISaveLoadService`, Reflex quét container và
 *      chỉ tìm thấy một "công thức" duy nhất cho interface đó: "Hãy tạo một `ProxySaveLoadService`".
 * 
 *    - `ProxySaveLoadService` có thể sử dụng các dịch vụ khác (như `PlayerPrefsSaveLoadService`)
 *      vì chúng được bind vào container dưới dạng các **lớp cụ thể (concrete types)**.
 *      Khi constructor của Proxy yêu cầu một `PlayerPrefsSaveLoadService` (chứ không phải `ISaveLoadService`),
 *      Reflex sẽ tìm chính xác "công thức" cho lớp cụ thể đó.
 * 
 *    => Đây là cách chúng ta có nhiều triển khai cùng tồn tại trong container, nhưng chỉ định một
 *       triển khai duy nhất (Proxy) làm "đại diện" chính thức cho một interface, che giấu hoàn toàn
 *       sự phức tạp khỏi các lớp logic game như `GameManager`.
 * 
 * 2. Kịch bản giả định: Điều gì xảy ra nếu chúng ta bind tất cả vào cùng một interface?
 * 
 *    Đây là một pattern nâng cao hơn gọi là `IEnumerable` Injection.
 *    Giả sử chúng ta thay đổi Installer như sau:
 * 
 *    // builder.AddSingleton(typeof(PlayerPrefsSaveLoadService)); // Xóa binding lớp cụ thể
 *    // builder.AddSingleton(typeof(FirebaseSaveLoadService));  // Xóa binding lớp cụ thể
 *    builder.AddSingleton(typeof(PlayerPrefsSaveLoadService), typeof(ISaveLoadService)); // Bind vào interface
 *    builder.AddSingleton(typeof(FirebaseSaveLoadService), typeof(ISaveLoadService));  // Bind vào interface
 *    builder.AddSingleton(typeof(ProxySaveLoadService), typeof(ISaveLoadService));    // Bind vào interface
 * 
 *    Và sửa constructor của `ProxySaveLoadService` để yêu cầu một danh sách:
 *    `public ProxySaveLoadService(NetworkStatusService network, IEnumerable<ISaveLoadService> services)`
 * 
 *    - **Hành vi của Reflex**: Khi Reflex thấy yêu cầu `IEnumerable<ISaveLoadService>`, nó sẽ không báo lỗi
 *      mơ hồ. Thay vào đó, nó sẽ thu thập TẤT CẢ các binding đã đăng ký cho `ISaveLoadService`
 *      (bao gồm cả PlayerPrefs, Firebase, và chính Proxy) và "tiêm" chúng vào dưới dạng một danh sách.
 *    - **Xử lý trong Proxy**: `Proxy` sẽ nhận được danh sách này và phải tự lọc ra các dịch vụ
 *      cụ thể mà nó cần (ví dụ: dùng Linq `.OfType<T>()`), đồng thời phải cẩn thận loại bỏ chính nó ra
 *      khỏi danh sách để tránh đệ quy vô hạn.
 *    - **Kết luận**: Đây là một kỹ thuật rất mạnh mẽ để tạo ra các hệ thống linh hoạt và có thể mở rộng
 *      (ví dụ: hệ thống plugin), nhưng nó phức tạp hơn và đòi hỏi xử lý cẩn thận bên trong lớp nhận dependency.
 *      Cách làm hiện tại của chúng ta (bind lớp cụ thể) đơn giản và rõ ràng hơn cho ví dụ này.
 * 
 * 3. Khái niệm "Khởi tạo lười biếng" (Lazy Instantiation)
 * 
 *    Khi bạn gọi `builder.AddSingleton(...)`, bạn KHÔNG hề tạo ra một đối tượng nào cả.
 *    Bạn chỉ đang đăng ký một "công thức" vào trong container. Các đối tượng chỉ thực sự được tạo ra
 *    (instantiated) khi chúng được yêu cầu lần đầu tiên.
 * 
 * 4. Hành trình của Reflex khi tạo GameManager (Quá trình "Truy vết ngược")
 * 
 *    Giả sử `GameplayUI` lần đầu tiên yêu cầu `GameManager`. Reflex sẽ:
 *    1. Tìm công thức cho `GameManager`, thấy cần `ISaveLoadService`.
 *    2. Tìm công thức cho `ISaveLoadService`, thấy cần tạo `ProxySaveLoadService`.
 *    3. Tìm công thức cho `ProxySaveLoadService`, thấy cần `NetworkStatusService`, `PlayerPrefsSaveLoadService`, v.v.
 *    4. Tìm công thức cho các lớp cụ thể này và TẠO MỚI chúng.
 *    5. Dùng các dependency vừa tạo để TẠO MỚI `ProxySaveLoadService`.
 *    6. Dùng `ProxySaveLoadService` vừa tạo để TẠO MỚI `GameManager`.
 *    7. Trả `GameManager` về cho `GameplayUI`.
 */
