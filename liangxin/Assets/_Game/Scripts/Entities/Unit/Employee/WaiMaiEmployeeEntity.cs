// 引用 System.Collections.Generic 命名空间，使用 Dictionary
using System.Collections.Generic; // 字典容器引用
// 引用 CYFramework 命名空间，使用 CY 入口与日志
using CYFramework; // 框架入口引用
// 引用实体系统命名空间，使用 IEntityPreShowData 接口
using CYFramework.Core.Entity; // 实体预显示接口引用
// 引用 CYFramework 计时器命名空间，使用 Timer
using CYFramework.Core.Timer; // 计时器类型引用
// 引用 UnityEngine 命名空间，使用 Vector/Mathf/SerializeField 等类型
using UnityEngine; // Unity 基础类型引用

/// <summary>
/// 外卖员员工实体：远程攻击单位。
/// 说明：该脚本不继承 EmployeeEntity（按需求每个职业独立脚本），但复用 UnitEntity 的通用战斗/发射子弹能力。
/// </summary>
[RequireComponent(typeof(HybridNavigationAgent))] // 约束必须挂载导航组件（用于右键移动）
[EntityPrefab("Prefabs/Entities/Unit/Employee/WaiMaiEmployeeEntity", "WaiMaiEmployeeEntity", "Employees")] // 绑定默认实体预制体信息（兜底）
public sealed class WaiMaiEmployeeEntity : UnitEntity, IEntityPreShowData<EmployeePreShowData>, IEmployeeControllable // 外卖员员工实体定义
{
    /// <summary>
    /// 外卖员子弹预制体路径（Resources 相对路径，无扩展名）。
    /// </summary>
    private const string WaiMaiBulletPrefabPath = "Prefabs/Entities/Projectiles/Unit/Player/WaiMaiBullet"; // 外卖员子弹预制体路径常量

    /// <summary>
    /// 外卖员子弹路径数组（供 UnitEntity 远程发射使用）。
    /// </summary>
    private static readonly string[] WaiMaiBulletPrefabPaths = { WaiMaiBulletPrefabPath }; // 外卖员子弹路径数组缓存（避免运行时分配）

    /// <summary>
    /// 员工碰撞体到实体的映射表：用于鼠标点击快速定位员工，避免在 Update 中 GetComponent 查找。
    /// </summary>
    private static readonly Dictionary<Collider2D, WaiMaiEmployeeEntity> ColliderEntityMap = // 碰撞体到外卖员实体映射表
        new Dictionary<Collider2D, WaiMaiEmployeeEntity>(64); // 预分配容量以减少扩容

    /// <summary>
    /// AI 检测间隔（秒）：用于控制“范围内找敌人”的频率，避免每帧遍历敌人列表。
    /// </summary>
    [SerializeField] private float _aiCheckInterval = 0.2f; // AI 检测间隔配置

    /// <summary>
    /// 是否仅在“静止/无导航路径”时才允许攻击：用于实现“移动中不攻击”的需求。
    /// </summary>
    [SerializeField] private bool _attackOnlyWhenIdle = true; // 仅静止时攻击开关（默认开启）

    /// <summary>
    /// 可视范围（来自 Employee.csv 的 SightRange）。
    /// </summary>
    private float _sightRange; // 外卖员可视范围缓存

    /// <summary>
    /// 单位管理器缓存（用于读取敌人列表）。
    /// </summary>
    private UnitManager _unitManager; // UnitManager 缓存引用

    /// <summary>
    /// Transform 缓存（用于预显示出生点设置）。
    /// </summary>
    private Transform _cachedTransform; // Transform 缓存引用

    /// <summary>
    /// Rigidbody2D 缓存（用于预显示阶段设置物理位置）。
    /// </summary>
    private Rigidbody2D _rigidbody2D; // 刚体缓存引用

    /// <summary>
    /// 导航代理缓存：用于接收玩家右键移动命令。
    /// </summary>
    private HybridNavigationAgent _navigationAgent; // 导航代理缓存引用

