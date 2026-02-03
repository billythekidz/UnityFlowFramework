# Kế hoạch Chuyển đổi Object Binding sang Roslyn Source Generator

Tài liệu này vạch ra kế hoạch chi tiết để nâng cấp hệ thống Object Binding từ việc tạo tệp vật lý sang sử dụng Roslyn Source Generator. Mục tiêu là để có một dự án sạch sẽ hơn, quy trình làm việc tự động và tích hợp sâu hơn vào quá trình biên dịch của Unity.

## Kiến trúc hiện tại

Hệ thống hiện tại rất thông minh, bao gồm hai luồng chính:
1.  **Luồng thủ công:** Người dùng chọn một `GameObject` và chạy một lệnh từ menu để tạo ra một tệp script `.cs` vật lý, gán nó vào `GameObject`, và bắt đầu theo dõi nó.
2.  **Luồng tự động:** Một script editor (`UIBindAutoMapping`) chạy ngầm, theo dõi các thay đổi trong cấu trúc của các `GameObject` đã được đăng ký. Khi phát hiện thay đổi (thông qua so sánh hash), nó sẽ tự động ghi đè lên tệp `.cs` đã tạo.

**Điểm yếu:** Việc tạo và sửa đổi các tệp `.cs` vật lý trong thư mục `Assets` làm lẫn lộn mã nguồn của dự án và có thể gây ra các lần biên dịch không mong muốn, làm gián đoạn quy trình làm việc.

## Kiến trúc đề xuất: Mô hình Lai (Hybrid)

Chúng ta sẽ giữ lại phần tốt nhất của hệ thống hiện tại (phát hiện thay đổi tự động) và kết hợp nó với sức mạnh của Source Generator.

1.  **Editor Scripts (Thu thập dữ liệu):**
    *   Vẫn chịu trách nhiệm theo dõi các `GameObject` và phát hiện các thay đổi trong cấu trúc cây thông qua so sánh hash.
    *   **Thay đổi chính:** Thay vì tạo mã C#, khi phát hiện thay đổi, nó sẽ quét cấu trúc cây và ghi dữ liệu có cấu trúc đó vào một tệp cache duy nhất: `HierarchyCache.json`.

2.  **Source Generator (Tạo mã):**
    *   Là một phần của quá trình biên dịch.
    *   Nó sẽ đọc `HierarchyCache.json`.
    *   Tạo ra các lớp `partial` tương ứng trong bộ nhớ. Mã này sẽ được thêm trực tiếp vào assembly mà không cần tạo tệp vật lý trong `Assets`.

---

## Các bước thực hiện chi tiết

### Bước 1: Tạo `HierarchyCache.json` và các Data Model

Tạo một tệp mới tại `Assets/ObjectsBinding/Sync/HierarchyCache.json`. Cấu trúc của nó sẽ được định nghĩa bởi các lớp C# sau:

```csharp
[System.Serializable]
public class HierarchyNode
{
    public string name;
    public string[] components;
    public List<HierarchyNode> children = new List<HierarchyNode>();
}

[System.Serializable]
public class CachedObject
{
    public string className; // Ví dụ: "MyPanelView"
    public HierarchyNode hierarchy;
}

[System.Serializable]
public class HierarchyCache
{
    public List<CachedObject> objects = new List<CachedObject>();
}
```

### Bước 2: Thiết lập Assembly cho Source Generator

Tạo một thư mục mới `Assets/ObjectsBinding.SourceGenerator` và cấu hình tệp `.asmdef` bên trong nó để chỉ chạy trên Editor và tham chiếu đến `Unity.CompilationPipeline.API`.

### Bước 3: Tạo Marker Attribute

Tạo một attribute để đánh dấu các lớp cần được Source Generator xử lý.

```csharp
// Đặt trong assembly chính của dự án
[System.AttributeUsage(System.AttributeTargets.Class)]
public class GenerateObjectBindingAttribute : System.Attribute { }
```

### Bước 4: Sửa đổi Editor Scripts

1.  **`UIBindAutoMapping.cs`:**
    *   Sửa đổi hàm `RegenerateUIBinding`. Thay vì gọi `UIBindCodeGenerator.GenerateCode`, nó sẽ:
        1.  Quét `GameObject` để tạo một đối tượng `HierarchyNode`.
        2.  Cập nhật `HierarchyCache.json` với dữ liệu `HierarchyNode` mới.
        3.  **Không** gọi `AssetDatabase.Refresh()`.
    *   Mở rộng các hàm xử lý sự kiện để bao gồm các trigger mới (xem phần "Quy trình làm việc mới"). Các hàm này sẽ chịu trách nhiệm gọi `AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate)` để kích hoạt Source Generator một cách có kiểm soát.

