using UnityEngine;
using Reflex.Core;
using Reflex.Attributes;
using System.Collections.Generic;
using System.Linq;

namespace Reflex.Examples.ConstructorAndEnumerable
{
    /// <summary>
    /// VÍ DỤ 2: INJECT VÀO CONSTRUCTOR VÀ DANH SÁCH (IENUMERABLE)
    /// 
    /// Hướng dẫn cài đặt:
    /// 1. Trong một Scene, tạo một GameObject rỗng, đặt tên là "SceneScope" và thêm component `Reflex.Core.SceneScope`.
    /// 2. Thêm component `ServiceInstaller` này vào GameObject "SceneScope".
    /// 3. Tạo một GameObject khác trong Scene và thêm component `ServiceManager` vào đó.
    /// 4. Chạy Scene và xem Console.
    /// 
    /// Kết quả mong đợi: Console sẽ in ra:
    /// "Service Manager started."
    /// "All services initialized: AnalyticsService, AudioService"
    /// </summary>

    // --- INTERFACES VÀ CÁC LỚP DỊCH VỤ ---

    public interface IService
    {
        void Initialize();
    }

    public class AnalyticsService : IService
    {
        public void Initialize() => Debug.Log("AnalyticsService Initialized.");
    }

    public class AudioService : IService
    {
        public void Initialize() => Debug.Log("AudioService Initialized.");
    }

    // --- LỚP CÀI ĐẶT (INSTALLER) ---

    public class ServiceInstaller : MonoBehaviour, IInstaller
    {
        public void InstallBindings(ContainerBuilder builder)
        {
            // Bind nhiều lớp triển khai cùng một interface.
            // AddSingleton đảm bảo chỉ có một instance của mỗi dịch vụ được tạo.
            builder.AddSingleton(typeof(AnalyticsService), typeof(IService));
            builder.AddSingleton(typeof(AudioService), typeof(IService));

            // Bind lớp `ServiceOrchestrator` sẽ được tạo bởi container.
            // Reflex sẽ tự động tìm các dependency (IEnumerable<IService>) trong constructor của nó.
            builder.AddSingleton(typeof(ServiceOrchestrator));
        }
    }

    // --- CÁC LỚP SỬ DỤNG DEPENDENCY ---

    // Lớp này là một POCO (Plain Old C# Object), không phải MonoBehaviour.
    // Reflex sẽ tạo nó và inject các dependency qua constructor.
    public class ServiceOrchestrator
    {
        private readonly IEnumerable<IService> _services;

        // Constructor Injection: Reflex sẽ cung cấp tất cả các IService đã được bind.
        public ServiceOrchestrator(IEnumerable<IService> services)
        {
            _services = services;
        }

        public void StartAllServices()
        {
            Debug.Log($"All services to initialize: {string.Join(", ", _services.Select(s => s.GetType().Name))}");
            foreach (var service in _services)
            {
                service.Initialize();
            }
        }
    }

    // Lớp MonoBehaviour này nhận ServiceOrchestrator đã được inject.
    public class ServiceManager : MonoBehaviour
    {
        [Inject]
        private readonly ServiceOrchestrator _orchestrator;

        private void Start()
        {
            Debug.Log("Service Manager started.");
            _orchestrator.StartAllServices();
        }
    }
}
