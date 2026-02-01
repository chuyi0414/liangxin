using GameFramework.DataTable;
using Pathfinding;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityGameFramework.Runtime;
using static UnityEngine.GraphicsBuffer;

/// <summary>
/// 单位实体基类
/// </summary>
public class UnitBaseEntity : EntityLogic
{
    /// <summary>
    /// 重力
    /// </summary>
    [SerializeField]
    protected Rigidbody2D _rigidbody2D;
    /// <summary>
    /// 移动向量
    /// </summary>
    protected Vector2 _moveInput;
    //当前子弹
    protected DRProjectile _dRProjectile;
    /// <summary>
    /// A* 自带的目标设置组件
    /// </summary>
    protected AIDestinationSetter _aIDestinationSetter;
    /// <summary>
    /// A*的寻路组件
    /// </summary>
    protected AIPath _aIPath;
    /// <summary>
    /// 视觉范围触发器的标识字符串，需要与子物体 UnitTriggerProxy 的 TriggerId 一致。
    /// </summary>
    protected string _visualScopeTriggerId = "VisualScope";
    /// <summary>
    /// 攻击范围触发器的标识字符串，需要与子物体 UnitTriggerProxy 的 TriggerId 一致。
    /// </summary>
    protected string _attackRangeTriggerId = "AttackRange";
    /// <summary>
    /// 单位阵营
    /// </summary>
    public CAMP Camp {  get; protected set; }
    /// <summary>
    /// 可视范围
    /// </summary>
    [SerializeField] protected CircleCollider2D _attackRangeCollider;
    /// <summary>
    /// 攻击范围的单位
    /// </summary>
    protected List<UnitBaseEntity> _attackRangeUnitList = new List<UnitBaseEntity>();
    /// <summary>
    /// 攻击范围
    /// </summary>
    [SerializeField] protected CircleCollider2D _visualScopeCollider;
    /// <summary>
    /// 可视范围的单位
    /// </summary>
    protected List<UnitBaseEntity> _visualScopeUnitList = new List<UnitBaseEntity>();
    /// <summary>
    /// 目标
    /// </summary>
    protected Transform _targetTransform;
    /// <summary>
    /// 是否能攻击
    /// </summary>
    protected bool _isAttack;
    /// <summary>
    /// 是否正在攻击
    /// </summary>
    protected bool _isAttacking;
    /// <summary>
    /// 已经经过的攻击间隔
    /// </summary>
    protected float _attackElapsedTime;

    protected override void OnShow(object userData)
    {
        base.OnShow(userData);

        _isAttack = false;
        _isAttacking = false;
    }

    /// <summary>
    /// 让AIPath以近似匀速运动的设置方法
    /// </summary>
    protected void ApplyConstantSpeedSettings()
    {
        // 加速度设很大，几乎立即达到最大速度
        _aIPath.maxAcceleration = 1000f;

        // 接近目标不减速
        _aIPath.slowdownDistance = 0f;

        // 转向时不减速
        _aIPath.slowWhenNotFacingTarget = false;
    }

    protected UnitBaseEntity GetNearestUnit(List<UnitBaseEntity> list, Vector3 selfPos)
    {
        UnitBaseEntity nearest = null;
        float minSqr = float.MaxValue;

        for (int i = 0; i < list.Count; i++)
        {
            var u = list[i];
            if (u == null) continue;

            float sqr = (u.transform.position - selfPos).sqrMagnitude;
            if (sqr < minSqr)
            {
                minSqr = sqr;
                nearest = u;
            }
        }

        return nearest;
    }

    /// <summary>
    /// 停止A*移动并清空旧路径
    /// </summary>
    protected void StopAIMove()
    {
        _aIDestinationSetter.target = null;
        _aIPath.isStopped = true;           // 平滑停止
        _aIPath.SetPath(null);              // 清空旧路径，避免继续走
        _aIPath.destination = transform.position;  // 可选：锁定目的地
        _aIPath.desiredVelocityWithoutLocalAvoidance = Vector3.zero; // 立即停
    }

    protected void StartAIMove(Transform target)
    {
        if(target == null)
          return;

        _aIPath.isStopped = false;                  // 恢复移动
        _aIDestinationSetter.target = target;
        _aIPath.destination = target.position;      // 强制设置目的地

        GameEntry.FlowFieldManager.Enqueue(() =>
        {
            _aIPath.SearchPath();
        });
    }
}

/// <summary>
/// 阵营
/// </summary>
public enum CAMP
{
    Protagonist = 1,
    Enemy = 2,
    NPC = 3
}

public enum ATTACKTYPE
{
    JinZhan = 1,
    YuanCheng = 2,
}