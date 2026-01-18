// 引用 CYFramework 命名空间，使用框架统一入口
using CYFramework; // CYFramework 入口引用
// 引用泛型集合命名空间，使用 List
using System.Collections.Generic; // 集合类型引用
// 引用实体系统命名空间，使用 EntityBase 等类型
using CYFramework.Core.Entity; // 实体系统类型引用
// 引用 UnityEngine，使用 MonoBehaviour/Transform 等类型
using UnityEngine; // Unity 引擎基础类型引用

/// <summary>
/// 公司实体：提供公司位置与追击距离配置。
/// </summary>
[EntityPrefab("Prefabs/Entities/Game/CompanyEntity", "CompanyEntity", "Scene")] // 绑定实体预制体信息
public class CompanyEntity : EntityBase // 公司实体定义
{
    /// <summary>当前场景中的公司实体（方便敌人获取位置）。</summary>
    public static CompanyEntity Current { get; private set; } // 当前公司实体静态引用
    /// <summary>公司碰撞体缓存（用于距离计算）。</summary>
    private Collider2D _cachedCollider2D; // 碰撞体缓存

    /// <summary>公司强制追击距离（<=该距离时敌人强制追公司）。</summary>
    [SerializeField] private float _forceChaseDistance = 4f; // 公司强制追击距离
    /// <summary>公司吸收范围</summary>
    [SerializeField] private CircleCollider2D _absorptionRange; // 公司吸收黑心范围
    /// <summary>吸收检测缓存（避免运行时分配）。</summary>
    private static readonly Collider2D[] _absorptionHits = new Collider2D[32]; // 吸收检测缓存
    /// <summary>吸收检测过滤器。</summary>
    private ContactFilter2D _absorptionFilter; // 吸收过滤器缓存
    /// <summary>吸收过滤器是否已初始化。</summary>
    private bool _absorptionFilterReady; // 过滤器初始化标记
    /// <summary>当前正在吸收的黑心数量。</summary>
    private int _absorbingCount; // 当前吸收计数
    /// <summary>员工刷新点</summary>
    [SerializeField] private Transform _createPoint; // 公司吸收黑心范围
    /// <summary>普通招聘刷新点 Id（WaveSpawnPoint.PointId）。</summary>
    private const string NormalRecruitSpawnPointId = "EmployeeRecruit"; // 普通招聘刷新点 Id
    /// <summary>普通招聘延迟队列（按波次触发生成）。</summary>
    private readonly List<PendingRecruit> _pendingNormalRecruits = new List<PendingRecruit>(8); // 普通招聘等待队列
    /// <summary>临时工回收队列（按波次回收）。</summary>
    private readonly List<TempRecruitRecord> _tempRecruitRecords = new List<TempRecruitRecord>(8); // 临时工回收队列

    /// <summary>
    /// 普通招聘等待记录：到达目标波次后生成员工。
    /// </summary>
    private struct PendingRecruit // 普通招聘等待记录结构体
    {
        /// <summary>员工配置 Id。</summary>
        public int EmployeeId; // 员工配置 Id
        /// <summary>目标波次数（到达后生成）。</summary>
        public int TargetWaveCount; // 目标波次数
    }

    /// <summary>
    /// 临时工回收记录：到达目标波次后回收员工。
    /// </summary>
    private struct TempRecruitRecord // 临时工回收记录结构体
    {
        /// <summary>临时工实体引用。</summary>
        public UnitEntity Employee; // 临时工实体
        /// <summary>到期波次数。</summary>
        public int ExpireWaveCount; // 到期波次数
    }

    /// <summary>公司强制追击距离（只读）。</summary>
    public float ForceChaseDistance => _forceChaseDistance; // 对外只读访问
    /// <summary>公司碰撞体缓存（只读）。</summary>
    public Collider2D CachedCollider2D => _cachedCollider2D; // 对外只读访问

