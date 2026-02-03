using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Frameworks.FSM_UniTask
{
    /// <summary>
    /// Một lớp State trừu tượng có khả năng chứa và quản lý một hệ thống FSM con (sub-states).
    /// Đây là nền tảng cho Hierarchical State Machine (HSM).
    /// </summary>
    public abstract class FSM_StateParent : FSM_State
    {
        private FSM_SubState _currentSubState;
        public FSM_SubState CurrentSubState => _currentSubState;
        private CancellationTokenSource _subStateCts;
    
        public FSM_StateParent(FSM_System stateMachine) : base(stateMachine)
        {
        }

        /// <summary>
        /// Khi State cha bắt đầu, tạo một CancellationTokenSource mới cho các sub-state.
        /// </summary>
        public override UniTask StateEnter(CancellationToken cancellationToken)
        {
            _subStateCts = new CancellationTokenSource();
            return base.StateEnter(cancellationToken);
        }

        /// <summary>
        /// Khi State cha kết thúc, hủy CancellationTokenSource của sub-state và thoát khỏi sub-state hiện tại.
        /// </summary>
        public override async UniTask StateExit()
        {
            _subStateCts?.Cancel();
            _subStateCts?.Dispose();

            if (_currentSubState != null)
            {
                await _currentSubState.StateExit();
                _currentSubState = null;
            }
        }

        /// <summary>
        /// Truyền tải lời gọi Update xuống cho sub-state hiện tại.
        /// </summary>
        public override UniTask StateUpdate(CancellationToken cancellationToken)
        {
            return _currentSubState?.StateUpdate(_subStateCts.Token) ?? UniTask.CompletedTask;
        }

        // (Bạn có thể làm tương tự cho StateLateUpdate, StateFixedUpdate nếu cần)

        /// <summary>
        /// Chuyển đổi sang một sub-state mới.
        /// </summary>
        public async UniTask GoToSubState(FSM_SubState newSubState)
        {
            if (_currentSubState == newSubState) return;

            if (_currentSubState != null)
            {
                await _currentSubState.StateExit();
            }

            _currentSubState = newSubState;
            Debug.Log($"[FSM] Parent State '{this.GetType().Name}' entering Sub-State: '{newSubState.GetType().Name}'");
            await _currentSubState.StateEnter(_subStateCts.Token);
        }

        /// <summary>
        /// Chuyển đổi sang một sub-state mới và truyền dữ liệu vào.
        /// </summary>
        public async UniTask GoToSubState<T>(FSM_SubState newSubState, T data)
        {
            await GoToSubState(newSubState); // Tái sử dụng logic chuyển state cơ bản
        
            // Sau khi đã vào state, gọi lại StateEnter với dữ liệu
            await _currentSubState.StateEnter(data, _subStateCts.Token);
        }
    }
}
