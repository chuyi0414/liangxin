using GameFramework;
using GameFramework.ObjectPool;
using UnityEngine;

/// <summary>
/// GF 通用对象池包装类。
/// 该类用于将任意目标对象包装为 GF 对象池可管理的 <see cref="ObjectBase"/>。
/// </summary>
public class GeneralGFObjectPool : ObjectBase
{
    /// <summary>
    /// 释放时是否销毁 Unity 对象。
    /// 当目标对象是 <see cref="UnityEngine.Object"/> 且该值为 true 时，释放阶段会执行销毁。
    /// </summary>
    private bool _destroyUnityObjectOnRelease;

    /// <summary>
    /// 获取当前包装的目标对象（泛型安全转换）。
    /// </summary>
    /// <typeparam name="T">目标对象类型。</typeparam>
    /// <returns>转换后的目标对象；若类型不匹配则返回 null。</returns>
    public T GetTarget<T>() where T : class
    {
        return Target as T;
    }

    /// <summary>
    /// 创建通用对象池包装对象。
    /// </summary>
    /// <param name="target">需要被对象池管理的目标对象。</param>
    /// <param name="name">对象名称；为空时自动使用 target.ToString()。</param>
    /// <param name="destroyUnityObjectOnRelease">释放时是否销毁 Unity 对象。</param>
    /// <returns>初始化完成的通用包装对象。</returns>
    public static GeneralGFObjectPool Create(object target, string name = null, bool destroyUnityObjectOnRelease = false)
    {
        if (target == null)
        {
            throw new GameFrameworkException("Target is invalid.");
        }

        GeneralGFObjectPool obj = ReferencePool.Acquire<GeneralGFObjectPool>();
        obj.Initialize(string.IsNullOrWhiteSpace(name) ? target.ToString() : name, target);
        obj.SetDestroyUnityObjectOnRelease(destroyUnityObjectOnRelease);
        return obj;
    }

    /// <summary>
    /// 设置释放策略：释放时是否销毁 Unity 对象。
    /// </summary>
    /// <param name="destroyUnityObjectOnRelease">是否在释放阶段销毁 Unity 对象。</param>
    protected void SetDestroyUnityObjectOnRelease(bool destroyUnityObjectOnRelease)
    {
        _destroyUnityObjectOnRelease = destroyUnityObjectOnRelease;
    }

    /// <summary>
    /// 清理包装对象状态。
    /// </summary>
    public override void Clear()
    {
        base.Clear();
        _destroyUnityObjectOnRelease = false;
    }

    /// <summary>
    /// 当对象池真正释放该对象时执行。
    /// 若启用了 Unity 对象销毁策略，且目标对象为 UnityEngine.Object，则执行销毁。
    /// </summary>
    /// <param name="isShutdown">是否为对象池关闭阶段触发的释放。</param>
    protected  override void Release(bool isShutdown)
    {
        if (!_destroyUnityObjectOnRelease)
        {
            return;
        }

        Object unityObject = Target as Object;
        if (unityObject != null)
        {
            Object.Destroy(unityObject);
        }
    }
}
