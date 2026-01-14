// 引用 CYFramework 命名空间，使用 CY.Timer 与日志
using CYFramework; // 框架入口引用
// 引用 CYFramework 计时器命名空间，使用 Timer
using CYFramework.Core.Timer; // 计时器类型引用
// 引用泛型集合命名空间，使用 List
using System.Collections.Generic; // 泛型集合引用
// 引用 UnityEngine 命名空间，使用 MonoBehaviour/LineRenderer/SpriteRenderer 等类型
using UnityEngine; // Unity 引擎基础类型引用

/// <summary>
/// 员工移动路径可视化器：在员工右键移动后显示“从脚下到终点”的路径线与路径点，并在移动过程中持续更新。
/// 目标：让玩家清晰看到单位的移动路线（RTS 常见交互），并且不产生运行时 GC（使用 NonAlloc 拷贝与对象复用）。
/// </summary>
[DisallowMultipleComponent] // 禁止重复挂载，避免重复绘制
public sealed class EmployeeMovePathVisualizer : MonoBehaviour // 员工移动路径可视化器组件
{
    /// <summary>
    /// 全局配置资源路径（Resources 相对路径，无扩展名）。
    /// </summary>
    private const string GlobalConfigResourcePath = "Configs/EmployeeMovePathVisualizerConfig"; // 全局配置资源路径常量

    /// <summary>
    /// 是否已尝试加载全局配置：用于避免反复 Load 导致开销与日志刷屏。
    /// </summary>
    private static bool _hasTriedLoadGlobalConfig; // 全局配置加载尝试标记

    /// <summary>
    /// 缓存的全局配置：从 Resources 加载后静态缓存，供所有实例复用。
    /// </summary>
    private static EmployeeMovePathVisualizerConfig _cachedGlobalConfig; // 全局配置静态缓存

    /// <summary>
    /// 是否使用全局配置：为 true 时会从 Resources 中读取统一配置并覆盖本组件的参数。
    /// 说明：该字段主要用于“自动添加的可视化器”，让未手动配置的员工统一走全局默认值。
    /// </summary>
    [SerializeField, HideInInspector] private bool _useGlobalConfig; // 是否使用全局配置（由代码控制，避免手动误改）
    /// <summary>
    /// 路径刷新间隔（秒）：用于在移动过程中同步导航重算后的路径变化。
    /// </summary>
    [SerializeField] private float _refreshInterval = 0.1f; // 刷新间隔配置

    /// <summary>
    /// 路径线排序值：用于控制 LineRenderer 的渲染顺序（2D 项目常用）。
    /// </summary>
    [SerializeField] private int _lineSortingOrder = 999; // 线排序值配置

    /// <summary>
    /// 路径线宽度（世界单位）。
    /// </summary>
    [SerializeField] private float _lineWidth = 0.05f; // 线宽配置

    /// <summary>
    /// 路径点排序值：用于控制 SpriteRenderer 的渲染顺序（2D 项目常用）。
    /// </summary>
    [SerializeField] private int _pointSortingOrder = 1000; // 点排序值配置

    /// <summary>
    /// 路径点显示大小（世界单位）：用于显示拐点/路径点。
    /// </summary>
    [SerializeField] private float _pointSize = 0.12f; // 点大小配置

    /// <summary>
    /// 路径可视化最多显示的点数量：用于限制绘制成本，避免极端路径导致点数过多。
    /// </summary>
    [SerializeField] private int _maxVisiblePoints = 64; // 最大点数配置

    /// <summary>
    /// 绘制层 Z 偏移：用于避免与地面/角色精灵完全重叠导致闪烁。
    /// </summary>
    [SerializeField] private float _zOffset = -0.1f; // Z 偏移配置

    /// <summary>
    /// 路径线颜色（含透明度）。
    /// </summary>
    [SerializeField] private Color _lineColor = new Color(0.2f, 0.9f, 1f, 0.85f); // 路径线颜色配置

    /// <summary>
    /// 路径点颜色（含透明度）。
    /// </summary>
    [SerializeField] private Color _pointColor = new Color(0.2f, 0.9f, 1f, 0.95f); // 路径点颜色配置

