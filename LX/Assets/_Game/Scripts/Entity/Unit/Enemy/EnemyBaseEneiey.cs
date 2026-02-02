using Pathfinding;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;
using static UnityEngine.GraphicsBuffer;

public class EnemyBaseEneiey : UnitBaseEntity
{
    /// <summary>
    /// 出生挤出最大迭代次数（防止过度迭代导致卡顿）。
    /// </summary>
    private const int SpawnPushMaxIterations = 4;
    /// <summary>
    /// 出生挤出最小移动阈值平方（低于该值则提前终止）。
    /// </summary>
    private const float SpawnPushMinMoveSqr = 0.01f;
    /// <summary>
    /// 出生挤出重叠检测缓存（NonAlloc，避免频繁 GC）。
    /// </summary>
    private readonly Collider2D[] _spawnOverlapBuffer = new Collider2D[16];
    /// <summary>
    /// 敌人数据
    /// </summary>
    protected DREnemy _dREnemy;

    /// <summary>
    /// AI激活等级（基于最近关键单位距离）
    /// </summary>
    private enum AIActiveLevel
    {
        /// <summary>完全激活</summary>
        Full = 0,
        /// <summary>降低频率</summary>
        LowFrequency = 1,
        /// <summary>简化逻辑</summary>
        Simplified = 2,
        /// <summary>极简逻辑</summary>
        Minimal = 3
    }

    /// <summary>
    /// 当前激活等级
    /// </summary>
    private AIActiveLevel _currentLevel = AIActiveLevel.Full;

    /// <summary>
    /// 上次检测等级时间
    /// </summary>
    private float _lastLevelCheckTime = 0f;

    /// <summary>
    /// 等级检测间隔
    /// </summary>
    private float _levelCheckInterval = 1f;

    /// <summary>
    /// 上次空间查询时间
    /// </summary>
    private float _lastSpatialQueryTime = 0f;

    /// <summary>
    /// 空间查询间隔
    /// </summary>
    private float _spatialQueryInterval = EnemyAIConfig.SpatialQueryInterval;
    /// <summary>
    /// 基础空间查询间隔（随AI等级变化）
    /// </summary>
    private float _baseSpatialQueryInterval = EnemyAIConfig.SpatialQueryInterval;
    /// <summary>
    /// 停止状态下的空间查询间隔（降低频率）
    /// </summary>
    private float _stopSpatialQueryInterval = 0.8f;

    /// <summary>
    /// 是否在 Full 等级也使用空间查询（用于减少触发器物理开销）
    /// </summary>
    private bool _useSpatialQueryInFull = EnemyAIConfig.UseSpatialQueryInFull;

    /// <summary>
    /// 上一次记录的目标位置（用于判断目标是否移动）
    /// </summary>
    private Vector3 _lastTargetPosition;

    /// <summary>
    /// 目标移动触发重算的距离阈值平方（避免频繁开方）
    /// </summary>
    private float _targetMoveSqrThreshold = 0.04f;
    /// <summary>
    /// 随机目标点最小刷新间隔（避免目标轻微移动导致频繁改点）
    /// </summary>
    private float _randomTargetRefreshInterval = 1.2f;
    /// <summary>
    /// 上次随机目标点刷新时间
    /// </summary>
    private float _lastRandomTargetRefreshTime = -999f;

    /// <summary>
    /// 随机目标点（用于分散路径，避免所有敌人指向同一点）
    /// </summary>
    private Transform _randomDestinationTransform;
    /// <summary>
    /// 当前随机目标点所属的目标
    /// </summary>
    private Transform _randomTargetOwner;
    /// <summary>
    /// 上一次记录的随机目标所属目标位置
    /// </summary>
    private Vector3 _randomTargetOwnerLastPos;
    /// <summary>
    /// 当前随机目标点
    /// </summary>
    private Vector3 _randomTargetPoint;
    /// <summary>
    /// 是否已有可复用的随机目标点
    /// </summary>
    private bool _hasRandomTargetPoint = false;
    /// <summary>
    /// 是否处于攻击范围停止状态
    /// </summary>
    private bool _isInAttackStop = false;
    /// <summary>
    /// 是否锁定当前目标（攻击范围内不切换）
    /// </summary>
    private bool _isTargetLocked = false;

    /// <summary>
    /// 可视范围距离（替代触发器半径）
    /// </summary>
    private float _visualScopeDistance = 0f;

    /// <summary>
    /// 攻击范围距离（替代触发器半径）
    /// </summary>
    private float _attackRangeDistance = 0f;

