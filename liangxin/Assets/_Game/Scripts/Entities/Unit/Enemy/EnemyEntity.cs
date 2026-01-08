// 引用 CYFramework 命名空间，使用框架统一入口
using CYFramework;
// 引用实体系统命名空间，使用 EntityBase 等类型
using CYFramework.Core.Entity;
// 引用 UnityEngine 命名空间，使用 Transform/Vector/Time/Gizmos
using UnityEngine;

/// <summary>
/// 敌人单位实体：提供可切换的 AI 入口与移动执行能力。
/// </summary>
[RequireComponent(typeof(HybridNavigationAgent))] // 约束必须挂载导航组件
[EntityPrefab("Prefabs/Entities/Unit/Enemy/EnemyEntity", "EnemyEntity", "Enemys")] // 绑定实体预制体信息
public sealed class EnemyEntity : UnitEntity // 敌人实体定义
{
    /// <summary>目的地刷新阈值（差值超过该距离会重新寻路）。</summary>
    [SerializeField] private float _destinationRefreshThreshold = 0.5f; // 目的地更新阈值
    /// <summary>身位距离（与目标保持的最小距离，<=0 表示贴近）。</summary>
    [SerializeField] private float _standOffDistance = 1f; // 身位距离配置
    /// <summary>是否显示攻击范围 Gizmos。</summary>
    [SerializeField] private bool _showAttackRangeGizmos = true; // 攻击范围显示开关
    /// <summary>是否始终显示攻击范围（不选中也显示）。</summary>
    [SerializeField] private bool _showAttackRangeAlways = true; // 是否常显攻击范围
    /// <summary>攻击范围 Gizmos 颜色。</summary>
    [SerializeField] private Color _attackRangeGizmosColor = new Color(1f, 0.6f, 0f, 0.8f); // 攻击范围颜色
    /// <summary>是否显示可视范围 Gizmos。</summary>
    [SerializeField] private bool _showSightRangeGizmos = true; // 可视范围显示开关
    /// <summary>是否始终显示可视范围（不选中也显示）。</summary>
    [SerializeField] private bool _showSightRangeAlways = true; // 是否常显可视范围
    /// <summary>可视范围 Gizmos 颜色。</summary>
    [SerializeField] private Color _sightRangeGizmosColor = new Color(0.2f, 0.7f, 1f, 0.8f); // 可视范围颜色
    /// <summary>可视范围（进入范围后允许追击）。</summary>
    [SerializeField] private float _sightRange = 6f; // 可视范围配置
    /// <summary>攻击停顿时长（秒）。</summary>
    [SerializeField] private float _attackStopDuration = 0.5f; // 攻击停顿配置
    /// <summary>污染伤害最小值（绝对值）。</summary>
    [SerializeField] private float _pollutionDamageMin = 0f; // 污染伤害最小值
    /// <summary>污染伤害最大值（绝对值）。</summary>
    [SerializeField] private float _pollutionDamageMax = 0f; // 污染伤害最大值
    /// <summary>金钱掉落概率（0-1）。</summary>
    [SerializeField] private float _moneyDropProb = 0f; // 金钱掉落概率
    /// <summary>金钱掉落最小数量。</summary>
    [SerializeField] private int _moneyDropMin = 0; // 金钱掉落最小数量
    /// <summary>金钱掉落最大数量。</summary>
    [SerializeField] private int _moneyDropMax = 0; // 金钱掉落最大数量
    /// <summary>黑心掉落概率（0-1）。</summary>
    [SerializeField] private float _blackHeartDropProb = 0f; // 黑心掉落概率
    /// <summary>黑心掉落最小数量。</summary>
    [SerializeField] private int _blackHeartDropMin = 0; // 黑心掉落最小数量
    /// <summary>黑心掉落最大数量。</summary>
    [SerializeField] private int _blackHeartDropMax = 0; // 黑心掉落最大数量

    [Header("AI配置")] // Inspector 分组：AI 配置
    [SerializeField] private EnemyAIBase _defaultAI; // 默认敌人 AI 资产