    /// <summary>
    /// 路径线材质覆盖：用于自定义 LineRenderer 的材质（例如发光/渐变等）。
    /// 说明：为空时使用脚本内置的共享默认材质（Sprites/Default）。
    /// </summary>
    [SerializeField] private Material _lineMaterialOverride; // 路径线材质覆盖

    /// <summary>
    /// 路径点材质覆盖：用于自定义 SpriteRenderer 的材质（例如发光/软边等）。
    /// 说明：为空时使用 SpriteRenderer 默认材质（Unity 内置）。
    /// </summary>
    [SerializeField] private Material _pointMaterialOverride; // 路径点材质覆盖

    /// <summary>
    /// 当前绑定的导航代理：用于读取剩余路径点。
    /// </summary>
    private HybridNavigationAgent _agent; // 导航代理引用

    /// <summary>
    /// 路径线渲染器：用于显示路径线。
    /// </summary>
    private LineRenderer _lineRenderer; // LineRenderer 缓存

    /// <summary>
    /// 路径点渲染器列表：用于显示每个路径点（对象复用，不频繁创建/销毁）。
    /// </summary>
    private readonly List<SpriteRenderer> _pointRenderers = new List<SpriteRenderer>(64); // 点渲染器缓存列表

    /// <summary>
    /// 路径点缓存数组：从导航代理 NonAlloc 拷贝出来的“剩余路径点”。
    /// </summary>
    private Vector2[] _pathPoints; // 路径点缓存数组

    /// <summary>
    /// 刷新计时器：用于循环更新路径线与路径点。
    /// </summary>
    private Timer _refreshTimer; // 刷新循环计时器

    /// <summary>
    /// 共享点精灵：使用白贴图创建的 Sprite，避免依赖额外资源文件。
    /// </summary>
    private static Sprite _sharedPointSprite; // 共享点精灵缓存

    /// <summary>
    /// 共享路径线材质：用于 LineRenderer，避免每个实例都创建新材质。
    /// </summary>
    private static Material _sharedLineMaterial; // 共享材质缓存

    /// <summary>
    /// 组件初始化：准备 LineRenderer、缓存数组与默认隐藏状态。
    /// </summary>
    private void Awake() // 生命周期：Awake
    {
        ApplyGlobalConfigIfNeeded(); // 若启用全局配置则优先应用（让默认值统一）
        EnsureConfigValid(); // 校验并修正配置参数
        PrepareLineRenderer(); // 准备 LineRenderer
        PreparePathBuffer(); // 准备路径点缓存数组
        HideImmediate(); // 默认隐藏（避免场景启动就显示）
    }

    /// <summary>
    /// 显示并绑定导航代理：开始持续刷新路径可视化，直到导航结束（HasPath=false）。
    /// </summary>
    /// <param name="agent">导航代理。</param>
    public void Show(HybridNavigationAgent agent) // 显示入口
    {
        ApplyGlobalConfigIfNeeded(); // 显示前应用全局配置（支持运行时调整全局资源后生效）
        EnsureConfigValid(); // 兜底校验配置（避免全局配置误填导致异常）
        PreparePathBuffer(); // 兜底准备缓存数组（支持运行时调整最大点数）
        _agent = agent; // 绑定导航代理
        if (_agent == null) // 代理为空判定
        {
            HideImmediate(); // 代理为空时直接隐藏
            return; // 结束
        }

        if (_lineRenderer == null) // 线渲染器缺失判定
        {
            PrepareLineRenderer(); // 兜底准备线渲染器
        }

        StartRefreshTimer(); // 启动刷新计时器
        RefreshNow(); // 立刻刷新一次，确保点击后即时显示
    }

    /// <summary>
    /// 设置是否使用全局配置：用于区分“自动添加组件走全局”与“手动添加组件走本地”两种场景。
    /// 注意：AddComponent 可能会先触发 Awake，因此此方法需要在运行时调用后再次应用配置。
    /// </summary>
    /// <param name="useGlobalConfig">是否使用全局配置。</param>
    public void SetUseGlobalConfig(bool useGlobalConfig) // 全局配置开关设置入口
    {
        _useGlobalConfig = useGlobalConfig; // 写入开关状态
        ApplyGlobalConfigIfNeeded(); // 若启用则立即应用全局配置
        EnsureConfigValid(); // 校验并修正参数
        PrepareLineRenderer(); // 兜底准备 LineRenderer
        PreparePathBuffer(); // 兜底准备缓存数组
    }