    /// <summary>
    /// AI 计时器：按间隔执行范围检测与攻击判定。
    /// </summary>
    private Timer _aiTimer; // AI 循环计时器

    /// <summary>
    /// 员工 Layer 索引缓存（用于判断“目标点是否被员工占用”）。
    /// </summary>
    // 注意：LayerMask.NameToLayer 不能在 MonoBehaviour 构造/字段初始化阶段调用，因此不在此处缓存 Layer 索引。 // Unity 约束说明注释

    /// <summary>
    /// 员工层遮罩缓存（用于 Physics2D NonAlloc 检测）。
    /// </summary>
    private int _employeeLayerMask; // 员工层遮罩缓存

    /// <summary>
    /// 移动命令占用检测命中缓存：用于避免在右键移动时产生 GC。
    /// </summary>
    private Collider2D[] _moveCommandOccupyHits; // 占用检测命中缓存数组

    /// <summary>
    /// 移动命令：寻找最近“未被员工占用”的偏移点时，单圈采样数量。
    /// </summary>
    private const int MoveCommandSamplesPerRing = 16; // 单圈采样点数量常量

    /// <summary>
    /// 移动命令：寻找最近“未被员工占用”的偏移点时，最大圈数。
    /// </summary>
    private const int MoveCommandMaxRings = 8; // 最大圈数常量

    /// <summary>
    /// 实现 IEmployeeControllable：返回自身单位引用。
    /// </summary>
    public UnitEntity Unit => this; // 返回自身作为单位引用

    /// <summary>
    /// 实体初始化：缓存必要管理器引用（低频）。
    /// </summary>
    /// <param name="userData">初始化用户数据。</param>
    protected override void OnEntityInit(object userData) // 实体初始化入口
    {
        base.OnEntityInit(userData); // 调用父类初始化（缓存 Transform/Collider）
        _unitManager = CY.Unit; // 缓存单位管理器引用
        _cachedTransform = transform; // 缓存 Transform（低频）
        _rigidbody2D = GetComponent<Rigidbody2D>(); // 缓存刚体组件（低频）
        _navigationAgent = GetComponent<HybridNavigationAgent>(); // 缓存导航代理组件（低频）
        _employeeLayerMask = BuildEmployeeLayerMask(); // 构建员工层遮罩缓存（用于移动命令占用检测）
        _moveCommandOccupyHits = new Collider2D[16]; // 分配占用检测命中缓存数组（一次性分配）
    }

    /// <summary>
    /// 应用预显示数据：在实体激活前设置出生点，确保第一帧位置正确。
    /// </summary>
    /// <param name="data">预显示数据（引用传递）。</param>
    public void ApplyPreShowData(ref EmployeePreShowData data) // 预显示出生点应用入口
    {
        if (!data.HasPosition) // 未提供出生点判定
        {
            return; // 未提供出生点时直接退出
        }

        var cachedTransform = _cachedTransform != null ? _cachedTransform : transform; // 获取可用 Transform
        var targetX = data.Position.x; // 读取目标 X
        var targetY = data.Position.y; // 读取目标 Y
        var targetZ = cachedTransform.position.z; // 保留当前 Z（2D 项目使用 XY 平面）

        cachedTransform.position = new Vector3(targetX, targetY, targetZ); // 同步 Transform 坐标

        if (_rigidbody2D == null) // 刚体未缓存判定
        {
            _rigidbody2D = GetComponent<Rigidbody2D>(); // 兜底获取刚体（低频）
        }

        if (_rigidbody2D != null) // 刚体存在判定
        {
            _rigidbody2D.position = new Vector2(targetX, targetY); // 设置刚体位置（用于物理系统）
            _rigidbody2D.velocity = Vector2.zero; // 清空线速度，避免继承池化残留
            _rigidbody2D.angularVelocity = 0f; // 清空角速度，避免旋转残留
        }
    }

