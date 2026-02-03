
using UnityEngine;

public class FSM_System : MonoBehaviour
{
    #region FSM State Management
    private FSM_State _currentState;
    public FSM_State currentState
    {
        get { return _currentState; }
    }
    public void GoToState(FSM_State newState)
    {
        _currentState?.StateExit();
        _currentState = newState;
        _currentState?.StateEnter();
    
    }
    public void GoToState(FSM_State newState, object data)
    {
        _currentState?.StateExit();
        _currentState = newState;
        _currentState?.StateEnter(data);
    }
    #endregion

    #region Unity MonoBehaviour Methods
    public virtual void Awake() {}
    public virtual void Start() {}
    public virtual void Update()
    {
        _currentState?.StateUpdate();
    }
    public virtual void LateUpdate()
    {
        _currentState?.StateLateUpdate();
    }
    public virtual void FixedUpdate()
    {
        _currentState?.StateFixedUpdate();
    }
    #endregion
}