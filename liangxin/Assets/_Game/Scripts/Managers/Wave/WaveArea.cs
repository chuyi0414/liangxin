// 引用泛型集合命名空间，使用 Dictionary/List
using System.Collections.Generic; // 集合类型引用
// 引用 CYFramework 入口，使用 CY.Event
using CYFramework; // 框架事件入口引用
// 引用 UnityEngine，使用 MonoBehaviour/Collider2D/Random
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// 波次区域触发与区域刷怪采样组件。
/// </summary>
public sealed class WaveArea : MonoBehaviour // 波次区域组件定义
{
    /// <summary>区域 Id（用于触发与刷怪）。</summary>
    [SerializeField] private string _areaId; // 区域 Id
    /// <summary>是否仅允许玩家触发区域事件。</summary>
    [SerializeField] private bool _onlyPlayer = true; // 仅玩家触发开关

    /// <summary>区域 Id（只读）。</summary>
    public string AreaId => _areaId; // 对外只读访问

    /// <summary>全局区域注册表（Id -> 列表）。</summary>
    private static readonly Dictionary<string, List<WaveArea>> Areas = new Dictionary<string, List<WaveArea>>(16); // 区域注册表

    /// <summary>碰撞体缓存。</summary>
    private Collider2D _collider2D; // 区域碰撞体缓存
    /// <summary>是否已注册。</summary>
    private bool _registered; // 注册标记

    /// <summary>
    /// 组件初始化：缓存碰撞体。
    /// </summary>
    private void Awake() // 生命周期：Awake
    {
        _collider2D = GetComponent<Collider2D>(); // 缓存碰撞体组件
        if (_collider2D == null) // 碰撞体缺失判定
        {
            CY.LogWarning("[WaveArea] 未挂载 Collider2D，区域触发与采样将不可用。"); // 输出警告日志
        }
    }

    /// <summary>
    /// 组件启用：注册区域。
    /// </summary>
    private void OnEnable() // 生命周期：OnEnable
    {
        Register(); // 注册区域
    }

    /// <summary>
    /// 组件禁用：注销区域。
    /// </summary>
    private void OnDisable() // 生命周期：OnDisable
    {
        Unregister(); // 注销区域
    }

    /// <summary>
    /// 编辑器校验：更新注册信息。
    /// </summary>
    private void OnValidate() // 生命周期：OnValidate
    {
        if (!Application.isPlaying) // 编辑器非运行判定
        {
            Unregister(); // 清理旧注册
            Register(); // 重新注册
        }
    }

    /// <summary>
    /// 触发进入：派发区域触发事件。
    /// </summary>
    /// <param name="other">进入的碰撞体。</param>
    private void OnTriggerEnter2D(Collider2D other) // 触发进入入口
    {
        if (!IsValidTrigger(other)) // 触发对象筛选
        {
            return; // 非有效触发时退出
        }

        PostAreaTriggerEvent(true); // 派发进入事件
    }

    /// <summary>
    /// 触发离开：派发区域触发事件。
    /// </summary>
    /// <param name="other">离开的碰撞体。</param>
    private void OnTriggerExit2D(Collider2D other) // 触发离开入口
    {
        if (!IsValidTrigger(other)) // 触发对象筛选
        {
            return; // 非有效触发时退出
        }

        PostAreaTriggerEvent(false); // 派发离开事件
    }

    /// <summary>
    /// 检查触发对象是否有效。
    /// </summary>
    /// <param name="other">触发碰撞体。</param>
    private bool IsValidTrigger(Collider2D other) // 触发筛选入口
    {
        if (other == null) // 碰撞体为空判定
        {
            return false; // 无效碰撞体时返回 false
        }

        if (string.IsNullOrEmpty(_areaId)) // 区域 Id 判定
        {
            return false; // 区域 Id 为空时返回 false
        }

        if (!_onlyPlayer) // 不限制触发对象判定
        {
            return true; // 不限制时直接通过
        }

        var unit = other.GetComponentInParent<UnitEntity>(); // 获取单位实体
        if (unit == null) // 单位为空判定
        {
            return false; // 无单位时返回 false
        }

        return unit.Camp == UnitCamp.Player; // 仅允许玩家触发
    }

