using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;
using GameFramework.Timer;

/// <summary>
/// 单位分帧更新管理器：负责分帧更新空间网格，避免大量单位同帧更新导致卡顿。
/// </summary>
public class UnitBatchUpdateManager : GameFrameworkComponent
{
	/// <summary>
    /// 框架计时器实例，用于分帧更新。
    /// </summary>
    private Timer _tickTimer;
    /// <summary>
    /// 所有需要管理的单位列表（敌人/员工都可注册）。
    /// </summary>
    private List<UnitBaseEntity> _units = new List<UnitBaseEntity>();

    /// <summary>
    /// 当前分帧更新索引。
    /// </summary>
    private int _currentIndex = 0;

    /// <summary>
    /// 每帧最多更新的单位数量（可在Inspector调整）。
    /// </summary>
    private int _updatePerFrame = EnemyAIConfig.UnitsUpdatePerFrame;

    /// <summary>
    /// 空间网格系统。
    /// </summary>
    private SpatialGrid _spatialGrid;

    /// <summary>
    /// 网格单元大小（可在Inspector调整）。
    /// </summary>
    private float _cellSize = EnemyAIConfig.GridCellSize;

    /// <summary>
    /// 记录每个单位的上一帧位置，用于更新空间网格。
    /// </summary>
    private Dictionary<UnitBaseEntity, Vector3> _lastPositions = new Dictionary<UnitBaseEntity, Vector3>();

    /// <summary>
    /// 计时器等待协程（确保 Timer 模块已就绪）。
    /// </summary>
    private Coroutine _timerRoutine;

    /// <summary>
    /// 组件初始化：创建空间网格。
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        _spatialGrid = new SpatialGrid(_cellSize);
        // 使用框架计时器驱动更新（代替 MonoBehaviour.Update）
        // 注意：Timer 组件可能还未初始化，需要等待。
        TryStartTimer();
    }

    /// <summary>
    /// 尝试启动计时器（如未就绪则等待）。
    /// </summary>
    private void TryStartTimer()
    {
        if (_tickTimer != null)
        {
            return;
        }

        if (GameEntry.Timer != null)
        {
            _tickTimer = GameEntry.Timer.Loop(0f, FrameworkTick);
            return;
        }

        if (_timerRoutine == null)
        {
            _timerRoutine = StartCoroutine(WaitForTimerReady());
        }
    }

    /// <summary>
    /// 等待 Timer 组件就绪后再启动。
    /// </summary>
    private IEnumerator WaitForTimerReady()
    {
        while (GameEntry.Timer == null)
        {
            yield return null;
        }

        _timerRoutine = null;
        _tickTimer = GameEntry.Timer.Loop(0f, FrameworkTick);
    }

	    /// <summary>
    /// 框架驱动的更新入口（代替 MonoBehaviour.Update）。
    /// </summary>
    private void FrameworkTick()
    {
        if (_units.Count == 0) return;

        int count = Mathf.Min(_updatePerFrame, _units.Count);

        for (int i = 0; i < count; i++)
        {
            if (_currentIndex >= _units.Count)
            {
                _currentIndex = 0;
            }

            UnitBaseEntity unit = _units[_currentIndex];
            if (unit != null && unit.gameObject.activeSelf)
            {
                Vector3 oldPos;
                if (!_lastPositions.TryGetValue(unit, out oldPos))
                {
                    oldPos = GetUnitCenter(unit);
                    _lastPositions[unit] = oldPos;
                }

                Vector3 newPos = GetUnitCenter(unit);
                _spatialGrid.UpdatePosition(unit, oldPos, newPos);
                _lastPositions[unit] = newPos;
            }

            _currentIndex++;
        }
    }

    /// <summary>
    /// 注册单位进入分帧更新管理器。
    /// </summary>
    /// <param name="unit">需要注册的单位。</param>
    public void RegisterUnit(UnitBaseEntity unit)
    {
        if (unit == null) return;
        if (_units.Contains(unit)) return;

        _units.Add(unit);
        _spatialGrid.Add(unit);

        // 记录初始位置
        _lastPositions[unit] = GetUnitCenter(unit);
    }

    /// <summary>
    /// 注销单位。
    /// </summary>
    /// <param name="unit">需要注销的单位。</param>
    public void UnregisterUnit(UnitBaseEntity unit)
    {
        if (unit == null) return;

        _units.Remove(unit);
        _spatialGrid.Remove(unit);

        // 移除位置缓存
        _lastPositions.Remove(unit);
    }

    /// <summary>
    /// 获取空间网格系统（供敌人查询附近单位使用）。
    /// </summary>
    public SpatialGrid GetSpatialGrid()
    {
        return _spatialGrid;
    }

    /// <summary>
    /// 每帧分批更新单位在空间网格中的位置。
    /// </summary>
    /*private void Update()
    {
        if (_units.Count == 0) return;

        int count = Mathf.Min(_updatePerFrame, _units.Count);

        for (int i = 0; i < count; i++)
        {
            if (_currentIndex >= _units.Count)
            {
                _currentIndex = 0;
            }

            UnitBaseEntity unit = _units[_currentIndex];
            if (unit != null && unit.gameObject.activeSelf)
            {
                // 更新单位位置到空间网格
                _spatialGrid.UpdatePosition(unit, unit.transform.position, unit.transform.position);
            }

            _currentIndex++;
        }
    }*/

    /// <summary>
    /// 清空管理器数据。
    /// </summary>
    public void Clear()
    {
        _units.Clear();
        _spatialGrid.Clear();
        _lastPositions.Clear();
        _currentIndex = 0;
    }

    /// <summary>
    /// 获取单位的“中心点”（优先使用受伤盒中心，解决偏移问题）
    /// </summary>
    private Vector3 GetUnitCenter(UnitBaseEntity unit)
    {
        if (unit == null)
        {
            return Vector3.zero;
        }

        BoxCollider2D hurtBox = unit.GetHurtBoxCollider();
        if (hurtBox != null)
        {
            return hurtBox.transform.TransformPoint(hurtBox.offset);
        }

        return unit.transform.position;
    }

	/// <summary>
    /// 组件禁用时停止计时器，避免重复更新。
    /// </summary>
    private void OnDisable()
    {
        if (_tickTimer != null)
        {
            GameEntry.Timer.Cancel(_tickTimer);
            _tickTimer = null;
        }

        if (_timerRoutine != null)
        {
            StopCoroutine(_timerRoutine);
            _timerRoutine = null;
        }
    }
}