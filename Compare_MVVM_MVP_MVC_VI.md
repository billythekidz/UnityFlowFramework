Các Mô Hình Kiến Trúc Trong Phát Triển Unity: Phân Tích Chuyên Sâu về MVC, MVP và MVVM
======================================================================================

1\. Tổng quan: Sự Phức Tạp Của Quản Lý Trạng Thái Trong Game Engine
-------------------------------------------------------------------

Phát triển ứng dụng thời gian thực 3D, đặc biệt là trong hệ sinh thái Unity, đặt ra những thách thức kỹ thuật độc đáo khác biệt hoàn toàn so với phát triển phần mềm doanh nghiệp truyền thống. Trong khi các nguyên lý cốt lõi của kỹ thuật phần mềm---như sự phân tách mối quan tâm (Separation of Concerns - SoC), tính mô đun hóa (modularity) và khả năng kiểm thử (testability)---vẫn giữ nguyên giá trị, việc áp dụng chúng vào một engine dựa trên thành phần (component-based) đòi hỏi sự điều chỉnh tinh tế.

Kiến trúc mặc định của Unity xoay quanh lớp `MonoBehaviour`. Đây là một thiết kế mạnh mẽ cho việc tạo mẫu nhanh (prototyping), cho phép các nhà phát triển gắn logic trực tiếp vào các đối tượng trong không gian 3D. Tuy nhiên, sự tiện lợi này thường dẫn đến một cái bẫy kiến trúc được gọi là "Spaghetti Code" hoặc anti-pattern "Massive View Controller". Trong mô hình này, dữ liệu (Data), logic nghiệp vụ (Business Logic) và logic trình bày (Presentation Logic) thường cùng tồn tại trong một lớp duy nhất. Khi quy mô dự án mở rộng, các lớp này trở nên phình to, khó bảo trì, khó kiểm thử tự động và cực kỳ dễ vỡ khi có yêu cầu thay đổi.^1^

Để giải quyết nợ kỹ thuật (technical debt) này, cộng đồng phát triển Unity đã tích cực áp dụng và điều chỉnh các mẫu kiến trúc từ thế giới phát triển phần mềm rộng lớn hơn: **Model-View-Controller (MVC)**, **Model-View-Presenter (MVP)**, và **Model-View-ViewModel (MVVM)**. Báo cáo này sẽ cung cấp một phân tích toàn diện và sâu sắc về ba mô hình này, được bối cảnh hóa cụ thể cho Unity. Chúng ta sẽ đi sâu vào cơ sở lý thuyết, triển khai thực tế thông qua các ví dụ mã nguồn chi tiết (tập trung vào Hệ thống Máu/Health System và UI), và tích hợp các thư viện lập trình phản ứng hiện đại như **R3** cũng như các mẫu hạ tầng như **Service Locator** và **Event Bus** để xây dựng các giải pháp cấp doanh nghiệp.^4^

### 1.1 Vấn đề "MonoBehaviour Nguyên Khối" (The Monolithic MonoBehaviour)

Trước khi đi sâu vào giải pháp, chúng ta cần mổ xẻ vấn đề. Một triển khai ngây thơ (naive) của một thanh máu (health bar) trong Unity thường bao gồm một script duy nhất gắn vào một GameObject. Script này nắm giữ giá trị máu hiện tại, kiểm tra đầu vào từ bàn phím, xử lý va chạm vật lý, và cập nhật trực tiếp thành phần UI Slider.

C#

```
// Anti-Pattern: MonoBehaviour Nguyên Khối (Tightly Coupled)
using UnityEngine;
using UnityEngine.UI;

public class NaiveHealth : MonoBehaviour
{
    // Dữ liệu (Model) nằm chung với View
    public float currentHealth;
    public float maxHealth = 100f;

    // Tham chiếu trực tiếp đến View
    public Slider healthSlider;
    public Image fillImage;

    // Logic Controller nằm trong Update
    void Update()
    {
        // Pha trộn Input detection và Business Logic
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TakeDamage(10);
        }
    }

    void TakeDamage(float amount)
    {
        // Business Logic
        currentHealth -= amount;
        if (currentHealth < 0) currentHealth = 0;

        // Presentation Logic (trực tiếp thao tác UI)
        if (healthSlider!= null)
        {
            healthSlider.value = currentHealth / maxHealth;
        }

        if (fillImage!= null)
        {
            // Logic đổi màu cứng (Hard-coded visual logic)
            fillImage.color = Color.Lerp(Color.red, Color.green, currentHealth / maxHealth);
        }

        if (currentHealth <= 0)
        {
            Debug.Log("Player Died"); // Side effect
        }
    }
}

```