    /// <summary>
    /// 立刻隐藏并停止刷新：用于单位隐藏/回收或到达终点后自动隐藏。
    /// </summary>
    public void HideImmediate() // 立刻隐藏入口
    {
        StopRefreshTimer(); // 停止刷新计时器
        SetVisible(false); // 隐藏所有可视化
        _agent = null; // 清空代理引用
    }

    /// <summary>
    /// 校验并修正配置参数：防止 Inspector 误配置导致异常或不显示。
    /// </summary>
    private void EnsureConfigValid() // 配置校验入口
    {
        if (_refreshInterval <= 0f) // 刷新间隔非法判定
        {
            _refreshInterval = 0.1f; // 回退到默认值
        }

        if (_maxVisiblePoints <= 0) // 最大点数非法判定
        {
            _maxVisiblePoints = 32; // 回退到默认值
        }

        if (_lineWidth <= 0f) // 线宽非法判定
        {
            _lineWidth = 0.05f; // 回退到默认值
        }

        if (_pointSize <= 0f) // 点大小非法判定
        {
            _pointSize = 0.12f; // 回退到默认值
        }

        // 注意：排序值允许为负（Unity 支持），因此不强制修正 SortingOrder。 // 排序值说明注释
    }

    /// <summary>
    /// 若启用全局配置，则从 Resources 加载全局配置并覆盖本组件参数。
    /// </summary>
    private void ApplyGlobalConfigIfNeeded() // 全局配置应用入口
    {
        if (!_useGlobalConfig) // 未启用全局配置判定
        {
            return; // 未启用时直接退出（保持本地 Inspector 配置）
        }

        var config = GetGlobalConfig(); // 获取全局配置（静态缓存）
        if (config == null) // 全局配置缺失判定
        {
            return; // 缺失时不覆盖本地参数
        }

        _refreshInterval = config.RefreshInterval; // 覆盖刷新间隔
        _lineWidth = config.LineWidth; // 覆盖线宽
        _pointSize = config.PointSize; // 覆盖点大小
        _maxVisiblePoints = config.MaxVisiblePoints; // 覆盖最大点数
        _zOffset = config.ZOffset; // 覆盖 Z 偏移
        _lineColor = config.LineColor; // 覆盖线颜色
        _pointColor = config.PointColor; // 覆盖点颜色
        _lineSortingOrder = config.LineSortingOrder; // 覆盖线排序值
        _pointSortingOrder = config.PointSortingOrder; // 覆盖点排序值
        _lineMaterialOverride = config.LineMaterialOverride; // 覆盖线材质
        _pointMaterialOverride = config.PointMaterialOverride; // 覆盖点材质
    }

    /// <summary>
    /// 获取全局配置：只会尝试加载一次并静态缓存。
    /// </summary>
    private static EmployeeMovePathVisualizerConfig GetGlobalConfig() // 全局配置获取入口
    {
        if (_hasTriedLoadGlobalConfig) // 已尝试加载判定
        {
            return _cachedGlobalConfig; // 返回缓存结果（可能为 null）
        }

        _hasTriedLoadGlobalConfig = true; // 标记已尝试加载
        _cachedGlobalConfig = CY.Resource.Load<EmployeeMovePathVisualizerConfig>(GlobalConfigResourcePath); // 从 Resources 加载全局配置
        if (_cachedGlobalConfig == null) // 读取失败判定
        {
            CY.LogWarning($"[EmployeeMovePathVisualizer] 未找到全局配置资源：{GlobalConfigResourcePath}"); // 输出缺失提示（仅一次）
        }

        return _cachedGlobalConfig; // 返回加载结果
    }

    /// <summary>
    /// 准备路径点缓存数组：用于从导航代理 NonAlloc 拷贝路径点。
    /// </summary>
    private void PreparePathBuffer() // 缓存数组准备入口
    {
        if (_pathPoints != null && _pathPoints.Length == _maxVisiblePoints) // 已准备且长度匹配判定
        {
            return; // 已准备则直接返回
        }

        _pathPoints = new Vector2[_maxVisiblePoints]; // 分配路径点缓存数组（一次性分配）
    }

