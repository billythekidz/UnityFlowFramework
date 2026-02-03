using UnityEngine;
using R3;

/// <summary>
/// Example demonstrating NewFody usage for a player character ViewModel.
/// This example shows:
/// 1. Basic property change notifications
/// 2. Subscribing to property changes
/// 3. Integration with Unity MonoBehaviour
/// 4. Manual notifications for computed properties
/// </summary>
namespace R3.Examples
{
    // ============================================================
    // MODEL - Giờ đây sử dụng ReactiveProperty của R3 để tự thông báo thay đổi.
    // ============================================================
    public class PlayerModel
    {
        public ReactiveProperty<string> Name { get; } = new();
        public ReactiveProperty<int> Level { get; } = new();
        public ReactiveProperty<float> Health { get; } = new();
        public ReactiveProperty<float> MaxHealth { get; } = new();
        public ReactiveProperty<int> Experience { get; } = new();
        public ReactiveProperty<int> Gold { get; } = new();
    }

    // ============================================================
    // VIEWMODEL - Lớp trung gian, chuyển đổi dữ liệu từ Model thành các luồng chỉ đọc (ReadOnly) cho View.
    // ============================================================
    public class PlayerViewModel
    {
        private readonly PlayerModel _model;

        // Phơi bày các thuộc tính dưới dạng chỉ đọc để View không thể thay đổi trực tiếp.
        public ReadOnlyReactiveProperty<string> Name => _model.Name;
        public ReadOnlyReactiveProperty<int> Level => _model.Level;
        public ReadOnlyReactiveProperty<float> Health => _model.Health;
        public ReadOnlyReactiveProperty<float> MaxHealth => _model.MaxHealth;
        public ReadOnlyReactiveProperty<int> Experience => _model.Experience;
        public ReadOnlyReactiveProperty<int> Gold => _model.Gold;

        // Thuộc tính tính toán: Tự động cập nhật khi Health hoặc MaxHealth thay đổi.
        public ReadOnlyReactiveProperty<float> HealthPercentage { get; }

        // Constructor
        public PlayerViewModel(PlayerModel model)
        {
            _model = model;

            // Khai báo mối quan hệ: HealthPercentage là kết quả của việc kết hợp Health và MaxHealth.
            // Không cần phải quản lý việc cập nhật thủ công nữa.
            HealthPercentage = model.Health
                .CombineLatest(model.MaxHealth, (h, max) => max > 0 ? h / max : 0f)
                .ToReadOnlyReactiveProperty();
        }

        // Các phương thức công khai để View tương tác, chúng sẽ thay đổi Model.
        public void TakeDamage(float damage)
        {
            _model.Health.Value = Mathf.Max(0, _model.Health.Value - damage);
        }

        public void Heal(float amount)
        {
            _model.Health.Value = Mathf.Min(_model.MaxHealth.Value, _model.Health.Value - amount);
        }

        public void GainExperience(int amount)
        {
            _model.Experience.Value += amount;

            // Level up every 100 experience
            int newLevel = _model.Experience.Value / 100 + 1;
            if (newLevel > _model.Level.Value)
            {
                _model.Level.Value = newLevel;
                _model.MaxHealth.Value = 100f + (_model.Level.Value - 1) * 20f; // Increase max health on level up
                _model.Health.Value = _model.MaxHealth.Value; // Restore to full health

                Debug.Log($"{Name.CurrentValue} leveled up to Level {Level.CurrentValue}!");
            }
        }

        public void AddGold(int amount)
        {
            _model.Gold.Value += amount;
        }

        public void ChangeName(string newName)
        {
            _model.Name.Value = newName;
        }
    }

    // ============================================================
    // VIEW - MonoBehaviour hiển thị dữ liệu và nhận input.
    // Giờ đây nó chỉ "đăng ký" vào các luồng dữ liệu và không cần quản lý state phức tạp.
    // ============================================================

    public class PlayerView : MonoBehaviour
    {
        private PlayerViewModel _player;

