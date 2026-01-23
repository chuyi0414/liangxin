using Pathfinding;
using UnityEngine;

/// <summary>
/// 动态障碍自动更新：物体移动后更新“旧+新”范围，避免残留不可走节点
/// </summary>
[RequireComponent(typeof(GraphUpdateScene))]
[RequireComponent(typeof(Collider2D))]
public class GraphUpdateOnMove2D : MonoBehaviour
{
    /// <summary>
    /// 触发更新的最小位移（世界坐标，单位：米）
    /// </summary>
    public float minMoveDistance = 0.02f;

    /// <summary>
    /// 最小更新间隔（秒），用于节流
    /// </summary>
    public float updateInterval = 0.2f;

    /// <summary>
    /// GraphUpdateScene 组件，用于生成图更新参数
    /// </summary>
    private GraphUpdateScene graphUpdateScene;

    /// <summary>
    /// 障碍物的 Collider2D，用于获取 Bounds
    /// </summary>
    private Collider2D obstacleCollider;

    /// <summary>
    /// 上一次记录的位置，用于判断是否移动
    /// </summary>
    private Vector3 lastPosition;

    /// <summary>
    /// 上一次记录的碰撞体范围，用于清理旧位置残留
    /// </summary>
    private Bounds lastBounds;

    /// <summary>
    /// 上一次更新的时间，用于节流
    /// </summary>
    private float lastUpdateTime;

    /// <summary>
    /// 组件初始化与安全检查
    /// </summary>
    private void Awake()
    {
        graphUpdateScene = GetComponent<GraphUpdateScene>();
        obstacleCollider = GetComponent<Collider2D>();

        if (graphUpdateScene == null || obstacleCollider == null)
        {
            Debug.LogError("缺少 GraphUpdateScene 或 Collider2D，无法进行动态更新。", this);
            enabled = false;
            return;
        }

        lastPosition = transform.position;
        lastBounds = obstacleCollider.bounds;
        lastUpdateTime = -updateInterval;
    }

    /// <summary>
    /// 每帧检测是否移动，满足条件则触发更新
    /// </summary>
    private void LateUpdate()
    {
        if (Time.time - lastUpdateTime < updateInterval) return;

        float movedSqr = (transform.position - lastPosition).sqrMagnitude;
        if (movedSqr < minMoveDistance * minMoveDistance) return;

        ApplyCombinedBoundsUpdate();

        lastPosition = transform.position;
        lastBounds = obstacleCollider.bounds;
        lastUpdateTime = Time.time;
    }

    /// <summary>
    /// 强制立即更新（可在外部逻辑中手动调用）
    /// </summary>
    public void ForceUpdate()
    {
        ApplyCombinedBoundsUpdate();
        lastPosition = transform.position;
        lastBounds = obstacleCollider.bounds;
        lastUpdateTime = Time.time;
    }

    /// <summary>
    /// 用“旧+新”范围合并更新，避免旧位置残留为障碍
    /// </summary>
    private void ApplyCombinedBoundsUpdate()
    {
        if (AstarPath.active == null) return;

        Bounds currentBounds = obstacleCollider.bounds;
        Bounds combinedBounds = currentBounds;
        combinedBounds.Encapsulate(lastBounds);

        GraphUpdateObject guo = graphUpdateScene.GetGraphUpdate();
        if (guo == null) return;

        guo.bounds = combinedBounds;
        AstarPath.active.UpdateGraphs(guo);
    }
}