    /// <summary>缓存 Transform，减少高频访问开销。</summary>
    private Transform _cachedTransform; // Transform 缓存
    /// <summary>混合导航代理（NavMesh/A*）。</summary>
    private HybridNavigationAgent _navigationAgent; // 导航代理缓存
    /// <summary>单位管理器缓存（用于获取老板/员工）。</summary>
    private UnitManager _unitManager; // UnitManager 缓存
    /// <summary>上一次目的地。</summary>
    private Vector2 _lastDestination; // 上次目标点
    /// <summary>是否已有目的地。</summary>
    private bool _hasDestination; // 是否已有有效目的地
    /// <summary>运行时默认 AI（无配置资产时创建）。</summary>
    private EnemyAIBase _runtimeDefaultAI; // 运行时 AI 实例
    /// <summary>当前正在使用的 AI。</summary>
    private EnemyAIBase _currentAI; // 当前 AI 缓存
    /// <summary>攻击停顿计时器（秒）。</summary>
    private float _attackLockTimer; // 攻击锁定计时器
    /// <summary>是否已处理死亡回收（防止重复执行）。</summary>
    private bool _hasDeathRecycled; // 死亡回收标记

    /// <summary>当前 AI（只读）。</summary>
    public EnemyAIBase CurrentAI => _currentAI; // 对外只读访问
    /// <summary>可视范围（只读）。</summary>
    public float SightRange => _sightRange; // 对外只读访问
    /// <summary>是否处于攻击停顿期。</summary>
    public bool IsAttackLocked => _attackLockTimer > 0f; // 对外只读访问
    /// <summary>
    /// 实体初始化：缓存组件与管理器引用。
    /// </summary>
    /// <param name="userData">初始化传入的数据。</param>
    protected override void OnEntityInit(object userData) // 实体初始化入口
    {
        base.OnEntityInit(userData); // 调用父类初始化
        _cachedTransform = transform; // 缓存 Transform
        _navigationAgent = GetComponent<HybridNavigationAgent>(); // 缓存导航组件
        _unitManager = CY.Unit; // 缓存单位管理器

        if (_defaultAI == null)
        {
            _runtimeDefaultAI = ScriptableObject.CreateInstance<EnemyAIBasic>(); // 创建运行时默认 AI
            _runtimeDefaultAI.hideFlags = HideFlags.HideAndDontSave; // 避免运行时实例被保存到场景
        }
    }

    /// <summary>
    /// 实体显示：应用数据并重置状态。
    /// </summary>
    /// <param name="userData">显示时传入的数据。</param>
    protected override void OnEntityShow(object userData) // 实体显示入口
    {
        ApplyEnemyData(userData as EnemyUnitRow); // 读取敌人数据行
        base.OnEntityShow(userData); // 调用父类显示
        _hasDestination = false; // 重置目的地标记
        _hasDeathRecycled = false; // 重置死亡回收标记
        SetAI(null, true); // 显示时强制重置为默认 AI
    }

    /// <summary>
    /// 应用敌人配置数据。
    /// </summary>
    /// <param name="row">敌人数据行。</param>
    private void ApplyEnemyData(EnemyUnitRow row) // 数据行应用入口
    {
        if (row == null)
        {
            CY.LogWarning("[EnemyEntity] 缺少敌人数据行，使用默认属性。"); // 输出缺失数据警告
            return; // 缺少数据时直接返回
        }

        var stats = new UnitStats // 组装基础属性结构体
        {
            MaxHp = row.MaxHp, // 最大生命值
            Attack = row.Attack, // 攻击力
            Defense = row.Defense, // 防御力
            DefensePenetration = row.DefensePenetration, // 固定防御穿透
            DefensePenetrationRate = row.DefensePenetrationRate, // 百分比防御穿透
            CritRate = row.CritRate, // 暴击率
            DodgeRate = row.DodgeRate, // 闪避率
            IsRanged = row.IsRanged, // 是否远程
            MoveSpeed = row.MoveSpeed, // 移动速度
            AttackRange = row.AttackRange, // 攻击范围
            AttackInterval = row.AttackInterval // 攻击间隔
        };

        _sightRange = row.SightRange; // 写入可视范围
        _attackStopDuration = row.AttackStopDuration; // 写入攻击停顿
        _pollutionDamageMin = Mathf.Max(0f, row.PollutionDamageMin); // 写入污染伤害最小值
        _pollutionDamageMax = Mathf.Max(_pollutionDamageMin, row.PollutionDamageMax); // 写入污染伤害最大值
        _moneyDropProb = Mathf.Clamp01(row.MoneyDropProb); // 写入金钱掉落概率
        _moneyDropMin = Mathf.Max(0, row.MoneyDropMin); // 写入金钱掉落最小数量
        _moneyDropMax = Mathf.Max(_moneyDropMin, row.MoneyDropMax); // 写入金钱掉落最大数量
        _blackHeartDropProb = Mathf.Clamp01(row.BlackHeartDropProb); // 写入黑心掉落概率
        _blackHeartDropMin = Mathf.Max(0, row.BlackHeartDropMin); // 写入黑心掉落最小数量
        _blackHeartDropMax = Mathf.Max(_blackHeartDropMin, row.BlackHeartDropMax); // 写入黑心掉落最大数量
        ApplyBaseData(row.Id, row.Code, row.Name, row.Camp, row.LifeState, row.Level, stats); // 写入单位基础数据
    }