    /// <summary>
    /// 实体显示：应用员工数据并启动基础 AI。
    /// </summary>
    /// <param name="userData">显示用户数据（期望为 EmployeeUnitRow）。</param>
    protected override void OnEntityShow(object userData) // 实体显示入口
    {
        ApplyEmployeeData(userData as EmployeeUnitRow); // 应用员工数据行（包含可视范围与基础属性）
        base.OnEntityShow(userData); // 调用父类显示（重置生命/派发事件等）
        RegisterToUnitManager(); // 注册到单位管理器员工列表（显示后才加入，避免显示前被敌人选中/攻击）
        RegisterColliderMap(); // 注册碰撞体映射（用于鼠标点击选中）
        RegisterClickRegistry(); // 注册统一点击注册表（支持多员工脚本）
        StartAiTimer(); // 启动 AI 计时器
    }

    /// <summary>
    /// 实体隐藏：停止基础 AI，避免回调访问已隐藏对象。
    /// </summary>
    protected override void OnEntityHide() // 实体隐藏入口
    {
        StopAiTimer(); // 停止 AI 计时器
        UnregisterFromUnitManager(); // 从单位管理器员工列表移除（隐藏后不应再作为敌人目标）
        UnregisterColliderMap(); // 移除碰撞体映射（避免回收后仍可被选中）
        UnregisterClickRegistry(); // 反注册统一点击注册表
        base.OnEntityHide(); // 调用父类隐藏
    }

    /// <summary>
    /// 实体回收：停止基础 AI，避免池化复用残留计时器回调。
    /// </summary>
    protected override void OnEntityRecycle() // 实体回收入口
    {
        StopAiTimer(); // 停止 AI 计时器
        UnregisterFromUnitManager(); // 回收时兜底移除单位管理器引用
        UnregisterColliderMap(); // 回收时兜底移除映射
        UnregisterClickRegistry(); // 回收时兜底反注册
        base.OnEntityRecycle(); // 调用父类回收
    }

    /// <summary>
    /// 将自身注册到 UnitManager 的员工列表（显示后再加入，避免显示前被敌人 AI 当作目标）。
    /// </summary>
    private void RegisterToUnitManager() // 员工列表注册入口
    {
        if (_unitManager == null) // 管理器为空判定
        {
            _unitManager = CY.Unit; // 重新获取单位管理器引用
        }

        if (_unitManager == null) // 仍为空判定
        {
            return; // 管理器缺失时直接退出
        }

        _unitManager.AddEmployee(this); // 将自身加入员工列表（内部会去重）
    }

    /// <summary>
    /// 将自身从 UnitManager 的员工列表移除（隐藏/回收后不应再作为敌人目标）。
    /// </summary>
    private void UnregisterFromUnitManager() // 员工列表移除入口
    {
        if (_unitManager == null) // 管理器为空判定
        {
            _unitManager = CY.Unit; // 尝试重新获取单位管理器引用
        }

        if (_unitManager == null) // 仍为空判定
        {
            return; // 管理器缺失时直接退出
        }

        _unitManager.RemoveEmployee(this); // 将自身从员工列表移除
    }

    /// <summary>
    /// 注册碰撞体到实体的映射（用于鼠标点击选中）。
    /// </summary>
    private void RegisterColliderMap() // 碰撞体映射注册入口
    {
        var collider2D = CachedCollider2D; // 获取父类缓存的碰撞体
        if (collider2D == null) // 碰撞体为空判定
        {
            return; // 无碰撞体时不注册
        }

        ColliderEntityMap[collider2D] = this; // 写入映射表（覆盖旧值）
    }

    /// <summary>
    /// 移除碰撞体到实体的映射（用于回收/隐藏清理）。
    /// </summary>
    private void UnregisterColliderMap() // 碰撞体映射移除入口
    {
        var collider2D = CachedCollider2D; // 获取父类缓存的碰撞体
        if (collider2D == null) // 碰撞体为空判定
        {
            return; // 无碰撞体时直接退出
        }

        if (!ColliderEntityMap.TryGetValue(collider2D, out var current)) // 映射不存在判定
        {
            return; // 未注册时直接退出
        }

        if (current != this) // 非本实例判定（避免误删别的复用对象）
        {
            return; // 非本实例时不处理
        }

        ColliderEntityMap.Remove(collider2D); // 从映射表移除
    }

