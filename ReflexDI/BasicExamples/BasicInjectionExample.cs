using UnityEngine;
using Reflex.Core;
using Reflex.Attributes;
using System.Collections.Generic;

namespace Reflex.Examples.BasicInjection
{
    /// <summary>
    /// VÍ DỤ 1: INJECTION CƠ BẢN
    /// 
    /// Hướng dẫn cài đặt:
    /// 1. Tạo một Prefab rỗng, đặt tên là "ProjectScope" và thêm component `Reflex.Core.ProjectScope`.
    /// 2. Thêm component `ProjectInstallerExample` này vào Prefab "ProjectScope".
    /// 3. Trong file `ReflexSettings` (tại Assets/Resources), kéo Prefab "ProjectScope" vào danh sách Project Scopes.
    /// 4. Trong một Scene, tạo một GameObject rỗng, đặt tên là "SceneScope" và thêm component `Reflex.Core.SceneScope`.
    /// 5. Thêm component `SceneInstallerExample` này vào GameObject "SceneScope".
    /// 6. Tạo một GameObject khác trong Scene và thêm component `Greeter` vào đó.
    /// 7. Chạy Scene và xem Console.
    /// 
    /// Kết quả mong đợi: Console sẽ in ra "Hello from Project and Scene!"
    /// </summary>

    // --- CÁC LỚP CÀI ĐẶT (INSTALLERS) ---

    // Installer cho toàn bộ dự án, chạy một lần duy nhất
    public class ProjectInstallerExample : MonoBehaviour, IInstaller
    {
        public void InstallBindings(ContainerBuilder builder)
        {
            // Bind một chuỗi string vào project container.
            // Bất kỳ scene nào cũng có thể truy cập được.
            builder.AddSingleton("Hello from Project");
            Debug.Log("[Reflex Example] ProjectInstallerExample configured.");
        }
    }

    // Installer cho một scene cụ thể
    public class SceneInstallerExample : MonoBehaviour, IInstaller
    {
        public void InstallBindings(ContainerBuilder builder)
        {
            // Bind một chuỗi string khác vào scene container.
            // Chỉ scene này mới truy cập được.
            builder.AddSingleton("and Scene!");
            Debug.Log("[Reflex Example] SceneInstallerExample configured.");
        }
    }

    // --- LỚP SỬ DỤNG DEPENDENCY ---

    public class Greeter : MonoBehaviour
    {
        // Reflex sẽ tự động inject tất cả các instance của 'string'
        // tìm thấy trong container của scene này và container cha (project).
        [Inject]
        private readonly IEnumerable<string> _messages;

        private void Start()
        {
            // Nối tất cả các chuỗi được inject và in ra.
            Debug.Log(string.Join(" ", _messages));
        }
    }
}
