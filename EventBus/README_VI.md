# Framework EventBus

Một hệ thống event bus (kênh sự kiện) toàn cục được thiết kế cho các dự án Unity, cung cấp một hệ thống nhắn tin tập trung hỗ trợ cả sự kiện từ server và sự kiện phía client.

Framework này giúp tách rời các thành phần khác nhau trong ứng dụng của bạn, giúp mã nguồn sạch hơn và dễ bảo trì hơn. Nó hỗ trợ hai chiến lược xử lý sự kiện chính:

1.  **Dựa trên Lệnh (Command-Based)**: Sử dụng các chuỗi hằng số (như trong file `Commands.cs`) để xác định sự kiện. Đây là phương pháp lý tưởng để xử lý các tin nhắn từ server, nơi các lệnh thường được gửi dưới dạng chuỗi.
2.  **Dựa trên Kiểu (Type-Based)**: Sử dụng chính kiểu dữ liệu (class/struct) của C# làm định danh sự kiện. Đây là lựa chọn hoàn hảo cho các sự kiện nội bộ phía client, đảm bảo an toàn kiểu (type safety) và giảm sự phụ thuộc vào các "chuỗi ma thuật" (magic strings).

## Tính năng

-   **Thiết kế Hệ thống Kép**: Đăng ký sự kiện bằng lệnh (chuỗi) hoặc bằng kiểu C#.
-   **Hỗ trợ Payload**: Gửi sự kiện kèm hoặc không kèm theo dữ liệu (payload).
-   **An toàn Bộ nhớ**: Tự động dọn dẹp các đăng ký lắng nghe sự kiện của các đối tượng `UnityEngine.Object` đã bị hủy.
-   **Quản lý Vòng đời**: Triển khai giao diện `IDisposable` để xóa tất cả các đăng ký, rất quan trọng để quản lý các event bus theo từng scene.
-   **Thân thiện với IoC**: Được thiết kế để tích hợp với các bộ chứa IoC (Inversion of Control) như `Reflex`.

## Tích hợp với Reflex

Để `EventBus` có thể được sử dụng trong toàn bộ ứng dụng của bạn, bạn nên đăng ký nó dưới dạng singleton trong bộ chứa IoC Reflex. Điều này đảm bảo rằng cùng một thể hiện (instance) được chia sẻ ở mọi nơi, tạo ra một kênh giao tiếp thực sự toàn cục.

**Ví dụ: Trong file installer chính của dự án**
```csharp
// Trong file ProjectInstaller.cs hoặc một file cài đặt tương tự

public class ProjectInstaller : Installer
{
    public override void InstallBindings()
    {
        // Đăng ký EventBus dưới dạng một singleton instance
        Container.Bind<IEventBus>().To<EventBus>().AsSingleton();
    }
}
```

## Hướng dẫn sử dụng

Sau khi đăng ký `IEventBus`, bạn có thể inject nó vào bất kỳ lớp nào cần gửi hoặc nhận sự kiện.

```csharp
public class MyService
{
    private readonly IEventBus _eventBus;

    // Inject event bus qua constructor
    public MyService(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }
}
```

### Đăng ký lắng nghe sự kiện (Subscribing)

**1. Dựa trên Lệnh (Command-Based) - Sự kiện từ Server**

Lắng nghe các sự kiện bằng cách sử dụng các lệnh chuỗi từ file `Commands.cs`.

```csharp
// Ví dụ: Lắng nghe sự kiện người chơi khác tham gia ván chơi
// Server gửi lệnh "OtherPlayerJoinGame" với dữ liệu người chơi.

public class GameManager
{
    private readonly IEventBus _eventBus;

    public GameManager(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.Subscribe<PlayerJoinData>(Commands.BOARD_PLAYER_JOIN, OnPlayerJoined);
    }

    private void OnPlayerJoined(PlayerJoinData data)
    {
        Debug.Log($"Người chơi {data.PlayerName} đã tham gia ván chơi!");
        // Thêm người chơi vào scene...
    }

    // Luôn hủy đăng ký để tránh rò rỉ bộ nhớ
    public void Dispose()
    {
        _eventBus.Unsubscribe<PlayerJoinData>(Commands.BOARD_PLAYER_JOIN, OnPlayerJoined);
    }
}
```

**2. Dựa trên Kiểu (Type-Based) - Sự kiện nội bộ Client**

Lắng nghe sự kiện dựa trên kiểu dữ liệu của chúng. Đây là phương pháp được ưu tiên cho các sự kiện nội bộ trong ứng dụng.

