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
    /// 视觉范围标识字符串（保留字段，当前使用空间查询替代触发器）。
    /// </summary>
    protected string _visualScopeTriggerId = "VisualScope";
    /// <summary>
    /// 攻击范围标识字符串（保留字段，当前使用空间查询替代触发器）。
    /// </summary>
    protected string _attackRangeTriggerId = "AttackRange";
    /// <summary>
    /// 单位阵营
    /// </summary>
    public CAMP Camp {  get; protected set; }
    /// <summary>
    /// 实体受伤盒（代表攻击/子弹可命中的区域）
    /// </summary>
    [SerializeField]
    protected BoxCollider2D _hurtBoxCollider;
    /// <summary>
    /// 获取实体受伤盒
    /// </summary>
    public BoxCollider2D GetHurtBoxCollider()
    {
        return _hurtBoxCollider;
    }
    /// <summary>
    /// 攻击范围的单位
    /// </summary>
    protected List<UnitBaseEntity> _attackRangeUnitList = new List<UnitBaseEntity>();
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
    /// <summary>
    /// 实体组件缓存：避免频繁 GetComponent 导致性能下降。
    /// </summary>
    private static Dictionary<GameObject, UnitBaseEntity> _entityCache = new Dictionary<GameObject, UnitBaseEntity>();

    /// <summary>
    /// 上一次路径更新的时间（用于限制寻路频率）。
    /// </summary>
    protected float _lastPathUpdateTime = 0f;

    /// <summary>
    /// 路径更新最小间隔（秒），默认0.5秒。
    /// </summary>
    protected float _pathUpdateInterval = 0.5f;

    protected override void OnShow(object userData)
    {
        base.OnShow(userData);

        _isAttack = false;
        _isAttacking = false;
        // 注册到缓存
        _entityCache[gameObject] = this;
    }

	protected override void OnHide(bool isShutdown, object userData)
	{
		base.OnHide(isShutdown, userData);
        // 从缓存移除
        _entityCache.Remove(gameObject);
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

    /// <summary>
    /// 开始 A* 移动（带寻路频率限制）。
    /// </summary>
    /// <param name="target">目标 Transform。</param>
    protected void StartAIMove(Transform target)
    {
        if (target == null) return;

        _aIPath.isStopped = false;          
        _aIDestinationSetter.target = target;
        _aIPath.destination = target.position;

        // 限制寻路频率，避免每帧计算路径
        float now = Time.time;
        if (now - _lastPathUpdateTime >= _pathUpdateInterval)
        {
        GameEntry.FlowFieldManager.Enqueue(() =>
        {
            _aIPath.SearchPath();
        }, this);

            _lastPathUpdateTime = now;
        }
    }

    /// <summary>
    /// 获取缓存的实体组件，避免频繁 GetComponent 调用。
    /// </summary>
    /// <param name="go">目标 GameObject。</param>
    /// <returns>UnitBaseEntity 实例，如果不存在则返回 null。</returns>
    public static UnitBaseEntity GetCachedEntity(GameObject go)
    {
        if (go == null) return null;

        if (!_entityCache.TryGetValue(go, out var entity))
        {
            entity = go.GetComponent<UnitBaseEntity>();
            if (entity != null)
            {
                _entityCache[go] = entity;
            }
        }

        return entity;
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