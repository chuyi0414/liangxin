using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

public class FlowFieldManager : GameFrameworkComponent
{
    /// <summary>每帧最多执行的重算任务数量（降低单帧峰值开销）</summary>
    public int MaxPerFrame = EnemyAIConfig.FlowFieldMaxPerFrame;

    /// <summary>等待执行的重算任务队列</summary>
    private readonly Queue<Action> _queue = new Queue<Action>();

    /// <summary>去重集合（避免重复加入相同任务）</summary>
    private readonly HashSet<object> _dedup = new HashSet<object>();
    /// <summary>
    /// 组件初始化（保留接口，暂无额外逻辑）
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
    }
    /// <summary>
    /// 加入一个重算任务；可选 key 用于去重（传 null 表示不去重）
    /// </summary>
    public void Enqueue(Action action, object key = null)
    {
        if (action == null)
            return;

        // 如果 key 已存在于去重集合中，则忽略本次加入
        if (key != null && _dedup.Contains(key))
            return;

        if (key != null)
            _dedup.Add(key);

        _queue.Enqueue(action);
    }

    /// <summary>
    /// 每帧执行指定数量的重算任务
    /// </summary>
    private void Update()
    {
        int count = 0;
        while (_queue.Count > 0 && count < MaxPerFrame)
        {
            Action action = _queue.Dequeue();
            action?.Invoke();
            count++;
        }

        // 队列执行完后清空去重集合，避免内存占用增长
        if (_queue.Count == 0)
            _dedup.Clear();
    }
}
