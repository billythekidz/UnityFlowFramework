# FSM_UniTask: Framework FSM Phân Cấp & Bất đồng bộ cho Unity

Tài liệu này cung cấp hướng dẫn toàn diện về framework `FSM_UniTask`, một hệ thống mạnh mẽ để tạo Máy Trạng Thái Phân Cấp (Hierarchical State Machines - HSM) trong Unity, được xây dựng với `UniTask` cho các khả năng bất đồng bộ mạnh mẽ.

## Tổng quan

`FSM_UniTask` là một framework máy trạng thái gọn nhẹ, hiệu năng cao, được thiết kế để quản lý các luồng logic game và UI phức tạp. Các điểm mạnh cốt lõi của nó là:

-   **Cấu trúc phân cấp (Hierarchical):** Các state có thể chứa các sub-state của riêng chúng, cho phép bạn chia nhỏ logic phức tạp thành các máy trạng thái lồng nhau, dễ quản lý.
-   **Thiết kế bất đồng bộ (Asynchronous by Design):** Tất cả các phương thức trong vòng đời của state (`Enter`, `Update`, `Exit`, v.v.) đều được xây dựng trên `UniTask`, giúp việc xử lý các sự kiện dựa trên thời gian, tải scene, gọi API và các hoạt động bất đồng bộ khác trở nên cực kỳ đơn giản mà không cần dùng callback hay Coroutine.
-   **Quản lý Hủy bỏ (Cancellation) mạnh mẽ:** Framework tự động quản lý `CancellationToken`, đảm bảo rằng khi một state được thoát ra, tất cả các hoạt động bất đồng bộ của nó sẽ được hủy một cách an toàn và đáng tin cậy, ngăn ngừa lỗi và rò rỉ bộ nhớ.
-   **Truyền dữ liệu (Data Passing):** Dễ dàng truyền dữ liệu giữa các state trong quá trình chuyển đổi.

## Các khái niệm cốt lõi

Framework được xây dựng dựa trên bốn lớp `abstract` cốt lõi.

### 1. `FSM_System`
-   "Bộ não" chính của máy trạng thái. Đây là một `MonoBehaviour` mà bạn sẽ gắn vào một GameObject tồn tại lâu dài trong scene.
-   **Trách nhiệm:**
    -   Quản lý state đang hoạt động hiện tại.
    -   Xử lý việc chuyển state thông qua `GoToState()`.
    -   Tạo và quản lý `CancellationToken` cho vòng đời của mỗi state.
    -   Điều khiển các vòng lặp bất đồng bộ `Update`, `LateUpdate`, và `FixedUpdate`.

### 2. `FSM_State`
-   Khối xây dựng cơ bản của FSM. Đại diện cho một state "lá" đơn lẻ không có state con.
-   **Trách nhiệm:**
    -   Triển khai logic của ứng dụng bên trong các phương thức vòng đời của nó (`StateEnter`, `StateUpdate`, `StateExit`).
    -   Giữ một tham chiếu đến `FSM_System` chính để yêu cầu chuyển state.

### 3. `FSM_StateParent`
-   Một state phức tạp hơn, kế thừa từ `FSM_State`. Nó có thể chứa và quản lý một FSM con của riêng mình.
-   **Trách nhiệm:**
    -   Tất cả các trách nhiệm của một `FSM_State`.
    -   Quản lý vòng đời của các sub-state thông qua `GoToSubState()`.
    -   Truyền các lệnh gọi `Update` xuống cho sub-state đang hoạt động.
    -   Quản lý một `CancellationToken` riêng cho các state con, độc lập với token của state cha.

### 4. `FSM_SubState`
-   Một state được quản lý bởi một `FSM_StateParent`.
-   Cấu trúc của nó gần như giống hệt `FSM_State`, nhưng nó giữ một tham chiếu đến `FSM_StateParent` cha của nó thay vì `FSM_System` tổng.

## Hướng dẫn sử dụng cơ bản

