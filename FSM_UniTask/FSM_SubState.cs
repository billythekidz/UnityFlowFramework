using System.Threading;
using Cysharp.Threading.Tasks;

namespace Frameworks.FSM_UniTask
{
    public abstract class FSM_SubState
    {
        protected FSM_State _parentState;

        public FSM_SubState(FSM_State parentState)
        {
            this._parentState = parentState;
        }

        public virtual UniTask StateEnter(CancellationToken cancellationToken) => UniTask.CompletedTask;
        public virtual UniTask StateEnter<T>(T data, CancellationToken cancellationToken) => UniTask.CompletedTask;
        public virtual UniTask StateUpdate(CancellationToken cancellationToken) => UniTask.CompletedTask;
        public virtual UniTask StateLateUpdate(CancellationToken cancellationToken) => UniTask.CompletedTask;
        public virtual UniTask StateFixedUpdate(CancellationToken cancellationToken) => UniTask.CompletedTask;
        public virtual UniTask StateExit() => UniTask.CompletedTask;
    }
}