    /// <summary>
    /// 实体初始化：缓存组件引用。
    /// </summary>
    /// <param name="userData">初始化传入的数据。</param>
    protected override void OnEntityInit(object userData) // 实体初始化入口
    {
        base.OnEntityInit(userData); // 调用父类初始化
        _cachedCollider2D = GetComponent<Collider2D>(); // 缓存碰撞体组件
        PrepareAbsorptionFilter(); // 初始化吸收过滤器
    }

    /// <summary>
    /// 实体显示：注册当前公司实体引用。
    /// </summary>
    /// <param name="userData">显示时传入的数据。</param>
    protected override void OnEntityShow(object userData) // 实体显示入口
    {
        base.OnEntityShow(userData); // 调用父类显示
        Current = this; // 写入当前公司实体
        _absorbingCount = 0; // 重置吸收计数
        _pendingNormalRecruits.Clear(); // 清空普通招聘等待队列
        _tempRecruitRecords.Clear(); // 清空临时工回收队列
        CY.Event.Subscribe<EmployeeRecruitRequestedEvent>(OnEmployeeRecruitRequested, this); // 订阅员工招聘请求事件
        CY.Event.Subscribe<WaveFinishedEvent>(OnWaveFinished, this); // 订阅波次结束事件
    }

    /// <summary>
    /// 实体隐藏：取消事件订阅，避免对象池复用导致重复响应。
    /// </summary>
    protected override void OnEntityHide() // 实体隐藏入口
    {
        CY.Event.UnsubscribeAll(this); // 取消当前公司实体的事件订阅
        _pendingNormalRecruits.Clear(); // 清空普通招聘等待队列
        _tempRecruitRecords.Clear(); // 清空临时工回收队列
        base.OnEntityHide(); // 调用父类隐藏
    }

    /// <summary>
    /// 实体更新：驱动黑心吸收检测。
    /// </summary>
    /// <param name="deltaTime">帧时间。</param>
    protected override void OnEntityUpdate(float deltaTime) // 实体更新入口
    {
        base.OnEntityUpdate(deltaTime); // 调用父类更新
        TryAbsorbBlackHearts(); // 尝试吸收范围内黑心
    }

    /// <summary>
    /// 准备吸收检测过滤器（只初始化一次）。
    /// </summary>
    private void PrepareAbsorptionFilter() // 吸收过滤器初始化入口
    {
        if (_absorptionFilterReady)
        {
            return; // 已初始化时直接返回
        }

        _absorptionFilter = new ContactFilter2D(); // 创建过滤器
        _absorptionFilter.useTriggers = true; // 允许触发器参与检测
        _absorptionFilter.useLayerMask = false; // 关闭层过滤，交由黑心映射判断
        _absorptionFilter.useDepth = false; // 关闭深度过滤
        _absorptionFilterReady = true; // 标记初始化完成
    }

    /// <summary>
    /// 尝试吸收范围内的黑心实体（按并发槽位限制）。
    /// </summary>
    private void TryAbsorbBlackHearts() // 黑心吸收检测入口
    {
        if (_absorptionRange == null)
        {
            return; // 吸收范围未配置时退出
        }

        if (!_absorptionRange.enabled)
        {
            return; // 吸收范围未启用时退出
        }

        PrepareAbsorptionFilter(); // 确保过滤器已准备
        var battleDataManager = CY.BattleDataManager; // 获取战斗数据管理器
        if (battleDataManager == null)
        {
            return; // 管理器未就绪时不吸收
        }

        var capacity = battleDataManager.BlackHeartAbsorbCount; // 读取并发吸收槽位
        if (capacity <= 0)
        {
            capacity = 1; // 容量无效时使用默认值
        }

        if (_absorbingCount >= capacity)
        {
            return; // 无可用槽位时退出
        }

        var hitCount = _absorptionRange.OverlapCollider(_absorptionFilter, _absorptionHits); // 获取范围内碰撞体数量
        if (hitCount <= 0)
        {
            return; // 未检测到碰撞体时退出
        }

        for (int i = 0; i < hitCount; i++)
        {
            var hitCollider = _absorptionHits[i]; // 获取当前命中碰撞体
            if (hitCollider == null)
            {
                continue; // 碰撞体为空时跳过
            }

            if (!BlackHeartEntity.TryGetEntityByCollider(hitCollider, out var blackHeartEntity))
            {
                continue; // 非黑心实体时跳过
            }

            if (blackHeartEntity == null)
            {
                continue; // 黑心实体为空时跳过
            }

            if (!blackHeartEntity.TryBeginAbsorb(this))
            {
                continue; // 无法开始吸收时跳过
            }

            _absorbingCount++; // 递增当前吸收计数
            if (_absorbingCount >= capacity)
            {
                break; // 达到并发上限时停止继续吸收
            }
        }
    }

