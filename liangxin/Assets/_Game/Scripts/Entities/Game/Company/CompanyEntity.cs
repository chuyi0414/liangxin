// 引用 CYFramework 命名空间，使用框架统一入口
using CYFramework; // CYFramework 入口引用
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
    [SerializeField] private float _forceChaseDistance = 2f; // 公司强制追击距离
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
        CY.Event.Subscribe<EmployeeRecruitRequestedEvent>(OnEmployeeRecruitRequested, this); // 订阅员工招聘请求事件
    }

    /// <summary>
    /// 实体隐藏：取消事件订阅，避免对象池复用导致重复响应。
    /// </summary>
    protected override void OnEntityHide() // 实体隐藏入口
    {
        CY.Event.UnsubscribeAll(this); // 取消当前公司实体的事件订阅
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
        CY.Event.UnsubscribeAll(this); // 回收时兜底取消事件订阅
        base.OnEntityRecycle(); // 调用父类回收
    }

    /// <summary>
    /// 员工招聘请求回调：在公司刷新点创建员工实体。
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

        var unitManager = CY.Unit; // 获取单位管理器
        if (unitManager == null) // 管理器为空判定
        {
            CY.LogWarning("[CompanyEntity] UnitManager 未就绪，无法创建员工。"); // 输出警告日志
            return; // 管理器为空时退出
        }

        var spawnTransform = _createPoint != null ? _createPoint : transform; // 获取创建点（缺失则回退公司本体）
        var spawnPosition = spawnTransform.position; // 读取创建点世界坐标
        var spawnPosition2D = new Vector2(spawnPosition.x, spawnPosition.y); // 转换为 2D 坐标（XY 平面）

        if (!unitManager.TryCreateEmployee(employeeId, spawnPosition2D, out _)) // 尝试创建员工实体
        {
            CY.LogWarning($"[CompanyEntity] 创建员工失败，EmployeeId={employeeId}"); // 输出创建失败日志
        }
    }
}