Đoạn mã trên vi phạm Nguyên lý Trách nhiệm Đơn lẻ (Single Responsibility Principle - SRP). `NaiveHealth` chịu trách nhiệm cho quá nhiều thứ: lưu trữ dữ liệu, xử lý đầu vào, logic trò chơi và hiển thị. Nếu chúng ta muốn thay đổi giao diện từ thanh trượt 2D sang một thanh máu 3D trên đầu nhân vật, chúng ta phải sửa đổi lớp này, có nguy cơ phá vỡ logic tính toán máu. Hơn nữa, làm thế nào để viết Unit Test cho logic `TakeDamage` mà không cần khởi động Unity Editor? Điều này là gần như không thể với kiến trúc trên.^7^

* * * * *

2\. Model-View-Controller (MVC): Nền Tảng Của Sự Phân Tách
----------------------------------------------------------

Mô hình Model-View-Controller (MVC) là tiền thân của hầu hết các kiến trúc giao diện người dùng hiện đại. Được Trygve Reenskaug phát triển tại Xerox PARC vào những năm 1970, MVC được thiết kế để tách biệt biểu diễn thông tin nội bộ khỏi cách thông tin đó được trình bày và chấp nhận từ người dùng. Trong bối cảnh Unity, MVC thường là bước đầu tiên mà các nhà phát triển thực hiện để thoát khỏi sự ràng buộc của MonoBehaviour.^1^

### 2.1 Định Nghĩa Kiến Trúc Trong Bối Cảnh Unity

Trong một cách diễn giải MVC chặt chẽ cho Unity:

-   **Model (Mô hình):** Là nơi chứa dữ liệu và logic nghiệp vụ cốt lõi. Trong Unity, đây thường là một lớp C# thuần túy (POCO - Plain Old CLR Object) hoặc một `ScriptableObject`. Model hoàn toàn không biết gì về View hay Controller. Nó sử dụng cơ chế Observer (thường là C# `event` hoặc `Action`) để thông báo cho các bên quan tâm khi trạng thái của nó thay đổi.

-   **View (Khung nhìn):** Là thành phần chịu trách nhiệm hiển thị dữ liệu cho người dùng. Trong Unity, View là một `MonoBehaviour` được gắn vào các phần tử UI hoặc GameObjects trong scene. View quan sát Model để cập nhật trạng thái hiển thị, nhưng không bao giờ trực tiếp thay đổi Model.

-   **Controller (Bộ điều khiển):** Đóng vai trò trung gian xử lý đầu vào. Nó nhận tín hiệu từ người dùng (Input) và chuyển đổi chúng thành các hành động trên Model. Controller có thể là một `MonoBehaviour` (để nhận `Update` hoặc `OnCollisionEnter`) hoặc một lớp thuần túy được điều phối bởi một Manager.

### 2.2 Triển Khai Chi Tiết: Hệ Thống Health MVC Refactored

Chúng ta hãy tái cấu trúc ví dụ "NaiveHealth" ở trên thành một cấu trúc MVC mạnh mẽ. Mục tiêu là cho phép thay đổi giao diện người dùng (View) mà không cần chạm vào logic (Model) hoặc phương thức đầu vào (Controller).^3^

#### 2.2.1 The Model: Logic Thuần Túy

Model là nguồn chân lý (Source of Truth). Bằng cách tách nó khỏi `MonoBehaviour`, chúng ta đảm bảo rằng nó có thể được tuần tự hóa (serialized) dễ dàng và quan trọng nhất là có thể kiểm thử được trong môi trường.NET thuần túy mà không cần API của Unity.

C#

```
// MVC Model: Logic C# thuần túy
using System;
using UnityEngine;

 // Cho phép hiển thị trong Inspector nếu được bọc
public class HealthModel
{
    // Dữ liệu được đóng gói (Encapsulation)
    private float _currentHealth;
    private float _maxHealth;

    // Observer Pattern: Model thông báo khi trạng thái thay đổi
    // Sử dụng Action thay vì delegate để gọn gàng hơn
    public event Action<float, float> OnHealthChanged; // current, max
    public event Action OnDeath;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public bool IsDead => _currentHealth <= 0;

    // Constructor để Dependency Injection hoặc khởi tạo
    public HealthModel(float maxHealth)
    {
        _maxHealth = maxHealth;
        _currentHealth = maxHealth;
    }

    // Logic nghiệp vụ thuần túy
    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        _currentHealth -= amount;

        // Clamp giá trị để đảm bảo tính toàn vẹn dữ liệu
        if (_currentHealth < 0) _currentHealth = 0;

        // Thông báo cho các Observer (View)
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        if (IsDead)
        {
            OnDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;

        _currentHealth += amount;
        if (_currentHealth > _maxHealth) _currentHealth = _maxHealth;

        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }
}

```

