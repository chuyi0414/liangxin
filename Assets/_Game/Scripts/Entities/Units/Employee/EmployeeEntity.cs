using CYFramework;
using CYFramework.Core.Entity;
using UnityEngine;

/// <summary>
/// 员工实体
/// </summary>
public class EmployeeEntity : EntityBase
{
    public EmployeeRow Data { get; private set; }
    public Collider2D Collider { get; private set; }

    [Header("Runtime")]
    [SerializeField] private float _currentHp;

    protected override void OnEntityInit(object userData)
    {
        base.OnEntityInit(userData);
        // 初始化组件引用，如 Animator, Rigidbody2D, NavMeshAgent 等
        Collider = GetComponent<Collider2D>();
    }

    protected override void OnEntityShow(object userData)
    {
        base.OnEntityShow(userData);
        
        if (userData is EmployeeRow data)
        {
            Data = data;
            // 初始化属性
            _currentHp = Data.Hp;
            
            // 根据 Data.JobTitle, Data.Attack 等设置表现或逻辑
            CY.Log($"[EmployeeEntity] 员工 {Data.Code} ({Data.JobTitle}) 上班了！");
        }
        else
        {
            CY.LogError("[EmployeeEntity] 缺少 EmployeeRow 数据！");
        }
        
        // 注册到 UnitManager
        if (CY.Unit != null)
        {
            CY.Unit.RegisterUnit(this);
        }
    }
    
    protected override void OnEntityHide()
    {
        base.OnEntityHide();
        
        // 从 UnitManager 注销
        if (CY.Unit != null)
        {
            CY.Unit.UnregisterUnit(this);
        }
    }

    protected override void OnEntityUpdate(float deltaTime)
    {
        base.OnEntityUpdate(deltaTime);
        
        // TODO: AI 逻辑 (移动、索敌、攻击)
    }

    /// <summary>
    /// 受伤逻辑
    /// </summary>
    public void TakeDamage(float damage)
    {
        _currentHp -= damage;
        if (_currentHp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        CY.Log($"[EmployeeEntity] 员工 {Data?.Code} 阵亡！");
        CY.Entity.RecycleEntity(this);
    }
}
