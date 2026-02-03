using System;
using Reflex.Core;
using UnityEngine;

public class ProjectInstaller : MonoBehaviour, IInstaller
{
    public void InstallBindings(ContainerBuilder builder)
    {
        builder.AddSingleton("Hello");
        builder.AddSingleton(new Logger(), typeof(ILogger)); // Đăng ký một instance có sẵn dưới dạng ILogger singleton.
        builder.AddSingleton(typeof(Logger), typeof(ILogger)); // Khởi tạo logger một cách lười biếng (lazily), ràng buộc với ILogger.
        builder.AddSingleton(typeof(Logger), typeof(Logger), typeof(ILogger)); // Khởi tạo Logger, ràng buộc với cả Logger và ILogger singleton.
        builder.AddSingleton<Logger>(c => new Logger(), typeof(ILogger)); // Khởi tạo thông qua một factory, ràng buộc với ILogger singleton.
        
        // builder.AddTransient(typeof(Logger), typeof(ILogger)); // A new logger every time
        // builder.AddTransient(new Logger(), typeof(ILogger)); // INVALID: AddTransient only accept typeof Type
        
        // builder.AddScoped(typeof(Logger), typeof(ILogger)); // One logger per scene
    }
}


public interface ILogger
{
    void Log(string message);
}

public class Logger : ILogger
{
    private readonly Guid id = Guid.NewGuid();

    public void Log(string message)
    {
        Debug.Log($"Logger: {id} {message}");
    }
}

/*
GIẢI THÍCH LOG OUTPUT TỪ ReflexGreeter.cs:

Logger: 4a43830b-d3c0-4ec8-a955-5c3273a4e8d0 Hello World Beautiful
Logger: 2986cab5-46fb-4768-bb1b-7fda94a24a39 From IEnumerable<ILogger> binding #1
Logger: 90e5b530-0ce7-4cba-8b6c-2bfe96fe008e From IEnumerable<ILogger> binding #2
Logger: 1422ce23-820f-4781-b77c-38a0b8583d17 From IEnumerable<ILogger> binding #3
Logger: 4a43830b-d3c0-4ec8-a955-5c3273a4e8d0 From IEnumerable<ILogger> binding #4


Lớp `ReflexGreeter` inject (tiêm) hai thứ:
1. `ILogger logger`: Một instance duy nhất của một logger.
2. `IEnumerable<ILogger> allLoggers`: Một tập hợp của TẤT CẢ các logger đã được đăng ký.

Khi nhiều triển khai được đăng ký cho cùng một interface (ở đây là ILogger), một injection trực tiếp (`ILogger logger`)
sẽ nhận được *cái được đăng ký cuối cùng*. Injection `IEnumerable<ILogger>` sẽ nhận được *tất cả* chúng, theo thứ tự đã được đăng ký.

Log output đã chứng minh điều này:

- LOG: "Logger: 4a43830b-d3c0-4ec8-a955-5c3273a4e8d0 Hello World Beautiful"
  - Dòng này đến từ `[Inject] ILogger logger;`. ID `4a43...` thuộc về logger được tạo bởi binding thứ 4 và cũng là cuối cùng.

- LOG: "Logger: 2986cab5-46fb-4768-bb1b-7fda94a24a39 From IEnumerable<ILogger> binding #1"
  - Nguồn: `builder.AddSingleton(new Logger(), typeof(ILogger));`
  - Cách hoạt động: Dòng này tạo ra một instance `Logger` ngay lập tức (`new Logger()`) và đăng ký đối tượng cụ thể đó làm singleton cho `ILogger`.

- LOG: "Logger: 90e5b530-0ce7-4cba-8b6c-2bfe96fe008e From IEnumerable<ILogger> binding #2"
  - Nguồn: `builder.AddSingleton(typeof(Logger), typeof(ILogger));`
  - Cách hoạt động: Dòng này yêu cầu container tạo một instance `Logger` một cách *lười biếng* (chỉ khi cần đến lần đầu tiên) và sử dụng nó làm singleton cho `ILogger`. Kết quả là một instance logger mới, riêng biệt.

- LOG: "Logger: 1422ce23-820f-4781-b77c-38a0b8583d17 From IEnumerable<ILogger> binding #3"
  - Nguồn: `builder.AddSingleton(typeof(Logger), typeof(Logger), typeof(ILogger));`
  - Cách hoạt động: Tương tự như trên, nhưng dòng này ràng buộc một instance `Logger` được tạo lười biếng duy nhất cho *cả hai* kiểu `Logger` và `ILogger`. Nó vẫn tạo ra một instance logger mới, riêng biệt.

- LOG: "Logger: 4a43830b-d3c0-4ec8-a955-5c3273a4e8d0 From IEnumerable<ILogger> binding #4"
  - Nguồn: `builder.AddSingleton<Logger>(c => new Logger(), typeof(ILogger));`
  - Cách hoạt động: Dòng này sử dụng một hàm factory `c => new Logger()` để tạo instance logger một cách lười biếng. Đây là logger thứ 4 và cuối cùng trong `IEnumerable`.
  - ID của nó (`4a43...`) khớp với thông điệp log đầu tiên bởi vì, với tư cách là `ILogger` được đăng ký cuối cùng, nó là cái được cung cấp cho trường `[Inject] ILogger logger;` trực tiếp.

---
SO SÁNH CÁC VÒNG ĐỜI (LIFETIME): SINGLETON, SCOPED, VÀ TRANSIENT

*   `AddSingleton` (Đơn thể):
    *   **Là gì:** Chỉ tạo MỘT instance duy nhất trong suốt vòng đời của ứng dụng. Mọi đối tượng yêu cầu dependency này đều nhận được cùng một instance.
    *   **Trường hợp sử dụng:** Các dịch vụ toàn cục, các lớp quản lý, đối tượng cấu hình cần được truy cập phổ biến và giữ trạng thái nhất quán (ví dụ: `GameSettings`, `SaveManager`).

*   `AddScoped` (Theo phạm vi):
    *   **Là gì:** Tạo một instance cho mỗi "scope". Trong thiết lập Unity điển hình của Reflex, một scope tương đương với một **Scene**. Instance này là một singleton *bên trong Scene đó*. Nếu bạn tải một Scene mới với một container mới, một instance mới sẽ được tạo.
    *   **Trường hợp sử dụng:** Các dịch vụ dành riêng cho Scene, như `LevelManager`, `UIManager` cho một màn chơi cụ thể, hoặc bất cứ thứ gì cần được reset khi Scene thay đổi.
    *   Ví dụ: `builder.AddScoped(typeof(Logger), typeof(ILogger));`

*   `AddTransient` (Tạm thời):
    *   **Là gì:** Tạo một instance hoàn toàn mới **mỗi khi** nó được inject. Nếu hai lớp khác nhau cùng yêu cầu một dependency, chúng sẽ nhận được hai instance riêng biệt.
    *   **Trường hợp sử dụng:** Các dịch vụ nhẹ, không có trạng thái (stateless) hoặc các đối tượng mà bạn cần đảm bảo không có trạng thái chia sẻ giữa các consumer (ví dụ: các lớp tính toán, xác thực, chuyển đổi dữ liệu).
    *   Lưu ý: Bạn không thể đăng ký một instance đã được tạo sẵn với `AddTransient`, vì mục đích của nó là tạo mới theo yêu cầu.
    *   Ví dụ: `builder.AddTransient(typeof(Logger), typeof(ILogger));`
*/