*Phân tích:* `HealthModel` không phụ thuộc vào `UnityEngine.UI`. Nó có thể được sử dụng cho cả nhân vật người chơi (có UI) và kẻ thù AI (không có UI hoặc UI khác biệt). Sự tách biệt này cho phép tái sử dụng mã cao.^11^

#### 2.2.2 The View: Quan Sát và Hiển Thị

Trong mô hình MVC truyền thống, View biết về Model. Nó đăng ký lắng nghe các sự kiện từ Model để tự cập nhật. Trong Unity, View buộc phải là `MonoBehaviour` để có thể tham chiếu đến các thành phần trong Scene.

C#

```
// MVC View: Xử lý hiển thị
using UnityEngine;
using UnityEngine.UI;

public class HealthViewMVC : MonoBehaviour
{

    private Slider _slider;
    private Image _fillImage;
    private Text _healthText;

    private Color _healthyColor = Color.green;
    private Color _criticalColor = Color.red;

    // View giữ tham chiếu đến Model để đăng ký sự kiện
    // Tuy nhiên, View KHÔNG được gọi hàm thay đổi dữ liệu của Model (đó là việc của Controller)
    public void Initialize(HealthModel model)
    {
        // Đăng ký sự kiện (Observer Pattern)
        model.OnHealthChanged += UpdateHealthBar;
        model.OnDeath += HandleDeath;

        // Cập nhật trạng thái ban đầu
        UpdateHealthBar(model.CurrentHealth, model.MaxHealth);
    }

    private void UpdateHealthBar(float current, float max)
    {
        float percentage = current / max;

        if (_slider!= null)
        {
            _slider.value = percentage;
        }

        if (_healthText!= null)
        {
            _healthText.text = $"{current:0}/{max:0}";
        }

        if (_fillImage!= null)
        {
            // Logic hiển thị thuần túy (Presentation Logic)
            _fillImage.color = Color.Lerp(_criticalColor, _healthyColor, percentage);
        }
    }

    private void HandleDeath()
    {
        // Chỉ xử lý phần nhìn của cái chết (ẩn UI, chơi animation, v.v.)
        if (_healthText!= null) _healthText.text = "DEAD";
        // Có thể trigger animation tại đây
    }

    // Dọn dẹp sự kiện để tránh Memory Leak
    private void OnDestroy()
    {
        // Lưu ý: Cần cơ chế để unsubscribe nếu Model tồn tại lâu hơn View
        // Trong ví dụ đơn giản này, ta giả định chúng có vòng đời gắn liền
    }
}

```

#### 2.2.3 The Controller: Điều Phối và Đầu Vào

Controller là bộ não kết nối. Trong Unity, một kịch bản phổ biến là Controller khởi tạo Model và View, sau đó lắng nghe Input từ người chơi để thao tác lên Model.

C#

```
// MVC Controller: Xử lý Input và Khởi tạo
using UnityEngine;

public class HealthControllerMVC : MonoBehaviour
{
    // Controller sở hữu Model và View
    private HealthModel _model;
    private HealthViewMVC _view;
    private float _initialHealth = 100f;

    void Awake()
    {
        // 1. Khởi tạo Model
        _model = new HealthModel(_initialHealth);

        // 2. Kết nối View với Model
        if (_view!= null)
        {
            _view.Initialize(_model);
        }
        else
        {
            Debug.LogError("View reference missing inside Controller!");
        }
    }

    void Update()
    {
        // 3. Xử lý Input (Input Polling)
        // Controller chuyển đổi hành động người dùng thành lệnh cho Model
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Input received: Take Damage");
            _model.TakeDamage(10f);
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            Debug.Log("Input received: Heal");
            _model.Heal(5f);
        }
    }
}

```

### 2.3 Đánh Giá Mô Hình MVC Trong Unity

**Ưu điểm:**

-   **Tách biệt dữ liệu:** Logic nghiệp vụ được an toàn trong lớp C# thuần túy, dễ dàng mang sang các dự án khác.

-   **Rõ ràng:** Luồng dữ liệu (Input -> Controller -> Model -> View) tương đối dễ hiểu.

**Nhược điểm:**

-   **Sự phụ thuộc của View:** View phụ thuộc trực tiếp vào Model (`HealthModel`). Nếu chúng ta thay đổi cấu trúc của Model (ví dụ đổi tên sự kiện), chúng ta phải sửa lại View. Điều này tạo ra sự kết nối chặt chẽ (tight coupling) không mong muốn giữa tầng hiển thị và tầng dữ liệu.^12^