    /// <summary>
    /// 注册统一点击注册表：供 PlayerEntity 通过碰撞体查询可控员工。
    /// </summary>
    private void RegisterClickRegistry() // 点击注册表注册入口
    {
        var collider2D = CachedCollider2D; // 获取自身碰撞体缓存
        if (collider2D == null) // 碰撞体缺失判定
        {
            return; // 缺失时直接退出
        }

        EmployeeClickRegistry.Register(collider2D, this); // 注册到统一注册表
    }

    /// <summary>
    /// 反注册统一点击注册表：避免对象池复用后选中到旧对象。
    /// </summary>
    private void UnregisterClickRegistry() // 点击注册表反注册入口
    {
        var collider2D = CachedCollider2D; // 获取自身碰撞体缓存
        if (collider2D == null) // 碰撞体缺失判定
        {
            return; // 缺失时直接退出
        }

        EmployeeClickRegistry.Unregister(collider2D, this); // 从统一注册表移除
    }

    /// <summary>
    /// 接收玩家右键移动命令：设置导航目标点。
    /// </summary>
    /// <param name="destination">目标世界坐标（XY）。</param>
    /// <returns>是否命令成功。</returns>
    public bool TryCommandMove(Vector2 destination) // 员工移动命令入口
    {
        if (LifeState != UnitLifeState.Alive) // 非存活状态判定
        {
            return false; // 非存活时不响应移动
        }

        if (_navigationAgent == null) // 导航代理缺失判定
        {
            _navigationAgent = GetComponent<HybridNavigationAgent>(); // 兜底获取导航代理（低频）
            if (_navigationAgent == null) // 仍缺失判定
            {
                return false; // 无导航代理时返回失败
            }
        }

        var finalDestination = destination; // 默认使用点击位置作为最终目标点
        TryAdjustMoveDestinationToNearestFree(ref finalDestination); // 若目标点被员工占用，则偏移到最近的空位
        return _navigationAgent.SetDestination(finalDestination, NavigationMode.Auto, false); // 下发导航目标（Auto）
    }

    /// <summary>
    /// 构建员工 LayerMask（若项目未配置 Employee 层，则回退使用自身 Layer）。
    /// </summary>
    /// <returns>员工层遮罩。</returns>
    private int BuildEmployeeLayerMask() // 员工层遮罩构建入口
    {
        var employeeLayer = LayerMask.NameToLayer("Employee"); // 在运行期获取 Employee 层索引（避免静态初始化阶段调用）
        if (employeeLayer >= 0) // Employee 层存在判定
        {
            return 1 << employeeLayer; // 使用 Employee 层构建遮罩
        }

        return 1 << gameObject.layer; // 回退使用自身所在层构建遮罩
    }