    /// <summary>
    /// 黑心吸收完成回调：释放槽位并继续吸收下一个。
    /// </summary>
    /// <param name="blackHeartEntity">完成吸收的黑心实体。</param>
    public void NotifyBlackHeartAbsorbed(BlackHeartEntity blackHeartEntity) // 黑心吸收完成通知入口
    {
        if (blackHeartEntity == null)
        {
            return; // 黑心实体为空时退出
        }

        if (_absorbingCount > 0)
        {
            _absorbingCount--; // 释放一个吸收槽位
        }
        else
        {
            _absorbingCount = 0; // 防止计数下溢
        }

        TryAbsorbBlackHearts(); // 尝试补充新的吸收目标
    }

    /// <summary>
    /// 实体回收：清理当前公司实体引用。
    /// </summary>
    protected override void OnEntityRecycle() // 实体回收入口
    {
        if (Current == this)
        {
            Current = null; // 清理静态引用
        }

        _absorbingCount = 0; // 回收时清空吸收计数
        _pendingNormalRecruits.Clear(); // 回收时清空普通招聘等待队列
        _tempRecruitRecords.Clear(); // 回收时清空临时工回收队列
        CY.Event.UnsubscribeAll(this); // 回收时兜底取消事件订阅
        base.OnEntityRecycle(); // 调用父类回收
    }

    /// <summary>
    /// 员工招聘请求回调：急聘/临时工立即生成，普通招聘延迟生成。
    /// </summary>
    /// <param name="evt">招聘请求事件（引用传递）。</param>
    private void OnEmployeeRecruitRequested(ref EmployeeRecruitRequestedEvent evt) // 员工招聘回调入口
    {
        if (Current != this) // 多实例保护判定
        {
            return; // 非当前公司实体时直接退出
        }

        var employeeId = evt.EmployeeId; // 读取员工配置 Id
        if (employeeId <= 0) // Id 无效判定
        {
            return; // Id 无效时直接退出
        }

        var recruitType = evt.RecruitType; // 读取招聘类型
        var recruitWaveCount = evt.RecruitWaveCount; // 读取招聘波数

        if (recruitType == RecruitType.Normal) // 普通招聘判定
        {
            QueueNormalRecruit(employeeId, recruitWaveCount); // 加入普通招聘等待队列
            return; // 普通招聘不立即生成
        }

        if (!TrySpawnEmployeeAtCreatePoint(employeeId, out var employee)) // 尝试在公司刷新点创建员工
        {
            return; // 创建失败时直接退出
        }

        if (recruitType == RecruitType.Temp) // 临时工判定
        {
            RegisterTempEmployee(employee, recruitWaveCount); // 注册临时工回收记录
        }
    }

