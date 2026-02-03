// using Cysharp.Threading.Tasks;
// using System.Threading;


// public abstract class FSM_State_UniTask
// {
//     protected readonly FSM_System _stateMachine;

//     public FSM_State_UniTask(FSM_System stateMachine)
//     {
//         _stateMachine = stateMachine;
//     }

//     public virtual UniTask StateEnter(CancellationToken cancellationToken) => UniTask.CompletedTask;
//     public virtual UniTask StateEnter<T>(T data, CancellationToken cancellationToken) => UniTask.CompletedTask;
//     public virtual UniTask StateUpdate(CancellationToken cancellationToken) => UniTask.CompletedTask;
//     public virtual UniTask StateLateUpdate(CancellationToken cancellationToken) => UniTask.CompletedTask;
//     public virtual UniTask StateFixedUpdate(CancellationToken cancellationToken) => UniTask.CompletedTask;
//     public virtual UniTask StateExit() => UniTask.CompletedTask;
// }