    /// <summary>
    /// 派发区域触发事件。
    /// </summary>
    /// <param name="isEnter">是否进入。</param>
    private void PostAreaTriggerEvent(bool isEnter) // 事件派发入口
    {
        var evt = new WaveAreaTriggerEvent // 创建事件数据
        {
            AreaId = _areaId, // 写入区域 Id
            IsEnter = isEnter // 写入进入/离开标记
        };
        CY.Event.Post(ref evt); // 派发事件
    }

    /// <summary>
    /// 获取指定区域内随机点（用于刷怪）。
    /// </summary>
    /// <param name="areaId">区域 Id。</param>
    /// <param name="position">输出位置。</param>
    public static bool TryGetRandomPoint(string areaId, out Vector2 position) // 随机点获取入口
    {
        position = Vector2.zero; // 默认输出
        if (string.IsNullOrEmpty(areaId)) // 区域 Id 判定
        {
            return false; // 无效 Id 时返回 false
        }

        if (!Areas.TryGetValue(areaId, out var list) || list == null || list.Count == 0) // 查找区域列表
        {
            return false; // 未找到区域时返回 false
        }

        for (int attempt = list.Count - 1; attempt >= 0; attempt--) // 迭代尝试列表
        {
            var index = Random.Range(0, list.Count); // 随机索引
            var area = list[index]; // 获取区域
            if (area == null) // 区域为空判定
            {
                list.RemoveAt(index); // 移除空引用
                continue; // 继续尝试
            }

            if (area.TryGetPointInArea(out position)) // 尝试采样点
            {
                return true; // 采样成功返回 true
            }
        }

        if (list.Count == 0) // 列表为空判定
        {
            Areas.Remove(areaId); // 移除空列表
        }

        return false; // 无法采样时返回 false
    }

    /// <summary>
    /// 尝试在当前区域内获取随机点。
    /// </summary>
    /// <param name="position">输出位置。</param>
    private bool TryGetPointInArea(out Vector2 position) // 区域采样入口
    {
        position = (Vector2)transform.position; // 默认位置回退到中心
        if (_collider2D == null) // 碰撞体缺失判定
        {
            return false; // 无碰撞体时返回 false
        }

        var bounds = _collider2D.bounds; // 获取碰撞体包围盒
        for (int i = 0; i < 12; i++) // 采样次数限制
        {
            var x = Random.Range(bounds.min.x, bounds.max.x); // 随机 X
            var y = Random.Range(bounds.min.y, bounds.max.y); // 随机 Y
            var candidate = new Vector2(x, y); // 生成候选点
            if (_collider2D.OverlapPoint(candidate)) // 判断是否在区域内
            {
                position = candidate; // 写入候选点
                return true; // 采样成功返回 true
            }
        }

        return false; // 未采样到有效点返回 false
    }

    /// <summary>
    /// 注册区域。
    /// </summary>
    private void Register() // 注册入口
    {
        if (_registered) // 已注册判定
        {
            return; // 已注册时直接退出
        }

        if (string.IsNullOrEmpty(_areaId)) // 区域 Id 判定
        {
            return; // Id 为空时不注册
        }

        if (!Areas.TryGetValue(_areaId, out var list)) // 获取列表
        {
            list = new List<WaveArea>(4); // 创建列表
            Areas.Add(_areaId, list); // 写入注册表
        }

        for (int i = 0; i < list.Count; i++) // 遍历列表
        {
            if (list[i] == this) // 已注册判定
            {
                _registered = true; // 标记已注册
                return; // 已存在时退出
            }
        }

        list.Add(this); // 添加到列表
        _registered = true; // 标记已注册
    }

    /// <summary>
    /// 注销区域。
    /// </summary>
    private void Unregister() // 注销入口
    {
        if (!_registered) // 未注册判定
        {
            return; // 未注册时退出
        }

        if (string.IsNullOrEmpty(_areaId)) // 区域 Id 判定
        {
            _registered = false; // 清理注册标记
            return; // Id 为空时退出
        }

        if (Areas.TryGetValue(_areaId, out var list) && list != null) // 获取列表
        {
            for (int i = list.Count - 1; i >= 0; i--) // 反向遍历
            {
                if (list[i] == this) // 命中自身判定
                {
                    list.RemoveAt(i); // 移除自身
                    break; // 移除后退出
                }
            }

            if (list.Count == 0) // 列表为空判定
            {
                Areas.Remove(_areaId); // 移除空列表
            }
        }

        _registered = false; // 清理注册标记
    }
}