    /// <summary>
    /// 波次结束回调：处理普通招聘生成与临时工回收。
    /// </summary>
    /// <param name="evt">波次结束事件。</param>
    private void OnWaveFinished(ref WaveFinishedEvent evt) // 波次结束回调入口
    {
        if (Current != this) // 多实例保护判定
        {
            return; // 非当前公司实体时直接退出
        }

        if (!TryGetCurrentWaveCount(out var currentWaveCount)) // 获取当前波次判定
        {
            return; // 获取失败时直接退出
        }

        ProcessPendingNormalRecruits(currentWaveCount); // 处理普通招聘队列
        ProcessTempRecruits(currentWaveCount); // 处理临时工回收
    }

    /// <summary>
    /// 加入普通招聘等待队列。
    /// </summary>
    /// <param name="employeeId">员工配置 Id。</param>
    /// <param name="recruitWaveCount">招聘波数。</param>
    private void QueueNormalRecruit(int employeeId, int recruitWaveCount) // 普通招聘排队入口
    {
        if (!TryGetCurrentWaveCount(out var currentWaveCount)) // 获取当前波次判定
        {
            return; // 获取失败时直接退出
        }

        var targetWaveCount = currentWaveCount + recruitWaveCount; // 计算目标波次数
        var record = new PendingRecruit // 创建等待记录
        {
            EmployeeId = employeeId, // 写入员工配置 Id
            TargetWaveCount = targetWaveCount // 写入目标波次数
        };
        _pendingNormalRecruits.Add(record); // 加入等待队列
    }

    /// <summary>
    /// 注册临时工回收记录。
    /// </summary>
    /// <param name="employee">临时工实体。</param>
    /// <param name="recruitWaveCount">招聘波数。</param>
    private void RegisterTempEmployee(UnitEntity employee, int recruitWaveCount) // 临时工注册入口
    {
        if (employee == null) // 实体为空判定
        {
            return; // 实体为空时直接退出
        }

        if (!TryGetCurrentWaveCount(out var currentWaveCount)) // 获取当前波次判定
        {
            return; // 获取失败时直接退出
        }

        var expireWaveCount = currentWaveCount + recruitWaveCount; // 计算到期波次数
        var record = new TempRecruitRecord // 创建回收记录
        {
            Employee = employee, // 写入临时工实体
            ExpireWaveCount = expireWaveCount // 写入到期波次数
        };
        _tempRecruitRecords.Add(record); // 加入回收队列
    }

    /// <summary>
    /// 处理普通招聘等待队列。
    /// </summary>
    /// <param name="currentWaveCount">当前波次数。</param>
    private void ProcessPendingNormalRecruits(int currentWaveCount) // 普通招聘处理入口
    {
        if (_pendingNormalRecruits.Count <= 0) // 队列为空判定
        {
            return; // 队列为空时直接退出
        }

        for (int i = _pendingNormalRecruits.Count - 1; i >= 0; i--) // 倒序遍历等待队列
        {
            var record = _pendingNormalRecruits[i]; // 获取当前记录
            if (currentWaveCount < record.TargetWaveCount) // 未到达目标波次判定
            {
                continue; // 未到达时跳过
            }

            if (!TryGetNormalRecruitSpawnPosition(out var spawnPosition)) // 获取普通招聘刷新点
            {
                _pendingNormalRecruits.RemoveAt(i); // 刷新点缺失时移除记录
                continue; // 继续下一个记录
            }

            if (!TrySpawnEmployeeAtPosition(record.EmployeeId, spawnPosition, out _)) // 尝试创建员工
            {
                _pendingNormalRecruits.RemoveAt(i); // 创建失败时移除记录
                continue; // 继续下一个记录
            }

            _pendingNormalRecruits.RemoveAt(i); // 生成成功后移除记录
        }
    }