    /// <summary>
    /// 准备 LineRenderer：设置材质、线宽、颜色与基础参数。
    /// </summary>
    private void PrepareLineRenderer() // LineRenderer 准备入口
    {
        _lineRenderer = GetComponent<LineRenderer>(); // 尝试获取现有 LineRenderer
        if (_lineRenderer == null) // 未挂载判定
        {
            _lineRenderer = gameObject.AddComponent<LineRenderer>(); // 动态添加 LineRenderer（无需手改预制体）
        }

        _lineRenderer.useWorldSpace = true; // 使用世界坐标绘制
        _lineRenderer.loop = false; // 不闭合
        _lineRenderer.positionCount = 0; // 初始化为空
        _lineRenderer.widthMultiplier = _lineWidth; // 设置线宽
        _lineRenderer.startColor = _lineColor; // 设置起点颜色
        _lineRenderer.endColor = _lineColor; // 设置终点颜色
        _lineRenderer.numCornerVertices = 2; // 增加拐角细分，避免折线过硬
        _lineRenderer.numCapVertices = 2; // 增加端点细分，端点更圆润
        _lineRenderer.alignment = LineAlignment.View; // 面向相机，避免旋转导致消失
        _lineRenderer.sortingOrder = _lineSortingOrder; // 设置排序值（2D 项目常用）
        ApplyLineMaterial(); // 应用路径线材质（支持材质覆盖）
    }

    /// <summary>
    /// 应用路径线材质：优先使用材质覆盖，其次使用共享默认材质。
    /// </summary>
    private void ApplyLineMaterial() // 路径线材质应用入口
    {
        if (_lineRenderer == null) // 线渲染器缺失判定
        {
            return; // 缺失时直接退出
        }

        if (_lineMaterialOverride != null) // 配置了材质覆盖判定
        {
            _lineRenderer.sharedMaterial = _lineMaterialOverride; // 使用覆盖材质（共享引用，避免实例化）
            return; // 已应用覆盖材质时结束
        }

        _lineRenderer.sharedMaterial = GetSharedLineMaterial(); // 使用共享默认材质，避免每实例创建材质
    }

    /// <summary>
    /// 获取共享路径线材质：优先复用静态缓存。
    /// </summary>
    private static Material GetSharedLineMaterial() // 共享材质获取入口
    {
        if (_sharedLineMaterial != null) // 已缓存判定
        {
            return _sharedLineMaterial; // 直接返回缓存材质
        }

        var shader = Shader.Find("Sprites/Default"); // 查找 Sprite 默认 Shader（项目一般内置）
        if (shader == null) // Shader 缺失判定
        {
            shader = Shader.Find("Unlit/Color"); // 兜底查找 Unlit/Color
        }

        _sharedLineMaterial = shader != null ? new Material(shader) : null; // 创建共享材质（一次性分配）
        if (_sharedLineMaterial != null) // 创建成功判定
        {
            _sharedLineMaterial.hideFlags = HideFlags.HideAndDontSave; // 隐藏并避免被保存到场景/资源
        }

        return _sharedLineMaterial; // 返回共享材质
    }

    /// <summary>
    /// 获取共享点精灵：使用白贴图创建一个 1x1 的 Sprite 作为路径点。
    /// </summary>
    private static Sprite GetSharedPointSprite() // 共享点精灵获取入口
    {
        if (_sharedPointSprite != null) // 已缓存判定
        {
            return _sharedPointSprite; // 直接返回缓存
        }

        var tex = Texture2D.whiteTexture; // 获取 Unity 内置白贴图（无额外资源依赖）
        var rect = new Rect(0f, 0f, tex.width, tex.height); // 使用整张贴图区域
        var pivot = new Vector2(0.5f, 0.5f); // 设置中心点为 pivot
        _sharedPointSprite = Sprite.Create(tex, rect, pivot, 100f); // 创建 Sprite（一次性分配，100 像素/单位方便缩放）
        return _sharedPointSprite; // 返回共享点精灵
    }

