using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Frameworks.FSM_UniTask
{
    public class FSM_System : MonoBehaviour
    {
        private FSM_State _currentState;
        public FSM_State CurrentState => _currentState;

        private CancellationTokenSource _cancellationTokenSource;
    
        public async UniTask GoToState(FSM_State newState)
        {
            // Cancel và đợi các loop cũ kết thúc
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                await UniTask.Yield(); // Cho các loop cũ kết thúc
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            if (_currentState != null)
            {
                await _currentState.StateExit();
            }

            _currentState = newState;

            if (_currentState != null)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;

                await _currentState.StateEnter(token);

                // Start async updates if the state is still active
                if (!token.IsCancellationRequested && _currentState == newState)
                {
                    AsyncUpdateLoop(token).Forget();
                    AsyncLateUpdateLoop(token).Forget();
                    AsyncFixedUpdateLoop(token).Forget();
                }
            }
        }

        public async UniTask GoToState(FSM_State newState, object data)
        {
            // Cancel và đợi các loop cũ kết thúc
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                await UniTask.Yield(); // Cho các loop cũ kết thúc
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            if (_currentState != null)
            {
                await _currentState.StateExit();
            }

            _currentState = newState;
            if (_currentState != null)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;

                await _currentState.StateEnter(data, token);
            
                if (!token.IsCancellationRequested && _currentState == newState)
                {
                    AsyncUpdateLoop(token).Forget();
                    AsyncLateUpdateLoop(token).Forget();
                    AsyncFixedUpdateLoop(token).Forget();
                }
            }
        }

        private async UniTaskVoid AsyncUpdateLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                
                    var currentState = _currentState;
                    if (!token.IsCancellationRequested && currentState != null)
                    {
                        await currentState.StateUpdate(token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FSM_System] Error in AsyncUpdateLoop: {ex}");
            }
        }
    
        private async UniTaskVoid AsyncLateUpdateLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, token);
                
                    var currentState = _currentState;
                    if (!token.IsCancellationRequested && currentState != null)
                    {
                        await currentState.StateLateUpdate(token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FSM_System] Error in AsyncLateUpdateLoop: {ex}");
            }
        }
    
        private async UniTaskVoid AsyncFixedUpdateLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await UniTask.Yield(PlayerLoopTiming.FixedUpdate, token);
                
                    var currentState = _currentState;
                    if (!token.IsCancellationRequested && currentState != null)
                    {
                        await currentState.StateFixedUpdate(token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FSM_System] Error in AsyncFixedUpdateLoop: {ex}");
            }
        }
        #region Unity MonoBehaviour Methods
        public virtual void Awake() { }

        public virtual void Start() { }

        public virtual void OnDestroy()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
        #endregion
    }
}