    /// <summary>
    /// 实体更新：根据距离与攻击范围决定追击目标。
    /// </summary>
    /// <param name="deltaTime">帧时间。</param>
    protected override void OnEntityUpdate(float deltaTime) // 实体更新入口
    {
        base.OnEntityUpdate(deltaTime); // 调用父类更新
        if (LifeState == UnitLifeState.Dead) // 死亡状态优先处理回收
        {
            HandleDeathRecycle(); // 执行死亡回收逻辑
            return; // 死亡后不再执行 AI
        }

        TickAttackLock(deltaTime); // 推进攻击停顿计时
        if (IsAttackLocked)
        {
            StopMovement(); // 攻击期间保持停下
            return; // 停顿期不执行 AI
        }

        if (_currentAI == null)
        {
            SetAI(null, true); // AI 未初始化时回退默认 AI
        }

        if (_currentAI == null)
        {
            return; // 仍为空时直接退出
        }

        _currentAI.Tick(this, deltaTime); // 执行当前 AI 逻辑
    }

    /// <summary>
    /// 处理死亡回收（停止移动、退出 AI、移除管理器引用、回收实体）。
    /// </summary>
    private void HandleDeathRecycle() // 死亡回收入口
    {
        if (_hasDeathRecycled) // 已处理过回收则直接返回
        {
            return; // 避免重复回收
        }

        _hasDeathRecycled = true; // 标记已处理死亡回收
        StopMovement(); // 停止移动避免继续寻路

        if (_currentAI != null) // 存在 AI 时执行退出
        {
            _currentAI.OnExit(this); // 通知 AI 退出
            _currentAI = null; // 清理当前 AI 引用
        }

        if (_unitManager == null) // 管理器为空时尝试重新获取
        {
            _unitManager = CY.Unit; // 重新缓存单位管理器
        }

        if (_unitManager != null) // 管理器有效时移除敌人引用
        {
            _unitManager.RemoveEnemy(this); // 从敌人列表移除
        }

        TryDropMoney(); // 尝试掉落金钱
        TryDropBlackHeart(); // 尝试掉落黑心

        if (Id > 0) // 实体 Id 有效时才回收
        {
            CY.Entity.RecycleEntity(Id); // 交给实体系统回收
        }
    }

    /// <summary>
    /// 尝试掉落金钱实体。
    /// </summary>
    private void TryDropMoney() // 金钱掉落入口
    {
        if (_moneyDropProb <= 0f) // 掉落概率无效时退出
        {
            return; // 概率无效直接返回
        }

        var roll = Random.value; // 获取随机概率
        if (roll > _moneyDropProb) // 未命中掉落概率时退出
        {
            return; // 概率未命中直接返回
        }

        var min = _moneyDropMin; // 读取最小数量
        var max = _moneyDropMax; // 读取最大数量
        if (max < min) // 最大值小于最小值时纠正
        {
            max = min; // 将最大值修正为最小值
        }

        if (max <= 0) // 最大数量无效时退出
        {
            return; // 数量无效直接返回
        }

        var count = min == max ? min : Random.Range(min, max + 1); // 计算掉落数量
        if (count <= 0) // 掉落数量无效时退出
        {
            return; // 掉落数量无效直接返回
        }

        if (_cachedTransform == null) // Transform 缓存缺失时重新获取
        {
            _cachedTransform = transform; // 重新缓存 Transform
        }

        var basePosition = _cachedTransform != null ? _cachedTransform.position : transform.position; // 读取掉落基准位置
        for (int i = 0; i < count; i++) // 按数量生成金币实体
        {
            var moneyEntity = CY.Entity.SpawnEntity<MoneyEntity>(); // 生成金币实体
            if (moneyEntity == null) // 生成失败时跳过
            {
                continue; // 生成失败直接跳过
            }

            var moneyTransform = moneyEntity.transform; // 获取金币 Transform
            var randomOffset = Random.insideUnitCircle * 2f; // 生成半径为 2 的随机偏移
            moneyTransform.position = new Vector3(basePosition.x + randomOffset.x, basePosition.y + randomOffset.y, moneyTransform.position.z); // 设置金币位置并保留 Z
        }
    }

