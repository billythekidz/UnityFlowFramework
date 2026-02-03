Dưới đây là file `README.md` tổng hợp lại toàn bộ kiến thức và các vấn đề chúng ta đã trao đổi về VContainer, được trình bày dưới dạng tài liệu tham khảo (Cheatsheet) để bạn dễ dàng tra cứu.

* * * * *

Markdown

```
# VContainer Unity - Tóm Tắt & Best Practices

Tài liệu tổng hợp các kỹ thuật sử dụng VContainer trong Unity, từ cơ bản đến nâng cao.

## 1. Setup Cơ Bản (Hello World)

Quy trình chuẩn để thiết lập VContainer:

1.  **Service:** Class logic thuần (C#), không kế thừa MonoBehaviour.
2.  **LifetimeScope:** "Bộ não" cấu hình dependency. Kế thừa `LifetimeScope`.
3.  **Entry Point:** Class điều khiển luồng game (thay thế Start/Update của Unity).

### Code mẫu `GameLifetimeScope.cs`
```csharp
public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // 1. Đăng ký Service logic
        builder.Register<HelloWorldService>(Lifetime.Singleton);

        // 2. Đăng ký Entry Point (Tự động chạy Start/Tick)
        builder.RegisterEntryPoint<GamePresenter>();
    }
}

```

* * * * *

2\. Các phương pháp Inject
--------------------------

### A. Method Injection (Khuyên dùng - Best Practice)

Sử dụng hàm `Construct` để nhận dependency.

-   **Ưu điểm:** Rõ ràng, dễ test, đảm bảo tính đóng gói (encapsulation).

-   **Yêu cầu:** Phải `Instantiate` qua Container.

C#

```
public class Player : MonoBehaviour
{
    private EnemyManager _enemyManager;

    [Inject]
    public void Construct(EnemyManager enemyManager)
    {
        this._enemyManager = enemyManager;
    }
}

```

### B. Field/Property Injection (Dùng cho nhanh)

Gắn attribute `[Inject]` trực tiếp lên biến.

-   **Ưu điểm:** Code ngắn gọn, không cần viết hàm.

-   **Nhược điểm:** Khó unit test, ẩn dependency.

-   **Lưu ý:** Biến `private` vẫn inject được.

C#

```
public class Player : MonoBehaviour
{
    [Inject] public EnemyManager enemyManager; // Public
    [Inject] private GameSetting gameSetting;  // Private
}

```

* * * * *

3\. Tạo Object (Instantiate)
----------------------------

⚠️ **QUAN TRỌNG:** Tuyệt đối **KHÔNG** dùng `Object.Instantiate()` của Unity nếu muốn Inject hoạt động.

### Cách đúng:

C#

```
public class Spawner : MonoBehaviour
{
    [Inject] IObjectResolver container; // Inject Container vào
    public GameObject prefab;

    void Spawn()
    {
        // Container sẽ tạo object -> Tìm [Inject] -> Điền dữ liệu
        var instance = container.Instantiate(prefab);
    }
}

```

*Nếu bắt buộc dùng `Object.Instantiate` (do thư viện khác), phải gọi `container.Inject(instance)` thủ công ngay sau đó.*

* * * * *

4\. Đăng ký MonoBehaviour (Singleton/Manager)
---------------------------------------------

Tùy vào vị trí của Component mà dùng lệnh đăng ký khác nhau:

| **Trường hợp** | **Lệnh sử dụng** |
| --- | --- |
| **Component đã có trong Scene** | `builder.RegisterComponent(instance);` |
| **Component nằm trong Prefab** | `builder.RegisterComponentInNewPrefab(prefab, Lifetime.Singleton);` |
| **Tạo GameObject rỗng mới** | `builder.RegisterComponentOnNewGameObject<T>(Lifetime.Singleton, "Name");` |

* * * * *

5\. Quản lý Scope (Vòng đời)
----------------------------

### Hierarchy

-   **Project Scope (Root):** Sống toàn game (`DontDestroyOnLoad`). Chứa các Global Service (Audio, UserData).

-   **Scene Scope (Child):** Sống theo màn chơi. Chứa logic gameplay (Enemy, Map).

    -   *Nguyên tắc:* Scene Scope nhìn thấy Project Scope. Project Scope **không** nhìn thấy Scene Scope.

### `Lifetime.Scoped`

-   Nếu đăng ký ở **Scene Scope**: Object đó là Singleton **trong màn chơi đó**. Reload scene -> Tạo object mới.

-   Nếu đăng ký ở **Project Scope**: Object đó là Singleton toàn cục (gần như `Lifetime.Singleton`).

* * * * *

6\. Lazy Loading & Factory (Func)
---------------------------------

Dùng khi muốn trì hoãn việc tạo object nặng hoặc cần tạo nhiều instance mới.

### Setup trong LifetimeScope

C#

```
// Đăng ký Service gốc
builder.Register<HeavyService>(Lifetime.Scoped);

// BẮT BUỘC: Đăng ký Factory cho Func hoặc Lazy
builder.RegisterFactory<HeavyService>(c => c.Resolve<HeavyService>());

```

### Sử dụng

C#

```
public class Consumer : MonoBehaviour
{
    // 1. Lazy Loading: Chỉ tạo khi gọi .Value (Chỉ 1 lần)
    [Inject] Lazy<HeavyService> lazyService;

    // 2. Factory: Mỗi lần gọi Invoke() là 1 lần Resolve
    [Inject] Func<HeavyService> serviceFactory;

    void Start() {
        var s = lazyService.Value; // Lúc này mới khởi tạo HeavyService
    }
}

```

*Mẹo:* Để mỗi Scene chỉ tạo 1 instance (Singleton per Scene), hãy đăng ký service là `Lifetime.Scoped` trong `SceneLifetimeScope` và dùng `Func` để lấy nó.

* * * * *

7\. Giải quyết vấn đề: Singleton gọi Scene Object
-------------------------------------------------

**Vấn đề:** Global Manager (Project Scope) cần điều khiển Player (Scene Scope). Inject trực tiếp sẽ lỗi hoặc null reference khi đổi scene.

**Giải pháp:** **Registry Pattern (Báo danh)**.

1.  **Global Manager:** Có hàm `Register(player)` và `Unregister()`.

2.  **Player (Scene):**

    -   `Start()`: Gọi `manager.Register(this)`.

    -   `OnDestroy()`: Gọi `manager.Unregister()`.

* * * * *

8\. Manual Resolve (Cần hạn chế)
--------------------------------

Dùng khi đứng ở code cũ hoặc nơi không thể Inject.

### Cách 1: Inject `IObjectResolver`

C#

```
var service = container.Resolve<MyService>();
// hoặc an toàn hơn
if (container.TryResolve<MyService>(out var s)) { ... }

```

### Cách 2: Static Access (Service Locator)

Tự tạo biến static trong `GameLifetimeScope`.

C#

```
// Trong GameLifetimeScope.cs
public static GameLifetimeScope Instance { get; private set; }
public IObjectResolver Container => base.Container;

// Nơi sử dụng:
var sv = GameLifetimeScope.Instance.Container.Resolve<MyService>();
```