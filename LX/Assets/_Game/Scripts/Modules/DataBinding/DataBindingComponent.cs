using System;
using System.Collections.Generic;
using QFramework;
using UnityEngine;
using UnityGameFramework.Runtime;

[DisallowMultipleComponent]
[AddComponentMenu("Game/Data Binding")]
public sealed class DataBindingComponent : GameFrameworkComponent
{
    /// <summary>
    /// 通用绑定字典，key 为业务自定义字符串，value 为 BindableProperty&lt;T&gt; 的实例。
    /// </summary>
    private readonly Dictionary<string, object> m_Bindings = new Dictionary<string, object>();

    /// <summary>
    /// 解绑句柄池，按 owner 与 key 分组保存绑定句柄。
    /// </summary>
    private readonly Dictionary<object, Dictionary<string, List<IUnRegister>>> m_UnregisterMap =
        new Dictionary<object, Dictionary<string, List<IUnRegister>>>();

    /// <summary>
    /// 创建一个可绑定属性实例（用于外部自定义）。
    /// </summary>
    /// <typeparam name="T">属性值类型。</typeparam>
    /// <param name="defaultValue">默认初始值。</param>
    /// <returns>可绑定属性实例。</returns>
    public BindableProperty<T> CreateProperty<T>(T defaultValue = default)
    {
        return new BindableProperty<T>(defaultValue);
    }

    /// <summary>
    /// 获取或创建指定类型的绑定属性。
    /// </summary>
    /// <typeparam name="T">属性值类型。</typeparam>
    /// <param name="key">绑定键名（字符串）。</param>
    /// <param name="defaultValue">若不存在则使用该默认值创建。</param>
    /// <returns>绑定属性实例，key 无效或类型不匹配时返回 null。</returns>
    public BindableProperty<T> Get<T>(string key, T defaultValue = default)
    {
        if (!IsValidKey(key))
        {
            return null;
        }

        if (m_Bindings.TryGetValue(key, out object cached))
        {
            if (cached is BindableProperty<T> existedProperty)
            {
                return existedProperty;
            }

            Log.Warning("DataBindingComponent: key '{0}' type mismatch.", key);
            return null;
        }

        BindableProperty<T> property = CreateProperty(defaultValue);
        m_Bindings[key] = property;
        return property;
    }

    /// <summary>
    /// 尝试获取指定类型的绑定属性。
    /// </summary>
    /// <typeparam name="T">属性值类型。</typeparam>
    /// <param name="key">绑定键名（字符串）。</param>
    /// <param name="property">输出绑定属性实例。</param>
    /// <returns>是否获取成功。</returns>
    public bool TryGet<T>(string key, out BindableProperty<T> property)
    {
        property = null;
        if (!IsValidKey(key))
        {
            return false;
        }

        if (!m_Bindings.TryGetValue(key, out object cached))
        {
            return false;
        }

        if (cached is BindableProperty<T> existedProperty)
        {
            property = existedProperty;
            return true;
        }

        Log.Warning("DataBindingComponent: key '{0}' type mismatch.", key);
        return false;
    }

    /// <summary>
    /// 设置指定类型的绑定属性值（不存在则创建）。
    /// </summary>
    /// <typeparam name="T">属性值类型。</typeparam>
    /// <param name="key">绑定键名（字符串）。</param>
    /// <param name="value">要设置的值。</param>
    public void Set<T>(string key, T value)
    {
        BindableProperty<T> property = Get(key, value);
        if (property == null)
        {
            return;
        }

        property.Value = value;
    }

