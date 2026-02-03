
using System;

[Serializable]
public class FSM_State
{
    protected FSM_System _stateMachine;
    protected string _name; public string Name => _name;
    public FSM_State() { }
    public FSM_State(FSM_System stateMachine, string name)
    {
        _stateMachine = stateMachine;
        _name = name;
    }

    public virtual void StateEnter() { }
    public virtual void StateEnter(object data) { }
    public virtual void StateUpdate() { }
    public virtual void StateLateUpdate() { }
    public virtual void StateFixedUpdate() { }
    public virtual void StateExit() { }

}