    /// <summary>
    /// 若目标点被员工占用，则把目标点偏移到最近的“未被员工占用”的位置。
    /// </summary>
    /// <param name="destination">输入/输出目标点（若被占用将被修改）。</param>
    private void TryAdjustMoveDestinationToNearestFree(ref Vector2 destination) // 目标点占用偏移入口
    {
        if (_employeeLayerMask == 0) // 员工层遮罩为空判定
        {
            return; // 未配置时不处理
        }

        if (_moveCommandOccupyHits == null || _moveCommandOccupyHits.Length == 0) // 命中数组缺失判定
        {
            return; // 缺失缓存时不处理
        }

        var selfCollider = CachedCollider2D; // 获取自身碰撞体缓存
        if (selfCollider == null) // 自身碰撞体缺失判定
        {
            return; // 缺失碰撞体时不处理
        }

        var occupyRadius = GetOccupyRadiusByCollider(selfCollider); // 计算自身占用半径（基于碰撞体尺寸）
        if (occupyRadius <= 0f) // 半径非法判定
        {
            return; // 半径非法时不处理
        }

        if (!IsPositionOccupiedByOtherEmployee(destination, occupyRadius)) // 目标点未被其他员工占用判定
        {
            return; // 未被占用时直接使用原目标点
        }

        var currentPos = _rigidbody2D != null ? _rigidbody2D.position : (Vector2)transform.position; // 获取当前坐标（优先刚体）
        var preferDir = destination - currentPos; // 计算“更偏向的搜索方向”（朝向目标点）
        var preferAngle = preferDir.sqrMagnitude > 0.0001f ? Mathf.Atan2(preferDir.y, preferDir.x) : 0f; // 计算偏好角度（用于优先搜索前方）
        var step = Mathf.Max(occupyRadius * 2f, 0.05f); // 计算每圈半径步进（以“两个同体积员工不重叠”的最小中心距为基准）

        for (int ring = 1; ring <= MoveCommandMaxRings; ring++) // 从近到远逐圈搜索
        {
            var radius = step * ring; // 计算当前圈半径
            var bestCandidateFound = false; // 标记是否找到当前圈候选点
            var bestCandidate = destination; // 当前圈最优候选点缓存

            for (int i = 0; i < MoveCommandSamplesPerRing; i++) // 在当前半径上采样多个方向点
            {
                var angle = preferAngle + (Mathf.PI * 2f) * (i / (float)MoveCommandSamplesPerRing); // 计算采样角度（以偏好角度为起点）
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius; // 计算偏移向量
                var candidate = destination + offset; // 计算候选点

                if (IsPositionOccupiedByOtherEmployee(candidate, occupyRadius)) // 候选点被占用判定
                {
                    continue; // 被占用则继续尝试下一点
                }

                bestCandidate = candidate; // 记录可用候选点
                bestCandidateFound = true; // 标记找到候选点
                break; // 当前圈找到一个最近半径的可用点即可退出（满足“最近”）
            }

            if (bestCandidateFound) // 当前圈找到可用点判定
            {
                destination = bestCandidate; // 覆盖目标点为最近空位
                return; // 完成偏移后直接返回
            }
        }
    }

    /// <summary>
    /// 基于碰撞体尺寸计算“占用半径”（用于判断点位是否被员工占用）。
    /// </summary>
    /// <param name="collider2D">碰撞体。</param>
    /// <returns>占用半径。</returns>
    private float GetOccupyRadiusByCollider(Collider2D collider2D) // 占用半径计算入口
    {
        if (collider2D == null) // 碰撞体为空判定
        {
            return 0f; // 为空时返回 0
        }

        var extents = collider2D.bounds.extents; // 获取 Bounds 半尺寸（世界坐标）
        var radius = Mathf.Max(extents.x, extents.y); // 取较大半轴作为占用半径
        return radius + 0.02f; // 增加少量余量避免贴边重叠
    }

    /// <summary>
    /// 判断指定位置是否被“其他员工”占用。
    /// </summary>
    /// <param name="position">需要检测的位置。</param>
    /// <param name="radius">检测半径（使用自身占用半径）。</param>
    /// <returns>是否被其他员工占用。</returns>
    private bool IsPositionOccupiedByOtherEmployee(Vector2 position, float radius) // 目标点占用检测入口
    {
        var hitCount = Physics2D.OverlapCircleNonAlloc(position, radius, _moveCommandOccupyHits, _employeeLayerMask); // 检测范围内员工碰撞体（NonAlloc）
        if (hitCount <= 0) // 未命中判定
        {
            return false; // 未命中任何员工时认为不占用
        }

        var selfCollider = CachedCollider2D; // 获取自身碰撞体缓存
        for (int i = 0; i < hitCount; i++) // 遍历命中结果
        {
            var hit = _moveCommandOccupyHits[i]; // 获取命中碰撞体
            if (hit == null) // 命中为空判定
            {
                continue; // 为空时跳过
            }

            if (hit == selfCollider) // 命中自身判定
            {
                continue; // 忽略自身
            }

            return true; // 命中其他员工则认为占用
        }

        return false; // 仅命中自身时认为不占用
    }

