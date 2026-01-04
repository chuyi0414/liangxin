using TMPro; // TextMeshPro 引用
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// 伤害飘字 UI 项（由管理器驱动刷新）。
/// </summary>
public sealed class DamageTextItem : MonoBehaviour // 伤害飘字组件
{
    /// <summary>伤害文本。</summary>
    [SerializeField] private TMP_Text _text;
    /// <summary>透明度控制（可选）。</summary>
    [SerializeField] private CanvasGroup _canvasGroup;
    /// <summary>生命周期（秒）。</summary>
    [SerializeField] private float _lifeTime = 0.8f;
    /// <summary>淡出时长（秒）。</summary>
    [SerializeField] private float _fadeDuration = 0.2f;
    /// <summary>上浮速度（世界单位/秒）。</summary>
    [SerializeField] private float _riseSpeed = 0.8f;
    /// <summary>普通伤害颜色。</summary>
    [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 1f);
    /// <summary>暴击伤害颜色。</summary>
    [SerializeField] private Color _critColor = new Color(1f, 0.25f, 0.25f, 1f);

    /// <summary>缓存 RectTransform。</summary>
    private RectTransform _rectTransform;
    /// <summary>当前世界坐标。</summary>
    private Vector2 _worldPosition;
    /// <summary>已运行时间。</summary>
    private float _elapsed;
    /// <summary>当前淡出透明度（0~1）。</summary>
    private float _currentAlpha;
    /// <summary>当前可见状态。</summary>
    private bool _isVisible;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>(); // 缓存 RectTransform
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>(); // 尝试自动缓存 CanvasGroup
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = false; // 禁用射线阻挡
            _canvasGroup.interactable = false; // 禁用交互
        }

        _currentAlpha = 1f; // 初始化透明度
        _isVisible = true; // 初始化可见状态
    }

    /// <summary>
    /// 初始化并显示伤害飘字。
    /// </summary>
    /// <param name="worldPos">世界坐标。</param>
    /// <param name="damage">伤害值。</param>
    /// <param name="isCrit">是否暴击。</param>
    public void Show(Vector2 worldPos, int damage, bool isCrit)
    {
        _worldPosition = worldPos; // 记录世界坐标
        _elapsed = 0f; // 重置计时
        _currentAlpha = 1f; // 重置透明度
        _isVisible = true; // 重置可见状态

        if (_text != null)
        {
            _text.color = isCrit ? _critColor : _normalColor; // 设置颜色
            _text.SetText("{0}", damage); // 设置文本内容
        }

        ApplyVisibility(true); // 应用可见状态
    }

    /// <summary>
    /// 刷新飘字（返回是否仍有效）。
    /// </summary>
    /// <param name="deltaTime">帧间隔。</param>
    /// <param name="root">UI 根节点。</param>
    /// <param name="worldCamera">世界相机。</param>
    /// <param name="uiCamera">UI 相机（Overlay 可为 null）。</param>
    public bool Tick(float deltaTime, RectTransform root, Camera worldCamera, Camera uiCamera)
    {
        _elapsed += deltaTime; // 累加计时
        if (_elapsed >= _lifeTime)
        {
            return false;
        }

        _worldPosition.y += _riseSpeed * deltaTime; // 更新世界坐标
        var visible = UpdatePosition(root, worldCamera, uiCamera); // 计算位置与可见状态
        UpdateAlpha(); // 计算透明度
        ApplyVisibility(visible); // 根据可见状态应用渲染
        return true;
    }

    /// <summary>
    /// 更新 UI 位置。
    /// </summary>
    private bool UpdatePosition(RectTransform root, Camera worldCamera, Camera uiCamera)
    {
        if (root == null || worldCamera == null || _rectTransform == null)
        {
            return false; // UI 依赖缺失时视为不可见
        }

        var screenPos = worldCamera.WorldToScreenPoint(_worldPosition); // 世界转屏幕坐标
        if (screenPos.z <= 0f)
        {
            return false; // 相机背面视为不可见
        }

        if (!IsScreenPointVisible(screenPos, uiCamera))
        {
            return false; // 不在 UI 相机视口内
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPos, uiCamera, out var localPos))
        {
            return false; // 转换失败视为不可见
        }

        if (!root.rect.Contains(localPos))
        {
            return false; // 不在 UI 根节点范围内
        }

        _rectTransform.anchoredPosition = localPos; // 更新 UI 位置
        return true; // 返回可见
    }

    /// <summary>
    /// 更新淡出透明度。
    /// </summary>
    private void UpdateAlpha()
    {
        if (_fadeDuration <= 0f)
        {
            _currentAlpha = 1f; // 淡出关闭时保持不透明
            return; // 结束淡出计算
        }

        var fadeStart = _lifeTime - _fadeDuration; // 淡出开始时间
        if (_elapsed <= fadeStart)
        {
            _currentAlpha = 1f; // 未到淡出时保持不透明
            return; // 结束淡出计算
        }

        var t = (_elapsed - fadeStart) / _fadeDuration; // 计算淡出进度
        if (t < 0f)
        {
            t = 0f; // 限制下界
        }

        if (t > 1f)
        {
            t = 1f; // 限制上界
        }

        _currentAlpha = 1f - t; // 更新透明度
    }

    /// <summary>
    /// 应用可见状态（离屏隐藏但继续计时）。
    /// </summary>
    /// <param name="visible">是否可见。</param>
    private void ApplyVisibility(bool visible)
    {
        if (_canvasGroup != null)
        {
            if (visible)
            {
                _canvasGroup.alpha = _currentAlpha; // 仅可见时应用透明度
            }
            else if (_isVisible || _canvasGroup.alpha != 0f)
            {
                _canvasGroup.alpha = 0f; // 离屏时隐藏渲染
            }
        }
        else if (_text != null)
        {
            if (_isVisible != visible)
            {
                _text.enabled = visible; // 切换文本可见状态
            }

            if (visible)
            {
                var color = _text.color; // 读取当前颜色
                color.a = _currentAlpha; // 同步淡出透明度
                _text.color = color; // 应用颜色
            }
        }

        _isVisible = visible; // 记录当前可见状态
    }

    /// <summary>
    /// 判断屏幕点是否在 UI 相机视口内。
    /// </summary>
    /// <param name="screenPos">屏幕坐标。</param>
    /// <param name="uiCamera">UI 相机。</param>
    private bool IsScreenPointVisible(Vector3 screenPos, Camera uiCamera)
    {
        if (uiCamera != null)
        {
            var rect = uiCamera.pixelRect; // UI 相机视口范围
            return screenPos.x >= rect.xMin && screenPos.x <= rect.xMax // 视口 X 范围判断
                && screenPos.y >= rect.yMin && screenPos.y <= rect.yMax; // 视口 Y 范围判断
        }

        return screenPos.x >= 0f && screenPos.x <= Screen.width // 屏幕 X 范围判断
            && screenPos.y >= 0f && screenPos.y <= Screen.height; // 屏幕 Y 范围判断
    }
}
