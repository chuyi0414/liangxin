using System;
using TMPro;
using UnityEngine;

/// <summary>
/// TMP 数值/自定义文本显示组件（零 GC 刷新）。
/// 用途：
/// 1) 显示“100/100”
/// 2) 显示“100%”
/// 3) 显示任意自定义文字
///
/// 性能与边界说明：
/// - 高频刷新请使用 TMP_Text.SetText(...)，避免 string 拼接/ToString 产生 GC。
/// - 本组件内置“值相同不刷新”的缓存，减少 TMP 重新生成网格的开销。
/// - 仅负责显示，不负责数据来源；推荐由事件（CY.Event）或业务逻辑在数值变化时调用。
///
/// 平台差异：
/// - WebGL/微信同样可用；若出现方块/缺字，通常是字体(Font Asset)未包含对应字形，需要设置 TMP Fallback 或更换字体资产。
/// </summary>
[DisallowMultipleComponent]
public sealed class UITmpValueText : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private TMP_Text _text;

    [Header("Format（可在 Inspector 自定义）")]
    [Tooltip(
        "一个组件实例通常只会用于一种显示方式（分数/百分比/自定义）。\n" +
        "- 分数推荐：\"{0}/{1}\" 或 \"HP {0}/{1}\"\n" +
        "- 百分比推荐：\"{0}%\" 或 \"剩余 {0}%\"\n" +
        "说明：SetFraction 会直接使用该格式（为空则回退默认分数格式）；SetPercent 若检测到格式包含 {1}（疑似误用分数格式）会回退默认百分比格式。")]
    [SerializeField] private string _format = "{0}/{1}";

    [Header("Performance")]
    [SerializeField] private bool _enableCache = true;

    private enum DisplayMode : byte
    {
        None = 0,
        Fraction = 1,
        Percent = 2,
        Custom = 3
    }

    private DisplayMode _mode;
    private int _lastA = int.MinValue;
    private int _lastB = int.MinValue;
    private int _lastPercent = int.MinValue;
    private string _lastCustom;

    private const string DefaultFractionFormat = "{0}/{1}";
    private const string DefaultPercentFormat = "{0}%";

    private void Awake()
    {
        // 兜底：允许忘记拖引用（仅 Awake 一次，不在高频路径 GetComponent）。
        if (_text == null)
        {
            _text = GetComponent<TMP_Text>();
        }
    }

    /// <summary>
    /// 显示“current/max”，例如“100/100”。
    /// </summary>
    public void SetFraction(int current, int max)
    {
        if (_text == null) return;

        if (_enableCache && _mode == DisplayMode.Fraction && current == _lastA && max == _lastB)
        {
            return;
        }

        _mode = DisplayMode.Fraction;
        _lastA = current;
        _lastB = max;

        // 分数：允许你自定义只显示 {0}（不显示最大值），因此这里不强制要求 {1} 存在。
        string format = string.IsNullOrEmpty(_format) ? DefaultFractionFormat : _format;
        _text.SetText(format, current, max);
    }

    /// <summary>
    /// 显示百分比（0~100），例如“100%”。
    /// </summary>
    public void SetPercent(int percent)
    {
        if (_text == null) return;

        // 边界：防止出现 -1% / 120%。
        if (percent < 0) percent = 0;
        else if (percent > 100) percent = 100;

        if (_enableCache && _mode == DisplayMode.Percent && percent == _lastPercent)
        {
            return;
        }

        _mode = DisplayMode.Percent;
        _lastPercent = percent;

        // 约定：百分比格式通常不应包含 {1}，否则可能出现占位符未填充导致显示异常，回退到默认格式。
        string format = _format;
        if (string.IsNullOrEmpty(format) || format.IndexOf("{1}", StringComparison.Ordinal) >= 0)
        {
            format = DefaultPercentFormat;
        }
        _text.SetText(format, percent);
    }

    /// <summary>
    /// 由 current/max 计算并显示百分比。
    /// </summary>
    public void SetPercentFromFraction(float current, float max)
    {
        // 边界：max<=0 时避免除零，按 0% 处理。
        if (max <= 0f)
        {
            SetPercent(0);
            return;
        }

        float normalized = current / max;
        if (normalized < 0f) normalized = 0f;
        else if (normalized > 1f) normalized = 1f;

        SetPercent(Mathf.RoundToInt(normalized * 100f));
    }

    /// <summary>
    /// 设置任意自定义文字。
    /// 注意：如果业务侧每次都“拼接出一个新字符串”，那字符串分配仍然会发生；
    /// 高频“自定义+数字”建议直接用 TMP.SetText("xxx{0}", value) 的方式格式化数值。
    /// </summary>
    public void SetCustom(string text)
    {
        if (_text == null) return;

        if (text == null) text = string.Empty;

        if (_enableCache && _mode == DisplayMode.Custom && string.Equals(text, _lastCustom))
        {
            return;
        }

        _mode = DisplayMode.Custom;
        _lastCustom = text;
        _text.text = text;
    }

    /// <summary>
    /// 清空显示（并重置缓存）。
    /// </summary>
    public void Clear()
    {
        if (_text == null) return;

        _text.text = string.Empty;

        _mode = DisplayMode.None;
        _lastA = int.MinValue;
        _lastB = int.MinValue;
        _lastPercent = int.MinValue;
        _lastCustom = null;
    }
}
