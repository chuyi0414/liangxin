// 引用泛型集合命名空间，使用 Dictionary/List
using System.Collections.Generic; // 集合类型引用
// 引用 UnityEngine，使用 MonoBehaviour/Vector2/Random
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// 波次刷新命名点：用于 SpecialPoint 刷新类型（圆形区域）。
/// </summary>
public sealed class WaveSpawnPoint : MonoBehaviour // 刷新命名点组件
{
    /// <summary>命名点 Id（与 SpawnType.csv 中 PointId 对应）。</summary>
    [SerializeField] private string _pointId; // 命名点 Id

    /// <summary>命名点 Id（只读）。</summary>
    public string PointId => _pointId; // 对外只读访问

    /// <summary>全局命名点注册表（Id -> 列表）。</summary>
    private static readonly Dictionary<string, List<WaveSpawnPoint>> Points = new Dictionary<string, List<WaveSpawnPoint>>(16); // 命名点注册表

    /// <summary>当前是否已注册。</summary>
    private bool _registered; // 注册标记

    /// <summary>
    /// 组件启用：注册命名点。
    /// </summary>
    private void OnEnable() // 生命周期：OnEnable
    {
        Register(); // 注册命名点
    }

    /// <summary>
    /// 组件禁用：注销命名点。
    /// </summary>
    private void OnDisable() // 生命周期：OnDisable
    {
        Unregister(); // 注销命名点
    }

    /// <summary>
    /// 编辑器校验：更新注册信息。
    /// </summary>
    private void OnValidate() // 生命周期：OnValidate
    {
        if (!Application.isPlaying)
        {
            Unregister(); // 先清理旧注册
            Register(); // 再注册新配置
        }
    }

    /// <summary>
    /// 获取指定命名点的随机位置（使用子物体 localScale 作为圆形范围）。
    /// </summary>
    /// <param name="pointId">命名点 Id。</param>
    /// <param name="position">输出位置。</param>
    public static bool TryGetRandomPoint(string pointId, out Vector2 position) // 随机点查询入口
    {
        position = Vector2.zero; // 默认输出
        if (string.IsNullOrEmpty(pointId))
        {
            return false; // Id 为空时返回失败
        }

        if (!Points.TryGetValue(pointId, out var list) || list == null || list.Count == 0)
        {
            return false; // 未找到命名点时返回失败
        }

        var index = Random.Range(0, list.Count); // 随机索引
        var point = list[index]; // 获取命名点
        if (point == null)
        {
            return false; // 命名点为空时返回失败
        }

        position = point.GetRandomPointInCircle(); // 输出圆形范围内的随机坐标
        return true; // 返回成功
    }

    /// <summary>
    /// 获取当前命名点圆形范围内的随机位置。
    /// </summary>
    private Vector2 GetRandomPointInCircle() // 圆形随机点入口
    {
        var center = (Vector2)transform.position; // 记录中心点
        var scale = transform.localScale; // 读取子物体 localScale
        var maxScale = scale.x > scale.y ? scale.x : scale.y; // 取最大缩放作为直径
        var radius = maxScale * 0.5f; // 将直径换算为半径
        if (radius <= 0f)
        {
            return center; // 半径无效时回退中心点
        }

        var offset = Random.insideUnitCircle * radius; // 生成圆内随机偏移
        return center + offset; // 返回随机位置
    }

    /// <summary>
    /// 注册命名点。
    /// </summary>
    private void Register() // 注册入口
    {
        if (_registered)
        {
            return; // 已注册时直接退出
        }

        if (string.IsNullOrEmpty(_pointId))
        {
            return; // Id 为空时不注册
        }

        if (!Points.TryGetValue(_pointId, out var list))
        {
            list = new List<WaveSpawnPoint>(4); // 创建列表
            Points.Add(_pointId, list); // 写入注册表
        }

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == this)
            {
                _registered = true; // 标记已注册
                return; // 避免重复注册
            }
        }

        list.Add(this); // 添加到列表
        _registered = true; // 标记已注册
    }

    /// <summary>
    /// 注销命名点。
    /// </summary>
    private void Unregister() // 注销入口
    {
        if (!_registered)
        {
            return; // 未注册时直接退出
        }

        if (string.IsNullOrEmpty(_pointId))
        {
            _registered = false; // 清理注册标记
            return; // Id 为空时直接退出
        }

        if (Points.TryGetValue(_pointId, out var list) && list != null)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == this)
                {
                    list.RemoveAt(i); // 从列表移除
                    break; // 移除后退出
                }
            }

            if (list.Count == 0)
            {
                Points.Remove(_pointId); // 移除空列表
            }
        }

        _registered = false; // 清理注册标记
    }
}
