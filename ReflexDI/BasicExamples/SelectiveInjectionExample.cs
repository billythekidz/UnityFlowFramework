using UnityEngine;
using Reflex.Core;
using Reflex.Attributes;

namespace Reflex.Examples.SelectiveInjection
{
    /// <summary>
    /// VÍ DỤ 3: INJECTION CÓ CHỌN LỌC BẰNG WRAPPER TYPES
    /// 
    /// Kỹ thuật này cho phép phân biệt giữa các dependency có cùng kiểu dữ liệu cơ bản (ví dụ: string).
    /// Thay vì dùng ID, chúng ta tạo các lớp "wrapper" để đảm bảo an toàn kiểu (type-safe).
    /// 
    /// Hướng dẫn cài đặt:
    /// 1. Trong một Scene, tạo một GameObject rỗng, đặt tên là "SceneScope" và thêm component `Reflex.Core.SceneScope`.
    /// 2. Thêm component `ConfigInstaller` này vào GameObject "SceneScope".
    /// 3. Tạo một GameObject khác trong Scene và thêm component `AppInfoDisplay` vào đó.
    /// 4. Chạy Scene và xem Console.
    /// 
    /// Kết quả mong đợi: Console sẽ in ra "App Name: My Awesome Game, Version: 1.0.2"
    /// </summary>

    // --- CÁC LỚP WRAPPER ---

    // Lớp cơ sở để tạo các kiểu "typed" từ một giá trị.
    public abstract class TypedInstance<T>
    {
        public T Value { get; }
        protected TypedInstance(T value) => Value = value;
        
        // Cho phép chuyển đổi ngầm định từ wrapper về kiểu giá trị gốc.
        public static implicit operator T(TypedInstance<T> typedInstance) => typedInstance.Value;
    }

    // Wrapper cho tên ứng dụng.
    public class AppName : TypedInstance<string>
    {
        public AppName(string value) : base(value) { }
    }

    // Wrapper cho phiên bản ứng dụng.
    public class AppVersion : TypedInstance<string>
    {
        public AppVersion(string value) : base(value) { }
    }

    // --- LỚP CÀI ĐẶT (INSTALLER) ---

    public class ConfigInstaller : MonoBehaviour, IInstaller
    {
        public void InstallBindings(ContainerBuilder builder)
        {
            // Bind các giá trị cấu hình bằng cách sử dụng các lớp wrapper.
            // Mặc dù cả hai đều là string, Reflex sẽ phân biệt chúng qua kiểu wrapper.
            builder.AddSingleton(new AppName("My Awesome Game"));
            builder.AddSingleton(new AppVersion("1.0.2"));

            // Bind lớp AppWindow sẽ được tạo bởi container.
            builder.AddSingleton(typeof(AppWindow));
        }
    }

    // --- CÁC LỚP SỬ DỤNG DEPENDENCY ---

    // Lớp POCO này yêu cầu các kiểu wrapper cụ thể trong constructor.
    public class AppWindow
    {
        private readonly string _appName;
        private readonly string _appVersion;

        // Constructor Injection: Yêu cầu AppName và AppVersion một cách tường minh.
        // Reflex sẽ biết chính xác cần inject wrapper nào.
        public AppWindow(AppName appName, AppVersion appVersion)
        {
            // Chúng ta có thể lấy giá trị string gốc thông qua chuyển đổi ngầm định.
            _appName = appName; 
            _appVersion = appVersion;
        }

        public string GetAppInfo()
        {
            return $"App Name: {_appName}, Version: {_appVersion}";
        }
    }

    // Lớp MonoBehaviour hiển thị thông tin.
    public class AppInfoDisplay : MonoBehaviour
    {
        [Inject]
        private readonly AppWindow _appWindow;

        private void Start()
        {
            Debug.Log(_appWindow.GetAppInfo());
        }
    }
}