    /// <summary>
    /// 尝试掉落黑心实体。
    /// </summary>
    private void TryDropBlackHeart() // 黑心掉落入口
    {
        if (_blackHeartDropProb <= 0f) // 掉落概率无效时退出
        {
            return; // 概率无效直接返回
        }

        var roll = Random.value; // 获取随机概率
        if (roll > _blackHeartDropProb) // 未命中掉落概率时退出
        {
            return; // 概率未命中直接返回
        }

        var min = _blackHeartDropMin; // 读取最小数量
        var max = _blackHeartDropMax; // 读取最大数量
        if (max < min) // 最大值小于最小值时纠正
        {
            max = min; // 将最大值修正为最小值
        }

        if (max <= 0) // 最大数量无效时退出
        {
            return; // 数量无效直接返回
        }

        var count = min == max ? min : Random.Range(min, max + 1); // 计算掉落数量
        if (count <= 0) // 掉落数量无效时退出
        {
            return; // 掉落数量无效直接返回
        }

        if (_cachedTransform == null) // Transform 缓存缺失时重新获取
        {
            _cachedTransform = transform; // 重新缓存 Transform
        }

        var basePosition = _cachedTransform != null ? _cachedTransform.position : transform.position; // 读取掉落基准位置
        for (int i = 0; i < count; i++) // 按数量生成黑心实体
        {
            var blackHeartEntity = CY.Entity.SpawnEntity<BlackHeartEntity>(); // 生成黑心实体
            if (blackHeartEntity == null) // 生成失败时跳过
            {
                continue; // 生成失败直接跳过
            }

            var blackHeartTransform = blackHeartEntity.transform; // 获取黑心 Transform
            var randomOffset = Random.insideUnitCircle * 2f; // 生成半径为 2 的随机偏移
            blackHeartTransform.position = new Vector3(basePosition.x + randomOffset.x, basePosition.y + randomOffset.y, blackHeartTransform.position.z); // 设置黑心位置并保留 Z
        }
    }

    /// <summary>
    /// 尝试攻击目标并进入停顿期。
    /// </summary>
    /// <param name="target">攻击目标。</param>
    public bool TryAttackTargetWithLock(UnitEntity target) // 攻击入口
    {
        if (!TryAttackTarget(target))
        {
            return false; // 攻击失败时返回
        }

        var duration = _attackStopDuration; // 读取攻击停顿时长
        BeginAttackLock(duration); // 进入攻击停顿期
        return true; // 攻击成功
    }

    /// <summary>
    /// 尝试攻击公司并进入停顿期。
    /// </summary>
    /// <param name="company">公司实体。</param>
    public bool TryAttackCompanyWithLock(CompanyEntity company) // 攻击公司入口
    {
        if (company == null)
        {
            return false; // 公司为空时返回失败
        }

        var manager = CY.BattleDataManager; // 获取战斗数据管理器
        if (manager == null)
        {
            return false; // 管理器缺失时返回失败
        }

        var conscienceDamage = BaseStats.Attack; // 使用攻击力作为良心伤害
        if (!TryConsumeAttackCooldown(conscienceDamage))
        {
            return false; // 无法进入攻击冷却时返回失败
        }

        var pollutionDamage = GetRandomPollutionDamage(); // 计算污染伤害
        manager.ApplyCompanyDamage(conscienceDamage, pollutionDamage); // 应用公司伤害
        BeginAttackLock(_attackStopDuration); // 进入攻击停顿期
        return true; // 返回攻击成功
    }

