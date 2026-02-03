using System.Threading;
using Cysharp.Threading.Tasks;

namespace Frameworks.FSM_UniTask
{
    public abstract class FSM_State
    {
        protected readonly FSM_System _stateMachine;

        public FSM_State(FSM_System stateMachine)
        {
            _stateMachine = stateMachine;
        }

        public virtual UniTask StateEnter(CancellationToken cancellationToken) => UniTask.CompletedTask;
        public virtual UniTask StateEnter<T>(T data, CancellationToken cancellationToken) => UniTask.CompletedTask;
        public virtual UniTask StateUpdate(CancellationToken cancellationToken) => UniTask.CompletedTask;
        public virtual UniTask StateLateUpdate(CancellationToken cancellationToken) => UniTask.CompletedTask;
        public virtual UniTask StateFixedUpdate(CancellationToken cancellationToken) => UniTask.CompletedTask;
        public virtual UniTask StateExit() => UniTask.CompletedTask;
    }
}
