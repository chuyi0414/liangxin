// 引用 CYFramework 命名空间，使用 CY 入口与日志
using CYFramework; // 框架入口引用
// 引用实体系统命名空间，使用 IEntityPreShowData 接口
using CYFramework.Core.Entity; // 实体预显示接口引用
// 引用 CYFramework 计时器命名空间，使用 Timer
using CYFramework.Core.Timer; // 计时器类型引用
// 引用 UnityEngine 命名空间，使用 SerializeField/Vector/Mathf/Physics2D 等类型
using UnityEngine; // Unity 基础类型引用

/// <summary>
/// 员工预显示数据：用于在实体激活前设置出生点，避免先使用预制体初始坐标导致“跳一下/位置不对”。
/// </summary>
public struct EmployeePreShowData // 员工预显示数据结构体
{
    /// <summary>是否提供有效出生点。</summary>
    public bool HasPosition; // 出生点有效标记
    /// <summary>出生点世界坐标（会在应用时保留 Z）。</summary>
    public Vector3 Position; // 出生点坐标
}

/// <summary>
/// 员工单位实体通用基类（用于保安/外卖员/通用员工等）。
/// 目标：把“预显示出生点 + 点击注册 + 右键移动 + 占位避让 + 基础 AI”抽到一处，避免每个职业脚本重复实现。
/// </summary>
public abstract class EmployeeEntityBase : UnitEntity, IEntityPreShowData<EmployeePreShowData>, IEmployeeControllable // 员工单位通用基类
{
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
    private float _sightRange; // 员工可视范围缓存

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
    /// 移动路径可视化器：用于右键移动时显示“从脚下到终点”的路径线与路径点。
    /// </summary>
    private EmployeeMovePathVisualizer _movePathVisualizer; // 路径可视化器缓存

    /// <summary>
    /// 实现 IEmployeeControllable：返回自身单位引用。
    /// </summary>
    public UnitEntity Unit => this; // 返回自身作为单位引用

    /// <summary>
    /// 员工是否强制远程：为空表示读取数据表的 IsRanged；非空表示覆盖数据表值。
    /// </summary>
    protected virtual bool? ForceIsRanged => null; // 远程/近战覆盖开关（默认不覆盖）

    /// <summary>
    /// 记录“员工数据行缺失”的警告日志（由派生类提供固定 Tag，避免频繁字符串拼接）。
    /// </summary>
    protected abstract void LogMissingEmployeeDataRow(); // 缺少数据行日志输出入口

    /// <summary>
    /// 当员工数据行缺失时的补充处理（派生类可覆盖，例如远程职业兜底配置子弹）。
    /// </summary>
    protected virtual void OnEmployeeDataMissing() // 数据缺失回调入口
    {
    }

    /// <summary>
    /// 当员工数据行已成功应用后执行的补充处理（派生类可覆盖，例如远程职业配置子弹数组）。
    /// </summary>
    /// <param name="row">已应用的数据行。</param>
    protected virtual void OnAfterEmployeeDataApplied(EmployeeUnitRow row) // 数据应用后回调入口
    {
    }

    /// <summary>
    /// 实体初始化：缓存必要组件与管理器引用（低频）。
    /// </summary>
    /// <param name="userData">初始化用户数据。</param>
    protected override void OnEntityInit(object userData) // 实体初始化入口
    {
        base.OnEntityInit(userData); // 调用父类初始化（缓存 Transform/Collider）
        _unitManager = CY.Unit; // 缓存单位管理器引用
        _cachedTransform = transform; // 缓存 Transform（低频）
        _rigidbody2D = GetComponent<Rigidbody2D>(); // 缓存刚体组件（低频）
        _navigationAgent = GetComponent<HybridNavigationAgent>(); // 缓存导航代理组件（低频）
        _movePathVisualizer = GetOrAddMovePathVisualizer(); // 缓存/创建路径可视化器（无需手改预制体）
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
        RegisterClickRegistry(); // 注册统一点击注册表（支持多员工脚本）
        StartAiTimer(); // 启动 AI 计时器
    }

    /// <summary>
    /// 实体隐藏：停止基础 AI，避免回调访问已隐藏对象。
    /// </summary>
    protected override void OnEntityHide() // 实体隐藏入口
    {
        StopAiTimer(); // 停止 AI 计时器
        HideMovePathVisualizer(); // 隐藏移动路径显示（避免隐藏后仍残留线与点）
        UnregisterFromUnitManager(); // 从单位管理器员工列表移除（隐藏后不应再作为敌人目标）
        UnregisterClickRegistry(); // 反注册统一点击注册表
        base.OnEntityHide(); // 调用父类隐藏
    }