    /// <summary>
    /// 统一绑定入口，返回取消注册句柄并由组件统一托管。
    /// </summary>
    /// <typeparam name="T">属性值类型。</typeparam>
    /// <param name="key">绑定键名（字符串）。</param>
    /// <param name="defaultValue">若不存在则使用该默认值创建。</param>
    /// <param name="onValueChanged">数值变化时的回调。</param>
    /// <param name="owner">绑定所属者（通常为 UI 实例）。</param>
    /// <param name="callInit">是否在绑定时立即回调一次当前值。</param>
    /// <returns>取消注册句柄，失败时返回 null。</returns>
    public IUnRegister Bind<T>(string key, T defaultValue, Action<T> onValueChanged, object owner, bool callInit = true)
    {
        if (owner == null)
        {
            Log.Warning("DataBindingComponent: owner is null.");
            return null;
        }

        if (onValueChanged == null)
        {
            Log.Warning("DataBindingComponent: onValueChanged is null.");
            return null;
        }

        BindableProperty<T> property = Get(key, defaultValue);
        if (property == null)
        {
            return null;
        }

        IUnRegister unregister = callInit ? property.RegisterWithInitValue(onValueChanged) : property.Register(onValueChanged);
        AddUnregister(owner, key, unregister);
        return unregister;
    }

    /// <summary>
    /// 解绑指定 owner 的全部绑定。
    /// </summary>
    /// <param name="owner">绑定所属者（通常为 UI 实例）。</param>
    public void UnbindAll(object owner)
    {
        if (owner == null)
        {
            Log.Warning("DataBindingComponent: owner is null.");
            return;
        }

        if (!m_UnregisterMap.TryGetValue(owner, out Dictionary<string, List<IUnRegister>> keyMap))
        {
            return;
        }

        foreach (KeyValuePair<string, List<IUnRegister>> pair in keyMap)
        {
            List<IUnRegister> unregisters = pair.Value;
            for (int i = 0; i < unregisters.Count; i++)
            {
                unregisters[i].UnRegister();
            }

            unregisters.Clear();
        }

        keyMap.Clear();
        m_UnregisterMap.Remove(owner);
    }

    /// <summary>
    /// 解绑指定 owner 的指定 key 绑定。
    /// </summary>
    /// <param name="owner">绑定所属者（通常为 UI 实例）。</param>
    /// <param name="key">绑定键名（字符串）。</param>
    public void Unbind(object owner, string key)
    {
        if (owner == null)
        {
            Log.Warning("DataBindingComponent: owner is null.");
            return;
        }

        if (!IsValidKey(key))
        {
            return;
        }

        if (!m_UnregisterMap.TryGetValue(owner, out Dictionary<string, List<IUnRegister>> keyMap))
        {
            return;
        }

        if (!keyMap.TryGetValue(key, out List<IUnRegister> unregisters))
        {
            return;
        }

        for (int i = 0; i < unregisters.Count; i++)
        {
            unregisters[i].UnRegister();
        }

        unregisters.Clear();
        keyMap.Remove(key);

        if (keyMap.Count == 0)
        {
            m_UnregisterMap.Remove(owner);
        }
    }

    /// <summary>
    /// 移除指定 key 的绑定属性。
    /// </summary>
    /// <param name="key">绑定键名（字符串）。</param>
    /// <returns>是否移除成功。</returns>
    public bool Remove(string key)
    {
        if (!IsValidKey(key))
        {
            return false;
        }

        return m_Bindings.Remove(key);
    }

    /// <summary>
    /// 校验 key 是否有效（非空且非空白）。
    /// </summary>
    /// <param name="key">绑定键名（字符串）。</param>
    /// <returns>key 是否有效。</returns>
    private bool IsValidKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            Log.Warning("DataBindingComponent: key is null or empty.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 将解绑句柄添加到 owner 与 key 对应的列表中。
    /// </summary>
    /// <param name="owner">绑定所属者（通常为 UI 实例）。</param>
    /// <param name="key">绑定键名（字符串）。</param>
    /// <param name="unregister">解绑句柄。</param>
    private void AddUnregister(object owner, string key, IUnRegister unregister)
    {
        if (owner == null || unregister == null)
        {
            return;
        }

        if (!IsValidKey(key))
        {
            return;
        }

        if (!m_UnregisterMap.TryGetValue(owner, out Dictionary<string, List<IUnRegister>> keyMap))
        {
            keyMap = new Dictionary<string, List<IUnRegister>>();
            m_UnregisterMap[owner] = keyMap;
        }

        if (!keyMap.TryGetValue(key, out List<IUnRegister> list))
        {
            list = new List<IUnRegister>();
            keyMap[key] = list;
        }

        list.Add(unregister);
    }
}