2.  **`UIBindEditor.cs`:**
    *   Sửa đổi hàm `GenerateSelected`. Thay vì tạo một tệp script đầy đủ, nó sẽ:
        1.  Tạo một tệp script **trống** chỉ chứa định nghĩa lớp `partial` và attribute `[GenerateObjectBinding]`.
        2.  Gán script này vào `GameObject`.
        3.  Gọi `UIBindAutoMapping.StartMonitoring` như cũ.
        4.  Kích hoạt việc tạo cache lần đầu tiên cho đối tượng này.

3.  **Tạo các Trigger mới:**
    *   **Play Mode Trigger:** Đăng ký vào `EditorApplication.playModeStateChanged` để kiểm tra và làm mới trước khi vào Play Mode.
    *   **Asset Save Trigger:** Tạo một lớp kế thừa từ `UnityEditor.AssetModificationProcessor` và triển khai `OnWillSaveAssets` để phát hiện các thay đổi trên Prefab được "Apply" từ scene.
    *   **Manual Trigger:** Thêm một menu item `Tools/UI Binding/Force Regenerate All` để chạy lại toàn bộ quá trình một cách thủ công.

### Bước 5: Viết Source Generator

Tạo một lớp `ObjectBindingGenerator : ISourceGenerator` trong assembly của generator.
*   Trong hàm `Execute`, nó sẽ:
    1.  Đọc và deserialize `HierarchyCache.json`.
    2.  Tìm tất cả các lớp `partial` trong dự án có attribute `[GenerateObjectBinding]`.
    3.  Với mỗi lớp tìm thấy, nó sẽ tra cứu dữ liệu phân cấp tương ứng trong cache (dựa trên tên lớp).
    4.  Sử dụng lại logic tạo mã hiện có (từ `UIBindEditor` và `UIBindCodeGenerator`) nhưng điều chỉnh để duyệt qua đối tượng `HierarchyNode` thay vì `Transform`.
    5.  Thêm mã nguồn đã tạo vào quá trình biên dịch bằng `context.AddSource()`.

---

## Quy trình làm việc mới (Đã cải tiến)

Đây là quy trình làm việc sau khi hoàn tất quá trình chuyển đổi.

### 1. Thiết lập (Lần đầu)
Bạn chọn một `GameObject`, chạy "Tools/UI Binding To Code". Thao tác này sẽ:
*   Tạo một script `MyView.cs` trống với lớp `partial` và attribute `[GenerateObjectBinding]`.
*   Gán script vào `GameObject`.
*   Thêm `GameObject` vào danh sách theo dõi trong `MonitoredObjects.json`.
*   Quét cấu trúc cây và lưu nó vào `HierarchyCache.json`.
*   Kích hoạt biên dịch để Source Generator chạy lần đầu.

### 2. Tự động cập nhật Cache (Hoàn toàn trong nền)
*   Bạn thay đổi cấu trúc của `GameObject` (thêm/xóa con, đổi tên, v.v.).
*   `UIBindAutoMapping` sẽ phát hiện sự thay đổi về hash trong lần kiểm tra tiếp theo (được kích hoạt bởi các sự kiện như lưu scene, mất focus...).
*   Nó sẽ tự động quét lại cấu trúc cây và cập nhật `HierarchyCache.json`.
*   Việc lưu tệp JSON này sẽ **không** kích hoạt biên dịch lại ngay lập tức, tránh làm gián đoạn công việc.

### 3. Tạo mã (Khi bạn sẵn sàng hoặc khi cần thiết)
Hệ thống sẽ kích hoạt quá trình biên dịch và chạy Source Generator trong các trường hợp sau:

*   **Hành động lưu trữ rõ ràng:**
    *   Sau khi bạn **lưu scene**.
    *   Sau khi bạn **thoát khỏi Prefab Mode**.
    *   Sau khi bạn **lưu Prefab** trong khi đang ở Prefab Mode.
    *   Sau khi bạn **"Apply" các thay đổi** từ một Prefab instance trong scene (được phát hiện bởi `AssetModificationProcessor`).

*   **Hành động bảo vệ:**
    *   Ngay trước khi bạn **vào Play Mode**.

*   **Hành động thủ công:**
    *   Khi bạn chọn **"Force Regenerate All"** từ menu.

Hành động kích hoạt này sẽ chạy `AssetDatabase.Refresh()`, khởi động quá trình biên dịch, và Source Generator sẽ đọc phiên bản mới nhất của `HierarchyCache.json` để tạo ra mã nguồn cập nhật trong bộ nhớ. IntelliSense của bạn sẽ được làm mới ngay sau đó.

## Lợi ích
*   **Dự án sạch sẽ:** Không còn các tệp `.cs` được tạo tự động trong thư mục `Assets`.
*   **Quy trình làm việc mượt mà:** Giảm thiểu các lần biên dịch không cần thiết, chỉ biên dịch tại các điểm hợp lý.
*   **Đáng tin cậy:** Bao quát nhiều kịch bản làm việc hơn, giảm thiểu lỗi do mã không được đồng bộ.
*   **Tích hợp sâu:** Hệ thống trở thành một phần tự nhiên của quá trình biên dịch, thay vì là một công cụ chạy bên ngoài.