-   **Controller "béo" (Fat Controller):** Trong Unity, ranh giới giữa View và Controller thường bị mờ nhạt vì cả hai đều thường là MonoBehaviour. Controller thường phải gánh vác quá nhiều trách nhiệm quản lý vòng đời.

-   **Khó kiểm thử View:** Vì View gắn liền với `MonoBehaviour` và tham chiếu trực tiếp Model cụ thể, việc viết unit test độc lập cho View rất khó khăn.

* * * * *

3\. Model-View-Presenter (MVP): Kiến Trúc "Passive View" Cho Khả Năng Kiểm Thử Tối Đa
-------------------------------------------------------------------------------------

Mô hình Model-View-Presenter (MVP) được coi là một bước tiến hóa tự nhiên của MVC, đặc biệt phù hợp cho các hệ thống UI phức tạp trong Unity. Điểm khác biệt cốt lõi nằm ở vai trò của **Presenter** và tính chất **Thụ động (Passive)** của View.

Trong MVP, View **không biết gì về Model**. Nó hoàn toàn "ngốc nghếch" (dumb/passive). Nó chỉ hiển thị những gì Presenter bảo nó hiển thị và chuyển tiếp mọi hành động của người dùng (như click chuột) tới Presenter. Presenter đóng vai trò trung gian tuyệt đối: lấy dữ liệu từ Model, định dạng nó, và đẩy vào View.^14^

### 3.1 Sức Mạnh Của Interface (Giao Diện)

Chìa khóa để triển khai MVP thành công trong Unity là việc sử dụng **Interface** cho View. Bằng cách để Presenter giao tiếp với một `IHealthView` thay vì lớp cụ thể `HealthViewUnity`, chúng ta đạt được sự tách biệt hoàn toàn (decoupling). Điều này cho phép chúng ta thay thế toàn bộ giao diện (ví dụ: chuyển từ uGUI sang UI Toolkit) mà không cần thay đổi một dòng code nào trong Presenter hay Model. Quan trọng hơn, nó cho phép **Unit Testing** dễ dàng bằng cách sử dụng các Mock Object.^16^

### 3.2 Triển Khai Chi Tiết: Hệ Thống MVP Health

#### 3.2.1 Bước 1: Định Nghĩa Hợp Đồng (The Interface)

Đây là bước quan trọng nhất. Chúng ta định nghĩa View *có thể làm gì* và *có thể thông báo gì*.

C#

```
// MVP: Hợp đồng giao diện cho View
using System;

public interface IHealthView
{
    // Command Methods: Presenter ra lệnh cho View
    void SetHealthText(string text);
    void UpdateHealthSlider(float value);
    void SetFillColor(UnityEngine.Color color);
    void ShowDeathScreen();

    // Events: View thông báo cho Presenter về hành động người dùng
    event Action OnHealRequested;
    event Action OnDamageRequested; // Ví dụ nút debug
}

```

#### 3.2.2 Bước 2: The View (Passive Implementation)

View thực tế chỉ là một lớp vỏ bọc mỏng xung quanh các thành phần Unity UI. Nó không chứa logic tính toán.

C#

```
// MVP: View cụ thể (MonoBehaviour)
using UnityEngine;
using UnityEngine.UI;
using System;

public class HealthViewMVP : MonoBehaviour, IHealthView
{
    private Slider _slider;
    private Text _text;
    private Image _fill;
    private Button _healButton;
    private Button _damageButton;
    private GameObject _deathPanel;

    // Triển khai Events từ Interface
    public event Action OnHealRequested;
    public event Action OnDamageRequested;

    private void Start()
    {
        // Binding UI events tới C# Actions
        if (_healButton) _healButton.onClick.AddListener(() => OnHealRequested?.Invoke());
        if (_damageButton) _damageButton.onClick.AddListener(() => OnDamageRequested?.Invoke());
    }

    // Triển khai các phương thức hiển thị
    public void SetHealthText(string text)
    {
        if (_text) _text.text = text;
    }

    public void UpdateHealthSlider(float value)
    {
        if (_slider) _slider.value = value;
    }

    public void SetFillColor(Color color)
    {
        if (_fill) _fill.color = color;
    }

    public void ShowDeathScreen()
    {
        if (_deathPanel) _deathPanel.SetActive(true);
    }
}

```

*Phân tích:* `HealthViewMVP` không có tham chiếu đến `HealthModel`. Nó không biết "Máu" là gì, chỉ biết hiển thị một con số float và một chuỗi string.

#### 3.2.3 Bước 3: The Presenter (Bộ Não Trung Gian)

Presenter chứa toàn bộ logic trình bày (Presentation Logic). Nó quyết định màu sắc của thanh máu dựa trên giá trị, định dạng chuỗi text, v.v.