    /// <summary>
    /// 可视范围碰撞体（仅用于读取半径与偏移，不参与物理）
    /// </summary>
    [SerializeField]
    private CircleCollider2D _visualScopeDataCollider;

    /// <summary>
    /// 攻击范围碰撞体（仅用于读取半径与偏移，不参与物理）
    /// </summary>
    [SerializeField]
    private CircleCollider2D _attackRangeDataCollider;

    /// <summary>
    /// 在Scene视图绘制敌人范围（便于调试）
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 如果未初始化数据表，使用0避免错误绘制
        float visual = _visualScopeDistance > 0f ? _visualScopeDistance : 0f;
        float attack = _attackRangeDistance > 0f ? _attackRangeDistance : 0f;

        // 视觉范围（绿色）
        if (visual > 0f)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(GetVisualScopeCenter(), visual);
        }

        // 攻击范围（红色）
        if (attack > 0f)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
            Gizmos.DrawWireSphere(GetAttackRangeCenter(), attack);
        }
    }

    /// <summary>
    /// 判断目标是否进入攻击范围（圆形范围与目标碰撞体的最近点）
    /// </summary>
    /// <param name="target">目标Transform</param>
    /// <returns>是否进入攻击范围</returns>
    private bool IsTargetInAttackRange(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        float range = _attackRangeDistance;
        if (range <= 0f)
        {
            return false;
        }

        Collider2D targetCollider = target.GetComponent<Collider2D>();
        if (targetCollider == null)
        {
            float distSqr = (target.position - GetAttackRangeCenter()).sqrMagnitude;
            return distSqr <= range * range;
        }

        Vector2 closestPoint = targetCollider.ClosestPoint(GetAttackRangeCenter());
        float distToBoxSqr = ((Vector2)GetAttackRangeCenter() - closestPoint).sqrMagnitude;
        return distToBoxSqr <= range * range;
    }

    /// <summary>
    /// 判断目标是否进入可视范围（圆形范围与目标碰撞体的最近点）
    /// </summary>
    /// <param name="target">目标Transform</param>
    /// <returns>是否进入可视范围</returns>
    private bool IsTargetInVisualScope(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        float range = _visualScopeDistance;
        if (range <= 0f)
        {
            return false;
        }

        UnitBaseEntity targetUnit = target.GetComponent<UnitBaseEntity>();
        if (targetUnit == null)
        {
            return false;
        }

        BoxCollider2D targetHurtBox2D = targetUnit.GetHurtBoxCollider();
        if (targetHurtBox2D != null)
        {
            Vector2 closestPoint = targetHurtBox2D.ClosestPoint(GetVisualScopeCenter());
            float distToBoxSqr = ((Vector2)GetVisualScopeCenter() - closestPoint).sqrMagnitude;
            return distToBoxSqr <= range * range;
        }

        return false;
    }

    /// <summary>
    /// 获取可视范围中心点（考虑碰撞体偏移）
    /// </summary>
    private Vector3 GetVisualScopeCenter()
    {
        if (_visualScopeDataCollider == null)
        {
            return transform.position;
        }

        return _visualScopeDataCollider.transform.TransformPoint(_visualScopeDataCollider.offset);
    }

    /// <summary>
    /// 获取攻击范围中心点（考虑碰撞体偏移）
    /// </summary>
    private Vector3 GetAttackRangeCenter()
    {
        if (_attackRangeDataCollider == null)
        {
            return transform.position;
        }

        return _attackRangeDataCollider.transform.TransformPoint(_attackRangeDataCollider.offset);
    }

    protected override void OnInit(object userData)
    {
        base.OnInit(userData);

        _aIDestinationSetter = GetComponent<AIDestinationSetter>();
        _aIPath = GetComponent<AIPath>();
        ApplyConstantSpeedSettings();
        // 禁用自动重算路径，避免每帧内部重复寻路（由我们手动控制）
        _aIPath.autoRepath.mode = AutoRepathPolicy.Mode.Never;
    }

    protected override void OnShow(object userData)
    {
        base.OnShow(userData);
        GameEntry.GameManager.UnitBatchUpdateManager.RegisterUnit(this);

        object[] os = userData as object[];
        transform.position = (Vector3)os[0];
        _dREnemy = (DREnemy)os[1];
        Camp = _dREnemy.Camp;
        _aIPath.maxSpeed = _dREnemy.MoveSeep;
        _visualScopeDistance = _dREnemy.VisualScope;
        _attackRangeDistance = _dREnemy.AttackRange;
        // 如果拖入了范围碰撞体，则读取半径覆盖数据表数值
        if (_visualScopeDataCollider != null)
        {
            _visualScopeDistance = _visualScopeDataCollider.radius;
            _visualScopeDataCollider.enabled = false;
        }
        if (_attackRangeDataCollider != null)
        {
            _attackRangeDistance = _attackRangeDataCollider.radius;
            _attackRangeDataCollider.enabled = false;
        }
        ResolveSpawnOverlap();
        StartAIMoveRandomAroundTarget(GameEntry.GameManager.companyEntity.transform);
        _lastTargetPosition = GameEntry.GameManager.companyEntity.transform.position;
    }

    protected override void OnHide(bool isShutdown, object userData)
    {
        base.OnHide(isShutdown, userData);
        GameEntry.GameManager.UnitBatchUpdateManager.UnregisterUnit(this);
        if (_randomDestinationTransform != null)
        {
            Destroy(_randomDestinationTransform.gameObject);
            _randomDestinationTransform = null;
        }
        _randomTargetOwner = null;
        _hasRandomTargetPoint = false;
        _isInAttackStop = false;
        _isTargetLocked = false;
    }

    /// <summary>
    /// 每帧更新：定时更新AI分级，并按等级执行逻辑。
    /// </summary>
    protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);

        // 定时更新AI等级
        if (Time.time - _lastLevelCheckTime >= _levelCheckInterval)
        {
            UpdateAIActiveLevel();
            _lastLevelCheckTime = Time.time;
        }

        // 根据等级执行逻辑
        if (_currentLevel == AIActiveLevel.Full && _useSpatialQueryInFull)
        {
            UpdateSpatialQuery();
        }
        else if (_currentLevel == AIActiveLevel.LowFrequency)
        {
            UpdateSpatialQuery();
        }
        else if (_currentLevel == AIActiveLevel.Simplified || _currentLevel == AIActiveLevel.Minimal)
        {
            // 远距离：只朝公司移动，不做目标检测
            if (GameEntry.GameManager.companyEntity != null)
            {
                if (_targetTransform != GameEntry.GameManager.companyEntity.transform)
                {
                    _targetTransform = GameEntry.GameManager.companyEntity.transform;
                }
                if (IsTargetInAttackRange(GameEntry.GameManager.companyEntity.transform))
                {
                    StopAIMove();
                }
                else
                {
                    StartAIMoveRandomAroundTarget(GameEntry.GameManager.companyEntity.transform);
                }
                _lastTargetPosition = GameEntry.GameManager.companyEntity.transform.position;
            }
        }

        // 目标移动时重新计算路径（保证跟随移动目标）
        if (_targetTransform != null && _currentLevel != AIActiveLevel.Minimal && !_isTargetLocked)
        {
            Vector3 currentTargetPosition = _targetTransform.position;
            float moveSqr = (currentTargetPosition - _lastTargetPosition).sqrMagnitude;
            if (moveSqr >= _targetMoveSqrThreshold)
            {
                // 如果目标仍在攻击范围内，不需要重算路径
                if (!IsTargetInAttackRange(_targetTransform))
                {
                    StartAIMoveRandomAroundTarget(_targetTransform);
                }
                _lastTargetPosition = currentTargetPosition;
            }
        }
    }

    /// <summary>
    /// 更新AI激活等级（基于最近关键单位距离）
    /// </summary>
    private void UpdateAIActiveLevel()
    {
        float distance = GameEntry.GameManager.GetDistanceToNearestKeyUnit(transform.position);

        AIActiveLevel newLevel;
        if (distance <= EnemyAIConfig.LevelFullDistance)
            newLevel = AIActiveLevel.Full;
        else if (distance <= EnemyAIConfig.LevelLowDistance)
            newLevel = AIActiveLevel.LowFrequency;
        else if (distance <= EnemyAIConfig.LevelSimplifiedDistance)
            newLevel = AIActiveLevel.Simplified;
        else
            newLevel = AIActiveLevel.Minimal;

        if (newLevel != _currentLevel)
        {
            _currentLevel = newLevel;
            ApplyAIActiveLevel();
        }
    }

    /// <summary>
    /// 应用AI等级设置（注意：寻路始终开启，保证绕障碍）
    /// </summary>
    private void ApplyAIActiveLevel()
    {
        // 保证寻路始终启用，防止穿墙
        if (_aIPath != null) _aIPath.enabled = true;

        switch (_currentLevel)
        {
            case AIActiveLevel.Full:
                _pathUpdateInterval = EnemyAIConfig.PathIntervalFull;
                _baseSpatialQueryInterval = EnemyAIConfig.SpatialQueryInterval;
                break;

            case AIActiveLevel.LowFrequency:
                _pathUpdateInterval = EnemyAIConfig.PathIntervalLow;
                _baseSpatialQueryInterval = EnemyAIConfig.SpatialQueryInterval * 1.5f;
                break;

            case AIActiveLevel.Simplified:
                _pathUpdateInterval = EnemyAIConfig.PathIntervalSimplified;
                _baseSpatialQueryInterval = EnemyAIConfig.SpatialQueryInterval * 3f;
                break;

            case AIActiveLevel.Minimal:
                _pathUpdateInterval = EnemyAIConfig.PathIntervalMinimal;
                _baseSpatialQueryInterval = EnemyAIConfig.SpatialQueryInterval * 6f;
                break;
        }
    }

    /// <summary>
    /// 使用空间网格查询附近单位（替代触发器）
    /// </summary>
    private void UpdateSpatialQuery()
    {
        float effectiveInterval = _isInAttackStop ? Mathf.Max(_baseSpatialQueryInterval, _stopSpatialQueryInterval) : _baseSpatialQueryInterval;
        _spatialQueryInterval = effectiveInterval;
        if (Time.time - _lastSpatialQueryTime < _spatialQueryInterval) return;
        _lastSpatialQueryTime = Time.time;

        SpatialGrid grid = GameEntry.GameManager.UnitBatchUpdateManager.GetSpatialGrid();
        if (grid == null) return;

        if (_targetTransform != null && IsTargetInAttackRange(_targetTransform))
        {
            _isTargetLocked = true;
            _isInAttackStop = true;
            StopAIMove();
            return;
        }
        else
        {
            _isTargetLocked = false;
            _isInAttackStop = false;
        }

        List<UnitBaseEntity> nearbyAttack = grid.GetNearbyCandidates(GetAttackRangeCenter(), _attackRangeDistance);
        _attackRangeUnitList.Clear();
        for (int i = 0; i < nearbyAttack.Count; i++)
        {
            UnitBaseEntity unit = nearbyAttack[i];
            if (unit == null) continue;

            if (unit.Camp == CAMP.Protagonist && IsTargetInAttackRange(unit.transform))
            {
                _attackRangeUnitList.Add(unit);
            }
        }

        List<UnitBaseEntity> nearbyVisual = grid.GetNearbyCandidates(GetVisualScopeCenter(), _visualScopeDistance);
        _visualScopeUnitList.Clear();
        for (int i = 0; i < nearbyVisual.Count; i++)
        {
            UnitBaseEntity unit = nearbyVisual[i];
            if (unit == null) continue;

            if (unit.Camp == CAMP.Protagonist && IsTargetInVisualScope(unit.transform))
            {
                _visualScopeUnitList.Add(unit);
            }
        }

        if (_attackRangeUnitList.Count > 0)
        {
            UnitBaseEntity nearest = GetNearestUnit(_attackRangeUnitList, transform.position);
            if (nearest != null)
            {
                _targetTransform = nearest.transform;
                _isTargetLocked = true;
                _isInAttackStop = true;
                StopAIMove();
                _lastTargetPosition = nearest.transform.position;
                return;
            }
        }

        if (_visualScopeUnitList.Count > 0)
        {
            UnitBaseEntity nearest = GetNearestUnit(_visualScopeUnitList, transform.position);
            if (nearest != null)
            {
                _targetTransform = nearest.transform;
                _isTargetLocked = false;
                _isInAttackStop = false;
                StartAIMoveRandomAroundTarget(nearest.transform);
                _lastTargetPosition = nearest.transform.position;
                return;
            }
        }

        if (GameEntry.GameManager.companyEntity != null)
        {
            _targetTransform = GameEntry.GameManager.companyEntity.transform;
            if (IsTargetInAttackRange(GameEntry.GameManager.companyEntity.transform))
            {
                _isTargetLocked = true;
                _isInAttackStop = true;
                StopAIMove();
            }
            else
            {
                _isTargetLocked = false;
                _isInAttackStop = false;
                StartAIMoveRandomAroundTarget(GameEntry.GameManager.companyEntity.transform);
            }
            _lastTargetPosition = GameEntry.GameManager.companyEntity.transform.position;
        }
    }

    /// <summary>
    /// 让敌人朝目标范围内的随机点移动（分散路径）。
    /// </summary>
    private void StartAIMoveRandomAroundTarget(Transform target)
    {
        if (target == null) return;

        EnsureRandomDestinationTransform();
        bool needNewPoint = !_hasRandomTargetPoint || target != _randomTargetOwner;
        if (!needNewPoint)
        {
            Vector3 currentOwnerPos = target.position;
            float moveSqr = (currentOwnerPos - _randomTargetOwnerLastPos).sqrMagnitude;
            if (moveSqr >= _targetMoveSqrThreshold && Time.time - _lastRandomTargetRefreshTime >= _randomTargetRefreshInterval)
            {
                needNewPoint = true;
            }
        }

        if (needNewPoint)
        {
            _randomTargetPoint = GetRandomPointAroundTarget(target);
            _randomTargetOwner = target;
            _randomTargetOwnerLastPos = target.position;
            _hasRandomTargetPoint = true;
            _lastRandomTargetRefreshTime = Time.time;
        }

        _randomDestinationTransform.position = _randomTargetPoint;
        StartAIMove(_randomDestinationTransform);
    }

    /// <summary>
    /// 确保随机目标点对象存在。
    /// </summary>
    private void EnsureRandomDestinationTransform()
    {
        if (_randomDestinationTransform != null) return;

        GameObject go = new GameObject("EnemyRandomDestination");
        go.hideFlags = HideFlags.HideInHierarchy;
        _randomDestinationTransform = go.transform;
    }

    /// <summary>
    /// 获取目标碰撞体范围内的随机点，并投射到最近可行走节点。
    /// </summary>
    private Vector3 GetRandomPointAroundTarget(Transform target)
    {
        Collider2D targetCollider = null;
        UnitBaseEntity targetUnit = target.GetComponent<UnitBaseEntity>();
        if (targetUnit != null)
        {
            targetCollider = targetUnit.GetHurtBoxCollider();
        }
        if (targetCollider == null)
        {
            targetCollider = target.GetComponent<Collider2D>();
        }

        if (targetCollider != null)
        {
            Bounds bounds = targetCollider.bounds;
            if (bounds.size.sqrMagnitude > 0f)
            {
                float x = Random.Range(bounds.min.x, bounds.max.x);
                float y = Random.Range(bounds.min.y, bounds.max.y);
                Vector3 randomPoint = new Vector3(x, y, target.position.z);
                return GetNearestWalkablePoint(randomPoint);
            }
        }

        return GetNearestWalkablePoint(target.position);
    }

    /// <summary>
    /// 返回离位置最近的可行走点（A*网格节点）。
    /// </summary>
    private Vector3 GetNearestWalkablePoint(Vector3 position)
    {
        if (AstarPath.active == null) return position;

        return (Vector3)AstarPath.active.GetNearest(position).position;
    }

    /// <summary>
    /// 出生时尝试将自身从同层单位重叠位置挤出，避免生成堆叠。
    /// </summary>
    private void ResolveSpawnOverlap()
    {
        Collider2D selfCollider = _hurtBoxCollider != null ? _hurtBoxCollider : GetComponent<Collider2D>();
        if (selfCollider == null || !selfCollider.enabled)
        {
            return;
        }

        Bounds bounds = selfCollider.bounds;
        float selfRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);
        if (selfRadius <= 0f)
        {
            return;
        }

        int sameLayerMask = 1 << gameObject.layer;
        Vector2 currentPos = transform.position;

        for (int iteration = 0; iteration < SpawnPushMaxIterations; iteration++)
        {
            int overlapCount = Physics2D.OverlapCircleNonAlloc(
                currentPos,
                selfRadius * 2f,
                _spawnOverlapBuffer,
                sameLayerMask);

            if (overlapCount <= 0)
            {
                break;
            }

            Vector2 repel = Vector2.zero;
            int hitCount = 0;

            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D hit = _spawnOverlapBuffer[i];
                if (hit == null || hit == selfCollider || !hit.enabled)
                {
                    continue;
                }

                Bounds hitBounds = hit.bounds;
                float hitRadius = Mathf.Max(hitBounds.extents.x, hitBounds.extents.y);
                if (hitRadius <= 0f)
                {
                    continue;
                }

                Vector2 otherPos = hitBounds.center;
                Vector2 diff = currentPos - otherPos;
                float dist = diff.magnitude;
                float minDist = selfRadius + hitRadius;

                if (dist < 0.0001f)
                {
                    diff = Random.insideUnitCircle.normalized;
                    dist = 0f;
                }

                if (dist < minDist)
                {
                    float push = minDist - dist;
                    repel += diff.normalized * push;
                    hitCount++;
                }
            }

            if (hitCount <= 0)
            {
                break;
            }

            repel /= hitCount;
            if (repel.sqrMagnitude <= SpawnPushMinMoveSqr)
            {
                break;
            }

            currentPos += repel;
        }

        transform.position = new Vector3(currentPos.x, currentPos.y, transform.position.z);
    }

}
