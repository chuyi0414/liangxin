using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

public class FlowFieldManager : GameFrameworkComponent
{
    /// <summary>每帧最多执行多少个重建请求</summary>
    public int MaxPerFrame = 30;

    /// <summary>等待执行的重建请求队列</summary>
    private readonly Queue<Action> _queue = new Queue<Action>();

    /// <summary>避免重复入队的集合（可选）</summary>
    private readonly HashSet<object> _dedup = new HashSet<object>();
    /// <summary>
    /// 组件初始化，必须调用基类注册逻辑
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
    }
    /// <summary>
    /// 入队一个重建请求（key用于去重，可传null表示不去重）
    /// </summary>
    public void Enqueue(Action action, object key = null)
    {
        if (action == null)
            return;

        // 如果有key并且已在队列中，则忽略
        if (key != null && _dedup.Contains(key))
            return;

        if (key != null)
            _dedup.Add(key);

        _queue.Enqueue(action);
    }

    /// <summary>
    /// 每帧执行有限数量的重建请求
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

        // 执行完本帧后，清理去重集合（简单做法）
        if (_queue.Count == 0)
            _dedup.Clear();
    }
}