    /// <summary>
    /// 处理临时工回收队列。
    /// </summary>
    /// <param name="currentWaveCount">当前波次数。</param>
    private void ProcessTempRecruits(int currentWaveCount) // 临时工回收入口
    {
        if (_tempRecruitRecords.Count <= 0) // 队列为空判定
        {
            return; // 队列为空时直接退出
        }

        for (int i = _tempRecruitRecords.Count - 1; i >= 0; i--) // 倒序遍历回收队列
        {
            var record = _tempRecruitRecords[i]; // 获取当前记录
            if (currentWaveCount < record.ExpireWaveCount) // 未到期判定
            {
                continue; // 未到期时跳过
            }

            var employee = record.Employee; // 获取临时工实体
            if (employee != null) // 实体有效判定
            {
                CY.Entity.RecycleEntity(employee); // 回收临时工实体
            }

            _tempRecruitRecords.RemoveAt(i); // 移除回收记录
        }
    }

    /// <summary>
    /// 在公司刷新点创建员工。
    /// </summary>
    /// <param name="employeeId">员工配置 Id。</param>
    /// <param name="employee">输出员工实体。</param>
    private bool TrySpawnEmployeeAtCreatePoint(int employeeId, out UnitEntity employee) // 公司刷新点创建入口
    {
        employee = null; // 默认输出为空
        var spawnTransform = _createPoint != null ? _createPoint : transform; // 获取创建点 Transform
        var spawnPosition = spawnTransform.position; // 读取创建点世界坐标
        var spawnPosition2D = new Vector2(spawnPosition.x, spawnPosition.y); // 转换为 2D 坐标
        return TrySpawnEmployeeAtPosition(employeeId, spawnPosition2D, out employee); // 在指定位置创建员工
    }

    /// <summary>
    /// 在指定位置创建员工。
    /// </summary>
    /// <param name="employeeId">员工配置 Id。</param>
    /// <param name="spawnPosition">生成位置（XY 平面）。</param>
    /// <param name="employee">输出员工实体。</param>
    private bool TrySpawnEmployeeAtPosition(int employeeId, Vector2 spawnPosition, out UnitEntity employee) // 指定位置创建入口
    {
        employee = null; // 默认输出为空
        var unitManager = CY.Unit; // 获取单位管理器
        if (unitManager == null) // 管理器为空判定
        {
            CY.LogWarning("[CompanyEntity] UnitManager 未就绪，无法创建员工。"); // 输出警告日志
            return false; // 管理器为空时返回失败
        }

        if (!unitManager.TryCreateEmployee(employeeId, spawnPosition, out employee)) // 尝试创建员工实体
        {
            CY.LogWarning($"[CompanyEntity] 创建员工失败，EmployeeId={employeeId}"); // 输出创建失败日志
            return false; // 创建失败时返回失败
        }

        return true; // 创建成功返回 true
    }

    /// <summary>
    /// 获取普通招聘刷新点位置。
    /// </summary>
    /// <param name="position">输出刷新点位置。</param>
    private bool TryGetNormalRecruitSpawnPosition(out Vector2 position) // 普通招聘刷新点获取入口
    {
        position = Vector2.zero; // 默认输出为零点
        if (!WaveSpawnPoint.TryGetRandomPoint(NormalRecruitSpawnPointId, out position)) // 获取命名点判定
        {
            CY.LogWarning($"[CompanyEntity] 未找到普通招聘刷新点，PointId={NormalRecruitSpawnPointId}"); // 输出缺失日志
            return false; // 获取失败时返回 false
        }

        return true; // 获取成功返回 true
    }

    /// <summary>
    /// 获取当前波次数（使用 WaveManager.CurrentWaveCount）。
    /// </summary>
    /// <param name="waveCount">输出波次数。</param>
    private bool TryGetCurrentWaveCount(out int waveCount) // 波次数获取入口
    {
        waveCount = 0; // 默认输出为 0
        var waveManager = CY.Wave; // 获取波次管理器
        if (waveManager == null) // 管理器为空判定
        {
            CY.LogWarning("[CompanyEntity] WaveManager 未就绪，无法计算波次数。"); // 输出警告日志
            return false; // 管理器为空时返回失败
        }

        waveCount = waveManager.CurrentWaveCount; // 读取当前波次数
        return true; // 返回获取成功
    }

}