```csharp
// Định nghĩa một class hoặc struct đơn giản cho sự kiện của bạn
public class PlayerProfileOpened
{
    public string PlayerId;
}

public class UIManager
{
    private readonly IEventBus _eventBus;

    public UIManager(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.Subscribe<PlayerProfileOpened>(OnProfileOpened);
    }

    private void OnProfileOpened(PlayerProfileOpened evt)
    {
        Debug.Log($"Mở hồ sơ cho người chơi: {evt.PlayerId}");
        // Hiển thị giao diện hồ sơ người chơi...
    }
    
    public void Dispose()
    {
        _eventBus.Unsubscribe<PlayerProfileOpened>(OnProfileOpened);
    }
}
```

### Gửi sự kiện (Publishing)

**1. Dựa trên Lệnh (Command-Based)**

Thông thường, một lớp trung tâm (ví dụ: `ServerMessageHandler`) sẽ nhận các tin nhắn thô từ server, phân tích chúng và gửi chúng lên `EventBus`.

```csharp
public class ServerMessageHandler
{
    private readonly IEventBus _eventBus;

    public ServerMessageHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public void HandleRawMessage(string command, string jsonPayload)
    {
        // Ví dụ cho sự kiện người chơi tham gia
        if (command == Commands.BOARD_PLAYER_JOIN)
        {
            var data = JsonUtility.FromJson<PlayerJoinData>(jsonPayload);
            _eventBus.Publish(Commands.BOARD_PLAYER_JOIN, data);
        }
        // Ví dụ cho một lệnh không có payload
        else if (command == Commands.BOARD_HAND_START)
        {
            _eventBus.Publish(Commands.BOARD_HAND_START);
        }
    }
}
```

**2. Dựa trên Kiểu (Type-Based)**

Bất kỳ thành phần nào trong mã client của bạn đều có thể gửi một sự kiện dựa trên kiểu.

```csharp
public class PlayerAvatar : MonoBehaviour
{
    [Inject] private readonly IEventBus _eventBus;
    public string PlayerId;

    public void OnAvatarClicked()
    {
        // Gửi một sự kiện để các hệ thống khác xử lý
        _eventBus.Publish(new PlayerProfileOpened { PlayerId = this.PlayerId });
    }
}
```

## Chuyển đổi từ hệ thống `Signal`

`EventBus` là một sự thay thế trực tiếp cho các mẫu thiết kế cũ như `Signal` hoặc `addHandler`. Việc chuyển đổi rất đơn giản.

### Trước: `Signal.addHandler`

Trước đây, bạn có thể đã đăng ký các handler như thế này:

```csharp
// Hệ thống cũ sử dụng một dispatcher Signal giả định
public class OldGameManager
{
    void Start()
    {
        // Giả sử 'gameSignal' là một instance signal toàn cục hoặc có thể truy cập được
        gameSignal.addHandler(Commands.BOARD_PLAYER_JOIN, OnPlayerJoined);
    }

    void OnDestroy()
    {
        gameSignal.removeHandler(Commands.BOARD_PLAYER_JOIN, OnPlayerJoined);
    }

    // Handler có thể nhận một đối tượng object chung hoặc một kiểu cụ thể
    private void OnPlayerJoined(object payload)
    {
        var data = (PlayerJoinData)payload;
        // ...
    }
}
```

### Sau: `IEventBus.Subscribe`

Với `EventBus` mới, đoạn mã tương đương sẽ sạch sẽ và an toàn về kiểu hơn.

```csharp
// Hệ thống mới sử dụng IEventBus được inject
public class NewGameManager : IDisposable
{
    private readonly IEventBus _eventBus;

    // Nhận bus qua constructor injection
    public NewGameManager(IEventBus eventBus)
    {
        _eventBus = eventBus;
        
        // Đăng ký với một payload cụ thể, được định kiểu rõ ràng
        _eventBus.Subscribe<PlayerJoinData>(Commands.BOARD_PLAYER_JOIN, OnPlayerJoined);
    }

    // Handler bây giờ được định kiểu mạnh
    private void OnPlayerJoined(PlayerJoinData data)
    {
        Debug.Log($"Người chơi {data.PlayerName} với ID {data.PlayerId} đã tham gia.");
        // ...
    }
    
    // Hủy đăng ký trong phương thức Dispose (được gọi bởi Reflex hoặc thủ công)
    public void Dispose()
    {
        _eventBus.Unsubscribe<PlayerJoinData>(Commands.BOARD_PLAYER_JOIN, OnPlayerJoined);
    }
}
```