    /// <summary>
    /// 应用员工配置数据（来自 Employee.csv）。
    /// </summary>
    /// <param name="row">员工数据行。</param>
    private void ApplyEmployeeData(EmployeeUnitRow row) // 员工数据应用入口
    {
        if (row == null) // 数据行为空判定
        {
            CY.LogWarning("[WaiMaiEmployeeEntity] 缺少员工数据行，使用默认属性。"); // 输出警告日志
            _sightRange = 0f; // 缺少数据时清空可视范围
            ApplyBulletSpeed(0f); // 缺少数据时使用默认子弹速度
            ApplyBulletArrayConfig(BulletSelectRule.Random, WaiMaiBulletPrefabPaths); // 缺少数据时仍配置外卖员子弹
            return; // 缺少数据时直接退出
        }

        var stats = new UnitStats // 组装单位基础属性结构体
        {
            MaxHp = row.MaxHp, // 最大生命值
            Attack = row.Attack, // 攻击力
            Defense = row.Defense, // 防御力
            DefensePenetration = row.DefensePenetration, // 固定防御穿透
            DefensePenetrationRate = row.DefensePenetrationRate, // 百分比防御穿透
            CritRate = row.CritRate, // 暴击率
            DodgeRate = row.DodgeRate, // 闪避率
            IsRanged = true, // 外卖员强制为远程单位
            MoveSpeed = row.MoveSpeed, // 移动速度
            AttackRange = row.AttackRange, // 攻击距离
            AttackInterval = row.AttackInterval // 攻击间隔
        };

        _sightRange = row.SightRange; // 写入可视范围（用于寻找敌人）
        ApplyBaseData(row.Id, row.Code, row.Name, row.Camp, row.LifeState, row.Level, stats); // 写入单位基础数据
        ApplyBulletSpeed(0f); // 子弹速度为 0 表示使用子弹预制体默认速度
        ApplyBulletArrayConfig(BulletSelectRule.Random, WaiMaiBulletPrefabPaths); // 配置外卖员子弹数组（单发固定子弹）
    }

    /// <summary>
    /// 启动 AI 计时器：按固定间隔执行“可视范围内找敌人并在攻击范围内攻击”。
    /// </summary>
    private void StartAiTimer() // AI 计时器启动入口
    {
        StopAiTimer(); // 启动前先停止旧计时器，避免重复启动

        if (_aiCheckInterval <= 0f) // 间隔无效判定
        {
            _aiCheckInterval = 0.2f; // 间隔无效时回退到默认值
        }

        _aiTimer = CY.Timer.Loop(_aiCheckInterval, TickBasicAi); // 启动循环计时器（不捕获闭包）
        TickBasicAi(); // 立即执行一次，避免等待首个间隔
    }

    /// <summary>
    /// 停止 AI 计时器。
    /// </summary>
    private void StopAiTimer() // AI 计时器停止入口
    {
        if (_aiTimer == null) // 计时器为空判定
        {
            return; // 计时器为空时直接退出
        }

        _aiTimer.Stop(); // 停止循环计时器
        _aiTimer = null; // 清空计时器引用
    }

