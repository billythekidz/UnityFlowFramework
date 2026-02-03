// using Cysharp.Threading.Tasks;
// using System.Threading;
// using UnityEngine;
// using UnityEngine.UI;

// // --- Example States (defined in the same file for simplicity) ---

// /// <summary>
// /// State: Wanders around randomly.
// /// </summary>
// public class WanderStateAsync : FSM_State_UniTask
// {
//     private readonly FSM_System_UniTask _fsm;
//     private readonly Transform _transform;
//     private readonly Text _statusText;

//     public WanderStateAsync(FSM_System_UniTask fsm, Transform transform, Text statusText)
//     {
//         _fsm = fsm;
//         _transform = transform;
//         _statusText = statusText;
//     }

//     public override async UniTask StateEnter(CancellationToken cancellationToken)
//     {
//         if(_statusText) _statusText.text = "Wandering...";
        
//         // Loop until cancelled
//         while (!cancellationToken.IsCancellationRequested)
//         {
//             // Move to a random position
//             Vector3 randomPos = _transform.position + new Vector3(Random.Range(-2f, 2f), Random.Range(-1f, 1f), 0);
//             float moveDuration = 1.0f;
//             float timer = 0f;

//             Vector3 startPos = _transform.position;
//             while (timer < moveDuration)
//             {
//                 if (cancellationToken.IsCancellationRequested) return; // Exit if cancelled
                
//                 timer += Time.deltaTime;
//                 _transform.position = Vector3.Lerp(startPos, randomPos, timer / moveDuration);
//                 await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
//             }

//             // Wait for a second before wandering again
//             await UniTask.Delay(1000, cancellationToken: cancellationToken);
//         }
//     }

//     public override async UniTask StateExit()
//     {
//         if(_statusText) _statusText.text = "Stopped Wandering";
//         await UniTask.CompletedTask;
//     }
// }

// /// <summary>
// /// State: Simulates an attack with a delay.
// /// </summary>
// public class AttackStateAsync : FSM_State_UniTask
// {
//     private readonly FSM_System_UniTask _fsm;
//     private readonly Text _statusText;

//     public AttackStateAsync(FSM_System_UniTask fsm, Text statusText)
//     {
//         _fsm = fsm;
//         _statusText = statusText;
//     }

//     public override async UniTask StateEnter(CancellationToken cancellationToken)
//     {
//         if(_statusText) _statusText.text = "Attacking!";
        
//         // Simulate a "charge up" time for the attack
//         await UniTask.Delay(500, cancellationToken: cancellationToken);
        
//         Debug.Log("ATTACK!"); // Perform the attack action
        
//         await UniTask.Delay(1000, cancellationToken: cancellationToken);
        
//         // Transition back to wandering
//         await _fsm.GoToState(new WanderStateAsync(_fsm, _fsm.transform, _statusText));
//     }
// }

// /// <summary>
// /// State: A simple state for when the entity is hit.
// /// </summary>
// public class HitStateAsync : FSM_State_UniTask
// {
//     private readonly FSM_System_UniTask _fsm;
//     private readonly Text _statusText;

//     public HitStateAsync(FSM_System_UniTask fsm, Text statusText)
//     {
//         _fsm = fsm;
//         _statusText = statusText;
//     }

//     public override async UniTask StateEnter(CancellationToken cancellationToken)
//     {
//         if(_statusText) _statusText.text = "Ouch!";
        
//         // Stunned for a short period
//         await UniTask.Delay(750, cancellationToken: cancellationToken);

//         // Go back to wandering
//         await _fsm.GoToState(new WanderStateAsync(_fsm, _fsm.transform, _statusText));
//     }
// }


// // --- Main Example Controller ---

// [RequireComponent(typeof(FSM_System_UniTask))]
// public class FSM_System_UniTask_Example : MonoBehaviour
// {
//     private FSM_System_UniTask _fsm;
    
//     [Header("UI For Example")]
//     [SerializeField] private Text _statusText;
//     [SerializeField] private Button _attackButton;
//     [SerializeField] private Button _hitButton;
    
//     private async void Start()
//     {
//         _fsm = GetComponent<FSM_System_UniTask>();
        
//         // Setup button listeners
//         if(_attackButton) _attackButton.onClick.AddListener(() => _fsm.GoToState(new AttackStateAsync(_fsm, _statusText)).Forget());
//         if(_hitButton) _hitButton.onClick.AddListener(() => _fsm.GoToState(new HitStateAsync(_fsm, _statusText)).Forget());

//         // Set the initial state
//         await _fsm.GoToState(new WanderStateAsync(_fsm, transform, _statusText));
//     }

//     private void OnDestroy()
//     {
//         // Clean up listeners
//         if(_attackButton) _attackButton.onClick.RemoveAllListeners();
//         if(_hitButton) _hitButton.onClick.RemoveAllListeners();
//     }
// }