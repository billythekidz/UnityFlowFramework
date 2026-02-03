Báo cáo Chuyên sâu: Chiến lược Triển khai Kiến trúc MVVM Hiệu năng cao trong Unity với Hệ sinh thái Cysharp (VContainer, R3, UniTask, VitalRouter)
==================================================================================================================================================

1\. Sự Chuyển dịch Kiến trúc trong Phát triển Unity Hiện đại
------------------------------------------------------------

### 1.1. Từ MonoBehaviour đến Tư duy Hướng Dữ liệu

Trong lịch sử phát triển của Unity, mô hình lập trình truyền thống xoay quanh `MonoBehaviour` đã đóng vai trò nền tảng giúp hàng triệu nhà phát triển tiếp cận engine này. Tuy nhiên, khi quy mô dự án vượt ra khỏi các nguyên mẫu (prototype) đơn giản để trở thành các sản phẩm thương mại phức tạp, mô hình "God Class" -- nơi một script quản lý cả logic game, dữ liệu và hiển thị UI -- bắt đầu bộc lộ những điểm yếu chí mạng. Sự phụ thuộc chặt chẽ (tight coupling) giữa logic nghiệp vụ và engine Unity làm cho việc kiểm thử đơn vị (Unit Testing) trở nên bất khả thi, trong khi việc quản lý trạng thái (State Management) trở thành một cơn ác mộng khi dữ liệu bị phân tán khắp các Inspector.^1^

Sự xuất hiện của mẫu thiết kế MVVM (Model-View-ViewModel), vốn có nguồn gốc từ hệ sinh thái XAML/WPF của Microsoft, đã mang lại một làn gió mới cho việc phát triển giao diện người dùng (UI) trong game. MVVM không chỉ đơn thuần là việc tách file; nó là một sự thay đổi tư duy từ "hướng sự kiện" (Event-driven) sang "hướng dữ liệu" (Data-driven) và "hướng phản ứng" (Reactive). Trong Unity, việc áp dụng MVVM đòi hỏi một bộ công cụ (stack) đủ mạnh để xử lý các vấn đề đặc thù của Game Loop, quản lý bộ nhớ (GC allocation), và đa luồng (multi-threading).

### 1.2. Hệ sinh thái Cysharp: Tiêu chuẩn mới cho.NET trong Unity

Trong bối cảnh đó, "Cysharp Stack" -- tập hợp các thư viện mã nguồn mở do Yoshifumi Kawai (neuecc) và cộng đồng phát triển -- đã nổi lên như một giải pháp toàn diện, tối ưu hóa triệt để cho hiệu năng của Unity. Khác với các thư viện.NET tiêu chuẩn, các công cụ của Cysharp được thiết kế với tư duy "Zero Allocation" và tích hợp sâu vào vòng đời PlayerLoop của Unity.

Báo cáo này sẽ phân tích chi tiết việc kết hợp bốn trụ cột công nghệ để xây dựng kiến trúc MVVM:

1.  **VContainer:** Quản lý Dependency Injection (DI) với tốc độ resolve cực nhanh và chi phí bộ nhớ thấp.^3^