    /// <summary>
    /// 获取随机污染伤害（使用配置区间）。
    /// </summary>
    private float GetRandomPollutionDamage() // 污染伤害随机入口
    {
        if (_pollutionDamageMax <= _pollutionDamageMin)
        {
            return _pollutionDamageMin; // 区间无效时使用最小值
        }

        return Random.Range(_pollutionDamageMin, _pollutionDamageMax); // 返回随机污染伤害
    }

    /// <summary>
    /// 进入攻击停顿期（停止移动）。
    /// </summary>
    /// <param name="duration">停顿时长（秒）。</param>
    public void BeginAttackLock(float duration) // 攻击停顿入口
    {
        if (duration <= 0f)
        {
            return; // 无效时长不处理
        }

        _attackLockTimer = duration; // 写入停顿计时
        StopMovement(); // 立刻停止移动
    }

    /// <summary>
    /// 推进攻击停顿计时。
    /// </summary>
    /// <param name="deltaTime">帧间隔。</param>
    private void TickAttackLock(float deltaTime) // 攻击停顿推进入口
    {
        if (_attackLockTimer <= 0f)
        {
            return; // 未处于停顿期时退出
        }

        _attackLockTimer -= deltaTime; // 递减计时
        if (_attackLockTimer < 0f)
        {
            _attackLockTimer = 0f; // 计时器下限保护
        }
    }

    /// <summary>
    /// 切换敌人 AI（传 null 则回退默认 AI）。
    /// </summary>
    /// <param name="ai">目标 AI 资产。</param>
    /// <param name="forceEnter">是否强制重新进入 AI。</param>
    public void SetAI(EnemyAIBase ai, bool forceEnter = false) // AI 切换入口
    {
        var nextAI = ai ?? ResolveDefaultAI(); // 解析要使用的 AI
        if (!ReferenceEquals(_currentAI, nextAI))
        {
            if (_currentAI != null)
            {
                _currentAI.OnExit(this); // 切换前通知旧 AI 退出
            }

            _currentAI = nextAI; // 写入新 AI
            if (_currentAI != null)
            {
                _currentAI.OnEnter(this); // 切换后通知新 AI 进入
            }
            return; // 切换完成后直接返回
        }

        if (forceEnter && _currentAI != null)
        {
            _currentAI.OnEnter(this); // 强制重新进入 AI
        }
    }

    /// <summary>
    /// 获取默认 AI（优先使用配置资产，其次使用运行时创建的基础 AI）。
    /// </summary>
    private EnemyAIBase ResolveDefaultAI() // 默认 AI 解析入口
    {
        if (_defaultAI != null)
        {
            return _defaultAI; // 使用配置的默认 AI
        }

        return _runtimeDefaultAI; // 回退到运行时基础 AI
    }

    /// <summary>
    /// 尝试获取当前位置。
    /// </summary>
    /// <param name="currentPos">输出当前坐标。</param>
    internal bool TryGetCurrentPosition(out Vector2 currentPos) // 当前位置获取入口
    {
        if (_cachedTransform == null)
        {
            _cachedTransform = transform; // 缓存丢失时重新获取 Transform
        }

        if (_cachedTransform == null)
        {
            currentPos = Vector2.zero; // 输出默认坐标
            return false; // 无法获取位置时返回失败
        }

        currentPos = (Vector2)_cachedTransform.position; // 写入当前位置
        return true; // 返回获取成功
    }

    /// <summary>
    /// 尝试获取公司位置。
    /// </summary>
    /// <param name="companyPos">输出公司坐标。</param>
    internal bool TryGetCompanyPosition(out Vector2 companyPos) // 公司位置获取入口
    {
        if (!TryGetCompany(out var company))
        {
            companyPos = Vector2.zero; // 输出默认坐标
            return false; // 公司不存在时返回失败
        }

        companyPos = (Vector2)company.transform.position; // 写入公司坐标
        return true; // 返回获取成功
    }

    /// <summary>
    /// 尝试获取公司实体。
    /// </summary>
    /// <param name="company">输出公司实体。</param>
    internal bool TryGetCompany(out CompanyEntity company) // 公司实体获取入口
    {
        company = CompanyEntity.Current; // 读取当前公司实体
        return company != null; // 返回是否存在
    }