        void Start()
        {
            // 1. Tạo Model để chứa dữ liệu thô
            var playerModel = new PlayerModel
            {
                Name = { Value = "Hero" },
                Level = { Value = 1 },
                MaxHealth = { Value = 100f },
                Health = { Value = 100f },
                Experience = { Value = 0 },
                Gold = { Value = 0 }
            };
            // 2. Tạo ViewModel và truyền Model vào
            _player = new PlayerViewModel(playerModel);

            // --- Data Binding với R3 ---
            // Đăng ký vào các luồng dữ liệu và cập nhật UI tương ứng.
            // .AddTo(this) sẽ tự động hủy đăng ký khi GameObject này bị destroy.
            _player.Name.Subscribe(UpdateNameDisplay).AddTo(this);
            _player.Level.Subscribe(UpdateLevelDisplay).AddTo(this);
            _player.Gold.Subscribe(UpdateGoldDisplay).AddTo(this);
            _player.HealthPercentage.Subscribe(UpdateHealthBar).AddTo(this);

            // Log initial state
            Debug.Log($"Player created: {_player.Name.CurrentValue}, Level {_player.Level.CurrentValue}, HP: {_player.Health.CurrentValue}/{_player.MaxHealth.CurrentValue}");
        }

        void Update()
        {
            // Example: Press keys to test the ViewModel
            if (Input.GetKeyDown(KeyCode.D))
            {
                _player.TakeDamage(20f); // View chỉ gọi phương thức, không quan tâm logic bên trong
            }

            if (Input.GetKeyDown(KeyCode.H))
            {
                _player.Heal(15f); // Logic hồi máu được xử lý trong ViewModel/Model
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                _player.GainExperience(50); // Logic lên cấp được xử lý trong ViewModel/Model
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                _player.AddGold(10); // Logic cộng tiền được xử lý trong ViewModel/Model
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                _player.ChangeName("Renamed Hero"); // Logic đổi tên được xử lý trong ViewModel/Model
            }
        }

        // UI update methods (would update actual UI in a real game)
        // Các phương thức này giờ nhận giá trị trực tiếp từ subscription.
        private void UpdateHealthBar(float percentage)
        {
            Debug.Log($"[UI] Update Health Bar: {percentage:P0}");
        }

        private void UpdateLevelDisplay(int level)
        {
            Debug.Log($"[UI] Update Level Display: Level {level}");
        }

        private void UpdateGoldDisplay(int gold)
        {
            Debug.Log($"[UI] Update Gold Display: {gold} gold");
        }

        private void UpdateNameDisplay(string name)
        {
            Debug.Log($"[UI] Update Name Display: {name}");
        }
    }

    // ============================================================
    // ADDITIONAL EXAMPLES
    // ============================================================

    /// <summary>
    /// Example: Inventory system with multiple related properties
    /// </summary>
    public class Inventory
    {
        public ReactiveProperty<int> WeaponSlots { get; } = new();
        public ReactiveProperty<int> ArmorSlots { get; } = new();
        public ReactiveProperty<int> PotionCount { get; } = new();
        public ReactiveProperty<float> CarryWeight { get; } = new();
        public ReactiveProperty<float> MaxCarryWeight { get; } = new();

        public ReadOnlyReactiveProperty<bool> IsOverweight { get; }

        public Inventory()
        {
            IsOverweight = CarryWeight
                .CombineLatest(MaxCarryWeight, (current, max) => current > max)
                .ToReadOnlyReactiveProperty();
        }

        public void AddItem(float weight)
        {
            CarryWeight.Value += weight;
        }

        public void RemoveItem(float weight)
        {
            CarryWeight.Value = Mathf.Max(0, CarryWeight.Value - weight);
        }
    }

    /// <summary>
    /// Example: Quest system with state tracking
    /// </summary>
    public class Quest
    {
        public ReactiveProperty<string> QuestName { get; } = new();
        public ReactiveProperty<string> Description { get; } = new();
        public ReactiveProperty<int> CurrentProgress { get; } = new();
        public ReactiveProperty<int> RequiredProgress { get; } = new();
        public ReactiveProperty<bool> IsCompleted { get; } = new();
        public ReactiveProperty<int> RewardGold { get; } = new();
        public ReactiveProperty<int> RewardExperience { get; } = new();

        public ReadOnlyReactiveProperty<float> CompletionPercentage { get; }

        public Quest()
        {
            CompletionPercentage = CurrentProgress
                .CombineLatest(RequiredProgress, (current, req) => req > 0 ? (float)current / req : 0f)
                .ToReadOnlyReactiveProperty();

            // Tự động đánh dấu hoàn thành
            CompletionPercentage
                .Where(p => p >= 1.0f && !IsCompleted.CurrentValue)
                .Subscribe(_ =>
                {
                    IsCompleted.Value = true;
                    Debug.Log($"Quest '{QuestName.CurrentValue}' completed!");
                });
        }

        public void UpdateProgress(int amount)
        {
            CurrentProgress.Value = Mathf.Min(RequiredProgress.Value, CurrentProgress.Value + amount);
        }
    }
}