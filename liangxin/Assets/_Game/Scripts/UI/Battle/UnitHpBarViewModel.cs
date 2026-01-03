using System; // IDisposable 引用
using CYFramework.Core.UI.MVVM; // Typed MVVM 引用

/// <summary>
/// 单位血条 ViewModel（Typed MVVM，避免装箱）。
/// </summary>
public sealed class UnitHpBarViewModel : TypedViewModel // 血条 ViewModel
{
    /// <summary>生命比例（0-1）。</summary>
    public ObservableProperty<float> HpRatio { get; } = new ObservableProperty<float>("HpRatio", 1f);
    /// <summary>当前生命值。</summary>
    public ObservableProperty<int> CurrentHp { get; } = new ObservableProperty<int>("CurrentHp", 0);
    /// <summary>最大生命值。</summary>
    public ObservableProperty<int> MaxHp { get; } = new ObservableProperty<int>("MaxHp", 0);

    /// <summary>
    /// 设置生命值并同步比例。
    /// </summary>
    /// <param name="current">当前生命值。</param>
    /// <param name="max">最大生命值。</param>
    public void SetHp(int current, int max)
    {
        if (max < 0)
        {
            max = 0;
        }

        if (current < 0)
        {
            current = 0;
        }

        if (max > 0 && current > max)
        {
            current = max;
        }

        MaxHp.Set(max);
        CurrentHp.Set(current);
        HpRatio.Set(max <= 0 ? 0f : (float)current / max);
    }

    /// <summary>
    /// 绑定血量比例变化。
    /// </summary>
    /// <param name="handler">变化回调。</param>
    public IDisposable BindHpRatio(ObservableProperty<float>.ChangedHandler handler)
    {
        return Subscribe(HpRatio, handler);
    }

    /// <summary>
    /// 绑定当前生命值变化。
    /// </summary>
    /// <param name="handler">变化回调。</param>
    public IDisposable BindCurrentHp(ObservableProperty<int>.ChangedHandler handler)
    {
        return Subscribe(CurrentHp, handler);
    }

    /// <summary>
    /// 绑定最大生命值变化。
    /// </summary>
    /// <param name="handler">变化回调。</param>
    public IDisposable BindMaxHp(ObservableProperty<int>.ChangedHandler handler)
    {
        return Subscribe(MaxHp, handler);
    }
}