Đây là cách thiết lập một FSM đơn giản với hai trạng thái: `Loading` và `MainMenu`.

### Bước 1: Tạo Máy Trạng Thái chính

Tạo một lớp kế thừa từ `FSM_System`. Khởi tạo tất cả các state cấp cao của bạn trong hàm constructor hoặc `Awake`.

```csharp
// File: MyGameStateMachine.cs
public class MyGameStateMachine : FSM_System
{
    // Định nghĩa các state của bạn
    public readonly LoadingState State_Loading;
    public readonly MainMenuState State_MainMenu;

    // Sử dụng constructor hoặc Awake để khởi tạo các state
    public MyGameStateMachine()
    {
        State_Loading = new LoadingState(this);
        State_MainMenu = new MainMenuState(this);
    }

    // Thiết lập state ban đầu trong hàm Start
    public override void Start()
    {
        base.Start();
        // Sử dụng .Forget() vì chúng ta không cần đợi việc chuyển state này ở đây
        GoToState(State_Loading).Forget();
    }
}
```

### Bước 2: Định nghĩa một State với Tác vụ Bất đồng bộ

`LoadingState` sẽ mô phỏng việc tải tài nguyên và sau đó chuyển sang `MainMenuState`.

```csharp
// File: LoadingState.cs
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public class LoadingState : FSM_State
{
    // Constructor phải truyền FSM_System cho lớp cơ sở
    public LoadingState(FSM_System fsm) : base(fsm) {}

    // Ghi đè các phương thức vòng đời
    public override async UniTask StateEnter(CancellationToken token)
    {
        Debug.Log("Đã vào Loading State. Đang tải tài nguyên...");

        // Mô phỏng một hoạt động tải tài nguyên trong 2 giây
        await UniTask.Delay(2000, cancellationToken: token);
        
        // Nếu state bị thoát ra trong khi chúng ta đang delay, token sẽ bị hủy,
        // một exception sẽ được ném ra, và dòng lệnh dưới đây sẽ không được thực thi.

        Debug.Log("Tải xong. Đang chuyển tới Main Menu...");

        // Lấy FSM system cụ thể để truy cập các state của nó
        var fsm = (MyGameStateMachine)_stateMachine;
        await _stateMachine.GoToState(fsm.State_MainMenu);
    }

    public override UniTask StateExit()
    {
        Debug.Log("Đang thoát khỏi Loading State.");
        return UniTask.CompletedTask;
    }
}
```

### Bước 3: Định nghĩa một State đơn giản

`MainMenuState` là một state đơn giản không làm gì khác ngoài việc ghi một thông báo ra log.

```csharp
// File: MainMenuState.cs
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public class MainMenuState : FSM_State
{
    public MainMenuState(FSM_System fsm) : base(fsm) {}

    public override UniTask StateEnter(CancellationToken token)
    {
        Debug.Log("Chào mừng tới Main Menu!");
        // State này hoàn thành logic vào state ngay lập tức
        return UniTask.CompletedTask;
    }
}
```

## Tham khảo API

### `FSM_System`
-   `UniTask GoToState(FSM_State newState)`: Chuyển sang một state mới.
-   `UniTask GoToState(FSM_State newState, object data)`: Chuyển sang một state mới và truyền dữ liệu cho nó.

### `FSM_State` / `FSM_SubState` (Các phương thức `virtual` để ghi đè)
-   `UniTask StateEnter(CancellationToken cancellationToken)`
-   `UniTask StateEnter<T>(T data, CancellationToken cancellationToken)`
-   `UniTask StateUpdate(CancellationToken cancellationToken)`
-   `UniTask StateLateUpdate(CancellationToken cancellationToken)`
-   `UniTask StateFixedUpdate(CancellationToken cancellationToken)`
-   `UniTask StateExit()`

### `FSM_StateParent` (Các phương thức `protected`)
-   `UniTask GoToSubState(FSM_SubState newSubState)`
-   `UniTask GoToSubState<T>(FSM_SubState newSubState, T data)`