    /// <summary>
    /// 获取当前点到公司碰撞体的最近距离平方。
    /// </summary>
    /// <param name="company">公司实体。</param>
    /// <param name="currentPos">当前坐标。</param>
    /// <param name="distanceSqr">输出距离平方。</param>
    internal bool TryGetCompanyDistanceSqr(CompanyEntity company, Vector2 currentPos, out float distanceSqr) // 公司距离计算入口
    {
        distanceSqr = float.MaxValue; // 初始化距离
        if (company == null)
        {
            return false; // 公司为空时返回失败
        }

        var collider = company.CachedCollider2D; // 获取公司碰撞体缓存
        if (collider != null && collider.enabled)
        {
            var closest = collider.ClosestPoint(currentPos); // 计算最近点
            distanceSqr = (closest - currentPos).sqrMagnitude; // 计算距离平方
            return true; // 使用碰撞体边界距离
        }

        var companyPos = (Vector2)company.transform.position; // 回退到中心点距离
        distanceSqr = (companyPos - currentPos).sqrMagnitude; // 计算中心点距离平方
        return true; // 返回成功
    }

    /// <summary>
    /// 朝指定目标移动。
    /// </summary>
    /// <param name="destination">目标坐标。</param>
    public void MoveTo(Vector2 destination) // 移动入口
    {
        if (_navigationAgent != null)
        {
            var needsUpdate = !_hasDestination || // 未设置过目的地
                              (destination - _lastDestination).sqrMagnitude > // 目标变化过大
                              _destinationRefreshThreshold * _destinationRefreshThreshold || // 超过更新阈值
                              !_navigationAgent.HasPath; // 路径失效需要重算

            if (needsUpdate)
            {
                _navigationAgent.SetDestination(destination); // 设置新目的地
                _lastDestination = destination; // 缓存最新目的地
                _hasDestination = true; // 标记已拥有目的地
            }
        }
        else
        {
            // 简易直线移动作为降级方案。
            var currentPos = (Vector2)_cachedTransform.position; // 获取当前坐标
            var diff = destination - currentPos; // 计算方向向量
            if (diff.sqrMagnitude <= 0.01f)
            {
                return; // 距离过近时停止移动
            }

            var speed = BaseStats.MoveSpeed; // 读取移动速度
            if (speed <= 0f)
            {
                return; // 速度无效时停止移动
            }

            diff.Normalize(); // 归一化方向
            _cachedTransform.position = new Vector3( // 直接更新位置作为降级方案
                currentPos.x + diff.x * speed * Time.deltaTime, // 计算 X 位移
                currentPos.y + diff.y * speed * Time.deltaTime, // 计算 Y 位移
                _cachedTransform.position.z); // 保持 Z 不变
        }
    }

    /// <summary>
    /// 在攻击范围内查找最近的老板/员工目标。
    /// </summary>
    /// <param name="currentPos">当前坐标。</param>
    /// <param name="attackRange">攻击范围。</param>
    internal UnitEntity FindChaseTargetInRange(Vector2 currentPos, float attackRange) // 目标查找入口
    {
        if (attackRange <= 0f || _unitManager == null)
        {
            if (_unitManager == null)
            {
                _unitManager = CY.Unit; // 缓存丢失时重新获取管理器
            }

            if (attackRange <= 0f || _unitManager == null)
            {
                return null; // 无有效范围或管理器时返回空
            }
        }

        var rangeSqr = attackRange * attackRange; // 攻击范围平方
        UnitEntity target = null; // 初始化目标引用
        var bestSqr = rangeSqr; // 记录当前最优距离

        var player = _unitManager.Player; // 获取老板引用
        if (player != null && player.LifeState == UnitLifeState.Alive)
        {
            if (TryGetTargetDistanceSqr(player, currentPos, out var distSqr) && distSqr <= bestSqr)
            {
                target = player; // 选择老板为追击目标
                bestSqr = distSqr; // 更新最优距离
            }
        }

        var employees = _unitManager.Employees; // 获取员工列表
        for (int i = 0; i < employees.Count; i++)
        {
            var employee = employees[i]; // 取出员工引用
            if (employee == null || employee.LifeState != UnitLifeState.Alive)
            {
                continue; // 过滤空引用或非存活目标
            }

            if (TryGetTargetDistanceSqr(employee, currentPos, out var distSqr) && distSqr <= bestSqr)
            {
                target = employee; // 选择员工为追击目标
                bestSqr = distSqr; // 更新最优距离
            }
        }

        return target; // 返回找到的目标
    }