C#

```
// MVP: Presenter (Logic trình bày)
using UnityEngine;

public class HealthPresenter
{
    private readonly HealthModel _model;
    private readonly IHealthView _view; // Chỉ phụ thuộc vào Interface

    // Constructor Injection
    public HealthPresenter(HealthModel model, IHealthView view)
    {
        _model = model;
        _view = view;

        // 1. Lắng nghe Model
        _model.OnHealthChanged += HandleModelChange;
        _model.OnDeath += HandleDeath;

        // 2. Lắng nghe View (User Input)
        _view.OnHealRequested += HandleHealInput;
        _view.OnDamageRequested += HandleDamageInput;

        // 3. Khởi tạo trạng thái View ban đầu
        RefreshView();
    }

    private void RefreshView()
    {
        HandleModelChange(_model.CurrentHealth, _model.MaxHealth);
    }

    // Xử lý khi dữ liệu thay đổi
    private void HandleModelChange(float current, float max)
    {
        float percentage = current / max;

        // Presenter quyết định định dạng dữ liệu
        _view.UpdateHealthSlider(percentage);
        _view.SetHealthText($"{current}/{max} HP");

        // Logic đổi màu nằm ở Presenter, không phải View
        if (percentage < 0.3f)
            _view.SetFillColor(Color.red);
        else
            _view.SetFillColor(Color.green);
    }

    private void HandleDeath()
    {
        _view.ShowDeathScreen();
    }

    // Xử lý Input từ View
    private void HandleHealInput()
    {
        _model.Heal(10f);
    }

    private void HandleDamageInput()
    {
        _model.TakeDamage(10f);
    }

    // Quan trọng: Dọn dẹp để tránh Memory Leak
    public void Dispose()
    {
        _model.OnHealthChanged -= HandleModelChange;
        _model.OnDeath -= HandleDeath;
        _view.OnHealRequested -= HandleHealInput;
        _view.OnDamageRequested -= HandleDamageInput;
    }
}

```

#### 3.2.4 Bước 4: Bootstrapper (Người Gắn Kết)

Vì Presenter không phải là MonoBehaviour, cần một ai đó khởi tạo nó. Đây thường là một script `Setup` hoặc sử dụng Dependency Injection.

C#

```
public class GameSetup : MonoBehaviour
{
    private HealthViewMVP _view; // Reference tới implementation cụ thể
    private HealthModel _model;
    private HealthPresenter _presenter;

    void Awake()
    {
        _model = new HealthModel(100f);
        // Presenter nhận vào Interface IHealthView, nhưng ta truyền instance HealthViewMVP
        _presenter = new HealthPresenter(_model, _view);
    }

    void OnDestroy()
    {
        _presenter.Dispose();
    }
}

```

### 3.3 Unit Testing Trong MVP

Đây là lợi thế lớn nhất của MVP. Chúng ta có thể kiểm thử logic hiển thị mà không cần Unity. Giả sử sử dụng thư viện **NSubstitute**:

C#

```

public void Test_WhenHealthCritical_ViewColorIsRed()
{
    // Arrange
    var model = new HealthModel(100);
    var view = Substitute.For<IHealthView>(); // Mock View
    var presenter = new HealthPresenter(model, view);

    // Act
    model.TakeDamage(80); // Máu còn 20 (20%)

    // Assert
    // Kiểm tra xem Presenter có gọi hàm SetFillColor với màu đỏ không
    view.Received().SetFillColor(Color.red);
}

```

Khả năng này là vô giá trong các dự án lớn cần độ ổn định cao.^18^

* * * * *

4\. Model-View-ViewModel (MVVM) và Lập Trình Phản Ứng (Reactive Programming)
----------------------------------------------------------------------------

MVVM ban đầu được Microsoft thiết kế cho WPF và XAML. Điểm khác biệt cốt lõi của nó so với MVP là **Data Binding** (Ràng buộc dữ liệu). Trong MVP, Presenter phải gọi thủ công `view.SetText(...)`. Trong MVVM, View tự động lắng nghe và cập nhật khi ViewModel thay đổi thông qua cơ chế binding, loại bỏ rất nhiều mã "boilerplate" (mã lặp lại).^11^

Tuy nhiên, Unity (trước UI Toolkit) không có hệ thống binding mạnh mẽ. Do đó, MVVM trong Unity thường đi kèm với các thư viện **Reactive Programming** như **UniRx** hoặc thư viện thế hệ mới **R3**.

### 4.1 R3 (Reactive Extensions 3rd Gen) Là Gì?