2.  **R3 (Reactive Extensions for C#):** Thế hệ thứ ba của thư viện Reactive, thay thế UniRx, giải quyết các vấn đề về quản lý thời gian và bộ nhớ.^5^

3.  **UniTask:** Giải pháp async/await hiệu năng cao, thay thế Coroutine và Task truyền thống.^3^

4.  **VitalRouter:** Hệ thống định tuyến lệnh (Command Routing) zero-allocation để phân tách giao tiếp giữa các module.^8^

* * * * *

2\. Nền tảng Kiến trúc: Dependency Injection với VContainer
-----------------------------------------------------------

### 2.1. Tại sao VContainer thay thế Zenject trong MVVM?

Trong kiến trúc MVVM, ViewModel cần phải là các lớp C# thuần túy (POCO - Plain Old CLR Objects) để đảm bảo tính độc lập và khả năng kiểm thử. Điều này đặt ra thách thức về việc ai sẽ khởi tạo ViewModel và cung cấp các dependency (như Model, Service) cho nó. Dependency Injection (DI) Container là câu trả lời.

Zenject (hay Extenject) từng là lựa chọn phổ biến, nhưng VContainer đã vượt trội hơn nhờ kiến trúc hiện đại và hiệu năng. VContainer sử dụng trình biên dịch IL (IL code generation) hoặc Reflection tối ưu để đạt tốc độ resolve nhanh gấp 5-10 lần Zenject, đồng thời loại bỏ hoàn toàn việc cấp phát bộ nhớ (GC) trong quá trình resolve đối tượng.^4^ Điều này cực kỳ quan trọng trong game, nơi việc sụt giảm khung hình (hiccup) do GC là không thể chấp nhận được.

### 2.2. Thiết lập Composition Root và LifetimeScope

Điểm khởi đầu của mọi ứng dụng sử dụng VContainer là `LifetimeScope`. Đây là nơi duy nhất trong ứng dụng được phép "biết" về cấu trúc của Container. Trong Unity, chúng ta thường thiết kế một hệ thống phân cấp Scope: `RootLifetimeScope` (cho toàn game) -> `SceneLifetimeScope` (cho từng màn chơi) -> `FeatureLifetimeScope` (cho từng chức năng cụ thể).

#### Code thực chiến: Cấu hình Root Scope

Dưới đây là ví dụ triển khai một `GameLifetimeScope` đóng vai trò là Global Scope, tồn tại xuyên suốt game (DontDestroyOnLoad):

C#

```
using VContainer;
using VContainer.Unity;
using UnityEngine;

public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Đăng ký Model: Dữ liệu người chơi, tồn tại duy nhất (Singleton)
        builder.Register<UserProfileModel>(Lifetime.Singleton);
        builder.Register<InventoryModel>(Lifetime.Singleton);

        // Đăng ký Service: Xử lý logic mạng, IO (Singleton)
        // Đăng ký Interface giúp dễ dàng Mock khi Unit Test
        builder.Register<IAuthenticationService, AuthenticationService>(Lifetime.Singleton);
        builder.Register<IAssetLoader, AddressablesAssetLoader>(Lifetime.Singleton);

        // EntryPoints: Các class logic không phải MonoBehaviour nhưng cần chạy theo vòng đời Unity
        builder.RegisterEntryPoint<GameInitializer>();
        builder.RegisterEntryPoint<NetworkTickManager>();
    }
}

```

### 2.3. EntryPoint: Thay thế Logic Khởi tạo trong MonoBehaviour

Một sai lầm phổ biến khi chuyển sang MVVM là vẫn giữ logic khởi tạo trong `Start()` của `MonoBehaviour`. VContainer cung cấp các interface `IStartable`, `IPostStartable`, `ITickable` để tách logic vòng đời ra khỏi `MonoBehaviour`.^3^

Đặc biệt, `IAsyncStartable` kết hợp với UniTask cho phép thực hiện chuỗi khởi tạo bất đồng bộ phức tạp (ví dụ: Login -> Load Config -> Load User Data) một cách tuần tự và rõ ràng trước khi game bắt đầu.

C#

```
using VContainer.Unity;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public class GameInitializer : IAsyncStartable
{
    private readonly IAuthenticationService _authService;
    private readonly UserProfileModel _userProfile;

    // Constructor Injection: VContainer tự động cung cấp dependency
    public GameInitializer(
        IAuthenticationService authService,
        UserProfileModel userProfile)
    {
        _authService = authService;
        _userProfile = userProfile;
    }

    public async UniTask StartAsync(CancellationToken cancellation)
    {
        Debug.Log("Game Initializing...");

        // Chuỗi khởi tạo tuần tự
        await _authService.InitializeAsync(cancellation);

        var loginResult = await _authService.LoginAnonymouslyAsync(cancellation);
        if (loginResult.IsSuccess)
        {
            await _userProfile.LoadDataAsync(loginResult.UserId, cancellation);
            Debug.Log("Game Ready!");
        }
        else
        {
            Debug.LogError("Login Failed!");
            // Xử lý lỗi hoặc hiển thị Popup Retry
        }
    }
}

```

### 2.4. Quản lý ViewModel với Scoped Lifetime

Trong MVVM, ViewModel thường gắn liền với vòng đời của View. Khi View (ví dụ: một cửa sổ Inventory) đóng lại, ViewModel tương ứng cũng nên được hủy để giải phóng bộ nhớ. VContainer hỗ trợ `Lifetime.Scoped` để giải quyết vấn đề này. Khi một `LifetimeScope` con (ví dụ: gắn trên Prefab của Inventory Window) được sinh ra, các ViewModel đăng ký `Scoped` bên trong nó sẽ được tạo mới và tự động `Dispose` khi Scope đó bị hủy.^9^

Bảng so sánh các loại Lifetime trong VContainer cho MVVM:

| **Lifetime** | **Mô tả** | **Sử dụng cho** |
| --- | --- | --- |
| **Singleton** | Một instance duy nhất cho toàn bộ Container cha. | Global Model, Network Service, Audio Manager. |
| **Scoped** | Một instance cho mỗi LifetimeScope. | ViewModel, Presenter của một màn hình cụ thể. |
| **Transient** | Tạo mới mỗi khi được yêu cầu. | Các item nhỏ trong list (ItemViewModel), Effect tạm thời. |

* * * * *

3\. Lõi Phản ứng: R3 và Sức mạnh của ReactiveProperty
-----------------------------------------------------

### 3.1. R3: Bước nhảy vọt từ UniRx

UniRx đã là tiêu chuẩn của Reactive Programming trong Unity suốt nhiều năm. Tuy nhiên, nó tồn tại những hạn chế về kiến trúc như việc sử dụng `IScheduler` gây khó khăn trong việc kiểm soát thời gian (Time) và frame (Frame count), cũng như vấn đề cấp phát bộ nhớ khi sử dụng các toán tử LINQ.

R3 (Reactive Extensions thế hệ 3) giải quyết triệt để các vấn đề này bằng cách tích hợp `TimeProvider` của.NET 8 và khái niệm `FrameProvider` dành riêng cho Unity.^6^ Điều này cho phép ViewModel kiểm soát chính xác logic thời gian (ví dụ: Debounce input, Delay animation) mà vẫn đảm bảo khả năng Testability (thông qua `FakeTimeProvider`).

### 3.2. ReactiveProperty: Cầu nối giữa Model và View

Trong kiến trúc MVVM, ViewModel không gọi trực tiếp View để cập nhật UI. Thay vào đó, ViewModel expose các thuộc tính đại diện cho trạng thái (State), và View sẽ "lắng nghe" (Observe) các thuộc tính này. `ReactiveProperty<T>` trong R3 chính là công cụ thực hiện việc này.

Một mô hình `ReactiveProperty` chuẩn trong R3 bao gồm:

1.  **ReactiveProperty (Mutable):** Dùng nội bộ trong ViewModel hoặc Model để thay đổi giá trị.

2.  **ReadOnlyReactiveProperty (Immutable):** Expose ra bên ngoài để View binding, đảm bảo tính đóng gói (Encapsulation).

#### Code thực chiến: ViewModel với R3

C#

```
using R3;
using System;

public class HealthViewModel : IDisposable
{
    // Backing field có thể thay đổi
    private readonly ReactiveProperty<float> _currentHp;
    private readonly ReactiveProperty<float> _maxHp;

    // Public properties chỉ đọc cho View
    public ReadOnlyReactiveProperty<float> CurrentHp => _currentHp;
    public ReadOnlyReactiveProperty<float> MaxHp => _maxHp;

    // Computed Property: Tự động tính toán khi HP hoặc MaxHP thay đổi
    public ReadOnlyReactiveProperty<float> HealthPercent { get; }

    public HealthViewModel(float initialHp, float maxHp)
    {
        _currentHp = new ReactiveProperty<float>(initialHp);
        _maxHp = new ReactiveProperty<float>(maxHp);

        // CombineLatest: Kết hợp giá trị mới nhất của 2 stream
        HealthPercent = _currentHp
           .CombineLatest(_maxHp, (current, max) => current / max)
           .ToReadOnlyReactiveProperty();
    }

    public void TakeDamage(float damage)
    {
        // Validate logic nghiệp vụ
        var newHp = Math.Max(0, _currentHp.Value - damage);
        _currentHp.Value = newHp;
    }

    public void Dispose()
    {
        // Hủy tất cả subscription và property khi ViewModel bị hủy
        _currentHp.Dispose();
        _maxHp.Dispose();
        HealthPercent.Dispose();
    }
}

```

### 3.3. Xử lý Đa luồng và FrameProvider

Một trong những lỗi crash phổ biến nhất trong Unity là truy cập Unity API (như `transform.position`, `text.text`) từ thread khác (Background Thread). R3 cung cấp cơ chế an toàn thông qua `ObserveOn` với `UnityFrameProvider`.^11^

Khác với `ObserveOnMainThread` chung chung của UniRx, R3 cho phép chọn chính xác thời điểm trong vòng lặp PlayerLoop:

-   `UnityFrameProvider.Update`: Thời điểm update logic game thông thường.

-   `UnityFrameProvider.FixedUpdate`: Dùng cho logic vật lý.

-   `UnityFrameProvider.LateUpdate`: Dùng cho camera hoặc cập nhật UI sau khi logic đã chạy xong (để tránh jitter).

Ví dụ xử lý dữ liệu nặng ở background và cập nhật UI an toàn:

C#

```
_searchQuery
   .Debounce(TimeSpan.FromMilliseconds(200)) // Chờ người dùng ngừng gõ
   .SelectAwait(async (query, ct) =>
    {
        // Chạy trên ThreadPool
        await UniTask.SwitchToThreadPool();
        return await SearchHeavyDataAsync(query, ct);
    })
   .ObserveOn(UnityFrameProvider.Update) // Quay về Main Thread
   .Subscribe(results => UpdateUI(results));

```

* * * * *

4\. Xử lý Bất đồng bộ: Sức mạnh của UniTask
-------------------------------------------

### 4.1. Vượt qua giới hạn của Coroutine

Coroutine của Unity, mặc dù dễ dùng, nhưng có nhược điểm lớn: tạo rác bộ nhớ (GC allocation) mỗi khi `yield return new...`, không hỗ trợ trả về giá trị (return value), và khó xử lý ngoại lệ (try-catch). `System.Threading.Tasks.Task` chuẩn của C# thì lại quá nặng nề cho Game Loop.

UniTask ra đời như một giải pháp thay thế hoàn hảo: Struct-based (zero allocation), tích hợp sâu vào PlayerLoop, và hỗ trợ đầy đủ API của Unity (như `UnityWebRequest`, `AssetBundle`, `Addressables`) thông qua các extension method `await`.^3^

### 4.2. UniTask trong ViewModel

Trong MVVM, ViewModel thường xuyên phải thực hiện các tác vụ bất đồng bộ như tải item, lưu game, hoặc chờ phản hồi từ server. Việc sử dụng UniTask giúp code trong ViewModel trở nên tuyến tính, dễ đọc và dễ debug hơn hẳn so với Callback Hell.

**Quy tắc An toàn:** Luôn luôn truyền `CancellationToken` vào các phương thức async trong ViewModel. `CancellationToken` này nên được link với vòng đời của View hoặc Scope chứa ViewModel đó.

C#

```
using Cysharp.Threading.Tasks;
using System.Threading;

public class InventoryViewModel
{
    private readonly IInventoryService _inventoryService;
    public ReactiveProperty<bool> IsLoading { get; } = new(false);

    public InventoryViewModel(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public async UniTask RefreshInventoryAsync(CancellationToken cancellation)
    {
        IsLoading.Value = true;
        try
        {
            // Tự động throw OperationCanceledException nếu cancellation được kích hoạt
            var items = await _inventoryService.GetItemsAsync(cancellation);

            // Xử lý dữ liệu
            _items.Value = items;
        }
        catch (OperationCanceledException)
        {
            // Xử lý khi task bị hủy (thường là bỏ qua)
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            // Hiển thị lỗi lên UI
        }
        finally
        {
            IsLoading.Value = false;
        }
    }
}

```

### 4.3. Ngăn chặn Memory Leak với Cancellation

Một vấn đề nghiêm trọng khi dùng async/await trong Unity là: Nếu GameObject bị hủy khi task đang chạy, task đó vẫn tiếp tục chạy ngầm (zombie task) và khi hoàn thành, nó cố gắng truy cập vào các component đã bị hủy, gây ra `MissingReferenceException` hoặc tệ hơn là logic sai lệch.

UniTask cung cấp `GetCancellationTokenOnDestroy()` cho MonoBehaviour. Tuy nhiên, trong ViewModel (Plain Class), chúng ta không có phương thức này. Giải pháp là implement `IDisposable` trong ViewModel và tạo `CancellationTokenSource` riêng.

C#

```
public class BaseViewModel : IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    // Property này được truyền xuống các hàm async
    protected CancellationToken CancellationToken => _cts.Token;

    public virtual void Dispose()
    {
        _cts.Cancel(); // Hủy tất cả task đang chạy
        _cts.Dispose();
    }
}

```

* * * * *

5\. Phân tách Module: VitalRouter và Command Pattern
----------------------------------------------------

### 5.1. Vấn đề "Spaghetti Code" trong giao tiếp UI

Khi ứng dụng lớn dần, các ViewModel thường cần giao tiếp với nhau. Ví dụ: `InventoryViewModel` cần thông báo cho `CharacterStatsViewModel` khi một món đồ được trang bị. Nếu chúng ta `Inject` ViewModel này vào ViewModel kia, chúng ta tạo ra sự phụ thuộc vòng (Circular Dependency) và cấu trúc code chặt chẽ khó tách rời.

VitalRouter giải quyết vấn đề này bằng mô hình "Mediator" hoặc "Command Bus" hiệu năng cao. Các ViewModel không biết về nhau, chúng chỉ biết về các "Lệnh" (Command).^8^

### 5.2. Định nghĩa Command và Routing

VitalRouter sử dụng Source Generator để tạo code định tuyến (routing) tại thời điểm biên dịch, loại bỏ hoàn toàn chi phí Reflection và GC allocation khi gửi lệnh.

Bước 1: Định nghĩa Command (Struct)

Sử dụng struct để đảm bảo zero allocation.

C#

```
using VitalRouter;

public readonly struct EquipItemCommand : ICommand
{
    public readonly string ItemId;
    public readonly EquipmentSlot Slot;

    public EquipItemCommand(string itemId, EquipmentSlot slot)
    {
        ItemId = itemId;
        Slot = slot;
    }
}

```

**Bước 2: Gửi Command từ ViewModel A**

C#

```
public class InventoryViewModel
{
    public void Equip(string itemId)
    {
        // Fire and forget hoặc await
        Router.Default.PublishAsync(new EquipItemCommand(itemId, EquipmentSlot.MainHand));
    }
}

```

Bước 3: Xử lý Command tại ViewModel B (hoặc Presenter)

Class xử lý cần được đánh dấu và method xử lý đánh dấu. Class này cũng phải là partial để Source Generator hoạt động.

C#

```
using VitalRouter;
using Cysharp.Threading.Tasks;

public partial class CharacterStatsPresenter
{
    private readonly CharacterModel _model;

    public CharacterStatsPresenter(CharacterModel model)
    {
        _model = model;
    }

    public async UniTask OnEquipItem(EquipItemCommand cmd, CancellationToken ct)
    {
        // Xử lý logic nghiệp vụ
        await _model.ApplyEquipmentStatsAsync(cmd.ItemId, ct);

        UnityEngine.Debug.Log($"Đã trang bị {cmd.ItemId} vào slot {cmd.Slot}");
    }
}

```

### 5.3. Interceptors: Middleware cho Logic Game

Sức mạnh thực sự của VitalRouter nằm ở hệ thống Interceptor. Nó cho phép chèn logic vào giữa quá trình gửi và nhận lệnh, tạo thành một pipeline xử lý (tương tự middleware trong web server). Điều này cực kỳ hữu ích cho:

-   **Validation:** Kiểm tra điều kiện (đủ level, đủ tiền) trước khi thực thi lệnh.

-   **Logging:** Ghi log toàn bộ hành động người chơi.

-   **Error Handling:** Bắt lỗi global cho toàn bộ command.

C#

```
public class LoggingInterceptor : ICommandInterceptor
{
    public async ValueTask InvokeAsync<T>(
        T command,
        PublishContext context,
        PublishContinuation<T> next)
        where T : ICommand
    {
        UnityEngine.Debug.Log($"[Command] {command.GetType().Name} executing...");

        try
        {
            await next(command, context); // Gọi handler tiếp theo
            UnityEngine.Debug.Log($"[Command] {command.GetType().Name} success.");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[Command] Failed: {ex.Message}");
            throw; // Rethrow hoặc nuốt lỗi tùy logic
        }
    }
}

```

* * * * *

6\. Thiết kế ViewModel: Lõi Logic của MVVM
------------------------------------------

### 6.1. Nguyên tắc thiết kế ViewModel chuẩn

ViewModel trong Unity cần tuân thủ nghiêm ngặt các nguyên tắc sau để đảm bảo hiệu quả:

1.  **Không phụ thuộc View:** ViewModel không được chứa tham chiếu đến `GameObject`, `Transform`, hay bất kỳ UI Component nào (`Button`, `Text`).

2.  **Input/Output Rõ ràng:**

    -   **Input:** Là các phương thức (Method) hoặc `Subject<T>` nhận sự kiện từ người dùng (Click, Drag, Input Text).

    -   **Output:** Là các `ReadOnlyReactiveProperty<T>` hoặc `Observable<T>` để View hiển thị dữ liệu.

3.  **Quản lý State:** ViewModel là nơi nắm giữ trạng thái tạm thời của UI (ví dụ: text đang nhập dở, tab đang chọn) và đồng bộ trạng thái bền vững từ Model.

### 6.2. ReactiveCommand: Xử lý Input An toàn

R3 cung cấp `ReactiveCommand`, một phiên bản nâng cấp của `Subject` chuyên dùng cho việc xử lý input từ nút bấm. Điểm mạnh của `ReactiveCommand` là khả năng quản lý trạng thái `CanExecute`.

Ví dụ: Nút "Nâng cấp" chỉ sáng khi người chơi có đủ vàng.

C#

```
public class UpgradeViewModel : IDisposable
{
    public ReadOnlyReactiveProperty<long> CurrentGold { get; }
    public ReadOnlyReactiveProperty<long> UpgradeCost { get; }

    // Command chỉ kích hoạt được khi đủ tiền
    public ReactiveCommand<Unit> UpgradeCommand { get; }

    public UpgradeViewModel(WalletModel wallet, UpgradeConfig config)
    {
        CurrentGold = wallet.Gold;
        UpgradeCost = new ReactiveProperty<long>(config.BaseCost);

        // Logic CanExecute: Gold >= Cost
        var canUpgrade = CurrentGold
           .CombineLatest(UpgradeCost, (gold, cost) => gold >= cost);

        // Tạo Command với điều kiện canUpgrade
        UpgradeCommand = new ReactiveCommand<Unit>(canUpgrade);

        // Xử lý logic khi command được thực thi
        UpgradeCommand.Subscribe(_ =>
        {
            wallet.Subtract(UpgradeCost.CurrentValue);
            // Thực hiện nâng cấp...
        });
    }
}

```

Với thiết kế này, View chỉ cần bind tính năng `interactable` của Button vào property `CanExecute` của `UpgradeCommand`. Button sẽ tự động enable/disable theo số tiền của người chơi mà không cần một dòng code `if-else` nào trong View.

* * * * *

7\. Triển khai Lớp View: Chiến lược Binding Đa Nền tảng
-------------------------------------------------------

View là nơi duy nhất trong MVVM được phép kế thừa `MonoBehaviour`. Nhiệm vụ của View là "Bind" (kết nối) các UI Component với ViewModel. Unity hiện có hai hệ thống UI chính: uGUI (Canvas truyền thống) và UI Toolkit (mới, dựa trên HTML/CSS). Cysharp stack hỗ trợ tốt cả hai, nhưng cách tiếp cận khác nhau.

### 7.1. Binding với uGUI (Canvas)

R3 hỗ trợ uGUI thông qua các Extension Methods rất mạnh mẽ, giúp code binding trở nên ngắn gọn và declarative (khai báo) [^15^], [^15^.

**Bảng các Extension Method thông dụng cho uGUI:**

| **Component** | **Sự kiện (View -> VM)** | **Binding (VM -> View)** |
| --- | --- | --- |
| **Button** | `button.OnClickAsObservable()` | `command.BindTo(button)` |
| **InputField** | `input.OnValueChangedAsObservable()` | `property.Subscribe(x => input.text = x)` |
| **Toggle** | `toggle.OnValueChangedAsObservable()` | `property.Subscribe(x => toggle.isOn = x)` |
| **Slider** | `slider.OnValueChangedAsObservable()` | `property.Subscribe(x => slider.value = x)` |
| **Text (Legacy)** | N/A | `property.SubscribeToText(textComponent)` |
| **TMP_Text** | N/A | `property.SubscribeToText(tmpComponent)` |

**Code mẫu Binding uGUI:**

C#

```
public class UserProfileView : MonoBehaviour
{
    private TMP_Text _nameText;
    private Button _changeNameButton;
    private TMP_InputField _nameInput;

    private UserProfileViewModel _vm;

    [Inject] // VContainer inject ViewModel vào đây
    public void Construct(UserProfileViewModel vm)
    {
        _vm = vm;
    }

    void Start()
    {
        // 1. One-way Binding: Hiển thị tên
        _vm.UserName
           .SubscribeToText(_nameText)
           .AddTo(this); // Quan trọng: Gắn subscription vào vòng đời GameObject

        // 2. Two-way Binding: Input Field
        // View -> ViewModel
        _nameInput.OnValueChangedAsObservable()
           .Subscribe(txt => _vm.InputName.Value = txt)
           .AddTo(this);

        // ViewModel -> View (để reset input khi cần)
        _vm.InputName
           .Subscribe(txt => _nameInput.text = txt)
           .AddTo(this);

        // 3. Command Binding
        // Bind lệnh click vào ViewModel
        _changeNameButton.OnClickAsObservable()
           .ThrottleFirst(TimeSpan.FromSeconds(1)) // Chống spam click
           .Subscribe(_ => _vm.ConfirmChangeNameCommand.Execute(Unit.Default))
           .AddTo(this);

        // Tự động disable nút nếu Input rỗng (thông qua CanExecute)
        _vm.ConfirmChangeNameCommand.CanExecute
           .SubscribeToInteractable(_changeNameButton)
           .AddTo(this);
    }
}

```

### 7.2. Binding với UI Toolkit (Hiện đại)

UI Toolkit không có sẵn các extension như uGUI trong R3 (tính đến thời điểm hiện tại), và cơ chế của nó dựa trên `VisualElement` thay vì `Component`. Tuy nhiên, chúng ta có thể xây dựng các extension method tương tự hoặc sử dụng các thư viện binding của cộng đồng.

Điểm khác biệt lớn nhất là UI Toolkit không dùng `Inspector` để tham chiếu trực tiếp. Chúng ta phải dùng `UQuery` (hàm `Q<T>`) để tìm element theo tên (xác định trong UI Builder).^12^

**Xây dựng Helper Binding cho UI Toolkit:**

C#

```
public static class R3UIToolkitExtensions
{
    // Binding Text cho Label
    public static IDisposable BindText(this Label label, Observable<string> source)
    {
        return source.Subscribe(label, (text, l) => l.text = text);
    }

    // Binding Click cho Button
    public static Observable<Unit> OnClickAsObservable(this Button button)
    {
        return Observable.FromEvent<ClickEvent>(
            h => button.RegisterCallback(h),
            h => button.UnregisterCallback(h)
        ).Select(_ => Unit.Default);
    }

    // Binding Class (để toggle style active/inactive)
    public static IDisposable BindClass(this VisualElement element, string className, Observable<bool> toggleSource)
    {
        return toggleSource.Subscribe(element, (isOn, el) =>
        {
            if (isOn) el.AddToClassList(className);
            else el.RemoveFromClassList(className);
        });
    }
}

```

**Sử dụng trong View:**

C#

```
public class InventoryUIView : MonoBehaviour
{
    private UIDocument _document;
    private InventoryViewModel _vm;

    [Inject]
    public void Construct(InventoryViewModel vm) => _vm = vm;

    void Start()
    {
        var root = _document.rootVisualElement;
        var equipBtn = root.Q<Button>("btn-equip");
        var statusLabel = root.Q<Label>("lbl-status");

        // Binding
        statusLabel.BindText(_vm.StatusMessage).AddTo(this);

        equipBtn.OnClickAsObservable()
           .Subscribe(_ => _vm.EquipCommand.Execute(Unit.Default))
           .AddTo(this);
    }
}

```

### 7.3. Xử lý Danh sách Phức tạp (ListView) với ObservableList

Một thách thức lớn trong MVVM là binding danh sách (ví dụ: danh sách 100 item trong kho đồ). Tạo 100 GameObject con là cách làm tồi tệ về hiệu năng. UI Toolkit cung cấp `ListView` ảo hóa (virtualization) để giải quyết việc này, nhưng làm sao kết nối nó với ViewModel?

Cysharp cung cấp thư viện `ObservableCollections` (tách riêng khỏi R3 nhưng tương thích hoàn toàn).^14^

1.  **ViewModel:** Sử dụng `ObservableList<ItemViewModel>`.

2.  **View:** Sử dụng `ListView` của UI Toolkit.

Chúng ta cần một Adapter để cầu nối giữa `ObservableList` và `ListView` của Unity.

C#

```
// ViewModel
public ObservableList<ItemViewModel> Items { get; } = new();

// View
private void BindList(ListView uiListView, ObservableList<ItemViewModel> sourceList)
{
    // Cài đặt Visual cho ListView
    uiListView.makeItem = () => new Label(); // Hoặc load từ VisualTreeAsset
    uiListView.bindItem = (element, index) =>
    {
        var itemVM = sourceList[index];
        (element as Label).text = itemVM.Name.Value;
    };

    // Quan trọng: Set itemsSource
    uiListView.itemsSource = sourceList; // ObservableList implement IList, nên gán được trực tiếp

    // Lắng nghe thay đổi của List để refresh UI khi có item thêm/xóa
    // ObservableCollections hỗ trợ R3 extension ObserveCountChanged
    sourceList.ObserveCountChanged()
       .Subscribe(_ => uiListView.RefreshItems())
       .AddTo(this);
}

```

Cách tiếp cận này tận dụng được khả năng ảo hóa của UI Toolkit (chỉ render những item đang nhìn thấy) trong khi vẫn giữ được tính reactive của dữ liệu.

* * * * *

8\. Các Tình huống Nâng cao và Chiến lược Tối ưu
------------------------------------------------

### 8.1. Multi-Scene MVVM và Navigation

Trong game, việc chuyển cảnh (Scene) hoặc mở các cửa sổ (Popup) lồng nhau là thường xuyên. VContainer hỗ trợ `LifetimeScope.CreateChild()` để tạo các scope con động.

Ví dụ: Hệ thống Navigation mở Popup.

1.  **NavigationService:** Được đăng ký Singleton.

2.  **Logic:** Khi gọi `OpenPopup<InventoryViewModel>()`, service sẽ load Prefab chứa `InventoryScope`.

3.  **Scope:** `InventoryScope` (con của Root) sẽ khởi tạo `InventoryViewModel` và các dependency riêng của nó.

4.  **VitalRouter:** Sử dụng để truyền dữ liệu khởi tạo vào Popup (ví dụ: `OpenPopupCommand` chứa `InventoryID`).

### 8.2. Unit Testing ViewModel

Một lợi ích to lớn của kiến trúc này là khả năng Unit Test. Vì ViewModel là POCO và các dependency đều là Interface, ta có thể dùng thư viện Mock (như `Moq` hoặc `NSubstitute`) để test logic.

**Ví dụ Test:**

C#

```

public void Test_Upgrade_Success()
{
    // Arrange
    var mockWallet = new Mock<IWalletService>();
    mockWallet.Setup(w => w.Gold).Returns(new ReactiveProperty<long>(1000));

    var vm = new UpgradeViewModel(mockWallet.Object);

    // Act
    vm.UpgradeCommand.Execute(Unit.Default);

    // Assert
    mockWallet.Verify(w => w.Subtract(It.IsAny<long>()), Times.Once);
}

```

### 8.3. Tối ưu Hiệu năng (Performance Tuning)

-   **Struct-based Command:** VitalRouter sử dụng `struct` cho Command để tránh GC. Luôn ưu tiên `struct` cho các message tần suất cao (như update position, health change).

-   **UniTaskTracker:** Sử dụng công cụ `UniTask Tracker` (có sẵn trong package) để kiểm tra xem có Task nào bị "treo" (leak) không. Task bị leak thường do quên truyền CancellationToken.

-   **R3 Allocation:** Tránh dùng `Select` + `Subscribe` liên tục trong `Update()`. Nếu cần update mỗi frame, hãy dùng `Observable.EveryUpdate()` thay vì bắn event thủ công.

### 8.4. Kết luận

Triển khai MVVM trong Unity với Cysharp Stack không chỉ là việc cài đặt thư viện, mà là việc áp dụng một kỷ luật kiến trúc nghiêm ngặt.

-   **VContainer** giữ cho cấu trúc ứng dụng lỏng lẻo và dễ mở rộng.

-   **R3** biến luồng dữ liệu trở nên rõ ràng và an toàn.

-   **UniTask** giải phóng sức mạnh xử lý đa luồng một cách đơn giản.

-   **VitalRouter** xóa bỏ sự rối rắm trong giao tiếp module.

Sự kết hợp này tạo nên một nền tảng vững chắc ("Solid Foundation") cho các tựa game quy mô lớn, đảm bảo code base luôn sạch sẽ, dễ bảo trì và đạt hiệu năng cao nhất có thể trên nền tảng Unity.