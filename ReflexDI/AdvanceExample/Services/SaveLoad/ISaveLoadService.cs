namespace TowerDefence.Reflex.Project.Services
{
    /// <summary>
    /// Interface (hợp đồng) cho một dịch vụ có khả năng lưu và tải dữ liệu.
    /// Bất kỳ lớp nào triển khai interface này đều phải cung cấp 2 phương thức Save và Load.
    /// </summary>
    public interface ISaveLoadService
    {
        void Save<T>(string key, T data);
        T Load<T>(string key, T defaultValue = default);
    }
}