Các tài liệu nghiên cứu 21 chỉ ra sự trỗi dậy của R3 như là người kế nhiệm của UniRx. R3 mang lại hiệu năng tốt hơn, tích hợp chặt chẽ với async/await của C# hiện đại, và giải quyết các vấn đề về phân bổ bộ nhớ (allocation) của các phiên bản trước.

Trong MVVM với R3, chúng ta không dùng C# event nữa. Chúng ta dùng ReactiveProperty<T>.

### 4.2 Triển Khai: Hệ Thống Reactive Health với R3

#### 4.2.1 The ViewModel

ViewModel trong MVVM đóng vai trò cung cấp dữ liệu đã được chuẩn bị sẵn cho View. Nó bộc lộ các dòng dữ liệu (Streams) thay vì giá trị tĩnh.

C#

```
// MVVM: ViewModel sử dụng R3
using R3;
using UnityEngine;

public class HealthViewModel : IDisposable
{
    private readonly HealthModel _model;

    // Reactive Properties: Dữ liệu đầu ra cho View
    // ReadOnlyReactiveProperty đảm bảo View không thể set ngược lại
    public ReadOnlyReactiveProperty<float> CurrentHealthRx { get; }
    public ReadOnlyReactiveProperty<float> HealthPercentageRx { get; }
    public ReadOnlyReactiveProperty<bool> IsDeadRx { get; }
    public ReadOnlyReactiveProperty<Color> HealthColorRx { get; }

    // Commands: Đầu vào từ View (Input)
    public Subject<Unit> OnHealCommand { get; } = new Subject<Unit>();
    public Subject<Unit> OnDamageCommand { get; } = new Subject<Unit>();

    public HealthViewModel(HealthModel model)
    {
        _model = model;

        // Chuyển đổi Event truyền thống của Model sang Reactive Stream
        // Observable.FromEvent giúp cầu nối giữa OOP và FRP (Functional Reactive Programming)
        var healthChangedStream = Observable.FromEvent<Action<float, float>, (float current, float max)>(
            h => (c, m) => h((c, m)),
            h => _model.OnHealthChanged += h,
            h => _model.OnHealthChanged -= h
        );

        // Binding Logic: Biến đổi dữ liệu thô thành dữ liệu hiển thị

        // 1. Current Health Stream
        CurrentHealthRx = healthChangedStream
           .Select(x => x.current)
           .ToReadOnlyReactiveProperty(_model.CurrentHealth);

        // 2. Percentage Stream
        HealthPercentageRx = healthChangedStream
           .Select(x => x.current / x.max)
           .ToReadOnlyReactiveProperty(1.0f);

        // 3. Color Logic Stream (Reactive Logic)
        HealthColorRx = HealthPercentageRx
           .Select(pct => pct < 0.3f? Color.red : Color.green)
           .ToReadOnlyReactiveProperty(Color.green);

        // 4. Death Stream
        IsDeadRx = Observable.FromEvent(
            h => _model.OnDeath += h,
            h => _model.OnDeath -= h
        ).Select(_ => true).ToReadOnlyReactiveProperty(false);

        // Xử lý Command
        OnHealCommand.Subscribe(_ => _model.Heal(10f));
        OnDamageCommand.Subscribe(_ => _model.TakeDamage(10f));
    }

    public void Dispose()
    {
        OnHealCommand.Dispose();
        OnDamageCommand.Dispose();
        CurrentHealthRx.Dispose();
        //... dispose các property khác
    }
}

```

#### 4.2.2 The View (Binder)

Trong MVVM, View (MonoBehaviour) chịu trách nhiệm "kết nối dây điện" (wiring). Nó đăng ký vào các ReactiveProperty.

C#

```
// MVVM: View (Binder Layer)
using UnityEngine;
using UnityEngine.UI;
using R3; // Sử dụng R3

public class HealthViewMVVM : MonoBehaviour
{
    private Slider _slider;
    private Text _text;
    private Image _fill;
    private Button _healButton;
    private Button _damageButton;
    private GameObject _deathScreen;

    private HealthViewModel _viewModel;

    public void Initialize(HealthViewModel viewModel)
    {
        _viewModel = viewModel;

        // 1. One-way Binding: ViewModel -> View
        // Tự động cập nhật UI khi ViewModel thay đổi

        _viewModel.HealthPercentageRx
           .Subscribe(val => _slider.value = val)
           .AddTo(this); // Tự động hủy đăng ký khi GameObject bị hủy (tính năng của R3)

        _viewModel.CurrentHealthRx
           .Subscribe(val => _text.text = $"HP: {val}")
           .AddTo(this);

        _viewModel.HealthColorRx
           .Subscribe(color => _fill.color = color)
           .AddTo(this);

        _viewModel.IsDeadRx
           .Where(isDead => isDead == true) // Lọc sự kiện
           .Subscribe(_ => _deathScreen.SetActive(true))
           .AddTo(this);

        // 2. Interaction: View -> ViewModel (Commands)

        // Chuyển đổi sự kiện click uGUI thành Observable
        _healButton.OnClickAsObservable()
           .Subscribe(_ => _viewModel.OnHealCommand.OnNext(Unit.Default))
           .AddTo(this);

        _damageButton.OnClickAsObservable()
           .Subscribe(_ => _viewModel.OnDamageCommand.OnNext(Unit.Default))
           .AddTo(this);
    }
}

```

