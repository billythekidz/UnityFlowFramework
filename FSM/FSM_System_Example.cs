
using System;
using UnityEngine;

public class FSM_System_Example : FSM_System
{
    [SerializeField] private EnemyConfig setupConfig;
    public EnemyStats stats;
    public Enemy_FSM_Attack attackState;
    public Enemy_FSM_Avade avadeState;
    public Enemy_FSM_FindAid findAidState;
    public Enemy_FSM_Wander wanderState;
    public Enemy_FSM_Hit hitState;
    public Enemy_FSM_Dead deadState;
    public Material modelColor;
    public Animator animator;
    [NonSerialized] public Transform cacheTransform;
    [NonSerialized] public Transform playerTransform;        
    public override void Awake()
    {
        base.Awake();
        cacheTransform = transform;
        playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        stats = setupConfig.CreateStats();
        CloneMaterial();
    }
    void CloneMaterial()
    {
        var newMaterial = new Material(modelColor);
        modelColor = newMaterial;
        GetComponentInChildren<Renderer>().material = newMaterial;
    }
    public override void Start()
    {
        base.Start();
        // attackState.Setup(this);
        // avadeState.Setup(this);
        // findAidState.Setup(this);
        // wanderState.Setup(this);
        // hitState.Setup(this);
        // deadState.Setup(this);
        GoToState(wanderState);
    }
    public void OnDamage(DamageData data)
    {
        stats.DecreaseHealth(data.damage);
        if (stats.IsAlive == true)
        {
            if (currentState != hitState) GoToState(hitState, data.timeStune);
        } else
        {
            if (currentState != deadState) GoToState(deadState);
        }
    }
    public void OnDead()
    {
        Destroy(gameObject);
    }
}