    /// <summary>
    /// 基础 AI Tick：默认仅在静止时（无导航路径）才会攻击；可视范围内找最近敌人，进入攻击范围则攻击（不主动移动）。
    /// </summary>
    private void TickBasicAi() // 基础 AI Tick 入口
    {
        if (LifeState != UnitLifeState.Alive) // 非存活状态判定
        {
            return; // 非存活状态时不执行 AI
        }

        if (_attackOnlyWhenIdle) // 仅静止时攻击开关开启判定
        {
            if (_navigationAgent != null && _navigationAgent.HasPath) // 导航存在路径（移动中/尚未到达）判定
            {
                return; // 移动中不执行攻击逻辑
            }
        }

        if (_unitManager == null) // 管理器为空判定
        {
            _unitManager = CY.Unit; // 尝试重新获取单位管理器引用
            if (_unitManager == null) // 仍为空判定
            {
                return; // 管理器为空时直接退出
            }
        }

        var sightRange = _sightRange; // 获取可视范围
        if (sightRange <= 0f) // 可视范围无效判定
        {
            sightRange = BaseStats.AttackRange; // 可视范围无效时回退为攻击距离
        }

        if (sightRange <= 0f) // 范围仍无效判定
        {
            return; // 无有效范围时不执行
        }

        if (!TryFindNearestEnemyInRange(sightRange, out var enemy, out var enemyDistSqr)) // 寻找最近敌人
        {
            return; // 未找到敌人时直接退出
        }

        var attackRange = BaseStats.AttackRange; // 获取攻击距离
        if (attackRange <= 0f) // 攻击距离无效判定
        {
            return; // 攻击距离无效时退出
        }

        var attackRangeSqr = attackRange * attackRange; // 计算攻击距离平方
        if (enemyDistSqr > attackRangeSqr) // 未进入攻击范围判定
        {
            return; // 不在攻击范围内时不攻击（按需求不移动）
        }

        TryAttackTarget(enemy); // 进入攻击范围则尝试攻击（内部包含冷却判断）
    }

    /// <summary>
    /// 在可视范围内寻找最近的敌人（UnitCamp.Enemy）。
    /// </summary>
    /// <param name="range">可视范围。</param>
    /// <param name="enemy">输出最近敌人。</param>
    /// <param name="distanceSqr">输出距离平方（基于碰撞体最近点）。</param>
    /// <returns>是否找到敌人。</returns>
    private bool TryFindNearestEnemyInRange(float range, out UnitEntity enemy, out float distanceSqr) // 敌人查找入口
    {
        enemy = null; // 默认输出为空
        distanceSqr = float.MaxValue; // 默认输出为最大距离

        var enemies = _unitManager.Enemies; // 获取敌人列表（由 UnitManager 维护）
        if (enemies == null || enemies.Count <= 0) // 列表为空判定
        {
            return false; // 无敌人时返回失败
        }

        var selfTransform = CachedTransform != null ? CachedTransform : transform; // 获取自身 Transform
        var selfPos = (Vector2)selfTransform.position; // 获取自身位置（XY 平面）
        var rangeSqr = range * range; // 计算可视范围平方

        for (int i = 0; i < enemies.Count; i++) // 遍历敌人列表
        {
            var candidate = enemies[i]; // 获取候选敌人
            if (candidate == null) // 候选为空判定
            {
                continue; // 候选为空时跳过
            }

            if (candidate.LifeState != UnitLifeState.Alive) // 非存活判定
            {
                continue; // 非存活时跳过
            }

            if (candidate.Camp != UnitCamp.Enemy) // 阵营不匹配判定
            {
                continue; // 非敌人阵营时跳过
            }

            var candidatePoint = selfPos; // 初始化目标点为自身位置
            var candidateCollider = candidate.CachedCollider2D; // 获取候选碰撞体缓存
            if (candidateCollider != null) // 碰撞体存在判定
            {
                candidatePoint = candidateCollider.ClosestPoint(selfPos); // 使用碰撞体最近点计算距离
            }
            else if (candidate.CachedTransform != null) // Transform 缓存存在判定
            {
                candidatePoint = (Vector2)candidate.CachedTransform.position; // 使用 Transform 坐标
            }
            else // 兜底分支
            {
                candidatePoint = (Vector2)candidate.transform.position; // 兜底使用 Transform 坐标
            }

            var diff = candidatePoint - selfPos; // 计算差向量
            var sqr = diff.sqrMagnitude; // 计算距离平方
            if (sqr > rangeSqr) // 超出可视范围判定
            {
                continue; // 超出范围时跳过
            }

            if (sqr >= distanceSqr) // 非更近目标判定
            {
                continue; // 非更近时跳过
            }

            enemy = candidate; // 更新最近敌人
            distanceSqr = sqr; // 更新最近距离平方
        }

        return enemy != null; // 返回是否找到敌人
    }
}