### 4.3 UI Toolkit: Tương Lai Của MVVM Trong Unity

Unity 6 và các phiên bản mới hơn đang đẩy mạnh **UI Toolkit**. Hệ thống này hỗ trợ Data Binding nguyên bản (native binding) giống như WPF. Bạn không cần viết mã binding thủ công như trên. Thay vào đó, bạn thiết lập `binding-path` trong file UXML và View sẽ tự động đồng bộ với bất kỳ đối tượng nào thực thi `INotifyPropertyChanged`.^11^

Ví dụ UXML binding:

XML

```
<ui:ProgressBar binding-path="HealthPercentage" />
<ui:Label binding-path="CurrentHealthText" />

```

Điều này biến Unity thành một môi trường MVVM thực thụ, giảm thiểu đáng kể lượng code "keo dính" (glue code) mà lập trình viên phải viết.

* * * * *

5\. Các Mẫu "Chất Kết Dính": Service Locator và Event Bus
---------------------------------------------------------

Dù bạn chọn MVC, MVP hay MVVM, một câu hỏi kiến trúc lớn luôn tồn tại: **Làm thế nào các thành phần tìm thấy nhau?** Nếu View cần Presenter, và Presenter cần Model, ai là người khởi tạo và kết nối chúng? Nếu Nhân vật chết, làm sao Hệ thống Âm thanh biết để phát tiếng hét mà không cần tham chiếu trực tiếp?.^5^

### 5.1 Service Locator Pattern

Service Locator là một kho chứa trung tâm nơi các hệ thống đăng ký bản thân. Dù thường bị tranh cãi là một "anti-pattern" vì nó che giấu sự phụ thuộc, nó cực kỳ phổ biến trong Unity nhờ tính đơn giản.

C#

```
// Triển khai Service Locator đơn giản
public static class ServiceLocator
{
    private static Dictionary<Type, object> _services = new Dictionary<Type, object>();

    public static void Register<T>(T service)
    {
        if (!_services.ContainsKey(typeof(T)))
            _services.Add(typeof(T), service);
    }

    public static T Get<T>()
    {
        if (_services.TryGetValue(typeof(T), out object service))
            return (T)service;

        throw new Exception($"Dịch vụ {typeof(T)} chưa được đăng ký.");
    }
}

// Sử dụng trong MVP Presenter
public class PlayerPresenter
{
    public PlayerPresenter()
    {
        // Presenter tự tìm kiếm Audio Service
        var audio = ServiceLocator.Get<IAudioService>();
        audio.PlaySound("Spawn");
    }
}

```

### 5.2 Event Bus (Hệ Thống Sự Kiện)

Để giải quyết vấn đề giao tiếp ngang hàng (Horizontal Communication), Event Bus là giải pháp tối ưu. Nó cho phép các hệ thống hoàn toàn tách biệt giao tiếp với nhau thông qua các thông điệp.

C#

```
// Triển khai Event Bus Generic an toàn kiểu dữ liệu (Type-Safe)
public static class EventBus<T> where T : IEvent
{
    private static Action<T> _onEvent;

    public static void Subscribe(Action<T> action) => _onEvent += action;
    public static void Unsubscribe(Action<T> action) => _onEvent -= action;
    public static void Raise(T eventItem) => _onEvent?.Invoke(eventItem);
}

// Định nghĩa sự kiện
public struct PlayerDamageEvent : IEvent
{
    public float DamageAmount;
    public Vector3 Location;
}

// Sử dụng:
// Nơi gây sát thương:
EventBus<PlayerDamageEvent>.Raise(new PlayerDamageEvent { DamageAmount = 10, Location = transform.position });

// Hệ thống Particle (Hiệu ứng máu):
EventBus<PlayerDamageEvent>.Subscribe(evt => SpawnBloodEffect(evt.Location));

```

Mô hình này giúp `HealthPresenter` không cần biết về `AudioSystem` hay `ParticleSystem`, giảm thiểu sự phụ thuộc chéo.^6^

* * * * *

6\. Phân Tích So Sánh và Bảng Tổng Hợp
--------------------------------------