    /// <summary>
    /// 实体回收：停止基础 AI，避免池化复用残留计时器回调。
    /// </summary>
    protected override void OnEntityRecycle() // 实体回收入口
    {
        StopAiTimer(); // 停止 AI 计时器
        HideMovePathVisualizer(); // 回收时兜底隐藏移动路径显示（避免对象池复用残留）
        UnregisterFromUnitManager(); // 回收时兜底移除单位管理器引用
        UnregisterClickRegistry(); // 回收时兜底反注册
        base.OnEntityRecycle(); // 调用父类回收
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

        var success = _navigationAgent.SetDestination(destination, NavigationMode.Auto, false); // 下发导航目标（Auto）
        if (!success) // 下发失败判定
        {
            HideMovePathVisualizer(); // 下发失败时隐藏路径显示（避免显示旧路径）
            return false; // 返回失败
        }

        ShowMovePathVisualizer(); // 下发成功后显示移动路径（持续更新直到到达）
        return true; // 返回成功
    }

    /// <summary>
    /// 获取或添加移动路径可视化器：用于右键移动显示路径线与路径点。
    /// </summary>
    private EmployeeMovePathVisualizer GetOrAddMovePathVisualizer() // 路径可视化器获取/创建入口
    {
        var visualizer = GetComponent<EmployeeMovePathVisualizer>(); // 尝试获取现有可视化器
        if (visualizer != null) // 已存在判定
        {
            return visualizer; // 直接返回
        }

        visualizer = gameObject.AddComponent<EmployeeMovePathVisualizer>(); // 动态添加可视化器组件（无需修改预制体）
        if (visualizer != null) // 添加成功判定
        {
            visualizer.SetUseGlobalConfig(true); // 自动添加的组件默认使用全局配置（手动挂载则默认使用本地配置）
        }

        return visualizer; // 返回可视化器引用
    }

    /// <summary>
    /// 显示移动路径：绑定当前导航代理并持续刷新路径显示。
    /// </summary>
    private void ShowMovePathVisualizer() // 移动路径显示入口
    {
        if (_movePathVisualizer == null) // 可视化器为空判定
        {
            _movePathVisualizer = GetOrAddMovePathVisualizer(); // 兜底创建可视化器
        }

        if (_movePathVisualizer == null) // 仍为空判定
        {
            return; // 无可视化器时直接退出
        }

        if (_navigationAgent == null) // 导航代理为空判定
        {
            return; // 无导航代理时不显示路径
        }

        _movePathVisualizer.Show(_navigationAgent); // 显示并绑定导航代理
    }

    /// <summary>
    /// 隐藏移动路径：用于下发失败/单位隐藏/回收等场景。
    /// </summary>
    private void HideMovePathVisualizer() // 移动路径隐藏入口
    {
        if (_movePathVisualizer == null) // 可视化器为空判定
        {
            return; // 为空时直接退出
        }

        _movePathVisualizer.HideImmediate(); // 立刻隐藏并停止刷新
    }

    /// <summary>
    /// 应用员工配置数据（来自 Employee.csv）。
    /// </summary>
    /// <param name="row">员工数据行。</param>
    private void ApplyEmployeeData(EmployeeUnitRow row) // 员工数据应用入口
    {
        if (row == null) // 数据行为空判定
        {
            LogMissingEmployeeDataRow(); // 输出“缺少数据行”警告日志
            _sightRange = 0f; // 缺少数据时清空可视范围
            OnEmployeeDataMissing(); // 通知派生类执行兜底逻辑
            return; // 缺少数据时直接退出
        }

        var isRanged = ForceIsRanged ?? row.IsRanged; // 计算是否远程（可被派生类覆盖）
        var stats = new UnitStats // 组装单位基础属性结构体
        {
            MaxHp = row.MaxHp, // 最大生命值
            Attack = row.Attack, // 攻击力
            Defense = row.Defense, // 防御力
            DefensePenetration = row.DefensePenetration, // 固定防御穿透
            DefensePenetrationRate = row.DefensePenetrationRate, // 百分比防御穿透
            CritRate = row.CritRate, // 暴击率
            CritMultiplier = row.CritMultiplier, // 暴击倍率
            DodgeRate = row.DodgeRate, // 闪避率
            IsRanged = isRanged, // 是否远程
            MoveSpeed = row.MoveSpeed, // 移动速度
            AttackRange = row.AttackRange, // 攻击距离
            AttackInterval = row.AttackInterval // 攻击间隔
        };

        _sightRange = row.SightRange; // 写入可视范围（用于寻找敌人）
        ApplyBaseData(row.Id, row.Code, row.Name, row.Camp, row.LifeState, row.Level, stats); // 写入单位基础数据
        OnAfterEmployeeDataApplied(row); // 通知派生类执行职业差异处理
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
