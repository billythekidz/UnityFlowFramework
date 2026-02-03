#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Gắn component này vào một GameObject để loại trừ nó và tất cả các con của nó
/// khỏi quá trình tạo mã của UIBind.
/// </summary>
public class UIBindIgnore : MonoBehaviour
{
    // Component này không cần logic. Nó chỉ đóng vai trò là một thẻ đánh dấu.
}
#endif