Để giúp bạn đưa ra quyết định kiến trúc chính xác, dưới đây là bảng so sánh chi tiết dựa trên các tiêu chí kỹ thuật.

| **Tiêu Chí** | **MVC (Phong cách Unity)** | **MVP (Passive View)** | **MVVM (Reactive)** |
| --- | --- | --- | --- |
| **Sự thông minh của View** | View biết Model (Subscribe event) | View "ngốc" (Chỉ hiển thị) | View biết ViewModel (Binding) |
| **Nơi chứa Logic UI** | Controller | Presenter | ViewModel |
| **Độ ghép nối (Coupling)** | Cao (View -> Model) | Thấp (Thông qua Interface) | Thấp (Thông qua Binding) |
| **Khả năng Unit Test** | Thấp (Phụ thuộc MonoBehaviour) | Rất Cao (Mock View dễ dàng) | Cao (ViewModel là POCO) |
| **Độ phức tạp mã nguồn** | Thấp | Trung bình (Nhiều Boilerplate) | Cao (Cần hiểu Reactive/Rx) |
| **Hiệu năng (Runtime)** | Cao nhất (Gọi hàm trực tiếp) | Cao (Gọi qua Interface) | Trung bình (Chi phí Event/Delegate) |
| **Khả năng Debug** | Dễ (Call stack rõ ràng) | Dễ | Khó (Stack trace phức tạp do Rx) |
| **Phù hợp nhất cho** | Prototype, Game nhỏ, Game Jam | Gameplay Logic phức tạp, TDD | UI phức tạp (Inventory, Shop) |

### 6.1 Phân Tích Hiệu Năng (Performance Analysis)

-   **MVC/MVP:** Sử dụng lời gọi hàm trực tiếp hoặc qua delegate C#. Chi phí rất thấp, không tạo ra rác bộ nhớ (GC Allocation) đáng kể nếu sử dụng cẩn thận. Phù hợp cho các vòng lặp game chặt chẽ (Update loop).

-   **MVVM (Rx):** Việc tạo ra các Stream (`Observable`), đăng ký (`Subscribe`), và closure (biến cục bộ trong lambda) thường tạo ra GC allocation. Dù R3 đã tối ưu hóa rất nhiều so với UniRx, việc sử dụng MVVM cho logic cập nhật từng khung hình (per-frame logic) của hàng nghìn entity vẫn không được khuyến khích. MVVM nên được giới hạn ở tầng UI (nơi tần suất cập nhật thấp hơn).^21^

* * * * *

7\. Kết Luận và Lời Khuyên Kiến Trúc
------------------------------------

Sự chuyển dịch từ "MonoBehaviour spaghetti" sang kiến trúc có cấu trúc là dấu hiệu của sự trưởng thành trong quy trình phát triển Unity.

1.  **Sử dụng MVC khi:** Bạn làm dự án nhỏ, prototype nhanh, hoặc team chưa quen với các pattern phức tạp. Nó tốt hơn để mọi thứ trong một file, nhưng không đủ linh hoạt cho dự án lớn.

2.  **Sử dụng MVP khi:** Bạn xây dựng các hệ thống Gameplay cốt lõi (Điều khiển nhân vật, Hệ thống chiến đấu). Khả năng tách biệt View qua Interface giúp bạn viết Unit Test cho logic game, đảm bảo độ ổn định cao. Đây là "điểm ngọt" (sweet spot) cân bằng giữa sự rõ ràng và khả năng kiểm soát.

3.  **Sử dụng MVVM khi:** Bạn xây dựng các hệ thống UI phức tạp, nhiều dữ liệu (Màn hình Inventory, Skill Tree, Shop, Settings). Đặc biệt nếu bạn sử dụng **UI Toolkit**, MVVM là lựa chọn bắt buộc để tận dụng sức mạnh của engine. Kết hợp với **R3**, bạn có thể tạo ra các giao diện phản hồi mượt mà với ít mã nguồn nhất.

**Kiến Trúc Lai (Hybrid Architecture):** Trong thực tế sản xuất, hiếm khi một dự án chỉ dùng một pattern. Một kiến trúc phổ biến là sử dụng **MVP cho Gameplay** (để tối ưu hiệu năng và kiểm soát) và **MVVM cho UI** (để tối ưu quy trình phát triển giao diện). Các hệ thống này được kết nối lỏng lẻo thông qua **Event Bus** hoặc **Service Locator**.

Bằng cách hiểu rõ điểm mạnh và yếu của từng mô hình, bạn có thể thiết kế những tựa game không chỉ chạy mượt mà mà còn dễ dàng bảo trì và mở rộng trong tương lai.