    /// <summary>
    /// 获取当前点到目标碰撞体的最近距离平方，优先使用 Collider2D 边界。
    /// </summary>
    /// <param name="target">目标单位。</param>
    /// <param name="currentPos">当前坐标。</param>
    /// <param name="distanceSqr">输出距离平方。</param>
    internal bool TryGetTargetDistanceSqr(UnitEntity target, Vector2 currentPos, out float distanceSqr) // 距离计算入口
    {
        distanceSqr = float.MaxValue; // 初始化距离
        if (target == null)
        {
            return false; // 目标为空时返回失败
        }

        var collider = target.GetComponent<Collider2D>(); // 获取目标碰撞体
        if (collider != null && collider.enabled)
        {
            var closest = collider.ClosestPoint(currentPos); // 计算最近点
            distanceSqr = (closest - currentPos).sqrMagnitude; // 计算距离平方
            return true; // 使用碰撞体边界距离
        }

        var targetPos = (Vector2)target.transform.position; // 回退到中心点距离
        distanceSqr = (targetPos - currentPos).sqrMagnitude; // 计算中心点距离平方
        return true; // 返回成功
    }

    /// <summary>
    /// 计算目标身位位置，避免贴身移动。
    /// </summary>
    /// <param name="currentPos">当前坐标。</param>
    /// <param name="targetPos">目标坐标。</param>
    internal Vector2 AdjustStandOffDestination(Vector2 currentPos, Vector2 targetPos) // 身位计算入口
    {
        if (_standOffDistance <= 0f)
        {
            return targetPos; // 身位无效时直接追目标
        }

        var diff = currentPos - targetPos; // 计算朝向向量
        var distSqr = diff.sqrMagnitude; // 计算距离平方
        var standSqr = _standOffDistance * _standOffDistance; // 计算身位距离平方
        if (distSqr <= standSqr)
        {
            return currentPos; // 已进入身位范围则保持当前位置
        }

        diff.Normalize(); // 归一化方向
        return targetPos + diff * _standOffDistance; // 计算身位目标点
    }

    /// <summary>
    /// 停止移动（用于公司不存在时兜底）。
    /// </summary>
    public void StopMovement() // 停止移动入口
    {
        if (_navigationAgent != null)
        {
            _navigationAgent.SetDestination(_cachedTransform.position); // 设置原地目标以停止
        }

        _hasDestination = false; // 清理目的地标记
    }

    /// <summary>
    /// 绘制攻击范围（仅在编辑器 Scene 视图中显示）。
    /// </summary>
    private void OnDrawGizmos() // Gizmos 绘制入口
    {
        if ((!_showAttackRangeGizmos || !_showAttackRangeAlways) &&
            (!_showSightRangeGizmos || !_showSightRangeAlways))
        {
            return; // 未启用常显时跳过绘制
        }

        DrawRangeGizmos(); // 绘制范围
    }

    /// <summary>
    /// 绘制攻击范围（选中时显示）。
    /// </summary>
    private void OnDrawGizmosSelected() // 选中时 Gizmos 绘制入口
    {
        if (!_showAttackRangeGizmos && !_showSightRangeGizmos)
        {
            return; // 未启用绘制时跳过
        }

        DrawRangeGizmos(); // 绘制范围
    }

    /// <summary>
    /// 统一绘制攻击范围，避免重复逻辑。
    /// </summary>
    private void DrawRangeGizmos() // 范围绘制实现
    {
        var t = _cachedTransform != null ? _cachedTransform : transform; // 获取可用 Transform

        var attackRange = BaseStats.AttackRange; // 获取攻击范围
        if (_showAttackRangeGizmos && attackRange > 0f)
        {
            Gizmos.color = _attackRangeGizmosColor; // 设置攻击范围颜色
            Gizmos.DrawWireSphere(t.position, attackRange); // 绘制攻击范围
        }

        var sightRange = _sightRange; // 获取可视范围
        if (_showSightRangeGizmos && sightRange > 0f)
        {
            Gizmos.color = _sightRangeGizmosColor; // 设置可视范围颜色
            Gizmos.DrawWireSphere(t.position, sightRange); // 绘制可视范围
        }
    }
}
