# GameFlowFramework Learning Project

This repository is a learning project for building a robust and scalable game framework in Unity. It explores various modern concepts and patterns, including:

- **MVVM (Model-View-ViewModel):** For clean separation of UI logic and game state.
- **Reactive Programming (with R3):** For managing complex data flows and events.
- **Async/Await (with UniTask):** For writing clean, non-blocking asynchronous code.
- **Dependency Injection (with Reflex):** For decoupling components and improving testability.
- **Custom Code Generation:** For reducing boilerplate and automating repetitive tasks.

## Cài đặt các Frameworks cần thiết

Dự án này sử dụng một số framework mạnh mẽ để hỗ trợ lập trình. Bạn cần cài đặt chúng thông qua **Unity Package Manager (UPM)**.

### Hướng dẫn cài đặt qua Git URL

Đây là cách đơn giản và được khuyến nghị nhất.

1.  Mở Unity Editor.
2.  Đi đến `Window` -> `Package Manager`.
3.  Nhấn vào nút **`+`** ở góc trên bên trái.
4.  Chọn **`Add package from git URL...`**.
5.  Dán lần lượt từng URL dưới đây vào và nhấn `Add`.

#### 1. UniTask

Tối ưu hóa `async/await` cho Unity, giúp viết code bất đồng bộ hiệu quả và không phát sinh rác (garbage).
```
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
```

#### 2. PrimeTween

Một thư viện Tweening hiệu năng cao và không phát sinh rác. Hướng dẫn cài tại đây:
Import PrimeTween from Asset Store. This is a preferable option because it comes with a Demo scene.
Optional: install via Unity Package Manager (UPM).
```
https://github.com/KyryloKuzyk/PrimeTween?tab=readme-ov-file#install-via-unity-package-manager-upm
```

#### 3. Reflex

Một framework Dependency Injection (DI) nhẹ và mạnh mẽ, giúp giảm sự phụ thuộc giữa các lớp.
```
https://github.com/gustavopsantos/reflex.git?path=/Assets/Reflex/
```

Sau khi cài đặt tất cả các package trên, project của bạn sẽ sẵn sàng để hoạt động.


### 4. R3 Installation Steps

Dùng cho lập trình phản ứng, quản lý luồng dữ liệu và sự kiện.

**1. Install NuGet Packages**
1.  Install NuGet using [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity)
    ```
    https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity
    ```
-   Open `Window -> NuGet -> Manage NuGet Packages`.
-   **Install R3:** Search for "**R3**" and press **Install**.
-   **Install ObservableCollections:** Search for "**ObservableCollections**" (also from Cysharp) and press **Install**.
-   **Install ObservableCollections.R3:** Search for "**ObservableCollections.R3**" and press **Install**. This is an extension module to make `ObservableCollections` work smoothly with `R3`.

-   If you encounter version conflict errors, please disable version validation in Player Settings(Edit -> Project Settings -> Player -> Scroll down and expand "Other Settings" than uncheck "Assembly Version Validation" under the "Configuration" section).

**2. Install R3.Unity Package**

-   Install the `R3.Unity` package by referencing the git URL:
    ```
    https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity
    ```

R3 uses the *.*.* release tag, so you can specify a version like #1.0.0. For example: `https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity#1.0.0`
