# Hệ Thống UI Binding

## 1. Khái Niệm

Hệ thống UI Binding là một công cụ code-generation cho Unity, được thiết kế để tự động hóa việc kết nối (binding) giữa code và các UI Components trong Scene.

**Vấn đề giải quyết:**
- Loại bỏ việc phải viết code boilerplate lặp đi lặp lại như `GetComponentInChildren`, `transform.Find`, hoặc kéo thả `[SerializeField]` thủ công.
- Giảm thiểu lỗi runtime (như `NullReferenceException`) do đổi tên hoặc thay đổi cấu trúc UI.
- Cung cấp một cách truy cập các UI element có cấu trúc, tường minh và an toàn về kiểu (strongly-typed).

**Cách hoạt động:**
Hệ thống sẽ quét cấu trúc của một GameObject và tự động tạo ra một lớp C# `partial` (View class). Lớp này chứa các thuộc tính và các lớp con (scope) ánh xạ 1-1 với cấu trúc của GameObject, giúp bạn truy cập component một cách dễ dàng, ví dụ: `view.Header.Button_Play.image.color = Color.green;`.

## 2. Tính Năng Chính

- **Tự động tạo mã:** Dựa trên cấu trúc của GameObject để tạo ra View class.
- **Truy cập theo Scope:** Tạo ra các lớp con lồng nhau (`Scope`) để mô phỏng cấu trúc của Prefab/GameObject, giúp truy cập có tổ chức.
- **Tham chiếu Component:** Tự động tìm và tham chiếu đến các component phổ biến (`Image`, `Button`, `TMP_Text`,...) và cả các component custom do người dùng tự định nghĩa.
- **Hỗ trợ View lồng nhau (Nested Views):** Một View cha có thể tham chiếu trực tiếp đến một View con, coi nó như một module hoàn chỉnh và không phân tích sâu vào bên trong.
- **Hỗ trợ đa chế độ:** Hoạt động tốt với GameObject trong Scene, Prefab asset trong Project, và trong chế độ Prefab Mode.
- **Tự động gán Inspector:** Tự động gán tham chiếu vào các `List` đã được `[SerializeField]` trong Inspector, giúp kiểm tra và gỡ lỗi một cách trực quan.
- **Cập nhật thủ công:** Cung cấp một nút `[Update Bindings]` trong Inspector để tạo lại mã nguồn khi có thay đổi về cấu trúc UI.

## 3. Workflow Sử Dụng

### a. Binding lần đầu

1.  Chọn một GameObject trong Scene, hoặc một Prefab trong cửa sổ Project. GameObject này sẽ là gốc của View.
2.  Từ menu, chọn **Tools > UI Binding > Start Binding This**.
3.  Một cửa sổ sẽ hiện ra, gợi ý tên lớp (ví dụ: `MyPopupView`). Bạn có thể giữ nguyên hoặc đổi tên, sau đó xác nhận.
4.  Hệ thống sẽ tạo ra 2 file:
    *   `MyPopupView.cs`: Nơi bạn sẽ viết logic code.
    *   `MyPopupView.g.cs`: File do hệ thống tự động tạo ra, chứa tất cả các tham chiếu. **Không bao giờ sửa file này.**

### b. Truy cập Component

Mở file `MyPopupView.cs` và truy cập các component thông qua các scope đã được tạo.

```csharp
// Ví dụ:
public partial class MyPopupView
{
    void Start()
    {
        // Truy cập vào Text của Button trong Header
        this.Header.Button_OK.Label.text = "Đồng ý";

        // Gán sự kiện click cho Button
        this.Header.Button_OK.button.onClick.AddListener(OnButtonOkClicked);

        // Truy cập một View con đã được binding
        this.PlayerInfoView.UpdateInfo("Player 1", 1000);
    }

    void OnButtonOkClicked()
    {
        //...
    }
}
```

### c. Cập nhật Binding

Khi bạn thay đổi cấu trúc UI (thêm, xóa, đổi tên GameObject), bạn cần cập nhật lại binding.

1.  Chọn GameObject gốc chứa script View.
2.  Trong Inspector, một nút **[Update Bindings]** sẽ xuất hiện ở trên cùng.
3.  Click vào nút đó. Hệ thống sẽ quét lại và tạo lại file `.g.cs` cho bạn.

## 4. Hướng Dẫn & Lưu Ý Quan Trọng

-   **Quy tắc đặt tên:** Luôn đặt tên GameObject một cách có ý nghĩa, không chứa ký tự đặc biệt. Tên GameObject sẽ được dùng để tạo ra tên thuộc tính trong code.
-   **Vị trí file:** Bạn có thể di chuyển cả cặp file `.cs` và `.g.cs` sang thư mục khác. Hệ thống sẽ tự động tìm đúng vị trí file script để cập nhật file `.g.cs` tương ứng.
-   **Lớp `partial`:** Toàn bộ logic của bạn phải được viết trong file `.cs`. File `.g.cs` sẽ bị ghi đè mỗi khi bạn cập nhật binding.
-   **View lồng nhau:** Để một View cha có thể nhận diện và tham chiếu đến View con, bạn phải thực hiện binding cho View con **trước**.
-   **Vấn đề với Auto-Mapping:** Sau khi code được tạo lại, hệ thống sẽ tự động gán tham chiếu vào các `List` trong Inspector. Nếu một `List` nào đó bị trống, hãy kiểm tra lại:
    -   GameObject/Component tương ứng có tồn tại trong cấu trúc không?
    -   Tên của GameObject có đúng không?
    -   Component có được hệ thống hỗ trợ không? (Nếu là component custom, hãy đảm bảo nó được `UIBindEditor.GetUIComponentsPublic` nhận diện).