    /// <summary>
    /// 启动刷新计时器：按固定间隔刷新路径可视化。
    /// </summary>
    private void StartRefreshTimer() // 刷新计时器启动入口
    {
        StopRefreshTimer(); // 启动前先停止旧计时器，避免重复 Loop
        _refreshTimer = CY.Timer.Loop(_refreshInterval, RefreshNow); // 启动循环计时器（不捕获闭包）
    }

    /// <summary>
    /// 停止刷新计时器：用于隐藏或销毁时清理。
    /// </summary>
    private void StopRefreshTimer() // 刷新计时器停止入口
    {
        if (_refreshTimer == null) // 计时器为空判定
        {
            return; // 为空时直接返回
        }

        _refreshTimer.Stop(); // 停止计时器
        _refreshTimer = null; // 清空计时器引用
    }

    /// <summary>
    /// 立即刷新一次路径显示：从导航代理获取“剩余路径点”，并更新线与点。
    /// </summary>
    private void RefreshNow() // 路径刷新入口
    {
        if (_agent == null) // 代理为空判定
        {
            HideImmediate(); // 代理为空时隐藏
            return; // 结束
        }

        if (!_agent.HasPath) // 已无路径（到达终点）判定
        {
            HideImmediate(); // 到达终点时自动隐藏
            return; // 结束
        }

        if (_pathPoints == null || _pathPoints.Length == 0) // 缓存数组缺失判定
        {
            PreparePathBuffer(); // 兜底准备缓存数组
        }

        var pointCount = _agent.CopyRemainingPathPointsNonAlloc(_pathPoints, true); // 拷贝“脚下+剩余路径点”
        if (pointCount < 2) // 点数不足以绘制线段判定
        {
            HideImmediate(); // 点数不足时隐藏（避免显示异常）
            return; // 结束
        }

        SetVisible(true); // 确保可视化处于显示状态
        UpdateLine(pointCount); // 更新路径线
        UpdatePoints(pointCount); // 更新路径点
    }

    /// <summary>
    /// 更新路径线：把缓存点写入 LineRenderer。
    /// </summary>
    /// <param name="pointCount">有效点数量。</param>
    private void UpdateLine(int pointCount) // 路径线更新入口
    {
        if (_lineRenderer == null) // 线渲染器缺失判定
        {
            return; // 缺失时直接退出
        }

        ApplyLineMaterial(); // 刷新时再次应用材质（支持运行时改材质覆盖）
        _lineRenderer.widthMultiplier = _lineWidth; // 同步线宽（允许运行时调参）
        _lineRenderer.startColor = _lineColor; // 同步颜色（允许运行时调参）
        _lineRenderer.endColor = _lineColor; // 同步颜色（允许运行时调参）
        _lineRenderer.sortingOrder = _lineSortingOrder; // 同步排序值（允许运行时调参）
        _lineRenderer.positionCount = pointCount; // 设置点数量

        var z = transform.position.z + _zOffset; // 计算绘制 Z（相对自身）
        for (int i = 0; i < pointCount; i++) // 遍历所有点
        {
            var p = _pathPoints[i]; // 读取缓存点
            _lineRenderer.SetPosition(i, new Vector3(p.x, p.y, z)); // 写入 LineRenderer 位置
        }
    }

    /// <summary>
    /// 更新路径点：确保点对象数量足够并摆放到对应位置。
    /// </summary>
    /// <param name="pointCount">有效点数量。</param>
    private void UpdatePoints(int pointCount) // 路径点更新入口
    {
        EnsurePointRendererCount(pointCount); // 确保点对象数量足够

        var z = transform.position.z + _zOffset; // 计算绘制 Z（相对自身）
        for (int i = 0; i < _pointRenderers.Count; i++) // 遍历已创建的点渲染器
        {
            var renderer = _pointRenderers[i]; // 获取当前点渲染器
            if (renderer == null) // 渲染器为空判定
            {
                continue; // 为空时跳过
            }

            if (i >= pointCount) // 超出有效点数量判定
            {
                renderer.gameObject.SetActive(false); // 关闭多余点对象
                continue; // 继续下一项
            }

            var p = _pathPoints[i]; // 读取缓存点
            renderer.transform.position = new Vector3(p.x, p.y, z); // 设置点位置
            renderer.color = _pointColor; // 同步点颜色（允许运行时调参）
            ApplyPointMaterial(renderer); // 刷新时再次应用材质（支持运行时改材质覆盖）
            renderer.sortingOrder = _pointSortingOrder; // 同步点排序值（允许运行时调参）
            renderer.transform.localScale = new Vector3(_pointSize, _pointSize, 1f); // 设置点大小
            renderer.gameObject.SetActive(true); // 启用点对象
        }
    }

    /// <summary>
    /// 确保路径点渲染器数量足够：不足则创建并缓存（对象复用）。
    /// </summary>
    /// <param name="requiredCount">需要的数量。</param>
    private void EnsurePointRendererCount(int requiredCount) // 点对象数量确保入口
    {
        if (requiredCount <= 0) // 需求数量非法判定
        {
            return; // 非法时直接退出
        }

        if (requiredCount > _maxVisiblePoints) // 超出最大限制判定
        {
            requiredCount = _maxVisiblePoints; // 裁剪到最大可见点数
        }

        while (_pointRenderers.Count < requiredCount) // 数量不足时循环创建
        {
            var index = _pointRenderers.Count; // 获取即将创建的索引
            var go = new GameObject($"MovePathPoint_{index}"); // 创建点对象（一次性创建，后续复用）
            go.transform.SetParent(transform, false); // 挂到自身下面，便于跟随单位层级
            go.transform.localPosition = Vector3.zero; // 初始化本地位置
            go.transform.localRotation = Quaternion.identity; // 初始化本地旋转
            go.transform.localScale = Vector3.one; // 初始化本地缩放

            var renderer = go.AddComponent<SpriteRenderer>(); // 添加 SpriteRenderer 用于显示点
            renderer.sprite = GetSharedPointSprite(); // 设置共享点精灵
            renderer.color = _pointColor; // 设置默认颜色
            ApplyPointMaterial(renderer); // 应用路径点材质（支持材质覆盖）
            renderer.sortingOrder = _pointSortingOrder; // 设置排序值，尽量显示在角色与地面之上（可按项目需要调整）

            go.SetActive(false); // 默认先关闭，显示时再启用
            _pointRenderers.Add(renderer); // 缓存渲染器引用
        }
    }

    /// <summary>
    /// 应用路径点材质：当配置了材质覆盖时，强制使用覆盖材质；否则使用默认材质。
    /// </summary>
    /// <param name="renderer">路径点 SpriteRenderer。</param>
    private void ApplyPointMaterial(SpriteRenderer renderer) // 路径点材质应用入口
    {
        if (renderer == null) // 渲染器为空判定
        {
            return; // 为空时直接退出
        }

        if (_pointMaterialOverride == null) // 未配置材质覆盖判定
        {
            return; // 不覆盖时保留 SpriteRenderer 默认材质
        }

        renderer.sharedMaterial = _pointMaterialOverride; // 使用覆盖材质（共享引用，避免实例化）
    }

    /// <summary>
    /// 设置整体可见性：控制 LineRenderer 与点对象的显示/隐藏。
    /// </summary>
    /// <param name="visible">是否可见。</param>
    private void SetVisible(bool visible) // 可见性设置入口
    {
        if (_lineRenderer != null) // 线渲染器存在判定
        {
            _lineRenderer.enabled = visible; // 设置线渲染器可见性
            if (!visible) // 隐藏判定
            {
                _lineRenderer.positionCount = 0; // 隐藏时清空点数量，避免残留
            }
        }

        for (int i = 0; i < _pointRenderers.Count; i++) // 遍历点对象
        {
            var renderer = _pointRenderers[i]; // 获取渲染器
            if (renderer == null) // 渲染器为空判定
            {
                continue; // 为空时跳过
            }

            renderer.gameObject.SetActive(visible); // 同步点对象可见性
        }
    }

    /// <summary>
    /// 组件销毁：停止计时器，避免回调访问已销毁对象。
    /// </summary>
    private void OnDestroy() // 生命周期：OnDestroy
    {
        StopRefreshTimer(); // 兜底停止刷新计时器
        _agent = null; // 清空代理引用
    }
}
